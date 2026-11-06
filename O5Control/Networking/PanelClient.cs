using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using O5Control.Core;
using O5Control.Networking.Protocol;

namespace O5Control.Networking;

internal sealed class PanelClient : IDisposable
{
    private const int MaxQueuedMessages = 4096;
    private const int ReceiveBufferSize = 16 * 1024;
    private const WebSocketCloseStatus UnauthorizedCloseCode = (WebSocketCloseStatus)4401;
    private const WebSocketCloseStatus ReplacedCloseCode = (WebSocketCloseStatus)4409;

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(65);
    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(10);

    private readonly Func<Uri> _gateway;
    private readonly CredentialStore _credentials;
    private readonly MainThread _mainThread;
    private readonly MessageRouter _router;
    private readonly Func<object> _hello;
    private readonly string _userAgent;

    private readonly ConcurrentQueue<string> _outbox = new();
    private readonly SemaphoreSlim _outboxSignal = new(0);
    private readonly PendingRequests _pending = new();
    private readonly Backoff _backoff = new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60));

    private CancellationTokenSource? _lifetime;
    private Task? _loop;
    private int _queued;
    private long _lastReceivedTicks;
    private volatile bool _connected;

    public PanelClient(Func<Uri> gateway, CredentialStore credentials, MainThread mainThread, MessageRouter router, Func<object> hello, string userAgent)
    {
        _gateway = gateway;
        _credentials = credentials;
        _mainThread = mainThread;
        _router = router;
        _hello = hello;
        _userAgent = userAgent;
    }

    public event Action? Connected;

    public event Action<string>? Disconnected;

    public event Action? Unauthorized;

    public bool IsConnected => _connected;

    public bool IsRunning => _loop is { IsCompleted: false };

    public void Start()
    {
        if (IsRunning || _credentials.Current is null)
            return;

        _lifetime = new CancellationTokenSource();
        var token = _lifetime.Token;
        _loop = Task.Run(() => RunAsync(token));
    }

    public void Stop()
    {
        if (_lifetime is null)
            return;

        _lifetime.Cancel();

        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(3));
        }
        catch (AggregateException)
        {
        }

        _lifetime.Dispose();
        _lifetime = null;
        _loop = null;
        _connected = false;
        _pending.FailAll(new OperationCanceledException("Connection stopped."));
    }

    public void Restart()
    {
        Stop();
        Start();
    }

    public void Send(string op, object? data = null, string? replyTo = null)
    {
        var json = JsonSerializer.Serialize(new OutgoingEnvelope(op, data, re: replyTo), Json.Options);
        Enqueue(json);
    }

    public void Reply(Envelope request, string op, object? data = null)
    {
        if (request.Id is not null)
            Send(op, data, request.Id);
    }

    public async Task<Envelope> RequestAsync(string op, object? data, TimeSpan? timeout = null)
    {
        if (!_connected)
            throw new InvalidOperationException("Not connected to O5 Control.");

        var id = Guid.NewGuid().ToString("N");
        var reply = _pending.Register(id, timeout ?? DefaultRequestTimeout);
        Enqueue(JsonSerializer.Serialize(new OutgoingEnvelope(op, data, id), Json.Options));

        var envelope = await reply.ConfigureAwait(false);
        if (envelope.Op == Ops.Error)
        {
            var error = envelope.Read<Protocol.Messages.ErrorMessage>();
            throw new PanelException(error?.Code ?? "error", error?.Message ?? "The panel rejected the request.");
        }

        return envelope;
    }

    public void Dispose()
    {
        Stop();
        _outboxSignal.Dispose();
    }

    private void Enqueue(string json)
    {
        _outbox.Enqueue(json);

        if (Interlocked.Increment(ref _queued) > MaxQueuedMessages && _outbox.TryDequeue(out _))
            Interlocked.Decrement(ref _queued);

        _outboxSignal.Release();
    }

    private async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var credential = _credentials.Current;
            if (credential is null)
                return;

            var stopForGood = false;
            var reason = "connection lost";

            using (var socket = CreateSocket(credential))
            {
                try
                {
                    using (var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        connectTimeout.CancelAfter(ConnectTimeout);
                        await socket.ConnectAsync(_gateway(), connectTimeout.Token).ConfigureAwait(false);
                    }

                    _backoff.Reset();
                    MarkReceived();
                    await SendRawAsync(socket, JsonSerializer.Serialize(new OutgoingEnvelope(Ops.Hello, _hello()), Json.Options), token).ConfigureAwait(false);

                    _connected = true;
                    _mainThread.Post(() => Connected?.Invoke());

                    using var session = CancellationTokenSource.CreateLinkedTokenSource(token);
                    var receive = ReceiveLoopAsync(socket, session.Token);
                    var send = SendLoopAsync(socket, session.Token);
                    var heartbeat = HeartbeatLoopAsync(socket, session.Token);

                    await Task.WhenAny(receive, send, heartbeat).ConfigureAwait(false);
                    session.Cancel();

                    if (socket.CloseStatus == UnauthorizedCloseCode)
                    {
                        stopForGood = true;
                        reason = "credential rejected";
                    }
                    else if (socket.CloseStatus == ReplacedCloseCode)
                    {
                        reason = "another connection with this credential took over";
                    }
                    else if (!string.IsNullOrEmpty(socket.CloseStatusDescription))
                    {
                        reason = socket.CloseStatusDescription!;
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    reason = "stopped";
                }
                catch (WebSocketException exception) when (IsUnauthorized(exception))
                {
                    stopForGood = true;
                    reason = "credential rejected";
                }
                catch (Exception exception)
                {
                    reason = exception.GetBaseException().Message;
                }
            }

            var wasConnected = _connected;
            _connected = false;
            _pending.FailAll(new IOException($"Disconnected: {reason}"));

            if (wasConnected)
                _mainThread.Post(() => Disconnected?.Invoke(reason));

            if (stopForGood)
            {
                _mainThread.Post(() => Unauthorized?.Invoke());
                return;
            }

            if (token.IsCancellationRequested)
                return;

            var delay = _backoff.Next();
            Log.Debug($"Reconnecting in {delay.TotalSeconds:0.0}s ({reason}).");

            try
            {
                await Task.Delay(delay, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private ClientWebSocket CreateSocket(Credential credential)
    {
        var socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = HeartbeatInterval;
        socket.Options.SetRequestHeader("Authorization", $"Bearer {credential.ServerId}.{credential.Secret}");
        socket.Options.SetRequestHeader("User-Agent", _userAgent);
        return socket;
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken token)
    {
        var buffer = new byte[ReceiveBufferSize];
        using var message = new MemoryStream();

        while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                if (socket.State == WebSocketState.CloseReceived)
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None).ConfigureAwait(false);
                return;
            }

            message.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage)
                continue;

            MarkReceived();
            var envelope = Parse(message.ToArray());
            message.SetLength(0);

            if (envelope is not null)
                Handle(envelope);
        }
    }

    private async Task SendLoopAsync(ClientWebSocket socket, CancellationToken token)
    {
        while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            await _outboxSignal.WaitAsync(token).ConfigureAwait(false);

            while (_outbox.TryDequeue(out var json))
            {
                Interlocked.Decrement(ref _queued);
                await SendRawAsync(socket, json, token).ConfigureAwait(false);
            }
        }
    }

    private async Task HeartbeatLoopAsync(ClientWebSocket socket, CancellationToken token)
    {
        while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            await Task.Delay(HeartbeatInterval, token).ConfigureAwait(false);

            if (DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastReceivedTicks) > StaleAfter.Ticks)
            {
                socket.Abort();
                return;
            }

            Send(Ops.Ping);
        }
    }

    private void Handle(Envelope envelope)
    {
        if (_pending.TryComplete(envelope))
            return;

        switch (envelope.Op)
        {
            case Ops.Ping:
                Send(Ops.Pong, replyTo: envelope.Id);
                return;
            case Ops.Pong:
                return;
            default:
                _mainThread.Post(() => _router.Dispatch(envelope));
                return;
        }
    }

    private static Envelope? Parse(byte[] payload)
    {
        try
        {
            return JsonSerializer.Deserialize<Envelope>(payload, Json.Options);
        }
        catch (JsonException exception)
        {
            Log.Warn($"Dropped a malformed message: {exception.Message}");
            return null;
        }
    }

    private static Task SendRawAsync(ClientWebSocket socket, string json, CancellationToken token)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        return socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
    }

    private static bool IsUnauthorized(WebSocketException exception) =>
        exception.Message.Contains("401") || exception.InnerException?.Message.Contains("401") == true;

    private void MarkReceived() => Interlocked.Exchange(ref _lastReceivedTicks, DateTime.UtcNow.Ticks);
}

internal sealed class PanelException : Exception
{
    public PanelException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

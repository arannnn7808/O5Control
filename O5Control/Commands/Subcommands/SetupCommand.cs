using System;
using CommandSystem;
using O5Control.Core;
using O5Control.Features.Setup;

namespace O5Control.Commands.Subcommands;

public sealed class SetupCommand : ICommand, IUsageProvider
{
    public string Command => "setup";

    public string[] Aliases { get; } = { "link" };

    public string Description => "Links this server using a code from the panel.";

    public string[] Usage { get; } = { "O5-XXXX-XXXX", "[en/es/fr/pl]" };

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!CommandContext.CanManage(sender, out response) || !CommandContext.TryGetRuntime(out var runtime, out response))
            return false;

        if (arguments.Count < 1)
        {
            response = $"Usage: o5 setup O5-XXXX-XXXX [language] (create the code in the panel: Servers → Connect a server). Languages: {Translation.Available}.";
            return false;
        }

        var code = SetupService.Normalize(arguments.At(0));
        if (!SetupService.IsValidCode(code))
        {
            response = "That does not look like an O5 Control code. It should look like O5-7KQ2-XM4P.";
            return false;
        }

        string? language = null;
        if (arguments.Count > 1)
        {
            if (!Translation.TryNormalize(arguments.At(1), out var normalized))
            {
                response = $"Unknown language '{arguments.At(1)}'. Available: {Translation.Available}.";
                return false;
            }

            language = normalized;
        }

        Redeem(runtime, code, language, sender);
        response = "Linking with O5 Control…";
        return true;
    }

    private static async void Redeem(Runtime runtime, string code, string? language, ICommandSender sender)
    {
        var result = await runtime.Setup.RedeemAsync(code, language);

        runtime.MainThread.Post(() =>
        {
            if (!result.Success)
            {
                Log.Error($"Setup failed: {result.Error}");
                sender.Respond($"Setup failed: {result.Error}", false);
                return;
            }

            runtime.ApplyLanguage(result.Instance!.Language);

            var message = $"Linked to {result.Instance.Name} as '{result.ServerName}'. Language: {Translation.Name(runtime.Texts.Language)}. Connecting…";
            Log.Info(message);
            sender.Respond(message);
            runtime.Relink();
        });
    }
}

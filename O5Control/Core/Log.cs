using LabApi.Features.Console;

namespace O5Control.Core;

internal static class Log
{
    public static bool DebugEnabled { get; set; }

    public static void Debug(string message)
    {
        if (DebugEnabled)
            Logger.Debug(message);
    }

    public static void Info(string message) => Logger.Info(message);

    public static void Warn(string message) => Logger.Warn(message);

    public static void Error(string message) => Logger.Error(message);
}

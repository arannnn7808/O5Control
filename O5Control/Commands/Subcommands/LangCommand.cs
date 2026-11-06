using System;
using CommandSystem;
using O5Control.Core;

namespace O5Control.Commands.Subcommands;

public sealed class LangCommand : ICommand, IUsageProvider
{
    public string Command => "lang";

    public string[] Aliases { get; } = { "language" };

    public string Description => "Changes the language of the messages players see.";

    public string[] Usage { get; } = { "en/es/fr/pl" };

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        var plugin = O5ControlPlugin.Instance;
        if (plugin is null)
        {
            response = "O5Control is not running.";
            return false;
        }

        if (arguments.Count < 1)
        {
            response = $"Current language: {Translation.Name(plugin.Translation.Language)}. Available: {Translation.Available}.";
            return true;
        }

        if (!CommandContext.CanManage(sender, out response))
            return false;

        if (!Translation.TryNormalize(arguments.At(0), out var language))
        {
            response = $"Unknown language. Available: {Translation.Available}.";
            return false;
        }

        plugin.SetLanguage(language);
        response = $"Language set to {Translation.Name(language)}. translations.yml has been rewritten with the {Translation.Name(language)} texts.";
        return true;
    }
}

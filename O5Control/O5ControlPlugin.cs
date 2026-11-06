using System;
using LabApi.Features;
using LabApi.Features.Console;
using LabApi.Loader;
using LabApi.Loader.Features.Plugins;
using LabApi.Loader.Features.Plugins.Enums;
using O5Control.Core;

namespace O5Control;

public sealed class O5ControlPlugin : Plugin<PluginConfig>
{
    public static O5ControlPlugin? Instance { get; private set; }

    public override string Name => "O5Control";

    public override string Description => "Connects this server to O5 Control: sanctions, staff sync, logs and live management.";

    public override string Author => "O5 Control";

    public override Version RequiredApiVersion { get; } = new(LabApiProperties.CompiledVersion);

    public override LoadPriority Priority => LoadPriority.Highest;

    private const string TranslationsFile = "translations.yml";

    internal Runtime? Runtime { get; private set; }

    public Translation Translation { get; private set; } = new();

    public override void LoadConfigs()
    {
        base.LoadConfigs();
        Translation = this.LoadConfig<Translation>(TranslationsFile) ?? new Translation();
    }

    internal void SetLanguage(string language)
    {
        Translation = Translation.Create(language);
        this.SaveConfig(Translation, TranslationsFile);
    }

    public override void Enable()
    {
        Instance = this;
        Runtime = new Runtime(this);

        try
        {
            Runtime.Start();
        }
        catch (Exception exception)
        {
            Logger.Error($"Failed to start: {exception}");
        }
    }

    public override void Disable()
    {
        Runtime?.Dispose();
        Runtime = null;
        Instance = null;
    }
}

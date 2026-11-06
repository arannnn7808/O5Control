using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace O5Control.Core;

public sealed class Translation
{
    public static readonly IReadOnlyDictionary<string, string> Languages = new Dictionary<string, string>
    {
        ["en"] = "English",
        ["es"] = "Español",
        ["fr"] = "Français",
        ["pl"] = "Polski",
    };

    [Description("Language of these texts. Change it with: o5 lang <en|es|fr|pl>. Placeholders: {reason}, {expires}, {issuer}.")]
    public string Language { get; set; } = "en";

    public string BanScreen { get; set; } = "You are banned from this network.\nReason: {reason}\nExpires: {expires}";

    public string PermanentLabel { get; set; } = "never";

    public string KickScreen { get; set; } = "You have been kicked.\nReason: {reason}";

    public string WarnBroadcast { get; set; } = "<color=#f5a524>You received a warning</color>\n{reason}";

    public string MuteBroadcast { get; set; } = "<color=#e5484d>You have been muted</color>\n{reason}";

    public string MuteExpiredBroadcast { get; set; } = "<color=#30a46c>Your mute has expired</color>";

    public string RemoteAdminBanSynced { get; set; } = "Ban sent to O5 Control and applied to the whole instance.";

    public string NorthwoodStaffProtected { get; set; } = "That player is Northwood staff and cannot be sanctioned.";

    public static bool TryNormalize(string? input, out string language)
    {
        language = (input ?? string.Empty).Trim().ToLowerInvariant();
        return Languages.ContainsKey(language);
    }

    public static string Available => string.Join(", ", Languages.Select(pair => $"{pair.Key} ({pair.Value})"));

    public static Translation Create(string language) => language switch
    {
        "es" => new Translation
        {
            Language = "es",
            BanScreen = "Estás baneado de esta red.\nMotivo: {reason}\nCaduca: {expires}",
            PermanentLabel = "nunca",
            KickScreen = "Has sido expulsado.\nMotivo: {reason}",
            WarnBroadcast = "<color=#f5a524>Has recibido un aviso</color>\n{reason}",
            MuteBroadcast = "<color=#e5484d>Has sido muteado</color>\n{reason}",
            MuteExpiredBroadcast = "<color=#30a46c>Tu mute ha terminado</color>",
            RemoteAdminBanSynced = "Ban enviado a O5 Control y aplicado en toda la instancia.",
            NorthwoodStaffProtected = "Ese jugador es staff de Northwood y no se puede sancionar.",
        },
        "fr" => new Translation
        {
            Language = "fr",
            BanScreen = "Vous êtes banni de ce réseau.\nMotif : {reason}\nExpire : {expires}",
            PermanentLabel = "jamais",
            KickScreen = "Vous avez été expulsé.\nMotif : {reason}",
            WarnBroadcast = "<color=#f5a524>Vous avez reçu un avertissement</color>\n{reason}",
            MuteBroadcast = "<color=#e5484d>Vous avez été rendu muet</color>\n{reason}",
            MuteExpiredBroadcast = "<color=#30a46c>Votre mute est terminé</color>",
            RemoteAdminBanSynced = "Ban envoyé à O5 Control et appliqué à toute l'instance.",
            NorthwoodStaffProtected = "Ce joueur fait partie du staff de Northwood et ne peut pas être sanctionné.",
        },
        "pl" => new Translation
        {
            Language = "pl",
            BanScreen = "Masz bana w tej sieci.\nPowód: {reason}\nWygasa: {expires}",
            PermanentLabel = "nigdy",
            KickScreen = "Zostałeś wyrzucony.\nPowód: {reason}",
            WarnBroadcast = "<color=#f5a524>Otrzymałeś ostrzeżenie</color>\n{reason}",
            MuteBroadcast = "<color=#e5484d>Zostałeś wyciszony</color>\n{reason}",
            MuteExpiredBroadcast = "<color=#30a46c>Twoje wyciszenie wygasło</color>",
            RemoteAdminBanSynced = "Ban wysłany do O5 Control i nałożony na całą instancję.",
            NorthwoodStaffProtected = "Ten gracz należy do staffu Northwood i nie można go ukarać.",
        },
        _ => new Translation(),
    };

    internal static string Name(string language) =>
        Languages.TryGetValue(language, out var name) ? name : language;
}

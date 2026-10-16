using System.Text.Json;
using Godot;

namespace ChronoClash.Game.Localization;

/// <summary>
/// Traductions. Le jeu est écrit en français ; chaque texte affiché passe par <see cref="T"/> (ou <see cref="F"/>
/// pour les textes avec des nombres), qui renvoie la traduction anglaise si le joueur a choisi l'anglais.
/// Les traductions sont dans game/translations/en.json : « texte français » → « English text ».
/// Pour ajouter un texte : l'écrire en français dans le code avec Loc.T("…"), puis ajouter sa ligne dans en.json.
/// Un texte sans traduction s'affiche simplement en français.
/// Les {0}, {1}… sont remplacés par les valeurs données à <see cref="F"/> (même ordre en français et en anglais).
/// </summary>
public static class Loc
{
    private const string EnglishFile = "res://translations/en.json";
    private static Dictionary<string, string>? _english;

    /// <summary>Langue actuelle : "fr" ou "en".</summary>
    public static string Language { get; private set; } = "fr";

    /// <summary>Prévient l'interface quand la langue change (pour se redessiner).</summary>
    public static event Action? LanguageChanged;

    public static void SetLanguage(string language)
    {
        Language = language == "en" ? "en" : "fr";
        if (Language == "en") _english ??= LoadEnglish();
        LanguageChanged?.Invoke();
    }

    /// <summary>Langue du système (anglais si l'ordinateur n'est pas en français).</summary>
    public static string SystemLanguage() => OS.GetLocaleLanguage() == "fr" ? "fr" : "en";

    private static Dictionary<string, string> LoadEnglish()
    {
        try
        {
            if (!Godot.FileAccess.FileExists(EnglishFile)) return new();
            using var file = Godot.FileAccess.Open(EnglishFile, Godot.FileAccess.ModeFlags.Read);
            var options = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
            return JsonSerializer.Deserialize<Dictionary<string, string>>(file.GetAsText(), options) ?? new();
        }
        catch (Exception e)
        {
            GD.PushWarning($"Traductions anglaises illisibles : {e.Message}");
            return new();
        }
    }

    /// <summary>Le texte dans la langue du joueur.</summary>
    public static string T(string french)
        => Language == "en" && _english != null && _english.TryGetValue(french, out var english) ? english : french;

    /// <summary>Le texte traduit, avec ses {0}, {1}… remplacés par les valeurs données.</summary>
    public static string F(string french, params object[] values)
    {
        try { return string.Format(T(french), values); }
        catch (FormatException) { return string.Format(french, values); }
    }
}

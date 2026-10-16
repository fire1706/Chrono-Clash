using ChronoClash.Game.Localization;
using Godot;

namespace ChronoClash.Game.Settings;

/// <summary>
/// Réglages du joueur (volumes, voix de l'annonceur, qualité graphique, langue, touches), enregistrés dans
/// user://settings.cfg. <see cref="Current"/> est chargé au premier usage ; <see cref="Save"/> enregistre et applique.
/// </summary>
public sealed class GameSettings
{
    private const string SavePath = "user://settings.cfg";
    private static GameSettings? _current;

    public static GameSettings Current => _current ??= Load();

    /// <summary>Volumes de 0 (muet) à 1 (maximum).</summary>
    public float MusicVolume { get; set; } = 0.6f;
    public float SfxVolume { get; set; } = 0.8f;
    /// <summary>Voix de l'annonceur (synthèse vocale du système).</summary>
    public bool VoiceEnabled { get; set; } = true;
    public float VoiceVolume { get; set; } = 0.8f;
    /// <summary>Qualité basse : troupes en pastilles, moins d'effets. Utile sur les petits ordinateurs.</summary>
    public bool LowQuality { get; set; }
    public string Language { get; set; } = Loc.SystemLanguage();
    /// <summary>Touches choisies par le joueur (action → touche physique). Les absentes gardent leur touche par défaut.</summary>
    public Dictionary<string, Key> Keys { get; } = new();

    /// <summary>Changé quand les réglages sont appliqués (l'audio écoute pour ajuster les volumes).</summary>
    public static event Action? Changed;

    public Key KeyFor(string action)
    {
        if (Keys.TryGetValue(action, out var k)) return k;
        foreach (var (a, _, d) in KeyBindings.Actions) if (a == action) return d;
        return Key.None;
    }

    private static GameSettings Load()
    {
        var s = new GameSettings();
        var cfg = new ConfigFile();
        if (cfg.Load(SavePath) == Error.Ok)
        {
            s.MusicVolume = (float)cfg.GetValue("audio", "music", s.MusicVolume);
            s.SfxVolume = (float)cfg.GetValue("audio", "sfx", s.SfxVolume);
            s.VoiceEnabled = (bool)cfg.GetValue("audio", "voice", s.VoiceEnabled);
            s.VoiceVolume = (float)cfg.GetValue("audio", "voice_volume", s.VoiceVolume);
            s.LowQuality = (bool)cfg.GetValue("video", "low_quality", s.LowQuality);
            s.Language = (string)cfg.GetValue("general", "language", s.Language);
            foreach (var (action, _, _) in KeyBindings.Actions)
                if (cfg.HasSectionKey("keys", action)) s.Keys[action] = (Key)(long)cfg.GetValue("keys", action);
        }
        s.ApplyNow();
        return s;
    }

    /// <summary>Applique les réglages (langue, touches) sans les enregistrer.</summary>
    public void ApplyNow()
    {
        Loc.SetLanguage(Language);
        KeyBindings.Apply(Keys);
        Changed?.Invoke();
    }

    public void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("audio", "music", MusicVolume);
        cfg.SetValue("audio", "sfx", SfxVolume);
        cfg.SetValue("audio", "voice", VoiceEnabled);
        cfg.SetValue("audio", "voice_volume", VoiceVolume);
        cfg.SetValue("video", "low_quality", LowQuality);
        cfg.SetValue("general", "language", Language);
        foreach (var (action, key) in Keys) cfg.SetValue("keys", action, (long)key);
        cfg.Save(SavePath);
        ApplyNow();
    }
}

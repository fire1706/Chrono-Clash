using Godot;

namespace ChronoClash.Game.Persistence;

/// <summary>
/// Nom, couleur et derniers réglages du joueur, enregistrés dans user://player.cfg
/// pour ne pas les retaper à chaque lancement.
/// </summary>
public sealed class PlayerProfile
{
    private const string SavePath = "user://player.cfg";

    public string Name { get; set; } = "Joueur";
    public int ColorIndex { get; set; }
    public string LastAddress { get; set; } = "127.0.0.1";
    public int Port { get; set; } = Net.EnetTransport.DefaultPort;
    public int Mode { get; set; }
    public int Difficulty { get; set; } = 1;
    public int Theme { get; set; }
    public bool Fog { get; set; } = true;
    public int Emblem { get; set; }
    /// <summary>Le tutoriel a été terminé (le bouton n'est plus mis en avant).</summary>
    public bool TutorialDone { get; set; }

    /// <summary>Couleurs proposées au joueur (nom affiché, valeur 0xRRGGBB).</summary>
    public static readonly (string Name, uint Rgb)[] ColorChoices =
    {
        ("Or", 0xF0B52A), ("Bleu", 0x3D7BE0), ("Rouge", 0xD64545), ("Violet", 0x9B59D0), ("Vert", 0x3FBF5A), ("Blanc", 0xEDEDED),
    };

    public uint ColorRgb => ColorChoices[Math.Clamp(ColorIndex, 0, ColorChoices.Length - 1)].Rgb;

    public static PlayerProfile Load()
    {
        var p = new PlayerProfile();
        var cfg = new ConfigFile();
        if (cfg.Load(SavePath) != Error.Ok) return p;
        p.Name = (string)cfg.GetValue("player", "name", p.Name);
        p.ColorIndex = (int)cfg.GetValue("player", "color", p.ColorIndex);
        p.LastAddress = (string)cfg.GetValue("network", "address", p.LastAddress);
        p.Port = (int)cfg.GetValue("network", "port", p.Port);
        p.Mode = (int)cfg.GetValue("match", "mode", p.Mode);
        p.Difficulty = (int)cfg.GetValue("match", "difficulty", p.Difficulty);
        p.Theme = (int)cfg.GetValue("match", "theme", p.Theme);
        p.Fog = (bool)cfg.GetValue("match", "fog", p.Fog);
        p.Emblem = (int)cfg.GetValue("player", "emblem", p.Emblem);
        p.TutorialDone = (bool)cfg.GetValue("player", "tutorial_done", p.TutorialDone);
        return p;
    }

    public void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("player", "name", Name);
        cfg.SetValue("player", "color", ColorIndex);
        cfg.SetValue("network", "address", LastAddress);
        cfg.SetValue("network", "port", Port);
        cfg.SetValue("match", "mode", Mode);
        cfg.SetValue("match", "difficulty", Difficulty);
        cfg.SetValue("match", "theme", Theme);
        cfg.SetValue("match", "fog", Fog);
        cfg.SetValue("player", "emblem", Emblem);
        cfg.SetValue("player", "tutorial_done", TutorialDone);
        cfg.Save(SavePath);
    }
}

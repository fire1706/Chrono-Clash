using System.Text.Json;
using ChronoClash.Core.Formations;
using Godot;

namespace ChronoClash.Game.Persistence;

/// <summary>
/// Les 4 formations dessinées par le joueur, enregistrées sur son ordinateur (user://formations.json)
/// pour les retrouver à la partie suivante. Au début de chaque partie, elles sont envoyées à l'hôte.
/// Sur Windows, user:// correspond à %APPDATA%\Godot\app_userdata\Chrono Clash\.
/// </summary>
public sealed class FormationStore
{
    private const string SavePath = "user://formations.json";

    public byte[][] Cells { get; } = new byte[4][];
    public string[] Names { get; } = new string[4];
    /// <summary>Formation choisie en dernier (0 à 3).</summary>
    public int Current { get; set; }

    private FormationStore()
    {
        for (int i = 0; i < 4; i++) ResetToPreset(i);
    }

    /// <summary>Remet une formation à son dessin de départ.</summary>
    public void ResetToPreset(int index)
    {
        var preset = FormationTemplate.Preset(index);
        Cells[index] = preset.Cells;
        Names[index] = preset.Name;
    }

    /// <summary>Lit le fichier enregistré ; en cas d'absence ou d'erreur, on repart des formations de départ.</summary>
    public static FormationStore Load()
    {
        var store = new FormationStore();
        try
        {
            if (!Godot.FileAccess.FileExists(SavePath)) return store;
            using var file = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Read);
            var saved = JsonSerializer.Deserialize<Saved>(file.GetAsText());
            if (saved?.Cells == null) return store;
            for (int i = 0; i < 4 && i < saved.Cells.Length; i++)
            {
                var cells = saved.Cells[i]?.Select(v => (byte)v).ToArray();
                if (cells != null && FormationTemplate.IsValid(cells)) store.Cells[i] = cells;
            }
            store.Current = Math.Clamp(saved.Current, 0, 3);
        }
        catch (Exception e)
        {
            GD.PushWarning($"Formations enregistrées illisibles, formations de départ utilisées : {e.Message}");
        }
        return store;
    }

    public void Save()
    {
        var saved = new Saved { Current = Current, Cells = Cells.Select(c => c.Select(v => (int)v).ToArray()).ToArray() };
        using var file = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Write);
        file?.StoreString(JsonSerializer.Serialize(saved));
    }

    private sealed class Saved
    {
        public int Current { get; set; }
        public int[][]? Cells { get; set; }
    }
}

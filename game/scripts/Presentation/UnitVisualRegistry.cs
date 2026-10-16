using ChronoClash.Core.Commands;
using Godot;

namespace ChronoClash.Game.Presentation;

/// <summary>
/// Cherche les images dessinées par le graphiste pour chaque troupe et chaque ère.
/// Convention (voir docs/ART_GUIDE.md) : res://assets/art/units/war_0.png (guerrier, Préhistoire), rng_3.png, tank_7.png…
/// et, en option, war_0_team.png : les zones en blanc de cette image prennent la couleur du clan.
/// Si une image n'existe pas, le jeu garde le dessin par défaut (UnitDrawings) : on peut avancer image par image.
/// </summary>
public sealed class UnitVisualRegistry
{
    private const string Folder = "res://assets/art/units/";
    private readonly Dictionary<(TroopKind, int), (Texture2D? Body, Texture2D? Team)> _cache = new();

    /// <summary>Taille à l'écran (en pixels du monde, zoom 1) de la plus grande dimension de l'image.</summary>
    public static float WorldSizeOf(TroopKind kind) => kind == TroopKind.Tank ? 46 : 22;

    public static string FilePrefix(TroopKind kind) => kind switch
    {
        TroopKind.Ranged => "rng",
        TroopKind.Tank => "tank",
        _ => "war",
    };

    /// <summary>Images de la troupe (null si le graphiste ne l'a pas encore dessinée).</summary>
    public (Texture2D? Body, Texture2D? Team) Get(TroopKind kind, int era)
    {
        if (_cache.TryGetValue((kind, era), out var found)) return found;
        string name = $"{FilePrefix(kind)}_{era}";
        var entry = (Load(Folder + name + ".png"), Load(Folder + name + "_team.png"));
        _cache[(kind, era)] = entry;
        return entry;
    }

    private static Texture2D? Load(string path) => ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
}

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
    private const string ChiefFolder = "res://assets/art/chiefs/";
    private readonly Dictionary<int, (Texture2D? Body, Texture2D? Team)> _chiefs = new();
    private readonly Dictionary<(TroopKind, int), (Texture2D? Body, Texture2D? Team)> _cache = new();

    /// <summary>Taille à l'écran (en pixels du monde, zoom 1) de la plus grande dimension de l'image.</summary>
    /// Images du prototype (tools/prototype-sprites) : 96 px = 46 px du monde pour les troupes à pied, 128 px = 64 px pour les tanks.
    public static float WorldSizeOf(TroopKind kind) => kind == TroopKind.Tank ? 64 : 46;

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

    /// <summary>Taille à l'écran d'un chef selon l'ère (les chefs à cheval et la jeep sont plus grands).</summary>
    /// Images du prototype : 128 px = 72 px du monde, pour tous les chefs (le cadre inclut les chevaux, la jeep, les armes).
    public static float ChiefWorldSize(int era) => 72;

    /// <summary>Images du chef d'une ère : res://assets/art/chiefs/chief_<ère>.png (+ _team).</summary>
    public (Texture2D? Body, Texture2D? Team) GetChief(int era)
    {
        if (_chiefs.TryGetValue(era, out var found)) return found;
        var entry = (Load($"{ChiefFolder}chief_{era}.png"), Load($"{ChiefFolder}chief_{era}_team.png"));
        _chiefs[era] = entry;
        return entry;
    }

    private static Texture2D? Load(string path) => ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
}

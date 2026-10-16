using ChronoClash.Core.Simulation;

namespace ChronoClash.Core.Map;

/// <summary>Couleur RVB sur 3 octets (le cœur ne dépend pas des couleurs de Godot).</summary>
public readonly record struct Rgb(byte R, byte G, byte B)
{
    public static Rgb Of(int r, int g, int b) => new((byte)r, (byte)g, (byte)b);
}

/// <summary>Style des arbres dessinés dans les forêts.</summary>
public enum TreeStyle { Leaf, Pine, Palm }

/// <summary>
/// Réglages d'un thème de carte : génération (quantité d'eau, rivières, rochers…) et couleurs du terrain.
/// Les valeurs sont celles du prototype HTML. Elles passeront dans data/themes.json à l'étape M2.
/// </summary>
public sealed class ThemeDef
{
    public required MapTheme Id { get; init; }

    // ---- Génération ----
    /// <summary>Décalage ajouté à l'altitude : négatif = plus d'eau (archipel), positif = moins d'eau (désert).</summary>
    public float HeightOffset { get; init; }
    /// <summary>Écart entre le niveau de l'eau et le niveau de l'eau profonde (infranchissable). Plus grand = plus d'eau traversable.</summary>
    public float DeepOffset { get; init; } = 0.03f;
    /// <summary>Multiplicateur de vitesse quand on patauge (ou sur la glace en hiver).</summary>
    public float WadeSpeed { get; init; } = 0.55f;
    /// <summary>Densité de végétation à partir de laquelle on est dans une forêt.</summary>
    public float ForestThreshold { get; init; } = 0.6f;
    public int Rivers { get; init; } = 3;
    public int MaxRocks { get; init; } = 680;
    /// <summary>Archipel : ajoute des gués en étoile et en anneau pour que toutes les îles soient reliées.</summary>
    public bool Fords { get; init; }

    // ---- Apparence ----
    public bool Flowers { get; init; } = true;
    public TreeStyle Trees { get; init; } = TreeStyle.Leaf;
    public Rgb Deep { get; init; }
    public Rgb Shallow { get; init; }
    public Rgb Sand { get; init; }
    public Rgb GrassA { get; init; }
    public Rgb GrassB { get; init; }
    public Rgb Dirt { get; init; }
    public Rgb Forest { get; init; }
    /// <summary>Couleurs de l'eau peu profonde, du bord (WetA) vers le large (WetB).</summary>
    public Rgb WetA { get; init; }
    public Rgb WetB { get; init; }
    public Rgb Tuft { get; init; }

    public static ThemeDef Get(MapTheme theme) => theme switch
    {
        MapTheme.Archipelago => Archipelago,
        MapTheme.Desert => Desert,
        MapTheme.Winter => Winter,
        _ => Temperate,
    };

    public static readonly ThemeDef Temperate = new()
    {
        Id = MapTheme.Temperate,
        Deep = Rgb.Of(38, 82, 106), Shallow = Rgb.Of(74, 131, 156), Sand = Rgb.Of(216, 197, 146),
        GrassA = Rgb.Of(124, 158, 74), GrassB = Rgb.Of(104, 142, 62), Dirt = Rgb.Of(150, 124, 82), Forest = Rgb.Of(72, 110, 52),
        WetA = Rgb.Of(128, 176, 170), WetB = Rgb.Of(74, 131, 156), Tuft = Rgb.Of(50, 80, 25),
    };

    public static readonly ThemeDef Archipelago = new()
    {
        Id = MapTheme.Archipelago, HeightOffset = -0.075f, DeepOffset = 0.11f, WadeSpeed = 0.6f, ForestThreshold = 0.58f,
        Rivers = 0, MaxRocks = 420, Fords = true,
        Deep = Rgb.Of(22, 66, 104), Shallow = Rgb.Of(54, 140, 170), Sand = Rgb.Of(236, 214, 160),
        GrassA = Rgb.Of(120, 172, 80), GrassB = Rgb.Of(96, 152, 70), Dirt = Rgb.Of(160, 140, 90), Forest = Rgb.Of(58, 116, 56),
        WetA = Rgb.Of(120, 200, 196), WetB = Rgb.Of(54, 140, 170), Tuft = Rgb.Of(50, 90, 25),
    };

    public static readonly ThemeDef Desert = new()
    {
        Id = MapTheme.Desert, HeightOffset = 0.035f, ForestThreshold = 0.72f, Rivers = 1, MaxRocks = 1000,
        Flowers = false, Trees = TreeStyle.Palm,
        Deep = Rgb.Of(60, 112, 132), Shallow = Rgb.Of(90, 156, 166), Sand = Rgb.Of(232, 206, 150),
        GrassA = Rgb.Of(222, 190, 128), GrassB = Rgb.Of(206, 170, 108), Dirt = Rgb.Of(186, 138, 86), Forest = Rgb.Of(150, 160, 80),
        WetA = Rgb.Of(120, 184, 176), WetB = Rgb.Of(70, 140, 150), Tuft = Rgb.Of(120, 90, 40),
    };

    public static readonly ThemeDef Winter = new()
    {
        Id = MapTheme.Winter, DeepOffset = 0.12f, WadeSpeed = 0.8f, ForestThreshold = 0.58f,
        Flowers = false, Trees = TreeStyle.Pine,
        Deep = Rgb.Of(40, 70, 96), Shallow = Rgb.Of(176, 206, 224), Sand = Rgb.Of(196, 204, 212),
        GrassA = Rgb.Of(238, 242, 246), GrassB = Rgb.Of(222, 230, 238), Dirt = Rgb.Of(186, 192, 200), Forest = Rgb.Of(214, 224, 232),
        WetA = Rgb.Of(222, 236, 244), WetB = Rgb.Of(176, 206, 224), Tuft = Rgb.Of(160, 175, 190),
    };
}

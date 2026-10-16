using ChronoClash.Core.Mathematics;

namespace ChronoClash.Core.Simulation;

/// <summary>État de la zone : en attente, en train de rétrécir, ou finale (plus de zone sûre).</summary>
public enum ZoneStage { Waiting, Shrinking, Final }

/// <summary>
/// La zone battle royale : un cercle qui rétrécit par phases. Seules les troupes et chefs individuellement
/// HORS du cercle prennent des dégâts. La prochaine zone est connue à l'avance (affichée en pointillés).
/// </summary>
public sealed class ZoneState
{
    public Vec2 Center { get; set; }
    public float Radius { get; set; }
    /// <summary>Rayon de départ (couvre toute la carte).</summary>
    public float InitialRadius { get; set; }
    public int Phase { get; set; }
    public ZoneStage Stage { get; set; } = ZoneStage.Waiting;
    /// <summary>Temps écoulé dans l'étape en cours (attente ou rétrécissement).</summary>
    public float Timer { get; set; }
    /// <summary>Prochaine zone (centre et rayon), ou null s'il n'y en a plus.</summary>
    public Vec2? NextCenter { get; set; }
    public float NextRadius { get; set; }
    /// <summary>Zone au début du rétrécissement (pour glisser vers la suivante).</summary>
    public Vec2 FromCenter { get; set; }
    public float FromRadius { get; set; }
    /// <summary>Dégâts par seconde hors zone (part de la vie maximale), 0 avant le premier rétrécissement.</summary>
    public float DamagePercent { get; set; }
    public float DamageTimer { get; set; }

    public bool IsOutside(float x, float y) => (new Vec2(x, y) - Center).Length > Radius;
    public bool IsOutside(Vec2 p) => IsOutside(p.X, p.Y);
}

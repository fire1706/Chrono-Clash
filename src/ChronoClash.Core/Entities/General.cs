using ChronoClash.Core.Commands;
using ChronoClash.Core.Data;
using ChronoClash.Core.Mathematics;

namespace ChronoClash.Core.Entities;

/// <summary>
/// Le chef d'un clan. C'est lui que le joueur dirige ; l'armée se place autour de lui.
/// S'il meurt, la troupe la plus proche prend le commandement (« chef promu ») avec ses propres statistiques.
/// Déplacé par <see cref="Systems.GeneralMovementSystem"/>, il attaque via <see cref="Systems.ChiefCombatSystem"/>.
/// </summary>
public sealed class General : Body
{
    /// <summary>Rayon de collision du chef, en pixels.</summary>
    public const float DefaultRadius = 14;

    public General(Vec2 position) : base(null, position, DefaultRadius, 240)
    {
        Angle = -MathF.PI / 2;
    }

    // ---- Ordres reçus ----
    /// <summary>Direction demandée au clavier (zéro si aucune touche). Prioritaire sur le chemin.</summary>
    public Vec2 InputDirection { get; set; }
    /// <summary>Étapes restantes du chemin demandé au clic droit.</summary>
    public List<Vec2> Path { get; } = new();
    /// <summary>Le joueur maintient la touche de sprint.</summary>
    public bool WantsSprint { get; set; }

    // ---- Endurance (sprint) ----
    public float Stamina { get; set; } = 100;
    public float StaminaRestTimer { get; set; }
    public bool Exhausted { get; set; }
    public bool Sprinting { get; set; }

    // ---- Statistiques de combat (changent avec l'ère, ou deviennent celles d'un soldat s'il est promu) ----
    public float Damage { get; set; } = 14;
    public float Cooldown { get; set; } = 0.8f;
    public float SpeedMultiplier { get; set; } = 1;
    /// <summary>Allonge au corps à corps (pixels au-delà du contact).</summary>
    public float Reach { get; set; } = 10;
    /// <summary>Portée de tir (0 = corps à corps uniquement). Les chefs tirent à partir de 1914.</summary>
    public float Range { get; set; }
    public string? Projectile { get; set; }
    public float Splash { get; set; }

    // ---- Succession ----
    /// <summary>Vrai si ce chef est un soldat qui a repris le commandement.</summary>
    public bool Promoted { get; set; }
    /// <summary>Type de troupe du soldat promu (pour ses statistiques et son dessin).</summary>
    public TroopKind HeirKind { get; set; }
    public int Promotions { get; set; }

    /// <summary>Applique les statistiques du chef d'une ère (ou du soldat promu à cette ère), en gardant la proportion de vie.</summary>
    public void ApplyEraStats(GameData data, int era)
    {
        float ratio = MaxHp > 0 ? Hp / MaxHp : 1;
        Era = era;
        if (Promoted)
        {
            var st = data.Stats(HeirKind, era);
            MaxHp = st.Hp; Damage = st.Damage; Cooldown = st.Cooldown; SpeedMultiplier = 1;
            Range = st.Range; Projectile = st.Projectile; Splash = st.Splash; Reach = 10;
        }
        else
        {
            var st = data.Chiefs[Math.Clamp(era, 0, data.Chiefs.Count - 1)];
            MaxHp = st.Hp; Damage = st.Damage; Cooldown = st.Cooldown; SpeedMultiplier = st.SpeedMultiplier;
            Range = st.Range; Projectile = st.Projectile; Splash = st.Splash; Reach = st.Reach;
        }
        Hp = MathF.Max(1, ratio * MaxHp);
    }
}

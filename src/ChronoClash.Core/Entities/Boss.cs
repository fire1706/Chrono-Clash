using ChronoClash.Core.Mathematics;

namespace ChronoClash.Core.Entities;

/// <summary>
/// Le boss neutre : une créature géante qui garde son repaire (des ruines). Il attaque quiconque s'approche,
/// frappe en zone, et rapporte beaucoup de ★ et de ◆ au clan qui l'abat. Sa forme dépend de l'ère moyenne des clans.
/// Il appartient au clan « neutre » du monde (World.Neutral), qui ne joue pas.
/// </summary>
public sealed class Boss : Body
{
    /// <summary>Les quatre formes de boss, selon l'ère moyenne des clans vivants.</summary>
    public static readonly string[] Names = { "Mammouth ancestral", "Dragon des âges", "Cuirassé titan", "Colosse mécanique" };

    public Boss(Army neutral, Vec2 lair, int era, int form, float hp, float damage, float cooldown) : base(neutral, lair, 30, hp)
    {
        Lair = lair;
        Era = era;
        Form = form;
        Damage = damage;
        Cooldown = cooldown;
        AttackTimer = 1;
    }

    /// <summary>Repaire du boss : il ne s'en éloigne jamais beaucoup.</summary>
    public Vec2 Lair { get; }
    /// <summary>Forme (0 à 3), voir <see cref="Names"/>.</summary>
    public int Form { get; }
    public string Name => Names[Form];
    public float Damage { get; }
    public float Cooldown { get; }
    public Body? Target { get; set; }
    public float ThinkTimer { get; set; }
}

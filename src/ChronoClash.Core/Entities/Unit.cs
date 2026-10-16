using ChronoClash.Core.Commands;
using ChronoClash.Core.Mathematics;

namespace ChronoClash.Core.Entities;

/// <summary>
/// Une troupe (guerrier, troupe à distance ou tank). Objet volontairement léger : il peut y en avoir
/// des centaines par armée. Elle rejoint sa place dans la formation, combat automatiquement les ennemis
/// à portée, et devient vétéran en survivant aux combats.
/// </summary>
public sealed class Unit : Body
{
    public Unit(int id, Army owner, TroopKind kind, int era, Vec2 position, float radius, float hp) : base(owner, position, radius, hp)
    {
        Id = id;
        Kind = kind;
        Era = era;
    }

    /// <summary>Identifiant unique dans la partie (sert aussi à trier les voisins sans double comptage).</summary>
    public int Id { get; }
    public TroopKind Kind { get; }

    // ---- Formation et déplacement ----
    /// <summary>Place dans la formation, relative au chef : X = vers l'avant, Y = vers la droite (pixels).</summary>
    public Vec2 Slot { get; set; }
    /// <summary>Chemin vers sa place quand un obstacle la sépare de son chef (sinon null).</summary>
    public List<Vec2>? Path { get; set; }
    /// <summary>Temps passé bloquée loin de son chef (au-delà de 5 s, elle le rejoint d'un coup).</summary>
    public float LostTime { get; set; }
    /// <summary>Âge en secondes (petite animation d'apparition).</summary>
    public float Age { get; set; }

    // ---- Combat ----
    /// <summary>Ennemi visé en ce moment (chef ou troupe), ou null.</summary>
    public Body? Target { get; set; }
    /// <summary>Temps avant de rechercher une cible (on ne cherche pas à chaque tick).</summary>
    public float ThinkTimer { get; set; }
    /// <summary>Ennemi inatteignable ignoré pendant un moment (de l'autre côté d'une rivière…).</summary>
    public Body? Ignored { get; set; }
    public double IgnoredUntil { get; set; }
    /// <summary>Temps passé à poursuivre une cible sans avancer.</summary>
    public float ChaseTime { get; set; }

    // ---- Tactiques ----
    /// <summary>Temps de charge restant (elle fonce tout droit en blessant tout ce qu'elle percute).</summary>
    public float ChargeTime { get; set; }
    /// <summary>Ennemis déjà percutés pendant cette charge (un seul coup chacun).</summary>
    public HashSet<Body> ChargeHits { get; } = new();
    /// <summary>Temps restant du cri de guerre (+30 % de dégâts, +15 % de vitesse).</summary>
    public float WarCryTime { get; set; }

    // ---- Vétérans ----
    /// <summary>Niveau de vétéran : 0 à 3 chevrons (+10 % de dégâts et de vie chacun).</summary>
    public int Veteran { get; set; }
    /// <summary>Nombre de combats survécus.</summary>
    public int Fights { get; set; }
    public bool InFight { get; set; }
    public double LastFightTime { get; set; } = -99;
}

using ChronoClash.Core.Mathematics;

namespace ChronoClash.Core.Entities;

/// <summary>
/// Tout ce qui occupe de la place sur la carte, appartient à un clan et peut être blessé : les chefs et les troupes.
/// Sert à la grille spatiale (recherche des voisins), au ramassage des bulles et au combat.
/// </summary>
public interface IBody
{
    Vec2 Position { get; }
    Vec2 Velocity { get; }
    float BodyRadius { get; }
    Army Owner { get; }
    bool IsDead { get; }
}

/// <summary>
/// Base commune des chefs (<see cref="General"/>) et des troupes (<see cref="Unit"/>) :
/// position, mouvement, vie, ère et petits minuteurs d'affichage.
/// </summary>
public abstract class Body : IBody
{
    protected Body(Army? owner, Vec2 position, float radius, float hp)
    {
        Owner = owner!;
        Position = PreviousPosition = LastPosition = position;
        Radius = radius;
        Hp = MaxHp = hp;
    }

    /// <summary>Le clan de ce corps (pour un chef : renseigné par le constructeur de <see cref="Army"/>).</summary>
    public Army Owner { get; internal set; }

    // ---- Position et mouvement ----
    public Vec2 Position { get; set; }
    /// <summary>Position au tick précédent : l'affichage glisse entre les deux pour rester fluide.</summary>
    public Vec2 PreviousPosition { get; set; }
    public Vec2 Velocity { get; set; }
    /// <summary>Direction du regard en radians (0 = vers la droite).</summary>
    public float Angle { get; set; }
    /// <summary>Compteur d'animation de marche (avance avec la vitesse).</summary>
    public float WalkCycle { get; set; }
    public float Radius { get; protected set; }
    /// <summary>Temps bloqué contre un obstacle, et dernière position (détection de blocage).</summary>
    public float StuckTime { get; set; }
    public Vec2 LastPosition { get; set; }

    // ---- Combat ----
    public float Hp { get; set; }
    public float MaxHp { get; set; }
    public bool Dead { get; set; }
    /// <summary>Ère de CE corps (pendant une évolution, une armée mélange deux ères).</summary>
    public int Era { get; set; }
    /// <summary>Temps restant avant la prochaine attaque.</summary>
    public float AttackTimer { get; set; }
    /// <summary>Moment (en secondes de partie) où ce corps a été touché pour la dernière fois.</summary>
    public double LastDamageTime { get; set; } = -99;

    // ---- Minuteurs d'affichage (le cœur les fait décroître, l'affichage les lit) ----
    /// <summary>Éclair blanc quand il est touché.</summary>
    public float HitFlash { get; set; }
    /// <summary>Lueur quand il vient de changer d'ère.</summary>
    public float EvolveFlash { get; set; }
    /// <summary>Animation d'attaque (coup de massue, recul de l'arme).</summary>
    public float SwingTime { get; set; }

    public float BodyRadius => Radius;
    public bool IsDead => Dead;
    public bool IsChief => this is General;

    /// <summary>Fait décroître les minuteurs d'affichage.</summary>
    public void TickVisualTimers(float dt)
    {
        HitFlash = MathF.Max(0, HitFlash - dt);
        EvolveFlash = MathF.Max(0, EvolveFlash - dt);
        SwingTime = MathF.Max(0, SwingTime - dt);
    }
}

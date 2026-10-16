using ChronoClash.Core.Data;
using ChronoClash.Core.Mathematics;

namespace ChronoClash.Core.AI;

/// <summary>Ce que fait un bot en ce moment (utile pour comprendre son comportement et pour le sprint).</summary>
public enum BotMode { Roam, Hunt, Flee, Zone, Objective }

/// <summary>
/// La « mémoire » d'un bot : sa personnalité, ce qu'il a vu récemment (position et force des autres clans),
/// son mode actuel et la formation choisie. Chaque bot a la sienne.
/// </summary>
public sealed class BotBrain
{
    public BotBrain(PersonalityDef personality, float firstThink)
    {
        Personality = personality;
        ThinkTimer = firstThink;
    }

    public PersonalityDef Personality { get; }
    public BotMode Mode { get; set; } = BotMode.Roam;
    public float ThinkTimer { get; set; }
    /// <summary>Formation actuellement demandée (-1 = pas encore choisie).</summary>
    public int Formation { get; set; } = -1;
    /// <summary>Dernière position et force connues de chaque clan aperçu (clé = numéro du clan).</summary>
    public Dictionary<int, (Vec2 Position, double SeenAt, float Strength)> Memory { get; } = new();
}

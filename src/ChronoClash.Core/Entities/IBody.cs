using ChronoClash.Core.Mathematics;

namespace ChronoClash.Core.Entities;

/// <summary>
/// Tout ce qui occupe de la place sur la carte et appartient à un clan : les chefs et les troupes.
/// Sert à la grille spatiale (recherche rapide des voisins), au ramassage des bulles et plus tard au combat.
/// </summary>
public interface IBody
{
    Vec2 Position { get; }
    float BodyRadius { get; }
    Army Owner { get; }
    bool IsDead { get; }
}

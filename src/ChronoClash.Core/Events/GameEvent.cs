namespace ChronoClash.Core.Events;

/// <summary>
/// Quelque chose de notable s'est produit dans la simulation. Le cœur ne joue aucun son et n'affiche rien :
/// l'UI, l'audio et l'annonceur (côté Godot) écoutent ces événements.
/// </summary>
public abstract record GameEvent(long Tick);

public sealed record MatchStartedEvent(long Tick) : GameEvent(Tick);

/// <summary>Le chemin demandé est impossible (destination dans l'eau profonde, île inaccessible…).</summary>
public sealed record PathFailedEvent(long Tick, int PlayerId) : GameEvent(Tick);

/// <summary>Un clan a ramassé une bulle (pour les effets visuels et les sons).</summary>
public sealed record BubbleCollectedEvent(long Tick, int ArmyId, Entities.BubbleKind Kind, float X, float Y, int Resources, int Points) : GameEvent(Tick);

/// <summary>Une troupe a été achetée.</summary>
public sealed record TroopRecruitedEvent(long Tick, int ArmyId, Commands.TroopKind Kind) : GameEvent(Tick);

/// <summary>Achat impossible (pas assez de ressources ◆).</summary>
public sealed record BuyFailedEvent(long Tick, int PlayerId, Commands.TroopKind Kind, int Cost, int Have) : GameEvent(Tick);

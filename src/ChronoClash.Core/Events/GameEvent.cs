namespace ChronoClash.Core.Events;

/// <summary>
/// Quelque chose de notable s'est produit dans la simulation. Le cœur ne joue aucun son et n'affiche rien :
/// l'UI, l'audio et l'annonceur (côté Godot) écoutent ces événements.
/// </summary>
public abstract record GameEvent(long Tick);

public sealed record MatchStartedEvent(long Tick) : GameEvent(Tick);

namespace ChronoClash.Core.Events;

/// <summary>
/// Quelque chose de notable s'est produit dans la simulation. Le cœur ne joue aucun son et n'affiche rien :
/// l'UI, l'audio et l'annonceur (côté Godot) écoutent ces événements.
/// </summary>
public abstract record GameEvent(long Tick);

public sealed record MatchStartedEvent(long Tick) : GameEvent(Tick);

/// <summary>Le chemin demandé est impossible (destination dans l'eau profonde, île inaccessible…).</summary>
public sealed record PathFailedEvent(long Tick, int PlayerId) : GameEvent(Tick);

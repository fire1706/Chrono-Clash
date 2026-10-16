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

/// <summary>Un chef ou une troupe est mort. Killer = clan du tueur (-1 = mort sans tueur, par exemple dans la zone).</summary>
public sealed record BodyDiedEvent(long Tick, int ArmyId, bool IsChief, Commands.TroopKind? Kind, float X, float Y, int KillerArmyId, int Points, int Resources, int Multiplier) : GameEvent(Tick);

/// <summary>Le chef est mort et un soldat a repris le commandement.</summary>
public sealed record ChiefPromotedEvent(long Tick, int ArmyId, Commands.TroopKind HeirKind) : GameEvent(Tick);

/// <summary>Un clan n'a plus ni chef ni troupe. Killer = clan qui l'a touché en dernier (-1 si aucun).</summary>
public sealed record ArmyEliminatedEvent(long Tick, int ArmyId, int KillerArmyId, int ClansLeft) : GameEvent(Tick);

/// <summary>Un clan commence son passage à l'ère suivante.</summary>
public sealed record EraStartedEvent(long Tick, int ArmyId, int Era) : GameEvent(Tick);

/// <summary>Toute l'armée du clan est passée à la nouvelle ère.</summary>
public sealed record EraCompletedEvent(long Tick, int ArmyId, int Era) : GameEvent(Tick);

/// <summary>Une tactique a été lancée (Count = nombre de troupes concernées).</summary>
public sealed record TacticUsedEvent(long Tick, int ArmyId, Commands.TacticKind Tactic, int Count, float X, float Y) : GameEvent(Tick);

/// <summary>Une tactique n'a pas pu être lancée (encore en recharge, pas de troupe adaptée…).</summary>
public sealed record TacticFailedEvent(long Tick, int PlayerId, Commands.TacticKind Tactic, string Reason, float Seconds = 0) : GameEvent(Tick);

/// <summary>Une troupe gagne un chevron de vétéran.</summary>
public sealed record VeteranEvent(long Tick, int ArmyId, float X, float Y, int Level) : GameEvent(Tick);

/// <summary>Un projectile tiré est arrivé (pour les effets d'impact et d'explosion).</summary>
public sealed record ProjectileImpactEvent(long Tick, string Kind, float X, float Y, float Splash) : GameEvent(Tick);

/// <summary>Une attaque au corps à corps (pour les étincelles et le son).</summary>
public sealed record MeleeHitEvent(long Tick, float X, float Y) : GameEvent(Tick);

/// <summary>La zone commence à se resserrer.</summary>
public sealed record ZoneShrinkingEvent(long Tick, int Phase) : GameEvent(Tick);

/// <summary>Un largage est annoncé (il tombera dans quelques secondes).</summary>
public sealed record CrateAnnouncedEvent(long Tick, float X, float Y, float Seconds) : GameEvent(Tick);

/// <summary>Un clan a récupéré le largage.</summary>
public sealed record CrateCollectedEvent(long Tick, int ArmyId, float X, float Y) : GameEvent(Tick);

/// <summary>Un boss est apparu, ou a été abattu (KillerArmyId = -1 s'il n'y a pas de tueur).</summary>
public sealed record BossSpawnedEvent(long Tick, string Name, float X, float Y) : GameEvent(Tick);
public sealed record BossKilledEvent(long Tick, string Name, int KillerArmyId, int Points, int Resources) : GameEvent(Tick);

/// <summary>Un clan a traversé un portail temporel (Count troupes ont évolué).</summary>
public sealed record PortalUsedEvent(long Tick, int ArmyId, float X, float Y, int Count) : GameEvent(Tick);

/// <summary>Un clan a capturé un sanctuaire.</summary>
public sealed record ShrineCapturedEvent(long Tick, int ArmyId, Entities.ShrineKind Kind, float X, float Y) : GameEvent(Tick);

/// <summary>Événement aléatoire : pluie de météores, orage (début et fin), migration de bulles.</summary>
public sealed record WorldEventStartedEvent(long Tick, string Kind) : GameEvent(Tick);

/// <summary>Un météore s'est écrasé.</summary>
public sealed record MeteorImpactEvent(long Tick, float X, float Y) : GameEvent(Tick);

/// <summary>La partie est finie : un seul clan reste debout.</summary>
public sealed record MatchEndedEvent(long Tick, int WinnerArmyId) : GameEvent(Tick);

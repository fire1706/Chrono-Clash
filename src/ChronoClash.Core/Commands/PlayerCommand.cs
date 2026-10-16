namespace ChronoClash.Core.Commands;

/// <summary>
/// Un ordre donné par un joueur ou un bot. En réseau, les clients n'envoient QUE des ordres,
/// jamais des positions. L'hôte applique les ordres et renvoie l'état du monde.
/// </summary>
public abstract record PlayerCommand(long Tick, int PlayerId);

public sealed record MoveToCommand(long Tick, int PlayerId, float X, float Y, bool Queue) : PlayerCommand(Tick, PlayerId);
public sealed record DirectionCommand(long Tick, int PlayerId, float Dx, float Dy) : PlayerCommand(Tick, PlayerId);
public sealed record StopCommand(long Tick, int PlayerId) : PlayerCommand(Tick, PlayerId);
public sealed record SprintCommand(long Tick, int PlayerId, bool On) : PlayerCommand(Tick, PlayerId);

public enum TacticKind { Charge, Retreat, Aim, WarCry }
public sealed record TacticCommand(long Tick, int PlayerId, TacticKind Kind, float AimX = 0, float AimY = 0) : PlayerCommand(Tick, PlayerId);

public enum TroopKind { Warrior, Ranged, Tank }
public sealed record BuyCommand(long Tick, int PlayerId, TroopKind Kind) : PlayerCommand(Tick, PlayerId);

public sealed record SelectFormationCommand(long Tick, int PlayerId, int Index) : PlayerCommand(Tick, PlayerId);
public sealed record SetFormationCellsCommand(long Tick, int PlayerId, int Index, byte[] Cells) : PlayerCommand(Tick, PlayerId);

/// <summary>Test : ajoute des troupes autour du chef. Refusé par l'hôte si la partie n'autorise pas les triches.</summary>
public sealed record DebugSpawnTroopsCommand(long Tick, int PlayerId, int Count) : PlayerCommand(Tick, PlayerId);

/// <summary>Test : fait apparaître un clan ennemi immobile devant le chef (EraOffset = ères d'écart avec le joueur).</summary>
public sealed record DebugSpawnEnemyCommand(long Tick, int PlayerId, int Count, int EraOffset) : PlayerCommand(Tick, PlayerId);

/// <summary>Test : ajoute des points ★ (pour essayer les changements d'ère).</summary>
public sealed record DebugAddPointsCommand(long Tick, int PlayerId, int Amount) : PlayerCommand(Tick, PlayerId);

using ChronoClash.Core.Commands;

namespace ChronoClash.Core.Net;

/// <summary>
/// Transforme un ordre en octets (pour l'envoyer) et inversement.
/// Format : [MessageType.Command][type d'ordre][tick (8 octets)][joueur (4 octets)][champs de l'ordre…]
///
/// Pour ajouter un nouvel ordre : lui donner un numéro dans <see cref="Kind"/>, puis l'écrire dans Encode
/// et le lire dans Decode, DANS LE MÊME ORDRE des champs. Penser à augmenter CoreInfo.ProtocolVersion.
/// </summary>
public static class CommandCodec
{
    private enum Kind : byte
    {
        MoveTo = 1, Direction = 2, Stop = 3, Sprint = 4, Tactic = 5, Buy = 6, SelectFormation = 7, SetFormationCells = 8, DebugSpawnTroops = 9, DebugSpawnEnemy = 10, DebugAddPoints = 11,
    }

    public static byte[] Encode(PlayerCommand command)
    {
        using var stream = new MemoryStream(32);
        using var w = new BinaryWriter(stream);
        w.Write((byte)MessageType.Command);
        switch (command)
        {
            case MoveToCommand c: Header(w, Kind.MoveTo, c); w.Write(c.X); w.Write(c.Y); w.Write(c.Queue); break;
            case DirectionCommand c: Header(w, Kind.Direction, c); w.Write(c.Dx); w.Write(c.Dy); break;
            case StopCommand c: Header(w, Kind.Stop, c); break;
            case SprintCommand c: Header(w, Kind.Sprint, c); w.Write(c.On); break;
            case TacticCommand c: Header(w, Kind.Tactic, c); w.Write((byte)c.Kind); w.Write(c.AimX); w.Write(c.AimY); break;
            case BuyCommand c: Header(w, Kind.Buy, c); w.Write((byte)c.Kind); break;
            case SelectFormationCommand c: Header(w, Kind.SelectFormation, c); w.Write((byte)c.Index); break;
            case SetFormationCellsCommand c: Header(w, Kind.SetFormationCells, c); w.Write((byte)c.Index); w.Write((ushort)c.Cells.Length); w.Write(c.Cells); break;
            case DebugSpawnTroopsCommand c: Header(w, Kind.DebugSpawnTroops, c); w.Write((ushort)c.Count); break;
            case DebugSpawnEnemyCommand c: Header(w, Kind.DebugSpawnEnemy, c); w.Write((ushort)c.Count); w.Write((sbyte)c.EraOffset); break;
            case DebugAddPointsCommand c: Header(w, Kind.DebugAddPoints, c); w.Write(c.Amount); break;
            default: throw new ArgumentException($"Ordre inconnu : {command.GetType().Name}");
        }
        w.Flush();
        return stream.ToArray();
    }

    private static void Header(BinaryWriter w, Kind kind, PlayerCommand c)
    {
        w.Write((byte)kind);
        w.Write(c.Tick);
        w.Write(c.PlayerId);
    }

    /// <summary>Relit un ordre. Renvoie null si le message est invalide (ne fait jamais planter l'hôte).</summary>
    public static PlayerCommand? Decode(byte[] data)
    {
        try
        {
            using var r = new BinaryReader(new MemoryStream(data));
            if ((MessageType)r.ReadByte() != MessageType.Command) return null;
            var kind = (Kind)r.ReadByte();
            long tick = r.ReadInt64();
            int player = r.ReadInt32();
            return kind switch
            {
                Kind.MoveTo => new MoveToCommand(tick, player, r.ReadSingle(), r.ReadSingle(), r.ReadBoolean()),
                Kind.Direction => new DirectionCommand(tick, player, r.ReadSingle(), r.ReadSingle()),
                Kind.Stop => new StopCommand(tick, player),
                Kind.Sprint => new SprintCommand(tick, player, r.ReadBoolean()),
                Kind.Tactic => new TacticCommand(tick, player, (TacticKind)r.ReadByte(), r.ReadSingle(), r.ReadSingle()),
                Kind.Buy => new BuyCommand(tick, player, (TroopKind)r.ReadByte()),
                Kind.SelectFormation => new SelectFormationCommand(tick, player, r.ReadByte()),
                Kind.SetFormationCells => DecodeCells(r, tick, player),
                Kind.DebugSpawnTroops => new DebugSpawnTroopsCommand(tick, player, r.ReadUInt16()),
                Kind.DebugSpawnEnemy => new DebugSpawnEnemyCommand(tick, player, r.ReadUInt16(), r.ReadSByte()),
                Kind.DebugAddPoints => new DebugAddPointsCommand(tick, player, r.ReadInt32()),
                _ => null,
            };
        }
        catch (EndOfStreamException) { return null; }
    }

    private static SetFormationCellsCommand DecodeCells(BinaryReader r, long tick, int player)
    {
        int index = r.ReadByte();
        int length = r.ReadUInt16();
        return new SetFormationCellsCommand(tick, player, index, r.ReadBytes(length));
    }
}

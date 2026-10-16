using ChronoClash.Core.Entities;
using ChronoClash.Core.Map;
using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Simulation;

namespace ChronoClash.Core.Net;

/// <summary>
/// Synchronisation des bulles : plutôt que de renvoyer les ~700 bulles 10 fois par seconde, l'hôte n'envoie
/// que les bulles apparues et disparues depuis le dernier envoi (en mode fiable, pour ne rien rater).
/// Au début de la partie, la liste complète part sous la forme d'un seul gros envoi.
/// </summary>
public sealed class BubbleSync
{
    private readonly HashSet<int> _known = new();
    private readonly List<Bubble> _added = new();
    private readonly List<int> _removed = new();

    /// <summary>Côté hôte : compare la liste actuelle à la précédente. Renvoie null si rien n'a changé.</summary>
    public byte[]? Diff(World world)
    {
        _added.Clear();
        _removed.Clear();
        var current = new HashSet<int>();
        foreach (var b in world.Bubbles)
        {
            current.Add(b.Id);
            if (!_known.Contains(b.Id)) _added.Add(b);
        }
        foreach (int id in _known) if (!current.Contains(id)) _removed.Add(id);
        _known.Clear();
        _known.UnionWith(current);
        if (_added.Count == 0 && _removed.Count == 0) return null;

        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream);
        w.Write((byte)MessageType.Bubbles);
        w.Write(_added.Count);
        foreach (var b in _added)
        {
            w.Write(b.Id);
            w.Write((byte)b.Kind);
            w.Write(SnapshotWriter.Q(b.Position.X));
            w.Write(SnapshotWriter.Q(b.Position.Y));
            w.Write(b.Radius);
        }
        w.Write(_removed.Count);
        foreach (int id in _removed) w.Write(id);
        w.Flush();
        return stream.ToArray();
    }

    /// <summary>Côté client : applique un envoi de l'hôte à la copie locale du monde.</summary>
    public static void Apply(World world, byte[] data)
    {
        using var r = new BinaryReader(new MemoryStream(data));
        if ((MessageType)r.ReadByte() != MessageType.Bubbles) return;
        int added = r.ReadInt32();
        for (int i = 0; i < added; i++)
        {
            int id = r.ReadInt32();
            var kind = (BubbleKind)r.ReadByte();
            var p = new Vec2(SnapshotReader.DQ(r.ReadUInt16()), SnapshotReader.DQ(r.ReadUInt16()));
            float radius = r.ReadSingle();
            world.Bubbles.Add(new Bubble(id, kind, p, radius, id * 0.37f % 6.28f));
        }
        int removed = r.ReadInt32();
        if (removed == 0) return;
        var gone = new HashSet<int>();
        for (int i = 0; i < removed; i++) gone.Add(r.ReadInt32());
        world.Bubbles.RemoveAll(b => gone.Contains(b.Id));
    }
}

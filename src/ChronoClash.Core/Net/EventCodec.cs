using System.Text.Json;
using ChronoClash.Core.Data;
using ChronoClash.Core.Events;

namespace ChronoClash.Core.Net;

/// <summary>
/// Envoie les événements de la simulation aux clients (pour leurs sons, annonces et effets).
/// Chaque événement est écrit avec son nom de type puis son contenu en JSON : tout nouvel événement
/// ajouté dans Events/ est donc transmis automatiquement, sans rien coder ici.
/// Les coups au corps à corps (très nombreux) ne sont pas envoyés.
/// </summary>
public static class EventCodec
{
    private static readonly Dictionary<string, Type> Types = typeof(GameEvent).Assembly.GetTypes()
        .Where(t => t.IsSubclassOf(typeof(GameEvent)) && !t.IsAbstract)
        .ToDictionary(t => t.Name);

    /// <summary>Faut-il transmettre cet événement ? (les plus fréquents et sans intérêt à distance sont filtrés)</summary>
    public static bool ShouldSend(GameEvent e) => e is not MeleeHitEvent && !(e is ProjectileImpactEvent p && p.Splash <= 0);

    public static byte[] Encode(IReadOnlyList<GameEvent> events)
    {
        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream);
        w.Write((byte)MessageType.Events);
        var list = events.Where(ShouldSend).ToList();
        w.Write((ushort)list.Count);
        foreach (var e in list)
        {
            w.Write(e.GetType().Name);
            w.Write(JsonSerializer.Serialize(e, e.GetType(), GameData.JsonOptions));
        }
        w.Flush();
        return stream.ToArray();
    }

    public static List<GameEvent> Decode(byte[] data)
    {
        var result = new List<GameEvent>();
        try
        {
            using var r = new BinaryReader(new MemoryStream(data));
            if ((MessageType)r.ReadByte() != MessageType.Events) return result;
            int n = r.ReadUInt16();
            for (int i = 0; i < n; i++)
            {
                string name = r.ReadString(), json = r.ReadString();
                if (Types.TryGetValue(name, out var type) && JsonSerializer.Deserialize(json, type, GameData.JsonOptions) is GameEvent e) result.Add(e);
            }
        }
        catch (Exception ex) when (ex is EndOfStreamException or JsonException) { }
        return result;
    }
}

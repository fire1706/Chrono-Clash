using System.Text;
using System.Text.Json;
using ChronoClash.Core.Data;
using ChronoClash.Core.Simulation;

namespace ChronoClash.Core.Net;

/// <summary>Un joueur humain dans le salon d'attente.</summary>
public sealed class LobbyPlayer
{
    public int PlayerId { get; set; }
    public string Name { get; set; } = "";
    public uint Color { get; set; }
    public bool IsHost { get; set; }
}

public sealed class HelloMessage
{
    public int Protocol { get; set; }
    public string Name { get; set; } = "";
    public uint Color { get; set; }
}

public sealed class WelcomeMessage
{
    public int PlayerId { get; set; }
}

public sealed class RejectMessage
{
    public string Reason { get; set; } = "";
}

public sealed class LobbyMessage
{
    public List<LobbyPlayer> Players { get; set; } = new();
    public MatchSettings Settings { get; set; } = new();
}

/// <summary>Un clan de la partie : qui le joue (humain ou bot), son nom, sa couleur, sa personnalité de bot.</summary>
public sealed class RosterEntry
{
    public int ArmyId { get; set; }
    public int PlayerId { get; set; }
    public string Name { get; set; } = "";
    public uint Color { get; set; }
    public bool IsBot { get; set; }
    public string? Personality { get; set; }
}

public sealed class StartMessage
{
    public MatchSettings Settings { get; set; } = new();
    public List<RosterEntry> Roster { get; set; } = new();
}

/// <summary>
/// Les messages rares (salon d'attente, début de partie) sont en JSON : faciles à lire et à faire évoluer.
/// Les messages fréquents (ordres, snapshots, bulles) sont en binaire compact.
/// </summary>
public static class JsonMessage
{
    public static byte[] Encode<T>(MessageType type, T body)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(body, GameData.JsonOptions);
        var bytes = new byte[json.Length + 1];
        bytes[0] = (byte)type;
        json.CopyTo(bytes, 1);
        return bytes;
    }

    public static T? Decode<T>(byte[] data) where T : class
    {
        try { return JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(data, 1, data.Length - 1), GameData.JsonOptions); }
        catch (JsonException) { return null; }
    }
}

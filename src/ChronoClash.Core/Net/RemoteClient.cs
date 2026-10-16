using ChronoClash.Core.Commands;
using ChronoClash.Core.Events;
using ChronoClash.Core.Simulation;

namespace ChronoClash.Core.Net;

/// <summary>
/// Côté joueur qui a rejoint une partie : il envoie ses ordres à l'hôte et tient à jour une COPIE du monde
/// à partir de ce que l'hôte lui envoie (la carte est regénérée localement grâce à la graine).
/// Il ne simule rien lui-même : c'est l'hôte qui fait autorité.
/// </summary>
public sealed class RemoteClient
{
    private readonly ITransport _transport;
    private readonly string _name;
    private readonly uint _color;
    private float _visionTimer;

    private readonly int _emblem;

    public RemoteClient(ITransport transport, string name, uint color, int emblem = 0)
    {
        _transport = transport;
        _name = name;
        _color = color;
        _emblem = emblem;
        _transport.PeerConnected += OnConnected;
        _transport.PeerDisconnected += _ => Disconnected?.Invoke();
    }

    /// <summary>Numéro de joueur donné par l'hôte (0 tant qu'on n'est pas accepté).</summary>
    public int PlayerId { get; private set; }
    public List<LobbyPlayer> Lobby { get; private set; } = new();
    public MatchSettings LobbySettings { get; private set; } = new();
    /// <summary>Copie locale du monde, créée au début de la partie.</summary>
    public World? World { get; private set; }
    /// <summary>Temps écoulé depuis le dernier snapshot (pour l'interpolation de l'affichage).</summary>
    public float SinceSnapshot { get; private set; }

    /// <summary>Oublie l'hôte (après un échec de connexion) : le numéro de joueur repasse à -1.</summary>
    public void Reset() => PlayerId = -1;

    public event Action? LobbyChanged;
    public event Action? MatchStarted;
    public event Action<string>? Rejected;
    public event Action? Disconnected;
    public event Action<GameEvent>? EventReceived;

    private void OnConnected(int peer)
    {
        if (peer != ClientSession.HostPeerId) return;
        _transport.Send(ClientSession.HostPeerId, JsonMessage.Encode(MessageType.Hello, new HelloMessage { Protocol = CoreInfo.ProtocolVersion, Name = _name, Color = _color, Emblem = _emblem }), reliable: true);
    }

    /// <summary>Envoie un ordre à l'hôte (en mode fiable : un ordre ne doit pas se perdre).</summary>
    public void Send(PlayerCommand command) => _transport.Send(ClientSession.HostPeerId, CommandCodec.Encode(command), reliable: true);

    public void Update(double realSeconds)
    {
        SinceSnapshot += (float)realSeconds;
        _transport.Poll(OnMessage);
        // Brouillard local : on recalcule la vision de notre clan (les autres nous sont envoyés déjà filtrés).
        if (World?.FindArmyOfPlayer(PlayerId) is { } me)
        {
            _visionTimer -= (float)realSeconds;
            if (_visionTimer <= 0) { _visionTimer = 0.25f; World.Vision[me.Id].Compute(me, World.Data.Tuning.Vision); }
        }
    }

    private void OnMessage(int peer, byte[] data)
    {
        if (data.Length == 0) return;
        switch ((MessageType)data[0])
        {
            case MessageType.Welcome:
                PlayerId = JsonMessage.Decode<WelcomeMessage>(data)?.PlayerId ?? 0;
                break;
            case MessageType.Reject:
                Rejected?.Invoke(JsonMessage.Decode<RejectMessage>(data)?.Reason ?? "refusé par l'hôte");
                break;
            case MessageType.Lobby:
                if (JsonMessage.Decode<LobbyMessage>(data) is { } lobby) { Lobby = lobby.Players; LobbySettings = lobby.Settings; LobbyChanged?.Invoke(); }
                break;
            case MessageType.Start:
                if (JsonMessage.Decode<StartMessage>(data) is { } start) BuildWorld(start);
                break;
            case MessageType.Snapshot when World != null:
                SnapshotReader.Apply(World, data);
                SinceSnapshot = 0;
                break;
            case MessageType.Bubbles when World != null:
                BubbleSync.Apply(World, data);
                break;
            case MessageType.Events when World != null:
                foreach (var e in EventCodec.Decode(data)) EventReceived?.Invoke(e);
                break;
        }
    }

    /// <summary>Crée la copie locale : même carte (même graine), mêmes clans dans le même ordre.</summary>
    private void BuildWorld(StartMessage start)
    {
        World = new World(start.Settings);
        Systems.ZoneSystem.Init(World);
        foreach (var entry in start.Roster.OrderBy(e => e.ArmyId))
        {
            var army = World.CreateArmy(entry.PlayerId, entry.Name, entry.Color, entry.IsBot);
            army.Emblem = entry.Emblem;
            if (entry.Personality != null) army.Brain = new AI.BotBrain(World.Data.Bots.Personality(entry.Personality), float.MaxValue); // pour l'affichage seulement
        }
        MatchStarted?.Invoke();
    }
}

using ChronoClash.Core.AI;
using ChronoClash.Core.Commands;
using ChronoClash.Core.Entities;
using ChronoClash.Core.Simulation;

namespace ChronoClash.Core.Net;

/// <summary>
/// Côté hôte : salon d'attente, puis partie.
/// - Le joueur de l'hôte parle par un transport LOCAL (en mémoire), comme en solo.
/// - Les autres joueurs passent par un transport RÉSEAU (ENet dans Godot) ; il est absent en solo.
/// Pendant la partie, l'hôte applique les ordres reçus, fait avancer la simulation et envoie à chaque joueur
/// distant : un snapshot filtré par son brouillard (10 fois par seconde), les événements et les bulles.
/// Un joueur qui se déconnecte en cours de partie est remplacé par un bot.
/// </summary>
public sealed class HostSession
{
    /// <summary>Intervalle entre deux snapshots (secondes).</summary>
    public const float SnapshotInterval = 0.1f;
    /// <summary>Le joueur de l'hôte est toujours le joueur numéro 1.</summary>
    public const int HostPlayerId = 1;

    private readonly ITransport? _local;
    private readonly ITransport? _network;
    private readonly Dictionary<int, int> _localPeers = new();   // connexion locale → joueur
    private readonly Dictionary<int, int> _networkPeers = new(); // connexion réseau → joueur
    private BubbleSync _bubbles = new();
    private float _snapshotTimer;
    private int _nextPlayerId = 2;

    /// <param name="local">Bout « hôte » du transport local (le joueur de cette machine).</param>
    /// <param name="network">Transport réseau en écoute, ou null pour une partie solo.</param>
    public HostSession(ITransport local, ITransport? network, string hostName, uint hostColor, int hostEmblem = 0)
    {
        _local = local;
        _network = network;
        Players.Add(new LobbyPlayer { PlayerId = HostPlayerId, Name = hostName, Color = hostColor, Emblem = hostEmblem, IsHost = true });
        if (_network != null)
        {
            _network.PeerDisconnected += OnPeerDisconnected;
        }
    }

    /// <summary>
    /// Serveur dédié : personne ne joue sur cette machine. Le premier joueur connecté devient le « chef de salon » :
    /// c'est lui qui choisit les réglages et lance la partie (message HostRequest).
    /// Quand tous les joueurs sont partis, le serveur revient au salon d'attente pour une nouvelle partie.
    /// </summary>
    public static HostSession Dedicated(ITransport network) => new(network);

    private HostSession(ITransport network)
    {
        _network = network;
        IsDedicated = true;
        _nextPlayerId = 1;
        _network.PeerDisconnected += OnPeerDisconnected;
    }

    /// <summary>Vrai pour un serveur dédié (aucun joueur local).</summary>
    public bool IsDedicated { get; }

    /// <summary>Messages pour la console du serveur (arrivées, départs, début et fin de partie).</summary>
    public event Action<string>? Log;

    /// <summary>Hôte pour une partie déjà créée (tests, ou partie lancée sans salon).</summary>
    public HostSession(Match match, ITransport local) : this(local, null, "Hôte", 0xF0B52A)
    {
        Match = match;
    }

    /// <summary>Joueurs humains du salon (l'hôte en premier).</summary>
    public List<LobbyPlayer> Players { get; } = new();
    /// <summary>Réglages choisis par l'hôte dans le salon.</summary>
    public MatchSettings LobbySettings { get; set; } = new();
    /// <summary>La partie, une fois lancée (null pendant le salon).</summary>
    public Match? Match { get; private set; }

    /// <summary>Le salon a changé (arrivée ou départ d'un joueur, réglages).</summary>
    public event Action? LobbyChanged;

    /// <summary>Associe une connexion locale à un joueur (en solo et pour l'hôte : le joueur 1).</summary>
    public void BindLocalPeer(int peerId, int playerId) => _localPeers[peerId] = playerId;

    /// <summary>Ancien nom de <see cref="BindLocalPeer"/> (gardé pour les tests de l'étape M1).</summary>
    public void BindPeer(int peerId, int playerId) => BindLocalPeer(peerId, playerId);

    /// <summary>Change les réglages du salon et prévient les joueurs.</summary>
    public void SetLobbySettings(MatchSettings settings)
    {
        LobbySettings = settings;
        BroadcastLobby();
    }

    /// <summary>
    /// Lance la partie : clans des humains (hôte d'abord, puis par ordre d'arrivée), bots pour compléter jusqu'à 10,
    /// puis envoie à chacun la liste des clans et toutes les bulles.
    /// </summary>
    public Match StartMatch(MatchSettings settings)
    {
        Match = new Match(settings);
        foreach (var p in Players) Match.AddArmy(p.PlayerId, p.Name, p.Color, isBot: false)!.Emblem = p.Emblem;
        if (settings.Mode != GameMode.Tutorial) Match.AddBots(); // le tutoriel se joue seul
        if (_network != null)
        {
            var start = new StartMessage
            {
                Settings = settings,
                Roster = Match.World.Armies.Select(a => new RosterEntry
                {
                    ArmyId = a.Id, PlayerId = a.PlayerId, Name = a.Name, Color = a.ColorRgb, IsBot = a.IsBot, Emblem = a.Emblem, Personality = a.Brain?.Personality.Id,
                }).ToList(),
            };
            _network.Send(0, JsonMessage.Encode(MessageType.Start, start), reliable: true);
            if (_bubbles.Diff(Match.World) is { } all) _network.Send(0, all, reliable: true);
        }
        return Match;
    }

    /// <summary>À appeler à chaque image : lit les messages, fait avancer la partie, envoie l'état aux joueurs.</summary>
    public void Update(double realSeconds)
    {
        _local?.Poll((peer, data) => OnMessage(peer, data, isNetwork: false));
        _network?.Poll((peer, data) => OnMessage(peer, data, isNetwork: true));
        if (Match == null) return;

        Match.Advance(realSeconds);
        if (_network == null || _networkPeers.Count == 0) return;

        if (Match.Events.Count > 0)
        {
            var events = EventCodec.Encode(Match.Events);
            if (events.Length > 3) _network.Send(0, events, reliable: true);
        }
        if (_bubbles.Diff(Match.World) is { } delta) _network.Send(0, delta, reliable: true);

        _snapshotTimer -= (float)realSeconds;
        if (_snapshotTimer > 0) return;
        _snapshotTimer = SnapshotInterval;
        foreach (var (peer, player) in _networkPeers)
            if (Match.World.FindArmyOfPlayer(player) is { } army)
                _network.Send(peer, SnapshotWriter.Write(Match.World, army), reliable: false);
    }

    private void OnMessage(int peer, byte[] data, bool isNetwork)
    {
        if (data.Length == 0) return;
        var type = (MessageType)data[0];
        if (type == MessageType.Command)
        {
            var peers = isNetwork ? _networkPeers : _localPeers;
            if (Match == null || !peers.TryGetValue(peer, out int player)) return; // inconnu ou partie pas commencée
            if (CommandCodec.Decode(data) is { } command)
                // Sécurité : on remplace le joueur indiqué par celui de la connexion (impossible de commander un autre clan).
                Match.Enqueue(command with { PlayerId = player });
            return;
        }
        if (isNetwork && type == MessageType.Hello) OnHello(peer, JsonMessage.Decode<HelloMessage>(data));
        if (isNetwork && type == MessageType.HostRequest) OnHostRequest(peer, JsonMessage.Decode<HostRequestMessage>(data));
    }

    /// <summary>
    /// Serveur dédié : le chef de salon change les réglages ou lance la partie.
    /// Les demandes des autres joueurs sont ignorées ; les commandes de test restent interdites.
    /// </summary>
    private void OnHostRequest(int peer, HostRequestMessage? request)
    {
        if (!IsDedicated || request == null || Match != null || !_networkPeers.TryGetValue(peer, out int player)) return;
        if (!Players.Any(p => p.PlayerId == player && p.IsHost)) return;
        LobbySettings = request.Settings with { AllowCheats = false };
        if (!request.Start) { BroadcastLobby(); return; }
        StartMatch(LobbySettings with { Seed = (uint)Random.Shared.Next() });
        Log?.Invoke($"Partie lancée : {Players.Count} joueur(s), mode {LobbySettings.Mode}");
    }

    /// <summary>Serveur dédié : retour au salon vide, prêt pour une nouvelle partie.</summary>
    private void ResetToLobby()
    {
        Match = null;
        Players.Clear();
        _networkPeers.Clear();
        _bubbles = new BubbleSync();
        _nextPlayerId = 1;
        Log?.Invoke("Retour au salon d'attente.");
        LobbyChanged?.Invoke();
    }

    /// <summary>Un joueur demande à rejoindre : on vérifie la version et la place disponible.</summary>
    private void OnHello(int peer, HelloMessage? hello)
    {
        string? refusal = hello == null ? "message invalide"
            : hello.Protocol != CoreInfo.ProtocolVersion ? $"version différente (toi {hello.Protocol}, hôte {CoreInfo.ProtocolVersion}) : mettez le jeu à jour"
            : Match != null ? "la partie a déjà commencé"
            : Players.Count >= MatchSettings.MaxClans ? "la partie est pleine (10 joueurs)"
            : null;
        if (refusal != null)
        {
            _network!.Send(peer, JsonMessage.Encode(MessageType.Reject, new RejectMessage { Reason = refusal }), reliable: true);
            return;
        }
        int playerId = _nextPlayerId++;
        _networkPeers[peer] = playerId;
        bool leader = IsDedicated && Players.Count == 0; // serveur dédié : le premier arrivé mène le salon
        string name = string.IsNullOrWhiteSpace(hello!.Name) ? $"Joueur {playerId}" : hello.Name.Trim()[..Math.Min(20, hello.Name.Trim().Length)];
        Players.Add(new LobbyPlayer { PlayerId = playerId, Name = name, Color = hello.Color, Emblem = Math.Clamp(hello.Emblem, 0, 7), IsHost = leader });
        Log?.Invoke($"{name} a rejoint ({Players.Count} joueur(s)){(leader ? " — chef de salon" : "")}");
        _network!.Send(peer, JsonMessage.Encode(MessageType.Welcome, new WelcomeMessage { PlayerId = playerId }), reliable: true);
        BroadcastLobby();
    }

    /// <summary>Départ d'un joueur : retiré du salon, ou remplacé par un bot si la partie est en cours.</summary>
    private void OnPeerDisconnected(int peer)
    {
        if (!_networkPeers.Remove(peer, out int player)) return;
        Log?.Invoke($"Joueur {player} parti");
        if (Match == null)
        {
            bool wasLeader = Players.Any(p => p.PlayerId == player && p.IsHost);
            Players.RemoveAll(p => p.PlayerId == player);
            if (IsDedicated && wasLeader && Players.Count > 0) Players[0].IsHost = true; // le suivant prend la main
            BroadcastLobby();
            return;
        }
        if (Match.World.FindArmyOfPlayer(player) is { } army && army.Alive)
        {
            var bots = Match.World.Data.Bots;
            army.IsBot = true;
            army.Brain = new BotBrain(bots.Personality(bots.PersonalityPool[(int)(Match.World.Random.NextDouble() * bots.PersonalityPool.Count)]), 0.5f);
            army.General.InputDirection = Mathematics.Vec2.Zero;
        }
        // Serveur dédié : plus personne ? On repart pour une nouvelle partie.
        if (IsDedicated && _networkPeers.Count == 0) ResetToLobby();
    }

    private void BroadcastLobby()
    {
        LobbyChanged?.Invoke();
        _network?.Send(0, JsonMessage.Encode(MessageType.Lobby, new LobbyMessage { Players = Players, Settings = LobbySettings }), reliable: true);
    }
}

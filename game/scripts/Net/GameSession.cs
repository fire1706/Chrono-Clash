using ChronoClash.Core.Commands;
using ChronoClash.Core.Entities;
using ChronoClash.Core.Events;
using ChronoClash.Core.Net;
using ChronoClash.Core.Simulation;
using Godot;

namespace ChronoClash.Game.Net;

/// <summary>Comment cette machine participe à la partie.</summary>
public enum SessionMode { None, Solo, Host, Client }

/// <summary>
/// Lien entre Godot et la simulation, dans les trois façons de jouer :
/// - Solo : hôte et joueur dans le même programme (transport local), 9 bots ;
/// - Héberger : la même chose, plus un transport ENet en écoute pour les amis ;
/// - Rejoindre : un client ENet qui reçoit une copie du monde de l'hôte.
/// L'affichage lit toujours <see cref="World"/> et envoie ses ordres par <see cref="Send"/>, quel que soit le mode.
/// Ce nœud doit être ajouté AVANT les nœuds d'affichage pour que la simulation avance d'abord à chaque image.
/// </summary>
public partial class GameSession : Node
{
    /// <summary>Le joueur de l'hôte (et du solo) est le joueur numéro 1.</summary>
    public const int SoloPlayerId = HostSession.HostPlayerId;
    /// <summary>Temps maximal pour se connecter à un hôte.</summary>
    private const double ConnectTimeout = 8;

    private HostSession? _host;
    private ClientSession? _localClient;
    private RemoteClient? _remote;
    private EnetTransport? _network;
    private double _connectTimer;

    public SessionMode Mode { get; private set; }

    /// <summary>Réglages de la dernière partie solo (pour « Rejouer »).</summary>
    public static MatchSettings? LastSoloSettings { get; private set; }

    /// <summary>Pause (solo seulement) : la simulation ne tourne plus.</summary>
    public bool Paused { get; set; }

    /// <summary>Le salon a changé, la partie commence, une erreur réseau (message à afficher).</summary>
    public event Action? LobbyChanged;
    public event Action? MatchStarted;
    public event Action<string>? NetworkError;
    /// <summary>Événements de la simulation (son, interface, annonceur s'y abonnent).</summary>
    public event Action<GameEvent>? EventRaised;

    public Match? Match => _host?.Match;
    public bool InMatch => Mode == SessionMode.Client ? _remote?.World != null : _host?.Match != null;
    public World World => Mode == SessionMode.Client ? _remote!.World! : _host!.Match!.World;
    public int LocalPlayerId => Mode == SessionMode.Client ? _remote!.PlayerId : SoloPlayerId;

    /// <summary>Le clan du joueur de cette machine.</summary>
    public Army LocalArmy => World.FindArmyOfPlayer(LocalPlayerId)!;

    /// <summary>
    /// Fraction (0 à 1) entre deux états du monde : l'affichage s'en sert pour glisser entre deux ticks (hôte)
    /// ou entre deux snapshots (client).
    /// </summary>
    public float Alpha => Mode == SessionMode.Client
        ? Mathf.Clamp(_remote!.SinceSnapshot / HostSession.SnapshotInterval, 0, 1)
        : (float)_host!.Match!.Clock.Alpha;

    /// <summary>Joueurs humains du salon d'attente.</summary>
    public IReadOnlyList<LobbyPlayer> LobbyPlayers => Mode == SessionMode.Client ? _remote!.Lobby : _host?.Players ?? new List<LobbyPlayer>();
    public MatchSettings LobbySettings => Mode == SessionMode.Client ? _remote!.LobbySettings : _host?.LobbySettings ?? new MatchSettings();
    /// <summary>Client accepté par l'hôte (il a reçu son numéro de joueur).</summary>
    public bool Accepted => Mode != SessionMode.Client || _remote!.PlayerId > 0;

    // ================= Démarrer =================

    /// <summary>Partie hors ligne : ton clan contre 9 bots.</summary>
    public void StartSolo(MatchSettings settings, string playerName, uint colorRgb, int emblem = 0)
    {
        LastSoloSettings = settings;
        CreateLocalHost(null, playerName, colorRgb, emblem);
        Mode = SessionMode.Solo;
        _host!.StartMatch(settings);
        MatchStarted?.Invoke();
    }

    /// <summary>Héberger : ouvre le salon d'attente. Renvoie un message d'erreur, ou null si tout va bien.</summary>
    public string? StartHost(int port, string playerName, uint colorRgb, int emblem = 0)
    {
        _network = EnetTransport.Host(port, out string error);
        if (_network == null) return error;
        CreateLocalHost(_network, playerName, colorRgb, emblem);
        _host!.LobbyChanged += () => LobbyChanged?.Invoke();
        Mode = SessionMode.Host;
        LobbyChanged?.Invoke();
        return null;
    }

    /// <summary>Rejoindre l'hôte à cette adresse. Renvoie un message d'erreur, ou null si la connexion est lancée.</summary>
    public string? Join(string address, int port, string playerName, uint colorRgb, int emblem = 0)
    {
        _network = EnetTransport.Join(address, port, out string error);
        if (_network == null) return error;
        _remote = new RemoteClient(_network, playerName, colorRgb, emblem);
        _remote.LobbyChanged += () => LobbyChanged?.Invoke();
        _remote.MatchStarted += () => MatchStarted?.Invoke();
        _remote.Rejected += reason => { NetworkError?.Invoke($"Connexion refusée : {reason}"); Leave(); };
        _remote.Disconnected += () => NetworkError?.Invoke("La connexion avec l'hôte a été perdue.");
        _remote.EventReceived += e => EventRaised?.Invoke(e);
        _connectTimer = ConnectTimeout;
        Mode = SessionMode.Client;
        return null;
    }

    private void CreateLocalHost(ITransport? network, string playerName, uint colorRgb, int emblem)
    {
        var (hostEnd, clientEnd) = LocalTransport.CreatePair();
        _host = new HostSession(hostEnd, network, playerName, colorRgb, emblem);
        _host.BindLocalPeer(clientEnd.LocalPeerId, SoloPlayerId);
        _localClient = new ClientSession(clientEnd, SoloPlayerId);
    }

    /// <summary>L'hôte change les réglages du salon (envoyés aux joueurs connectés).</summary>
    public void HostSetSettings(MatchSettings settings) => _host?.SetLobbySettings(settings);

    /// <summary>
    /// Vrai si ce joueur peut lancer la partie : l'hôte, ou le chef de salon d'un serveur dédié.
    /// </summary>
    public bool CanStartMatch => Mode == SessionMode.Host || (Mode == SessionMode.Client && _remote!.IsLeader);

    /// <summary>Chef de salon d'un serveur dédié : demande au serveur de lancer la partie avec ces réglages.</summary>
    public void RequestDedicatedStart(MatchSettings settings) => _remote?.RequestMatch(settings, start: true);

    /// <summary>L'hôte lance la partie avec les joueurs du salon (+ des bots pour compléter).</summary>
    public void HostStart(MatchSettings settings)
    {
        if (_host == null || _host.Match != null) return;
        _host.StartMatch(settings);
        MatchStarted?.Invoke();
    }

    /// <summary>Quitte la partie ou le salon (ferme la connexion).</summary>
    public void Leave()
    {
        _network?.Close();
        _network = null;
    }

    public override void _ExitTree() => Leave();

    // ================= Pendant la partie =================

    /// <summary>
    /// Envoie à l'hôte les 4 formations dessinées par le joueur et celle qu'il utilise.
    /// À appeler juste après le démarrage de la partie.
    /// </summary>
    public void SendFormations(Persistence.FormationStore store)
    {
        for (int i = 0; i < 4; i++)
            Send(new SetFormationCellsCommand(World.Tick, LocalPlayerId, i, (byte[])store.Cells[i].Clone()));
        Send(new SelectFormationCommand(World.Tick, LocalPlayerId, store.Current));
    }

    /// <summary>
    /// Le joueur local peut-il voir ce point ? Faux dans le brouillard de guerre.
    /// Une fois son clan éliminé, il voit toute la carte (mode spectateur).
    /// </summary>
    public bool CanSee(Core.Mathematics.Vec2 p)
        => !World.Settings.FogOfWar || !LocalArmy.Alive || World.IsVisibleTo(LocalArmy, p);

    /// <summary>Envoie un ordre du joueur local à l'hôte.</summary>
    public void Send(PlayerCommand command)
    {
        if (Mode == SessionMode.Client) _remote?.Send(command);
        else _localClient?.Send(command);
    }

    public override void _Process(double delta)
    {
        if (Mode == SessionMode.Client)
        {
            _remote!.Update(delta);
            // Pas de réponse de l'hôte au bout de quelques secondes : on abandonne.
            if (_remote.PlayerId == 0 && _network != null)
            {
                _connectTimer -= delta;
                if (_connectTimer <= 0 || !_network.IsOpen) { NetworkError?.Invoke("Impossible de joindre l'hôte (adresse, port ou pare-feu ?)."); Leave(); _remote.Reset(); }
            }
            return;
        }
        if (_host == null) return;
        if (Paused && Mode == SessionMode.Solo) return; // en pause, le temps s'arrête (en solo seulement)
        _host.Update(delta);
        if (_host.Match != null) foreach (var e in _host.Match.Events) EventRaised?.Invoke(e);
    }
}

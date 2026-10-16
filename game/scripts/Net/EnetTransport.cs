using ChronoClash.Core.Net;
using ChronoClash.Core.Simulation;
using Godot;

namespace ChronoClash.Game.Net;

/// <summary>
/// Le vrai réseau (UDP), avec ENet de Godot. On utilise directement le « peer » ENet, sans le système RPC
/// de Godot : le cœur du jeu envoie et reçoit des paquets d'octets, c'est tout ce dont il a besoin.
/// Canal 0 = fiable (ordres, salon, événements, bulles) ; canal 1 = non fiable ordonné (snapshots :
/// si un snapshot se perd, le suivant arrive 0,1 s plus tard, inutile de le renvoyer).
/// </summary>
public sealed class EnetTransport : ITransport
{
    /// <summary>Port UDP par défaut. L'hôte doit l'ouvrir sur sa box pour jouer par internet.</summary>
    public const int DefaultPort = 7777;

    private readonly ENetMultiplayerPeer _peer = new();

    private EnetTransport(bool isHost)
    {
        IsHost = isHost;
        _peer.PeerConnected += id => PeerConnected?.Invoke((int)id);
        _peer.PeerDisconnected += id => PeerDisconnected?.Invoke((int)id);
    }

    /// <summary>Ouvre une partie en écoute sur ce port. Renvoie null (avec un message) en cas d'échec.</summary>
    public static EnetTransport? Host(int port, out string error)
    {
        var t = new EnetTransport(true);
        var result = t._peer.CreateServer(port, MatchSettings.MaxClans, 2);
        error = result == Error.Ok ? "" : $"Impossible d'ouvrir le port {port} ({result}). Il est peut-être déjà utilisé.";
        return result == Error.Ok ? t : null;
    }

    /// <summary>Se connecte à un hôte. La connexion se termine quelques instants plus tard (événement PeerConnected).</summary>
    public static EnetTransport? Join(string address, int port, out string error)
    {
        var t = new EnetTransport(false);
        var result = t._peer.CreateClient(address, port, 2);
        error = result == Error.Ok ? "" : $"Adresse invalide ou réseau indisponible ({result}).";
        return result == Error.Ok ? t : null;
    }

    public int LocalPeerId => _peer.GetUniqueId();
    public bool IsHost { get; }

    /// <summary>Vrai tant que la connexion est ouverte (ou en cours d'ouverture).</summary>
    public bool IsOpen => _peer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Disconnected;
    public bool IsConnected => _peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected;

    public event Action<int>? PeerConnected;
    public event Action<int>? PeerDisconnected;

    public void Send(int peerId, ReadOnlySpan<byte> data, bool reliable)
    {
        if (!IsOpen) return;
        _peer.TransferChannel = reliable ? 0 : 1;
        _peer.TransferMode = reliable ? MultiplayerPeer.TransferModeEnum.Reliable : MultiplayerPeer.TransferModeEnum.UnreliableOrdered;
        _peer.SetTargetPeer(peerId); // 0 = tout le monde
        _peer.PutPacket(data.ToArray());
    }

    public void Poll(Action<int, byte[]> onMessage)
    {
        if (!IsOpen) return;
        _peer.Poll();
        while (_peer.GetAvailablePacketCount() > 0)
        {
            int from = _peer.GetPacketPeer(); // à lire AVANT GetPacket
            var data = _peer.GetPacket();
            onMessage(from, data);
        }
    }

    public void Close() => _peer.Close();
}

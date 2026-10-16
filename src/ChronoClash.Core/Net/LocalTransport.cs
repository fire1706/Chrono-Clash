namespace ChronoClash.Core.Net;

/// <summary>
/// « Réseau » en mémoire, utilisé pour jouer hors ligne : l'hôte et le joueur sont dans le même programme,
/// mais les ordres passent quand même par le même chemin que sur internet.
/// Ainsi le mode solo teste en permanence le code du multijoueur.
/// </summary>
public sealed class LocalTransport : ITransport
{
    private readonly Queue<(int From, byte[] Data)> _inbox = new();
    private LocalTransport? _other;

    private LocalTransport(int peerId) => LocalPeerId = peerId;

    /// <summary>Crée deux extrémités reliées : l'hôte (pair 1) et le client local (pair 2).</summary>
    public static (LocalTransport Host, LocalTransport Client) CreatePair()
    {
        var host = new LocalTransport(1);
        var client = new LocalTransport(2);
        host._other = client;
        client._other = host;
        return (host, client);
    }

    public int LocalPeerId { get; }
    public bool IsHost => LocalPeerId == 1;

    public event Action<int>? PeerConnected;
    public event Action<int>? PeerDisconnected;

    /// <summary>Annonce une déconnexion aux deux bouts (tests du multijoueur).</summary>
    public void SimulateDisconnect()
    {
        PeerDisconnected?.Invoke(_other!.LocalPeerId);
        _other.PeerDisconnected?.Invoke(LocalPeerId);
    }

    /// <summary>
    /// Annonce la connexion aux deux bouts (comme le ferait un vrai réseau). Sert aux tests du multijoueur :
    /// une paire locale se comporte alors comme un hôte et un client reliés par internet.
    /// </summary>
    public void SimulateConnect()
    {
        PeerConnected?.Invoke(_other!.LocalPeerId);
        _other.PeerConnected?.Invoke(LocalPeerId);
    }

    public void Send(int peerId, ReadOnlySpan<byte> data, bool reliable)
    {
        // En mémoire, tout arrive toujours : « fiable » ou non ne change rien.
        _other?._inbox.Enqueue((LocalPeerId, data.ToArray()));
    }

    public void Poll(Action<int, byte[]> onMessage)
    {
        while (_inbox.Count > 0)
        {
            var (from, data) = _inbox.Dequeue();
            onMessage(from, data);
        }
    }
}

namespace ChronoClash.Core.Net;

/// <summary>
/// Abstraction du réseau. Le cœur ne sait pas comment les octets voyagent :
/// LocalTransport (en mémoire) pour le solo hors ligne, EnetTransport (Godot, UDP) pour héberger
/// ou rejoindre une partie, et plus tard une version sans Godot pour le serveur dédié.
/// </summary>
public interface ITransport
{
    /// <summary>Identifiant de cette machine (1 = l'hôte).</summary>
    int LocalPeerId { get; }

    bool IsHost { get; }

    /// <summary>Envoie des octets à un pair (0 = tout le monde). Fiable = renvoyé jusqu'à réception.</summary>
    void Send(int peerId, ReadOnlySpan<byte> data, bool reliable);

    /// <summary>Récupère les messages reçus depuis le dernier appel.</summary>
    void Poll(Action<int, byte[]> onMessage);

    event Action<int>? PeerConnected;
    event Action<int>? PeerDisconnected;
}

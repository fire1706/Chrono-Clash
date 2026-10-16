using ChronoClash.Core.Commands;

namespace ChronoClash.Core.Net;

/// <summary>
/// Côté joueur : envoie ses ordres à l'hôte. En solo, l'hôte est dans le même programme (LocalTransport).
/// (Étape M5 : recevra aussi les snapshots et tiendra à jour une copie locale du monde.)
/// </summary>
public sealed class ClientSession
{
    /// <summary>L'hôte est toujours le pair numéro 1.</summary>
    public const int HostPeerId = 1;

    private readonly ITransport _transport;

    public ClientSession(ITransport transport, int localPlayerId)
    {
        _transport = transport;
        LocalPlayerId = localPlayerId;
    }

    /// <summary>Le joueur que cette machine contrôle.</summary>
    public int LocalPlayerId { get; }

    /// <summary>Envoie un ordre à l'hôte (toujours en mode fiable : un ordre ne doit pas se perdre).</summary>
    public void Send(PlayerCommand command) => _transport.Send(HostPeerId, CommandCodec.Encode(command), reliable: true);
}

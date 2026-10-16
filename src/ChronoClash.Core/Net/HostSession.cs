using ChronoClash.Core.Simulation;

namespace ChronoClash.Core.Net;

/// <summary>
/// Côté hôte : reçoit les ordres des joueurs par le transport, les donne à la partie, et fait avancer la simulation.
/// (Étape M5 : enverra aussi les snapshots du monde à chaque client.)
/// </summary>
public sealed class HostSession
{
    private readonly ITransport _transport;
    /// <summary>Quel joueur se trouve derrière chaque connexion : un client ne peut commander que SON clan.</summary>
    private readonly Dictionary<int, int> _playerOfPeer = new();

    public HostSession(Match match, ITransport transport)
    {
        Match = match;
        _transport = transport;
    }

    public Match Match { get; }

    /// <summary>Associe une connexion (pair réseau) à un joueur de la partie.</summary>
    public void BindPeer(int peerId, int playerId) => _playerOfPeer[peerId] = playerId;

    /// <summary>À appeler à chaque image : lit les messages reçus puis fait avancer la partie.</summary>
    public void Update(double realSeconds)
    {
        _transport.Poll(OnMessage);
        Match.Advance(realSeconds);
    }

    private void OnMessage(int fromPeer, byte[] data)
    {
        var command = CommandCodec.Decode(data);
        if (command == null) return;                                   // message abîmé : ignoré
        if (!_playerOfPeer.TryGetValue(fromPeer, out int player)) return; // connexion inconnue : ignorée
        // Sécurité : on remplace le joueur indiqué par celui de la connexion (impossible de commander un autre clan).
        Match.Enqueue(command with { PlayerId = player });
    }
}

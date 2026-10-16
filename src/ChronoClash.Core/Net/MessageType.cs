namespace ChronoClash.Core.Net;

/// <summary>Premier octet de chaque message réseau : indique comment lire la suite.</summary>
public enum MessageType : byte
{
    /// <summary>Un ordre de joueur (client → hôte), binaire, voir CommandCodec.</summary>
    Command = 1,
    /// <summary>Client → hôte : « bonjour, je m'appelle… » (JSON).</summary>
    Hello = 2,
    /// <summary>Hôte → client : « bienvenue, tu es le joueur n° … » (JSON).</summary>
    Welcome = 3,
    /// <summary>Hôte → client : connexion refusée, avec la raison (JSON).</summary>
    Reject = 4,
    /// <summary>Hôte → tous : état du salon d'attente (JSON).</summary>
    Lobby = 5,
    /// <summary>Hôte → tous : la partie commence (réglages + liste des clans, JSON).</summary>
    Start = 6,
    /// <summary>Hôte → un client : état du monde vu par son clan (binaire, voir SnapshotWriter), 10 fois par seconde.</summary>
    Snapshot = 7,
    /// <summary>Hôte → tous : événements de la simulation (sons, annonces…), voir EventCodec.</summary>
    Events = 8,
    /// <summary>Hôte → tous : bulles apparues et disparues, voir BubbleSync.</summary>
    Bubbles = 9,
}

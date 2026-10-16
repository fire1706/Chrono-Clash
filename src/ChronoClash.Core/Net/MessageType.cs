namespace ChronoClash.Core.Net;

/// <summary>Premier octet de chaque message réseau : indique comment lire la suite.</summary>
public enum MessageType : byte
{
    /// <summary>Un ordre de joueur (client → hôte).</summary>
    Command = 1,
    // Étape M5 : Hello (connexion), Lobby, Snapshot (hôte → clients), Events…
}

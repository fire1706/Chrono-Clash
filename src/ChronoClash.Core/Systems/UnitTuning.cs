namespace ChronoClash.Core.Systems;

/// <summary>
/// Réglages des déplacements des troupes (valeurs du prototype HTML).
/// La vitesse propre de chaque troupe est dans data/troops.json.
/// </summary>
public static class UnitTuning
{
    /// <summary>Réactivité de l'accélération des troupes.</summary>
    public const float Acceleration = 8f;
    /// <summary>Vitesse de rotation des troupes (radians par seconde).</summary>
    public const float TurnRate = 8f;
    /// <summary>Au-delà de cette distance de sa place, une troupe accélère de <see cref="CatchUpMultiplier"/> pour rattraper.</summary>
    public const float CatchUpDistance = 70f, CatchUpMultiplier = 1.3f;
    /// <summary>En sprint, les troupes suivent un peu moins vite que le chef.</summary>
    public const float SprintFollowFactor = 0.95f;
    /// <summary>Freinage à l'approche de sa place (plus grand = s'arrête plus sèchement).</summary>
    public const float ArrivalBraking = 5f;

    /// <summary>Distance à sa place à partir de laquelle on vérifie si la troupe est bloquée.</summary>
    public const float StuckCheckDistance = 34f;
    /// <summary>Temps bloqué avant de calculer un chemin, et avant d'abandonner un chemin qui ne marche pas.</summary>
    public const float StuckPathDelay = 0.45f, StuckReplanDelay = 1.2f;
    /// <summary>Une troupe bloquée à plus de <see cref="LostDistance"/> px de son chef pendant <see cref="LostTeleportDelay"/> s le rejoint d'un coup.</summary>
    public const float LostDistance = 250f, LostTeleportDelay = 5f;
    /// <summary>Calculs de chemin autorisés par tick pour toutes les troupes (protège les performances).</summary>
    public const int PathBudgetPerTick = 12;
    /// <summary>Recouvrement autorisé entre deux troupes (0,95 = elles peuvent se chevaucher de 5 %).</summary>
    public const float SeparationFactor = 0.95f;
}

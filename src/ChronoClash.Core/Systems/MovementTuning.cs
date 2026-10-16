namespace ChronoClash.Core.Systems;

/// <summary>
/// Réglages des déplacements du chef (valeurs du prototype HTML).
/// Pour rendre le chef plus rapide, plus nerveux ou plus endurant, c'est ici.
/// Ces valeurs passeront dans data/tuning.json à l'étape M2.
/// </summary>
public static class MovementTuning
{
    /// <summary>Vitesse de marche en pixels par seconde.</summary>
    public const float BaseSpeed = 190f;
    /// <summary>Multiplicateur de vitesse en sprint.</summary>
    public const float SprintMultiplier = 1.65f;

    /// <summary>Endurance perdue par seconde de sprint (sur 100).</summary>
    public const float StaminaDrainPerSecond = 26f;
    /// <summary>Endurance regagnée par seconde en marchant / à l'arrêt.</summary>
    public const float StaminaRegenMoving = 12f, StaminaRegenIdle = 20f;
    /// <summary>Délai (s) après un sprint avant que l'endurance remonte.</summary>
    public const float StaminaRegenDelay = 0.9f;
    /// <summary>Endurance à retrouver pour pouvoir sprinter à nouveau après épuisement.</summary>
    public const float ExhaustionRecovery = 30f;

    /// <summary>Réactivité de l'accélération (plus grand = plus nerveux). Freinage un peu plus vif.</summary>
    public const float AccelerationMoving = 9f, AccelerationStopping = 12f;
    /// <summary>Vitesse de rotation du chef en radians par seconde.</summary>
    public const float TurnRate = 11f;
    /// <summary>Vitesse de rotation de la formation (l'armée pivote plus lentement que le chef).</summary>
    public const float FormationTurnRate = 2.4f;

    /// <summary>Distance à laquelle une étape intermédiaire est considérée atteinte, et la dernière.</summary>
    public const float WaypointReach = 18f, FinalReach = 5f;
    /// <summary>Distance de ralentissement à l'approche de la destination finale.</summary>
    public const float ArrivalSlowdownDistance = 70f;
    /// <summary>Temps bloqué contre un obstacle avant de recalculer le chemin.</summary>
    public const float StuckReplanDelay = 0.5f;
}

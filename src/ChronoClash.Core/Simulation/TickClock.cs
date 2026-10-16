namespace ChronoClash.Core.Simulation;

/// <summary>
/// Transforme le temps réel (variable selon la vitesse de l'ordinateur) en ticks de simulation à pas fixe.
/// La simulation avance toujours par petits pas identiques : c'est indispensable pour que l'hôte,
/// les tests et le futur serveur dédié obtiennent les mêmes résultats.
/// </summary>
public sealed class TickClock
{
    /// <summary>Nombre de ticks de simulation par seconde.</summary>
    public const int TicksPerSecond = 20;

    /// <summary>Durée d'un tick en secondes.</summary>
    public const double TickDuration = 1.0 / TicksPerSecond;

    /// <summary>Nombre maximal de ticks rattrapés en une fois (évite de geler le jeu après un gros ralentissement).</summary>
    public int MaxCatchUpTicks { get; init; } = 10;

    private double _accumulator;

    /// <summary>Fraction du tick suivant déjà écoulée (0 à 1), utile pour interpoler l'affichage.</summary>
    public double Alpha => _accumulator / TickDuration;

    /// <summary>Ajoute du temps réel écoulé et renvoie le nombre de ticks à simuler maintenant.</summary>
    public int Advance(double realSeconds)
    {
        if (realSeconds < 0) realSeconds = 0;
        _accumulator += realSeconds;
        // petite marge pour absorber les erreurs d'arrondi des nombres à virgule
        int ticks = (int)((_accumulator + 1e-9) / TickDuration);
        if (ticks > MaxCatchUpTicks)
        {
            _accumulator = 0;
            return MaxCatchUpTicks;
        }
        _accumulator = Math.Max(0, _accumulator - ticks * TickDuration);
        return ticks;
    }
}

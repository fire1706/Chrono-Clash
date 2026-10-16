using ChronoClash.Core.Simulation;

namespace ChronoClash.Core.Systems;

/// <summary>
/// Recalcule le brouillard de guerre de chaque clan 4 fois par seconde (voir <see cref="VisionMap"/>).
/// Les bots s'en servent pour ne voir que ce qu'ils voient vraiment ; l'affichage du joueur aussi.
/// </summary>
public static class VisionSystem
{
    private const float Interval = 0.25f;

    public static void Update(World world, float dt)
    {
        world.VisionTimer -= dt;
        if (world.VisionTimer > 0) return;
        world.VisionTimer = Interval;
        var tuning = world.Data.Tuning.Vision;
        for (int i = 0; i < world.Armies.Count; i++) world.Vision[i].Compute(world.Armies[i], tuning);
    }
}

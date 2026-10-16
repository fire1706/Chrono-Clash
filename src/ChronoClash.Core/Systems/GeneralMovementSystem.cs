using ChronoClash.Core.Entities;
using ChronoClash.Core.Map;
using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Simulation;
using T = ChronoClash.Core.Systems.MovementTuning;

namespace ChronoClash.Core.Systems;

/// <summary>
/// Déplace les chefs à chaque tick : clavier ou chemin, sprint et endurance, terrain (forêt, gué…),
/// glissement le long des obstacles, recalcul de chemin en cas de blocage, rotation.
/// Port de la fonction stepGeneral du prototype HTML.
/// </summary>
public static class GeneralMovementSystem
{
    public static void Update(World world, float dt)
    {
        foreach (var army in world.Armies)
        {
            if (!army.Alive || army.General.Dead) continue;
            Move(world, army, dt);
        }
    }

    private static void Move(World world, Army army, float dt)
    {
        var g = army.General;
        var terrain = world.Terrain;
        g.PreviousPosition = g.Position;

        // 1. D'où vient l'intention de mouvement ? Le clavier passe avant le chemin (et l'annule).
        Vec2 direction;
        float arrive = 1f;
        bool retreating = army.RetreatTime > 0;
        if (retreating)
        {
            // Tactique « Retrait » : le chef recule tout droit, sans sprint, en gardant son orientation.
            direction = army.RetreatDirection;
        }
        else if (g.InputDirection.LengthSquared > 0)
        {
            direction = g.InputDirection.Normalized();
            g.Path.Clear();
        }
        else direction = FollowPath(g, out arrive);
        bool moving = direction.LengthSquared > 0;

        // 2. Endurance et sprint.
        if (g.Exhausted && g.Stamina > T.ExhaustionRecovery) g.Exhausted = false;
        g.Sprinting = g.WantsSprint && moving && !g.Exhausted && g.Stamina > 0 && !retreating;
        if (g.Sprinting)
        {
            g.Stamina = MathF.Max(0, g.Stamina - T.StaminaDrainPerSecond * dt);
            g.StaminaRestTimer = T.StaminaRegenDelay;
            if (g.Stamina <= 0) g.Exhausted = true;
        }
        else
        {
            g.StaminaRestTimer -= dt;
            if (g.StaminaRestTimer <= 0)
                g.Stamina = MathF.Min(100, g.Stamina + (moving ? T.StaminaRegenMoving : T.StaminaRegenIdle) * dt);
        }

        // 3. Vitesse : on se rapproche en douceur de la vitesse voulue (accélération / freinage).
        // La vitesse dépend aussi du chef de l'ère (un chevalier à cheval va plus vite qu'un centurion).
        float maxSpeed = T.BaseSpeed * g.SpeedMultiplier * (g.Sprinting ? T.SprintMultiplier : 1f) * (retreating ? 0.85f : 1f)
                         * terrain.SpeedFactor(g.Position.X, g.Position.Y) * arrive;
        float k = MathF.Min(1, (moving ? T.AccelerationMoving : T.AccelerationStopping) * dt);
        g.Velocity += (direction * maxSpeed - g.Velocity) * k;

        // 4. Déplacement avec glissement : si on ne peut pas avancer en diagonale, on essaie chaque axe.
        var next = g.Position + g.Velocity * dt;
        if (terrain.IsWalkable(next, g.Radius)) g.Position = next;
        else if (terrain.IsWalkable(next.X, g.Position.Y, g.Radius)) { g.Position = new Vec2(next.X, g.Position.Y); g.Velocity = new Vec2(g.Velocity.X, g.Velocity.Y * 0.5f); }
        else if (terrain.IsWalkable(g.Position.X, next.Y, g.Radius)) { g.Position = new Vec2(g.Position.X, next.Y); g.Velocity = new Vec2(g.Velocity.X * 0.5f, g.Velocity.Y); }
        else g.Velocity *= 0.2f;

        // 5. Bloqué sur un chemin ? On le recalcule.
        if (g.Path.Count > 0)
        {
            float moved = Vec2.Distance(g.Position, g.LastPosition);
            g.StuckTime = moved < maxSpeed * dt * 0.2f ? g.StuckTime + dt : 0;
            if (g.StuckTime > T.StuckReplanDelay)
            {
                var end = g.Path[^1];
                var replanned = world.Pathfinder.Plan(g.Position, end);
                g.Path.Clear();
                if (replanned != null) g.Path.AddRange(replanned);
                g.StuckTime = 0;
            }
        }
        g.LastPosition = g.Position;

        // 6. Rotation du chef (et, plus lentement, de sa formation) vers la direction du mouvement.
        float speed = g.Velocity.Length;
        if (speed > 15 && !retreating) g.Angle = MathUtil.TurnTowards(g.Angle, g.Velocity.Angle, T.TurnRate * dt);
        if (speed > 15) g.WalkCycle += speed * dt * 0.075f;
        if (speed > 40 && !retreating) army.FormationAngle = MathUtil.TurnTowards(army.FormationAngle, g.Angle, T.FormationTurnRate * dt);
    }

    /// <summary>
    /// Direction vers l'étape suivante du chemin (et retire les étapes atteintes).
    /// <paramref name="arrive"/> ralentit le chef à l'approche de la destination finale.
    /// </summary>
    private static Vec2 FollowPath(General g, out float arrive)
    {
        arrive = 1f;
        while (g.Path.Count > 0)
        {
            var target = g.Path[0];
            var delta = target - g.Position;
            float d = delta.Length;
            bool last = g.Path.Count == 1;
            if (d < (last ? T.FinalReach : T.WaypointReach)) { g.Path.RemoveAt(0); continue; }
            if (last) arrive = MathUtil.Clamp(d / T.ArrivalSlowdownDistance, 0.15f, 1f);
            return delta / d;
        }
        return Vec2.Zero;
    }
}

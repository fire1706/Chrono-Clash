using ChronoClash.Core.Entities;
using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Simulation;
using T = ChronoClash.Core.Systems.UnitTuning;

namespace ChronoClash.Core.Systems;

/// <summary>
/// Déplace les troupes vers leur place dans la formation, puis les écarte les unes des autres.
/// Comprend les protections anti-blocage du prototype :
/// place dans l'eau ou un rocher → rapprochée du chef ; bloquée → calcul de chemin ;
/// chemin qui ne marche pas → recalcul ; perdue loin du chef pendant 5 s → elle le rejoint.
/// (À l'étape M3, ce système laissera la main au combat quand un ennemi est à portée.)
/// </summary>
public static class UnitMovementSystem
{
    private static readonly List<IBody> Neighbors = new();

    public static void Update(World world, float dt)
    {
        int pathBudget = T.PathBudgetPerTick;
        foreach (var army in world.Armies)
        {
            if (!army.Alive) continue;
            foreach (var u in army.Units)
                if (!u.Dead) MoveUnit(world, army, u, dt, ref pathBudget);
        }
        world.Grid.Rebuild(world);
        Separate(world);
    }

    private static void MoveUnit(World world, Army army, Unit u, float dt, ref int pathBudget)
    {
        var terrain = world.Terrain;
        var stats = world.Data.Stats(u.Kind, u.Era);
        var chief = army.General;
        u.PreviousPosition = u.Position;
        u.Age += dt;

        // 1. Où est ma place ? Si elle tombe dans l'eau ou un rocher, on la rapproche du chef jusqu'à un endroit libre.
        var slot = FormationSystem.SlotWorldPosition(army, u.Slot);
        if (!terrain.IsWalkable(slot, u.Radius))
            for (int k = 1; k <= 12; k++)
            {
                var p = Vec2.Lerp(slot, chief.Position, k / 12f);
                if (terrain.IsWalkable(p, u.Radius)) { slot = p; break; }
            }

        // 2. Si un chemin existe, on le suit (sauf s'il ne mène plus à la bonne place).
        var target = slot;
        if (u.Path != null && u.Path.Count > 0)
        {
            if (Vec2.Distance(u.Path[^1], slot) > 160 || Vec2.Distance(u.Position, slot) < 40) u.Path = null;
            else
            {
                if (Vec2.Distance(u.Path[0], u.Position) < 14) u.Path.RemoveAt(0);
                if (u.Path.Count > 0) target = u.Path[0]; else u.Path = null;
            }
        }

        // 3. Vitesse : plus vite si on est loin, en douceur à l'arrivée, ralentie par le terrain.
        var delta = target - u.Position;
        float distance = delta.Length, farFromSlot = Vec2.Distance(slot, u.Position);
        float sprint = chief.Sprinting ? MovementTuning.SprintMultiplier * T.SprintFollowFactor : 1f;
        float maxSpeed = stats.Speed * sprint * (farFromSlot > T.CatchUpDistance ? T.CatchUpMultiplier : 1f) * terrain.SpeedFactor(u.Position.X, u.Position.Y);
        float wanted = MathF.Min(maxSpeed, distance * T.ArrivalBraking);
        var direction = distance > 0.5f ? delta / distance : Vec2.Zero;
        float k2 = MathF.Min(1, T.Acceleration * dt);
        u.Velocity += (direction * wanted - u.Velocity) * k2;

        // 4. Déplacement avec glissement le long des obstacles.
        var next = u.Position + u.Velocity * dt;
        if (terrain.IsWalkable(next, u.Radius)) u.Position = next;
        else if (terrain.IsWalkable(next.X, u.Position.Y, u.Radius)) { u.Position = new Vec2(next.X, u.Position.Y); u.Velocity = new Vec2(u.Velocity.X, u.Velocity.Y * 0.5f); }
        else if (terrain.IsWalkable(u.Position.X, next.Y, u.Radius)) { u.Position = new Vec2(u.Position.X, next.Y); u.Velocity = new Vec2(u.Velocity.X * 0.5f, u.Velocity.Y); }
        else u.Velocity *= 0.3f;

        // 5. Anti-blocage.
        float moved = Vec2.Distance(u.Position, u.LastPosition);
        u.LastPosition = u.Position;
        bool blocked = moved < maxSpeed * dt * 0.25f;
        u.StuckTime = farFromSlot > T.StuckCheckDistance && blocked ? u.StuckTime + dt : MathF.Max(0, u.StuckTime - dt);
        if (u.StuckTime > T.StuckReplanDelay && u.Path != null) u.Path = null;                // le chemin ne marche pas : on l'oublie
        if (u.StuckTime > T.StuckPathDelay && u.Path == null && pathBudget > 0)               // bloquée : on cherche un chemin
        {
            pathBudget--;
            u.StuckTime = 0;
            var path = world.Pathfinder.Plan(u.Position, slot, u.Radius);
            u.Path = path is { Count: > 0 } ? path : null;
        }
        u.LostTime = farFromSlot > T.LostDistance && blocked ? u.LostTime + dt : MathF.Max(0, u.LostTime - dt);
        if (u.LostTime > T.LostTeleportDelay) RejoinChief(world, u, chief);

        // 6. Orientation : vers où elle va, ou dans le sens de la formation à l'arrêt.
        float speed = u.Velocity.Length;
        float facing = speed > 18 ? u.Velocity.Angle : army.FormationAngle;
        u.Angle = MathUtil.TurnTowards(u.Angle, facing, T.TurnRate * dt);
        if (speed > 10) u.WalkCycle += speed * dt * (u.Kind == Commands.TroopKind.Tank ? 0.045f : 0.09f);
    }

    /// <summary>Dernier recours : la troupe réapparaît juste à côté de son chef.</summary>
    private static void RejoinChief(World world, Unit u, General chief)
    {
        for (int k = 0; k < 20; k++)
        {
            float a = (float)world.Random.NextDouble() * MathF.PI * 2, r = 30 + (float)world.Random.NextDouble() * 70;
            var p = chief.Position + Vec2.FromAngle(a) * r;
            if (!world.Terrain.IsWalkable(p, u.Radius)) continue;
            u.Position = u.PreviousPosition = u.LastPosition = p;
            u.Path = null;
            u.LostTime = 0;
            return;
        }
    }

    /// <summary>Écarte les troupes qui se chevauchent (et les repousse hors des chefs, qui eux ne bougent pas).</summary>
    private static void Separate(World world)
    {
        var terrain = world.Terrain;
        foreach (var army in world.Armies)
        {
            if (!army.Alive) continue;
            foreach (var u in army.Units)
            {
                if (u.Dead) continue;
                world.Grid.Query(u.Position.X, u.Position.Y, u.Radius + 16, Neighbors);
                foreach (var other in Neighbors)
                {
                    if (other == u || other.IsDead) continue;
                    bool otherIsChief = other is General;
                    if (!otherIsChief && ((Unit)other).Id <= u.Id) continue; // chaque paire une seule fois
                    var d = other.Position - u.Position;
                    float min = (u.Radius + other.BodyRadius) * T.SeparationFactor, d2 = d.LengthSquared;
                    if (d2 >= min * min || d2 < 1e-4f) continue;
                    float dist = MathF.Sqrt(d2);
                    // Un chef ne recule pas : la troupe fait tout le chemin. Sinon chacune fait la moitié.
                    float push = (min - dist) / (otherIsChief ? 1 : 2);
                    var offset = d / dist * push;
                    if (terrain.IsWalkable(u.Position - offset, u.Radius)) u.Position -= offset;
                    if (!otherIsChief && other is Unit o && terrain.IsWalkable(o.Position + offset, o.Radius)) o.Position += offset;
                }
            }
        }
    }
}

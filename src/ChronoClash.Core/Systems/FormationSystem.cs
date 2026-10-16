using ChronoClash.Core.Entities;
using ChronoClash.Core.Formations;
using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Simulation;

namespace ChronoClash.Core.Systems;

/// <summary>
/// Donne à chaque troupe sa place dans la formation de son clan, quand c'est nécessaire
/// (troupe ajoutée ou morte, formation changée ou redessinée).
/// Les troupes sont réparties pour éviter qu'elles se croisent : celles qui sont déjà devant
/// prennent les places de devant, celles de gauche les places de gauche.
/// </summary>
public static class FormationSystem
{
    public static void Update(World world)
    {
        foreach (var army in world.Armies)
            if (army.Alive && army.SlotsDirty) Reassign(world, army);
    }

    public static void Reassign(World world, Army army)
    {
        var layout = FormationLayout.Compute(army.CurrentFormation.Cells, army.CountByKind(), world.Data);
        army.SlotScale = layout.Scale;
        float c = MathF.Cos(army.FormationAngle), s = MathF.Sin(army.FormationAngle);
        var g = army.General.Position;
        for (int kind = 0; kind < 3; kind++)
        {
            var units = army.Units.Where(u => !u.Dead && (int)u.Kind == kind).ToList();
            float spacing = world.Data.Troop((Commands.TroopKind)kind).Spacing;
            // Position actuelle de chaque troupe dans le repère de la formation (avant / droite).
            var keyed = units.Select(u =>
            {
                var d = u.Position - g;
                int forward = (int)MathF.Floor((d.X * c + d.Y * s) / spacing + 0.5f);
                float right = -d.X * s + d.Y * c;
                return (Unit: u, Forward: forward, Right: right);
            })
            .OrderByDescending(k => k.Forward).ThenBy(k => k.Right)
            .ToList();
            var slots = layout.SlotsByKind[kind];
            for (int i = 0; i < keyed.Count; i++) keyed[i].Unit.Slot = i < slots.Count ? slots[i] : new Vec2(-40, 0);
        }
        army.SlotsDirty = false;
    }

    /// <summary>Position dans le monde de la place d'une troupe, selon la position et l'orientation actuelles du chef.</summary>
    public static Vec2 SlotWorldPosition(Army army, Vec2 slot, float depthFactor = 1f)
    {
        float c = MathF.Cos(army.FormationAngle), s = MathF.Sin(army.FormationAngle);
        float f = slot.X * depthFactor, l = slot.Y;
        var g = army.General.Position;
        return new Vec2(g.X + c * f - s * l, g.Y + s * f + c * l);
    }
}

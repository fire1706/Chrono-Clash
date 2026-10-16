using ChronoClash.Core.Commands;
using ChronoClash.Core.Entities;
using ChronoClash.Core.Events;
using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Simulation;

namespace ChronoClash.Core.Systems;

/// <summary>
/// Les 4 tactiques (touches 1, 2, 3, R), avec temps de recharge :
/// - Charge : 40 % des guerriers et tanks de devant foncent tout droit et blessent tout ce qu'ils percutent.
/// - Retrait : l'armée recule 3 s en gardant sa formation et cesse le combat.
/// - Viser : toute l'armée concentre ses attaques autour d'un point pendant 5 s.
/// - Cri de guerre : les troupes proches du chef gagnent +30 % de dégâts et +15 % de vitesse pendant 6 s.
/// Les durées et recharges sont dans data/tuning.json (section "tactics").
/// </summary>
public static class TacticSystem
{
    /// <summary>Fait décroître les recharges et les effets en cours.</summary>
    public static void Update(World world, float dt)
    {
        foreach (var a in world.Armies)
        {
            for (int i = 0; i < a.TacticCooldowns.Length; i++) a.TacticCooldowns[i] = MathF.Max(0, a.TacticCooldowns[i] - dt);
            a.RetreatTime = MathF.Max(0, a.RetreatTime - dt);
            a.AimTime = MathF.Max(0, a.AimTime - dt);
        }
    }

    /// <summary>Lance une tactique si possible. Renvoie faux (et un événement explicatif) sinon.</summary>
    public static bool Use(World world, Army army, TacticKind kind, Vec2 aimPoint, List<GameEvent> events)
    {
        var tuning = world.Data.Tuning.Tactics;
        var g = army.General;
        if (!army.Alive || g.Dead) return false;
        if (army.TacticCooldowns[(int)kind] > 0)
        {
            events.Add(new TacticFailedEvent(world.Tick, army.PlayerId, kind, $"encore {army.TacticCooldowns[(int)kind]:0} s de recharge"));
            return false;
        }
        var forward = Vec2.FromAngle(army.FormationAngle);
        int count = 0;
        switch (kind)
        {
            case TacticKind.Charge:
            {
                // Les 40 % de guerriers et de tanks les plus en avant dans la formation.
                var chargers = new List<Unit>();
                foreach (var troop in new[] { TroopKind.Warrior, TroopKind.Tank })
                {
                    var front = army.Units.Where(u => !u.Dead && u.Kind == troop).OrderByDescending(u => u.Slot.X).ToList();
                    chargers.AddRange(front.Take((int)MathF.Ceiling(front.Count * tuning.Charge.Share)));
                }
                if (chargers.Count == 0)
                {
                    events.Add(new TacticFailedEvent(world.Tick, army.PlayerId, kind, "il faut des guerriers ou des tanks pour charger"));
                    return false;
                }
                army.ChargeDirection = forward;
                foreach (var u in chargers) { u.ChargeTime = tuning.Charge.Duration; u.ChargeHits.Clear(); u.Target = null; u.Path = null; }
                count = chargers.Count;
                break;
            }
            case TacticKind.Retreat:
                army.RetreatTime = tuning.Retreat.Duration;
                army.RetreatDirection = forward * -1;
                g.Path.Clear();
                foreach (var u in army.Units) { u.Target = null; u.ChargeTime = 0; }
                count = army.Units.Count;
                break;
            case TacticKind.Aim:
                army.AimPoint = aimPoint;
                army.AimTime = tuning.Aim.Duration;
                foreach (var u in army.Units) u.ThinkTimer = 0; // tout le monde recherche une cible tout de suite
                count = army.Units.Count;
                break;
            case TacticKind.WarCry:
                foreach (var u in army.Units)
                    if (!u.Dead && Vec2.Distance(u.Position, g.Position) < tuning.WarCry.Radius) { u.WarCryTime = tuning.WarCry.Duration; count++; }
                break;
        }
        army.TacticCooldowns[(int)kind] = tuning.Of(kind).Cooldown;
        var at = kind == TacticKind.Aim ? aimPoint : g.Position;
        events.Add(new TacticUsedEvent(world.Tick, army.Id, kind, count, at.X, at.Y));
        return true;
    }
}

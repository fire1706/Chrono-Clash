using ChronoClash.Core.Commands;
using ChronoClash.Core.Events;
using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Simulation;

namespace ChronoClash.Core.Systems;

/// <summary>
/// Applique les ordres reçus pendant ce tick (humains ou bots).
/// C'est le SEUL endroit où les ordres des joueurs modifient le monde :
/// pour ajouter un nouvel ordre, on crée son type dans Commands/ et on le traite ici.
/// </summary>
public static class CommandSystem
{
    public static void Apply(World world, IReadOnlyList<PlayerCommand> commands, List<GameEvent> events)
    {
        foreach (var command in commands)
        {
            var army = world.FindArmyOfPlayer(command.PlayerId);
            if (army == null || !army.Alive || army.General.Dead) continue; // ordre d'un joueur inconnu ou éliminé : ignoré
            var g = army.General;

            switch (command)
            {
                case MoveToCommand move:
                {
                    // Avec « Maj », on ajoute une étape à la suite du chemin actuel.
                    var from = move.Queue && g.Path.Count > 0 ? g.Path[^1] : g.Position;
                    var path = world.Pathfinder.Plan(from, new Vec2(move.X, move.Y));
                    if (path == null) { events.Add(new PathFailedEvent(world.Tick, command.PlayerId)); break; }
                    if (!move.Queue) g.Path.Clear();
                    g.Path.AddRange(path);
                    g.StuckTime = 0;
                    break;
                }
                case DirectionCommand dir:
                    g.InputDirection = new Vec2(dir.Dx, dir.Dy);
                    break;
                case StopCommand:
                    g.Path.Clear();
                    break;
                case SprintCommand sprint:
                    g.WantsSprint = sprint.On;
                    break;
                // Les autres ordres (tactiques, achats, formations) arrivent aux étapes M2 et M3.
            }
        }
    }
}

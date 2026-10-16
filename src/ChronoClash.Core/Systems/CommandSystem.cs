using ChronoClash.Core.Commands;
using ChronoClash.Core.Events;
using ChronoClash.Core.Formations;
using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Simulation;

namespace ChronoClash.Core.Systems;

/// <summary>
/// Applique les ordres reçus pendant ce tick (humains ou bots).
/// C'est le SEUL endroit où les ordres des joueurs modifient le monde :
/// pour ajouter un nouvel ordre, on crée son type dans Commands/, on l'ajoute à Net/CommandCodec.cs et on le traite ici.
/// Les ordres sont toujours vérifiés : un ordre invalide est ignoré, jamais une cause de plantage.
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
                    // On borne la direction : un client ne peut pas demander une « super vitesse ».
                    g.InputDirection = new Vec2(MathUtil.Clamp(dir.Dx, -1, 1), MathUtil.Clamp(dir.Dy, -1, 1));
                    break;
                case StopCommand:
                    g.Path.Clear();
                    break;
                case SprintCommand sprint:
                    g.WantsSprint = sprint.On;
                    break;
                case BuyCommand buy when Enum.IsDefined(buy.Kind):
                    Recruitment.TryBuy(world, army, buy.Kind, events);
                    break;
                case SelectFormationCommand select when select.Index >= 0 && select.Index < Entities.Army.FormationCount:
                    army.FormationIndex = select.Index;
                    army.SlotsDirty = true;
                    break;
                case SetFormationCellsCommand set when set.Index >= 0 && set.Index < Entities.Army.FormationCount && FormationTemplate.IsValid(set.Cells):
                    Array.Copy(set.Cells, army.Formations[set.Index].Cells, FormationTemplate.CellCount);
                    army.Formations[set.Index].Cells[FormationTemplate.ChiefRow * FormationTemplate.Columns + FormationTemplate.ChiefColumn] = 0; // la case du chef reste vide
                    if (set.Index == army.FormationIndex) army.SlotsDirty = true;
                    break;
                case DebugSpawnTroopsCommand debug when world.Settings.AllowCheats:
                    Recruitment.SpawnTestTroops(world, army, Math.Clamp(debug.Count, 1, 100));
                    break;
                // Les tactiques (charge, retrait, visée, cri de guerre) arrivent à l'étape M3.
            }
        }
    }
}

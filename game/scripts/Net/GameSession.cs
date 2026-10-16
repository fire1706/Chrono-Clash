using ChronoClash.Core.Commands;
using ChronoClash.Core.Entities;
using ChronoClash.Core.Events;
using ChronoClash.Core.Net;
using ChronoClash.Core.Simulation;
using Godot;

namespace ChronoClash.Game.Net;

/// <summary>
/// Lien entre Godot et la simulation. En solo, il crée l'hôte ET le client dans le même programme,
/// reliés par un LocalTransport : les ordres du joueur passent par le même chemin qu'en réseau.
/// À l'étape M5, il saura aussi héberger (EnetTransport en écoute) ou rejoindre une partie.
///
/// Ce nœud doit être ajouté AVANT les nœuds d'affichage pour que la simulation avance d'abord à chaque image.
/// </summary>
public partial class GameSession : Node
{
    /// <summary>En solo, le joueur local est le joueur numéro 1.</summary>
    public const int SoloPlayerId = 1;

    private HostSession _host = null!;
    private ClientSession _client = null!;

    public Match Match { get; private set; } = null!;
    public World World => Match.World;
    public int LocalPlayerId => _client.LocalPlayerId;

    /// <summary>Le clan du joueur de cette machine.</summary>
    public Army LocalArmy => World.FindArmyOfPlayer(LocalPlayerId)!;

    /// <summary>Fraction (0 à 1) du tick en cours : l'affichage s'en sert pour glisser entre deux ticks.</summary>
    public float Alpha => (float)Match.Clock.Alpha;

    /// <summary>Événements de la simulation (son, interface, annonceur s'y abonnent).</summary>
    public event Action<GameEvent>? EventRaised;

    /// <summary>Démarre une partie hors ligne : ton clan contre 9 bots.</summary>
    public void StartSolo(MatchSettings settings, string playerName, uint colorRgb)
    {
        Match = new Match(settings);
        Match.AddArmy(SoloPlayerId, playerName, colorRgb, isBot: false);
        Match.AddBots();

        var (hostEnd, clientEnd) = LocalTransport.CreatePair();
        _host = new HostSession(Match, hostEnd);
        _host.BindPeer(clientEnd.LocalPeerId, SoloPlayerId);
        _client = new ClientSession(clientEnd, SoloPlayerId);
    }

    /// <summary>
    /// Envoie à l'hôte les 4 formations dessinées par le joueur et celle qu'il utilise.
    /// À appeler juste après le démarrage de la partie.
    /// </summary>
    public void SendFormations(Persistence.FormationStore store)
    {
        for (int i = 0; i < 4; i++)
            Send(new SetFormationCellsCommand(World.Tick, LocalPlayerId, i, (byte[])store.Cells[i].Clone()));
        Send(new SelectFormationCommand(World.Tick, LocalPlayerId, store.Current));
    }

    /// <summary>
    /// Le joueur local peut-il voir ce point ? Faux dans le brouillard de guerre.
    /// Une fois son clan éliminé, il voit toute la carte (mode spectateur).
    /// </summary>
    public bool CanSee(Core.Mathematics.Vec2 p)
        => !World.Settings.FogOfWar || !LocalArmy.Alive || World.IsVisibleTo(LocalArmy, p);

    /// <summary>Envoie un ordre du joueur local à l'hôte.</summary>
    public void Send(PlayerCommand command) => _client.Send(command);

    public override void _Process(double delta)
    {
        _host.Update(delta);
        foreach (var e in Match.Events) EventRaised?.Invoke(e);
    }
}

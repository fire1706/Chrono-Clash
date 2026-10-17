using ChronoClash.Core;
using ChronoClash.Core.Net;
using ChronoClash.Core.Simulation;
using ChronoClash.Game.Net;
using Godot;

namespace ChronoClash.Game.Bootstrap;

/// <summary>
/// Serveur dédié : le jeu lancé SANS affichage sur un ordinateur ou un serveur loué, qui héberge les parties.
/// Personne n'y joue : le premier joueur connecté mène le salon (choisit les réglages et lance la partie).
/// Quand tout le monde est parti, le serveur revient au salon pour une nouvelle partie.
///
/// Lancement (voir docs/M7.md) :
///   ChronoClash.exe --headless -- --server --port 7777
/// ou avec l'export « Serveur dédié » de Godot (Linux) sur une machine louée.
/// </summary>
public partial class DedicatedServer : Node
{
    private HostSession _host = null!;

    /// <summary>Vrai si le jeu a été lancé en mode serveur (--server, ou sans affichage).</summary>
    public static bool IsRequested()
        => OS.GetCmdlineUserArgs().Contains("--server") || DisplayServer.GetName() == "headless";

    private static string Arg(string name, string fallback)
    {
        var args = OS.GetCmdlineUserArgs();
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
    }

    public override void _Ready()
    {
        Engine.MaxFps = 60; // inutile de tourner plus vite : la simulation avance à 20 ticks par seconde
        int port = int.TryParse(Arg("--port", ""), out var p) ? p : EnetTransport.DefaultPort;
        var network = EnetTransport.Host(port, out string error);
        if (network == null)
        {
            GD.PrintErr($"[serveur] {error}");
            GetTree().Quit(1);
            return;
        }
        _host = HostSession.Dedicated(network);
        _host.LobbySettings = new MatchSettings { Mode = Arg("--mode", "classic") == "quick" ? GameMode.Quick : GameMode.Classic };
        _host.Log += message => GD.Print($"[serveur {Time.GetTimeStringFromSystem()}] {message}");
        GD.Print($"[serveur] {CoreInfo.GameName} {CoreInfo.Version} (protocole {CoreInfo.ProtocolVersion}) en écoute sur le port UDP {port}.");
        GD.Print("[serveur] Le premier joueur qui rejoint mène le salon et lance la partie. Ctrl+C pour arrêter.");
    }

    public override void _Process(double delta) => _host?.Update(delta);
}

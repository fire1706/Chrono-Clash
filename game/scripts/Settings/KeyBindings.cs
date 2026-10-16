using ChronoClash.Game.Localization;
using Godot;

namespace ChronoClash.Game.Settings;

/// <summary>
/// Les raccourcis clavier, sous forme d'« actions » Godot (InputMap). Chaque action a une touche PHYSIQUE :
/// même position sur un clavier AZERTY ou QWERTY (par exemple « move_up » = Z en AZERTY, W en QWERTY).
/// Les flèches restent toujours disponibles pour se déplacer, en plus de la touche choisie.
/// Le joueur peut changer chaque touche dans Réglages ; elles sont enregistrées avec les autres réglages.
/// </summary>
public static class KeyBindings
{
    /// <summary>Toutes les actions : nom interne, libellé affiché, touche par défaut.</summary>
    public static readonly (string Action, string Label, Key Default)[] Actions =
    {
        ("move_up", "Avancer", Key.W), ("move_down", "Reculer", Key.S), ("move_left", "Gauche", Key.A), ("move_right", "Droite", Key.D),
        ("sprint", "Sprint", Key.Space), ("stop", "Halte", Key.X),
        ("tactic_charge", "Charge", Key.Key1), ("tactic_retreat", "Retrait", Key.Key2), ("tactic_aim", "Viser", Key.Key3), ("tactic_cry", "Cri de guerre", Key.R),
        ("buy_warrior", "Acheter un guerrier", Key.Key4), ("buy_ranged", "Acheter une troupe à distance", Key.Key5), ("buy_tank", "Acheter un tank", Key.Key6),
        ("formation_next", "Formation suivante", Key.Tab), ("formation_editor", "Éditeur de formation", Key.F),
        ("formation_1", "Formation 1", Key.Key7), ("formation_2", "Formation 2", Key.Key8), ("formation_3", "Formation 3", Key.Key9), ("formation_4", "Formation 4", Key.Key0),
        ("camera_lock", "Verrouiller la caméra", Key.Y), ("camera_center", "Recentrer la caméra", Key.C), ("leaderboard", "Classement", Key.L),
        ("pause", "Pause", Key.Escape),
    };

    private static readonly Dictionary<string, Key> Arrows = new()
    {
        ["move_up"] = Key.Up, ["move_down"] = Key.Down, ["move_left"] = Key.Left, ["move_right"] = Key.Right,
    };

    /// <summary>Installe les actions dans Godot avec les touches choisies (ou celles par défaut).</summary>
    public static void Apply(IReadOnlyDictionary<string, Key> chosen)
    {
        foreach (var (action, _, fallback) in Actions)
        {
            if (!InputMap.HasAction(action)) InputMap.AddAction(action);
            InputMap.ActionEraseEvents(action);
            var key = chosen.TryGetValue(action, out var k) ? k : fallback;
            InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
            if (Arrows.TryGetValue(action, out var arrow)) InputMap.ActionAddEvent(action, new InputEventKey { Keycode = arrow });
        }
    }

    /// <summary>Nom de la touche telle qu'elle est écrite sur le clavier du joueur (Z en AZERTY pour « move_up »…).</summary>
    public static string LabelOf(Key physical)
    {
        var logical = DisplayServer.KeyboardGetKeycodeFromPhysical(physical);
        string name = OS.GetKeycodeString(logical);
        return name switch { "Space" => Loc.T("Espace"), "Escape" => Loc.T("Échap"), _ => name };
    }

    /// <summary>Touche actuelle d'une action (pour l'afficher sur un bouton).</summary>
    public static string LabelOf(string action) => LabelOf(GameSettings.Current.KeyFor(action));
}

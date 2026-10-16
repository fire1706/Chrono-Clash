using ChronoClash.Core.Simulation;
using ChronoClash.Game.Net;
using ChronoClash.Game.Persistence;
using Godot;

namespace ChronoClash.Game.UI;

/// <summary>
/// Écran d'accueil et salon d'attente (version simple de l'étape M5 ; le vrai écran titre arrive à l'étape M6).
/// - Choisir son nom, sa couleur et les réglages de la partie ;
/// - Jouer seul, Héberger (les amis rejoignent avec ton adresse IP), ou Rejoindre une adresse ;
/// - Dans le salon : liste des joueurs ; l'hôte lance la partie (les places libres sont prises par des bots).
/// </summary>
public partial class LobbyScreen : Control
{
    private GameSession _session = null!;
    private PlayerProfile _profile = null!;

    private LineEdit _name = null!;
    private OptionButton _color = null!, _mode = null!, _difficulty = null!, _theme = null!;
    private CheckBox _fog = null!;
    private LineEdit _address = null!;
    private SpinBox _port = null!;
    private VBoxContainer _menu = null!, _lobby = null!;
    private Label _players = null!, _status = null!, _lobbyInfo = null!;
    private Button _start = null!;

    public void Setup(GameSession session, PlayerProfile profile)
    {
        _session = session;
        _profile = profile;
        SetAnchorsPreset(LayoutPreset.FullRect);
        Build();
        _session.LobbyChanged += RefreshLobby;
        _session.NetworkError += ShowError;
    }

    public override void _ExitTree()
    {
        _session.LobbyChanged -= RefreshLobby;
        _session.NetworkError -= ShowError;
    }

    // ================= Construction =================

    private void Build()
    {
        AddChild(new ColorRect { Color = new Color(0.09f, 0.08f, 0.06f), AnchorRight = 1, AnchorBottom = 1 });
        var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
        AddChild(center);
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(560, 0) };
        column.AddThemeConstantOverride("separation", 10);
        center.AddChild(column);

        var title = new Label { Text = "Chrono Clash", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 52);
        title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.45f));
        column.AddChild(title);

        // ---- Joueur ----
        var playerRow = Row(column, "Ton nom");
        _name = new LineEdit { Text = _profile.Name, MaxLength = 20, CustomMinimumSize = new Vector2(220, 0) };
        playerRow.AddChild(_name);
        _color = new OptionButton();
        foreach (var (name, _) in PlayerProfile.Colors) _color.AddItem(name);
        _color.Selected = _profile.ColorIndex;
        playerRow.AddChild(_color);

        // ---- Réglages ----
        var settingsRow = Row(column, "Partie");
        _mode = Options(settingsRow, new[] { "Classique", "Rapide", "Bac à sable" }, _profile.Mode);
        _difficulty = Options(settingsRow, new[] { "Bots faciles", "Bots normaux", "Bots difficiles" }, _profile.Difficulty);
        _theme = Options(settingsRow, new[] { "Tempérée", "Archipel", "Désert", "Hiver" }, _profile.Theme);
        _fog = new CheckBox { Text = "Brouillard", ButtonPressed = _profile.Fog };
        settingsRow.AddChild(_fog);
        foreach (var o in new[] { _mode, _difficulty, _theme }) o.ItemSelected += _ => PushSettings();
        _fog.Toggled += _ => PushSettings();

        // ---- Menu principal ----
        _menu = new VBoxContainer();
        _menu.AddThemeConstantOverride("separation", 8);
        column.AddChild(_menu);
        _menu.AddChild(BigButton("Jouer seul (contre 9 bots)", PlaySolo));
        var hostRow = new HBoxContainer();
        hostRow.AddChild(BigButton("Héberger une partie", Host));
        hostRow.AddChild(new Label { Text = "  port" });
        _port = new SpinBox { MinValue = 1024, MaxValue = 65535, Value = _profile.Port };
        hostRow.AddChild(_port);
        _menu.AddChild(hostRow);
        var joinRow = new HBoxContainer();
        joinRow.AddChild(BigButton("Rejoindre", JoinGame));
        _address = new LineEdit { Text = _profile.LastAddress, PlaceholderText = "adresse IP de l'hôte", CustomMinimumSize = new Vector2(240, 0) };
        joinRow.AddChild(_address);
        _menu.AddChild(joinRow);

        // ---- Salon d'attente ----
        _lobby = new VBoxContainer { Visible = false };
        _lobby.AddThemeConstantOverride("separation", 8);
        column.AddChild(_lobby);
        _lobbyInfo = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _lobby.AddChild(_lobbyInfo);
        _players = new Label();
        _players.AddThemeFontSizeOverride("font_size", 18);
        _lobby.AddChild(_players);
        _start = BigButton("Lancer la partie", StartHostedMatch);
        _lobby.AddChild(_start);
        _lobby.AddChild(BigButton("Quitter le salon", () => GetTree().ReloadCurrentScene()));

        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _status.AddThemeColorOverride("font_color", new Color(1f, 0.55f, 0.45f));
        column.AddChild(_status);
    }

    private static HBoxContainer Row(Container parent, string label)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(90, 0) });
        parent.AddChild(row);
        return row;
    }

    private static OptionButton Options(Container parent, string[] items, int selected)
    {
        var o = new OptionButton();
        foreach (var i in items) o.AddItem(i);
        o.Selected = Math.Clamp(selected, 0, items.Length - 1);
        parent.AddChild(o);
        return o;
    }

    private static Button BigButton(string text, Action onPressed)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(260, 44) };
        b.Pressed += onPressed;
        return b;
    }

    // ================= Actions =================

    private MatchSettings ReadSettings(bool cheats) => new()
    {
        Seed = GD.Randi(),
        Mode = (GameMode)_mode.Selected,
        Difficulty = (BotDifficulty)_difficulty.Selected,
        Theme = (MapTheme)_theme.Selected,
        FogOfWar = _fog.ButtonPressed,
        AllowCheats = cheats,
    };

    private void SaveProfile()
    {
        _profile.Name = string.IsNullOrWhiteSpace(_name.Text) ? "Joueur" : _name.Text.Trim();
        _profile.ColorIndex = _color.Selected;
        _profile.LastAddress = _address.Text.Trim();
        _profile.Port = (int)_port.Value;
        _profile.Mode = _mode.Selected;
        _profile.Difficulty = _difficulty.Selected;
        _profile.Theme = _theme.Selected;
        _profile.Fog = _fog.ButtonPressed;
        _profile.Save();
    }

    private void PlaySolo()
    {
        SaveProfile();
        _session.StartSolo(ReadSettings(cheats: true), _profile.Name, _profile.ColorRgb);
    }

    private void Host()
    {
        SaveProfile();
        string? error = _session.StartHost(_profile.Port, _profile.Name, _profile.ColorRgb);
        if (error != null) { ShowError(error); return; }
        _menu.Visible = false;
        _lobby.Visible = true;
        PushSettings();
        RefreshLobby();
    }

    private void JoinGame()
    {
        SaveProfile();
        if (string.IsNullOrWhiteSpace(_profile.LastAddress)) { ShowError("Indique l'adresse IP de l'hôte."); return; }
        string? error = _session.Join(_profile.LastAddress, _profile.Port, _profile.Name, _profile.ColorRgb);
        if (error != null) { ShowError(error); return; }
        _menu.Visible = false;
        _lobby.Visible = true;
        _status.Text = "";
        RefreshLobby();
    }

    private void StartHostedMatch()
    {
        SaveProfile();
        _session.HostStart(ReadSettings(cheats: false));
    }

    /// <summary>L'hôte envoie ses réglages au salon dès qu'il les change.</summary>
    private void PushSettings()
    {
        if (_session.Mode == SessionMode.Host) _session.HostSetSettings(ReadSettings(cheats: false));
    }

    private void ShowError(string message)
    {
        _status.Text = message;
        _menu.Visible = true;
        _lobby.Visible = _session.Mode == SessionMode.Host;
    }

    private void RefreshLobby()
    {
        bool host = _session.Mode == SessionMode.Host;
        _start.Visible = host;
        foreach (var o in new Control[] { _mode, _difficulty, _theme, _fog }) ((BaseButton)o).Disabled = _session.Mode == SessionMode.Client;
        if (_session.Mode == SessionMode.Client)
        {
            var s = _session.LobbySettings;
            _mode.Selected = (int)s.Mode; _difficulty.Selected = (int)s.Difficulty; _theme.Selected = (int)s.Theme; _fog.ButtonPressed = s.FogOfWar;
        }
        var players = _session.LobbyPlayers;
        _players.Text = players.Count == 0
            ? "Connexion à l'hôte…"
            : string.Join("\n", players.Select(p => $"● {p.Name}{(p.IsHost ? " (hôte)" : "")}")) + $"\n\n{players.Count} joueur(s) — {MatchSettings.MaxClans - players.Count} place(s) pour des bots";
        if (host)
        {
            var addresses = IP.GetLocalAddresses().Where(a => a.Contains('.') && !a.StartsWith("127.") && !a.StartsWith("169.254")).ToList();
            _lobbyInfo.Text = $"Tu héberges la partie. Tes amis doivent choisir « Rejoindre » avec l'adresse : {(addresses.Count > 0 ? string.Join(" ou ", addresses) : "(introuvable)")} et le port {_profile.Port}.\n" +
                              $"En réseau local, ça suffit. Par internet, ouvre le port UDP {_profile.Port} sur ta box et donne-leur ton adresse IP publique.";
        }
        else _lobbyInfo.Text = _session.Accepted && players.Count > 0 ? "Connecté ! En attente que l'hôte lance la partie…" : $"Connexion à {_profile.LastAddress}:{_profile.Port}…";
    }
}

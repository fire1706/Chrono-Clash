using ChronoClash.Core.Simulation;
using ChronoClash.Game.Localization;
using ChronoClash.Game.Net;
using ChronoClash.Game.Persistence;
using ChronoClash.Game.Presentation;
using Godot;

namespace ChronoClash.Game.UI;

/// <summary>
/// Écran titre et menus :
/// - accueil : Jouer, Tutoriel, Défis et records, Réglages, Quitter ;
/// - préparation : nom, couleur, emblème, réglages de partie, puis Jouer seul / Héberger / Rejoindre ;
/// - salon d'attente : liste des joueurs ; l'hôte lance la partie (les places libres sont prises par des bots).
/// </summary>
public partial class TitleScreen : Control
{
    private GameSession _session = null!;
    private PlayerProfile _profile = null!;

    private VBoxContainer _home = null!, _setup = null!, _lobby = null!;
    private LineEdit _name = null!, _address = null!;
    private OptionButton _color = null!, _emblem = null!, _mode = null!, _difficulty = null!, _theme = null!;
    private CheckBox _fog = null!;
    private SpinBox _port = null!;
    private EmblemPreview _preview = null!;
    private Label _players = null!, _status = null!, _lobbyInfo = null!;
    private Button _start = null!;
    private float _time;

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
        foreach (var child in GetChildren()) child.QueueFree();
        var bg = new TextureRect { AnchorRight = 1, AnchorBottom = 1, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered };
        // Image de fond du graphiste si elle existe (game/assets/art/ui/title_background.png), sinon un fond uni.
        if (ResourceLoader.Exists("res://assets/art/ui/title_background.png")) bg.Texture = GD.Load<Texture2D>("res://assets/art/ui/title_background.png");
        AddChild(new ColorRect { Color = new Color(0.09f, 0.08f, 0.06f), AnchorRight = 1, AnchorBottom = 1 });
        AddChild(bg);

        var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
        AddChild(center);
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(620, 0) };
        column.AddThemeConstantOverride("separation", 10);
        center.AddChild(column);
        column.AddChild(UiKit.Title("Chrono Clash", 64));
        var tagline = UiKit.Text(Loc.T("Dix clans. Huit ères. Un seul survivant."), 18, false);
        tagline.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(tagline);
        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 16) });

        // ---- Accueil ----
        _home = new VBoxContainer();
        _home.AddThemeConstantOverride("separation", 10);
        column.AddChild(_home);
        _home.AddChild(Centered(UiKit.Button("Jouer", () => Show(_setup), 320, 50)));
        var tutorial = UiKit.Button(_profile.TutorialDone ? "Tutoriel" : "Tutoriel (conseillé)", StartTutorial, 320, 50);
        if (!_profile.TutorialDone) tutorial.AddThemeColorOverride("font_color", UiKit.Gold);
        _home.AddChild(Centered(tutorial));
        _home.AddChild(Centered(UiKit.Button("Défis et records", () => AddChild(new RecordsPanel(() => { })), 320, 50)));
        _home.AddChild(Centered(UiKit.Button("Réglages", () => AddChild(new SettingsPanel(Build)), 320, 50)));
        _home.AddChild(Centered(UiKit.Button("Quitter", () => GetTree().Quit(), 320, 50)));

        // ---- Préparation ----
        _setup = new VBoxContainer { Visible = false };
        _setup.AddThemeConstantOverride("separation", 8);
        column.AddChild(_setup);
        var who = UiKit.Row(_setup, "Ton clan");
        _name = new LineEdit { Text = _profile.Name, MaxLength = 20, CustomMinimumSize = new Vector2(170, 0) };
        who.AddChild(_name);
        _color = UiKit.Options(who, PlayerProfile.ColorChoices.Select(c => c.Name).ToArray(), _profile.ColorIndex);
        _emblem = UiKit.Options(who, Emblems.Names, _profile.Emblem);
        _preview = new EmblemPreview { Emblem = _profile.Emblem };
        who.AddChild(_preview);
        _color.ItemSelected += _ => UpdatePreview();
        _emblem.ItemSelected += _ => UpdatePreview();
        UpdatePreview();

        var match = UiKit.Row(_setup, "Partie");
        _mode = UiKit.Options(match, new[] { "Classique", "Rapide", "Bac à sable" }, Math.Min(_profile.Mode, 2));
        _difficulty = UiKit.Options(match, new[] { "Bots faciles", "Bots normaux", "Bots difficiles" }, _profile.Difficulty);
        _theme = UiKit.Options(match, new[] { "Tempérée", "Archipel", "Désert", "Hiver" }, _profile.Theme);
        _fog = new CheckBox { Text = Loc.T("Brouillard"), ButtonPressed = _profile.Fog, FocusMode = FocusModeEnum.None };
        match.AddChild(_fog);
        foreach (var o in new[] { _mode, _difficulty, _theme }) o.ItemSelected += _ => PushSettings();
        _fog.Toggled += _ => PushSettings();

        _setup.AddChild(UiKit.Button("Jouer seul (contre 9 bots)", PlaySolo, 620, 46));
        var hostRow = new HBoxContainer();
        hostRow.AddChild(UiKit.Button("Héberger une partie", Host, 300, 42));
        hostRow.AddChild(new Label { Text = "  " + Loc.T("port") });
        _port = new SpinBox { MinValue = 1024, MaxValue = 65535, Value = _profile.Port };
        hostRow.AddChild(_port);
        _setup.AddChild(hostRow);
        var joinRow = new HBoxContainer();
        joinRow.AddChild(UiKit.Button("Rejoindre", JoinGame, 300, 42));
        _address = new LineEdit { Text = _profile.LastAddress, PlaceholderText = Loc.T("adresse IP de l'hôte"), CustomMinimumSize = new Vector2(240, 0) };
        joinRow.AddChild(_address);
        _setup.AddChild(joinRow);
        _setup.AddChild(UiKit.Button("Retour", () => Show(_home), 200, 36));

        // ---- Salon d'attente ----
        _lobby = new VBoxContainer { Visible = false };
        _lobby.AddThemeConstantOverride("separation", 8);
        column.AddChild(_lobby);
        _lobbyInfo = UiKit.Text("", 14);
        _lobby.AddChild(_lobbyInfo);
        _players = UiKit.Text("", 18);
        _lobby.AddChild(_players);
        _start = UiKit.Button("Lancer la partie", StartHostedMatch, 320, 46);
        _lobby.AddChild(_start);
        _lobby.AddChild(UiKit.Button("Quitter le salon", () => { _session.Leave(); GetTree().ReloadCurrentScene(); }, 320, 38));

        _status = UiKit.Text("", 14);
        _status.AddThemeColorOverride("font_color", new Color(1f, 0.55f, 0.45f));
        column.AddChild(_status);
    }

    private static CenterContainer Centered(Control c)
    {
        var box = new CenterContainer();
        box.AddChild(c);
        return box;
    }

    private void Show(VBoxContainer page)
    {
        _home.Visible = page == _home;
        _setup.Visible = page == _setup;
        _lobby.Visible = page == _lobby;
        _status.Text = "";
    }

    private void UpdatePreview()
    {
        _preview.Emblem = _emblem.Selected;
        _preview.Tint = CoreConvert.ColorOf(PlayerProfile.ColorChoices[_color.Selected].Rgb);
        _preview.QueueRedraw();
    }

    // ================= Actions =================

    private MatchSettings ReadSettings(bool cheats, GameMode? mode = null) => new()
    {
        Seed = GD.Randi(),
        Mode = mode ?? (GameMode)_mode.Selected,
        Difficulty = (BotDifficulty)_difficulty.Selected,
        Theme = (MapTheme)_theme.Selected,
        FogOfWar = _fog.ButtonPressed,
        AllowCheats = cheats,
    };

    private void SaveProfile()
    {
        _profile.Name = string.IsNullOrWhiteSpace(_name.Text) ? "Joueur" : _name.Text.Trim();
        _profile.ColorIndex = _color.Selected;
        _profile.Emblem = _emblem.Selected;
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
        _session.StartSolo(ReadSettings(cheats: true), _profile.Name, _profile.ColorRgb, _profile.Emblem);
    }

    private void StartTutorial()
    {
        // Le tutoriel : seul sur la carte tempérée, sans zone ni brouillard.
        var settings = new MatchSettings { Seed = GD.Randi(), Mode = GameMode.Tutorial, Theme = MapTheme.Temperate, FogOfWar = false, AllowCheats = true };
        _session.StartSolo(settings, _profile.Name, _profile.ColorRgb, _profile.Emblem);
    }

    private void Host()
    {
        SaveProfile();
        string? error = _session.StartHost(_profile.Port, _profile.Name, _profile.ColorRgb, _profile.Emblem);
        if (error != null) { ShowError(error); return; }
        Show(_lobby);
        PushSettings();
        RefreshLobby();
    }

    private void JoinGame()
    {
        SaveProfile();
        if (string.IsNullOrWhiteSpace(_profile.LastAddress)) { ShowError(Loc.T("Indique l'adresse IP de l'hôte.")); return; }
        string? error = _session.Join(_profile.LastAddress, _profile.Port, _profile.Name, _profile.ColorRgb, _profile.Emblem);
        if (error != null) { ShowError(error); return; }
        Show(_lobby);
        RefreshLobby();
    }

    private void StartHostedMatch()
    {
        SaveProfile();
        if (_session.Mode == SessionMode.Client) _session.RequestDedicatedStart(ReadSettings(cheats: false)); // serveur dédié
        else _session.HostStart(ReadSettings(cheats: false));
    }

    private void PushSettings()
    {
        if (_session.Mode == SessionMode.Host) _session.HostSetSettings(ReadSettings(cheats: false));
    }

    private void ShowError(string message)
    {
        if (_session.Mode == SessionMode.Client) Show(_setup);
        _status.Text = message;
    }

    private void RefreshLobby()
    {
        bool host = _session.Mode == SessionMode.Host;
        _start.Visible = _session.CanStartMatch;
        var players = _session.LobbyPlayers;
        _players.Text = players.Count == 0
            ? Loc.T("Connexion à l'hôte…")
            : string.Join("\n", players.Select(p => $"● {p.Name}{(p.IsHost ? " " + Loc.T("(hôte)") : "")}")) + "\n\n" +
              Loc.F("{0} joueur(s) — {1} place(s) pour des bots", players.Count, MatchSettings.MaxClans - players.Count);
        if (host)
        {
            var addresses = IP.GetLocalAddresses().Where(a => a.Contains('.') && !a.StartsWith("127.") && !a.StartsWith("169.254")).ToList();
            _lobbyInfo.Text = Loc.F("Tu héberges la partie. Tes amis choisissent « Rejoindre » avec l'adresse {0} et le port {1}.", addresses.Count > 0 ? string.Join(" / ", addresses) : "?", _profile.Port) + "\n" +
                              Loc.F("En réseau local, ça suffit. Par internet, ouvre le port UDP {0} sur ta box et donne ton adresse IP publique.", _profile.Port);
        }
        else
        {
            var s = _session.LobbySettings;
            _lobbyInfo.Text = (_session.CanStartMatch ? Loc.T("Tu mènes le salon de ce serveur : lance la partie quand tout le monde est là (réglages de l'écran précédent).")
                : _session.Accepted && players.Count > 0 ? Loc.T("Connecté ! En attente que l'hôte lance la partie…") : Loc.F("Connexion à {0}:{1}…", _profile.LastAddress, _profile.Port))
                + "\n" + Loc.F("Partie : {0} · {1} · {2}", UiKit.ModeName(s.Mode), UiKit.DifficultyName(s.Difficulty), UiKit.ThemeName(s.Theme));
        }
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        if (_lobby.Visible) RefreshLobby(); // pour l'état de connexion côté client
    }
}

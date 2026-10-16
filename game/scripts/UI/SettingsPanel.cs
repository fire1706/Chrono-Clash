using ChronoClash.Game.Localization;
using ChronoClash.Game.Settings;
using Godot;

namespace ChronoClash.Game.UI;

/// <summary>
/// Écran des réglages (depuis le menu principal ou la pause) : langue, volumes, voix de l'annonceur,
/// qualité graphique, et touches du clavier (clic sur une touche puis appui sur la nouvelle ; Échap annule).
/// Si une touche est déjà prise par une autre action, les deux actions échangent leurs touches.
/// </summary>
public partial class SettingsPanel : Control
{
    private readonly Action _onClose;
    private string? _waitingFor;
    private readonly Dictionary<string, Button> _keyButtons = new();

    public SettingsPanel(Action onClose) => _onClose = onClose;

    /// <summary>Constructeur sans paramètre exigé par Godot (non utilisé par le jeu).</summary>
    public SettingsPanel() : this(() => { }) { }

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Build();
    }

    private void Build()
    {
        foreach (var child in GetChildren()) child.QueueFree();
        _keyButtons.Clear();
        var s = GameSettings.Current;
        AddChild(new ColorRect { Color = new Color(0, 0, 0, 0.55f), AnchorRight = 1, AnchorBottom = 1 });
        var column = UiKit.CenteredPanel(this, 760);
        column.AddChild(UiKit.Title("Réglages"));

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 30);
        column.AddChild(columns);
        var left = new VBoxContainer();
        left.AddThemeConstantOverride("separation", 8);
        columns.AddChild(left);

        var lang = UiKit.Row(left, "Langue", 160);
        var language = UiKit.Options(lang, new[] { "Français", "English" }, s.Language == "en" ? 1 : 0);
        language.ItemSelected += i => { s.Language = i == 1 ? "en" : "fr"; s.Save(); Build(); };

        UiKit.Slider(left, "Musique", s.MusicVolume, v => { s.MusicVolume = v; s.Save(); });
        UiKit.Slider(left, "Effets sonores", s.SfxVolume, v => { s.SfxVolume = v; s.Save(); });
        var voiceRow = UiKit.Row(left, "Annonceur", 160);
        var voice = new CheckBox { Text = Loc.T("Voix activée"), ButtonPressed = s.VoiceEnabled, FocusMode = FocusModeEnum.None };
        voice.Toggled += on => { s.VoiceEnabled = on; s.Save(); };
        voiceRow.AddChild(voice);
        UiKit.Slider(left, "Volume de la voix", s.VoiceVolume, v => { s.VoiceVolume = v; s.Save(); });
        var qualityRow = UiKit.Row(left, "Graphismes", 160);
        var quality = UiKit.Options(qualityRow, new[] { "Qualité haute", "Qualité basse" }, s.LowQuality ? 1 : 0);
        quality.ItemSelected += i => { s.LowQuality = i == 1; s.Save(); };
        left.AddChild(UiKit.Text(Loc.T("La qualité basse simplifie les troupes et les effets : utile si le jeu rame."), 12));

        // ---- Touches ----
        var right = new VBoxContainer();
        columns.AddChild(right);
        right.AddChild(UiKit.Text(Loc.T("Touches (clique puis appuie sur la nouvelle touche)"), 13));
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(360, 380) };
        right.AddChild(scroll);
        var keys = new VBoxContainer();
        scroll.AddChild(keys);
        foreach (var (action, label, _) in KeyBindings.Actions)
        {
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = Loc.T(label), CustomMinimumSize = new Vector2(220, 0) });
            var b = new Button { Text = KeyBindings.LabelOf(action), CustomMinimumSize = new Vector2(110, 28), FocusMode = FocusModeEnum.None };
            string a = action;
            b.Pressed += () => { _waitingFor = a; b.Text = Loc.T("Appuie…"); };
            row.AddChild(b);
            keys.AddChild(row);
            _keyButtons[action] = b;
        }
        right.AddChild(UiKit.Button("Touches par défaut", () => { s.Keys.Clear(); s.Save(); Build(); }, 220, 32));

        column.AddChild(UiKit.Button("Fermer", Close));
    }

    private void Close()
    {
        _waitingFor = null;
        QueueFree();
        _onClose();
    }

    /// <summary>Capture la nouvelle touche quand on change un raccourci ; Échap ferme (ou annule).</summary>
    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
        GetViewport().SetInputAsHandled();
        if (_waitingFor == null)
        {
            if (key.Keycode == Key.Escape) Close();
            return;
        }
        var s = GameSettings.Current;
        if (key.Keycode != Key.Escape || _waitingFor == "pause")
        {
            var newKey = key.PhysicalKeycode;
            var oldKey = s.KeyFor(_waitingFor);
            // Touche déjà utilisée ailleurs : on échange.
            foreach (var (other, _, _) in KeyBindings.Actions)
                if (other != _waitingFor && s.KeyFor(other) == newKey) s.Keys[other] = oldKey;
            s.Keys[_waitingFor] = newKey;
            s.Save();
        }
        _waitingFor = null;
        foreach (var (action, button) in _keyButtons) button.Text = KeyBindings.LabelOf(action);
    }
}

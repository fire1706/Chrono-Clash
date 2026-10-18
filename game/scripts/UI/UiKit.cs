using ChronoClash.Game.Localization;
using Godot;

namespace ChronoClash.Game.UI;

/// <summary>
/// Petits outils pour construire l'interface en code avec un style commun (panneaux sombres aux coins arrondis,
/// boutons, titres). Pour changer l'apparence de tous les menus d'un coup, c'est ici.
/// </summary>
public static class UiKit
{
    // ---- Palette du prototype HTML (thème sombre) ----
    /// <summary>Ocre : boutons principaux, titres, accents (--ochre).</summary>
    public static readonly Color Gold = Color.FromHtml("#e8ac34");
    /// <summary>Encre sombre posée sur l'ocre (--ochre-ink).</summary>
    public static readonly Color OchreInk = Color.FromHtml("#241703");
    /// <summary>Fond des panneaux (--panel), texte (--ink), texte secondaire (--muted), filets (--line).</summary>
    public static readonly Color PanelColor = new(30 / 255f, 27 / 255f, 24 / 255f, 0.93f);
    public static readonly Color Ink = Color.FromHtml("#f1ebe1");
    public static readonly Color Muted = Color.FromHtml("#b0a596");
    public static readonly Color Line = new(241 / 255f, 235 / 255f, 225 / 255f, 0.16f);

    // ---- Polices du prototype : Barlow (texte) et Barlow Condensed extra-gras (titres), licence libre OFL ----
    private static Font? _body, _bodyBold, _titleFont;
    private static Font? LoadFont(string file) => ResourceLoader.Exists($"res://assets/fonts/{file}") ? GD.Load<Font>($"res://assets/fonts/{file}") : null;
    public static Font? BodyFont => _body ??= LoadFont("Barlow-Regular.woff2");
    public static Font? BoldFont => _bodyBold ??= LoadFont("Barlow-SemiBold.woff2");
    public static Font? TitleFont => _titleFont ??= LoadFont("BarlowCondensed-ExtraBold.woff2");

    private static StyleBoxFlat Box(Color bg, Color border, int radius = 8, int borderWidth = 1, int padH = 12, int padV = 7) => new()
    {
        BgColor = bg, BorderColor = border, CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        BorderWidthBottom = borderWidth, BorderWidthTop = borderWidth, BorderWidthLeft = borderWidth, BorderWidthRight = borderWidth,
        ContentMarginLeft = padH, ContentMarginRight = padH, ContentMarginTop = padV, ContentMarginBottom = padV,
    };

    /// <summary>
    /// Le thème de toute l'interface, repris du prototype : boutons sombres à bord fin et coins arrondis, champs de saisie,
    /// listes déroulantes, cases à cocher, curseurs et barres de progression aux couleurs ocre.
    /// Appliqué une fois à la fenêtre par GameRoot : tous les menus et le HUD en profitent.
    /// </summary>
    public static Theme CreateTheme()
    {
        var t = new Theme();
        if (BodyFont != null) t.DefaultFont = BodyFont;
        t.DefaultFontSize = 16;
        var normal = Box(new Color(1, 1, 1, 0.05f), Line);
        var hover = Box(new Color(1, 1, 1, 0.1f), new Color(Gold, 0.7f));
        var pressed = Box(new Color(Gold, 0.25f), Gold);
        var disabled = Box(new Color(1, 1, 1, 0.02f), new Color(Line, 0.08f));
        foreach (var type in new[] { "Button", "OptionButton" })
        {
            t.SetStylebox("normal", type, normal);
            t.SetStylebox("hover", type, hover);
            t.SetStylebox("pressed", type, pressed);
            t.SetStylebox("hover_pressed", type, pressed);
            t.SetStylebox("disabled", type, disabled);
            t.SetStylebox("focus", type, new StyleBoxEmpty());
            t.SetColor("font_color", type, Ink);
            t.SetColor("font_hover_color", type, Colors.White);
            t.SetColor("font_pressed_color", type, Colors.White);
            t.SetColor("font_disabled_color", type, new Color(Ink, 0.4f));
            if (BoldFont != null) t.SetFont("font", type, BoldFont);
        }
        t.SetStylebox("normal", "LineEdit", Box(new Color(0, 0, 0, 0.25f), Line));
        t.SetStylebox("focus", "LineEdit", Box(new Color(0, 0, 0, 0.25f), Gold));
        t.SetColor("font_color", "LineEdit", Ink);
        t.SetColor("font_color", "Label", Ink);
        t.SetColor("font_color", "CheckBox", Ink);
        t.SetStylebox("panel", "PanelContainer", PanelStyle());
        t.SetStylebox("panel", "PopupMenu", Box(PanelColor, Line, 6, 1, 6, 6));
        t.SetColor("font_color", "PopupMenu", Ink);
        t.SetColor("font_hover_color", "PopupMenu", Colors.White);
        t.SetStylebox("hover", "PopupMenu", Box(new Color(Gold, 0.25f), new Color(0, 0, 0, 0), 4, 0, 4, 2));
        t.SetStylebox("background", "ProgressBar", Box(new Color(0, 0, 0, 0.35f), new Color(0, 0, 0, 0), 4, 0, 0, 0));
        t.SetStylebox("fill", "ProgressBar", Box(Gold, new Color(0, 0, 0, 0), 4, 0, 0, 0));
        t.SetStylebox("slider", "HSlider", Box(new Color(0, 0, 0, 0.35f), new Color(0, 0, 0, 0), 3, 0, 0, 2));
        t.SetStylebox("grabber_area", "HSlider", Box(Gold, new Color(0, 0, 0, 0), 3, 0, 0, 2));
        t.SetStylebox("grabber_area_highlight", "HSlider", Box(Gold.Lightened(0.2f), new Color(0, 0, 0, 0), 3, 0, 0, 2));
        return t;
    }

    /// <summary>Bouton principal (« Jouer », « Lancer la partie »…) : fond ocre et texte sombre, comme le prototype.</summary>
    public static void MakePrimary(Button b)
    {
        b.AddThemeStyleboxOverride("normal", Box(Gold, Gold, 8, 0, 22, 11));
        b.AddThemeStyleboxOverride("hover", Box(Gold.Lightened(0.12f), Gold, 8, 0, 22, 11));
        b.AddThemeStyleboxOverride("pressed", Box(Gold.Darkened(0.12f), Gold, 8, 0, 22, 11));
        foreach (var c in new[] { "font_color", "font_hover_color", "font_pressed_color" }) b.AddThemeColorOverride(c, OchreInk);
        b.AddThemeFontSizeOverride("font_size", 18);
    }

    /// <summary>Clic sonore des boutons (branché par GameRoot sur l'AudioManager quand il existe).</summary>
    public static Action? OnClick;

    /// <summary>Panneau du prototype (« card ») : fond sombre translucide, filet clair, coins de 10 px, ombre portée.</summary>
    public static StyleBoxFlat PanelStyle(float alpha = 0.93f)
    {
        var box = Box(new Color(PanelColor, alpha), Line, 10, 1, 24, 22);
        box.ShadowColor = new Color(0, 0, 0, 0.5f);
        box.ShadowSize = 14;
        box.ShadowOffset = new Vector2(0, 8);
        return box;
    }

    /// <summary>Panneau centré à l'écran contenant une colonne ; renvoie la colonne (où ajouter le contenu).</summary>
    public static VBoxContainer CenteredPanel(Control parent, float width)
    {
        var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
        parent.AddChild(center);
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", PanelStyle());
        center.AddChild(panel);
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(width, 0) };
        column.AddThemeConstantOverride("separation", 10);
        panel.AddChild(column);
        return column;
    }

    /// <summary>Titre en Barlow Condensed extra-gras (comme les h1 du prototype).</summary>
    public static Label Title(string text, int size = 30)
    {
        var l = new Label { Text = Loc.T(text), HorizontalAlignment = HorizontalAlignment.Center };
        if (TitleFont != null) l.AddThemeFontOverride("font", TitleFont);
        l.AddThemeFontSizeOverride("font_size", (int)(size * 1.3f)); // la police condensée paraît plus petite
        l.AddThemeColorOverride("font_color", Ink);
        return l;
    }

    public static Label Text(string text, int size = 15, bool wrap = true)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", size);
        if (wrap) l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return l;
    }

    public static Button Button(string text, Action onPressed, float width = 280, float height = 42)
    {
        var b = new Button { Text = Loc.T(text), CustomMinimumSize = new Vector2(width, height), FocusMode = Control.FocusModeEnum.None };
        b.Pressed += () => { OnClick?.Invoke(); onPressed(); };
        return b;
    }

    public static HBoxContainer Row(Container parent, string label, float labelWidth = 120)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new Label { Text = Loc.T(label), CustomMinimumSize = new Vector2(labelWidth, 0) });
        parent.AddChild(row);
        return row;
    }

    public static OptionButton Options(Container parent, string[] items, int selected)
    {
        var o = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        foreach (var i in items) o.AddItem(Loc.T(i));
        o.Selected = Math.Clamp(selected, 0, items.Length - 1);
        parent.AddChild(o);
        return o;
    }

    /// <summary>Noms affichés des modes, difficultés et thèmes.</summary>
    public static string ModeName(Core.Simulation.GameMode m) => Loc.T(m switch
    {
        Core.Simulation.GameMode.Quick => "Partie rapide",
        Core.Simulation.GameMode.Sandbox => "Bac à sable",
        Core.Simulation.GameMode.Tutorial => "Tutoriel",
        _ => "Partie classique",
    });

    public static string DifficultyName(Core.Simulation.BotDifficulty d) => Loc.T(d switch
    {
        Core.Simulation.BotDifficulty.Easy => "Bots faciles",
        Core.Simulation.BotDifficulty.Hard => "Bots difficiles",
        _ => "Bots normaux",
    });

    public static string ThemeName(Core.Simulation.MapTheme t) => Loc.T(t switch
    {
        Core.Simulation.MapTheme.Archipelago => "Archipel",
        Core.Simulation.MapTheme.Desert => "Désert",
        Core.Simulation.MapTheme.Winter => "Hiver",
        _ => "Tempérée",
    });

    /// <summary>Curseur 0 à 100 % avec son libellé.</summary>
    public static HSlider Slider(Container parent, string label, float value, Action<float> changed)
    {
        var row = Row(parent, label, 160);
        var s = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.01, Value = value, CustomMinimumSize = new Vector2(220, 0) };
        s.ValueChanged += v => changed((float)v);
        row.AddChild(s);
        return s;
    }
}

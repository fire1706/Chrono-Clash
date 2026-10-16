using ChronoClash.Game.Localization;
using Godot;

namespace ChronoClash.Game.UI;

/// <summary>
/// Petits outils pour construire l'interface en code avec un style commun (panneaux sombres aux coins arrondis,
/// boutons, titres). Pour changer l'apparence de tous les menus d'un coup, c'est ici.
/// </summary>
public static class UiKit
{
    public static readonly Color Gold = new(1f, 0.85f, 0.45f);
    public static readonly Color PanelColor = new(0.12f, 0.1f, 0.08f, 0.96f);

    /// <summary>Clic sonore des boutons (branché par GameRoot sur l'AudioManager quand il existe).</summary>
    public static Action? OnClick;

    public static StyleBoxFlat PanelStyle(float alpha = 0.96f) => new()
    {
        BgColor = new Color(PanelColor, alpha), CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
        ContentMarginLeft = 22, ContentMarginRight = 22, ContentMarginTop = 18, ContentMarginBottom = 18,
        BorderColor = new Color(0.94f, 0.71f, 0.16f, 0.5f), BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
    };

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

    public static Label Title(string text, int size = 30)
    {
        var l = new Label { Text = Loc.T(text), HorizontalAlignment = HorizontalAlignment.Center };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", Gold);
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

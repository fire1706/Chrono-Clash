using ChronoClash.Game.Localization;
using ChronoClash.Game.Stats;
using Godot;

namespace ChronoClash.Game.UI;

/// <summary>Défis débloqués et records personnels (depuis le menu principal).</summary>
public partial class RecordsPanel : Control
{
    private readonly Action _onClose;

    public RecordsPanel(Action onClose) => _onClose = onClose;

    /// <summary>Constructeur sans paramètre exigé par Godot (non utilisé par le jeu).</summary>
    public RecordsPanel() : this(() => { }) { }

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        var a = Achievements.Load();
        AddChild(new ColorRect { Color = new Color(0, 0, 0, 0.55f), AnchorRight = 1, AnchorBottom = 1 });
        var column = UiKit.CenteredPanel(this, 760);
        column.AddChild(UiKit.Title("Défis et records"));

        var data = Core.Data.GameData.Default;
        string place = a.BestPlacement > 10 ? "—" : $"{a.BestPlacement}";
        column.AddChild(UiKit.Text(
            Loc.F("Parties jouées : {0}   ·   Victoires : {1}   ·   Meilleure place : {2}", a.Played, a.Wins, place) + "\n" +
            Loc.F("Ère la plus avancée : {0}   ·   Plus grande armée : {1}   ·   Plus de victimes : {2}", Loc.T(data.Eras[Math.Clamp(a.BestEra, 0, 7)].Name), a.MaxArmy, a.MaxKills) + "\n" +
            Loc.F("Meilleur multiplicateur : ×{0}   ·   Plus longue survie : {1} min", a.BestMultiplier, (int)(a.LongestSurvival / 60)), 15));

        column.AddChild(UiKit.Text(Loc.F("Défis : {0} / {1}", a.Unlocked.Count, Achievements.Challenges.Length), 18));
        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 8);
        column.AddChild(grid);
        foreach (var c in Achievements.Challenges)
        {
            bool done = a.Unlocked.Contains(c.Id);
            var box = new VBoxContainer { CustomMinimumSize = new Vector2(230, 0), Modulate = new Color(1, 1, 1, done ? 1 : 0.45f) };
            var name = UiKit.Text((done ? "★ " : "☆ ") + Loc.T(c.Name), 15, false);
            name.AddThemeColorOverride("font_color", done ? UiKit.Gold : Colors.White);
            box.AddChild(name);
            box.AddChild(UiKit.Text(Loc.T(c.Description), 12));
            grid.AddChild(box);
        }
        column.AddChild(UiKit.Button("Fermer", () => { QueueFree(); _onClose(); }));
    }
}

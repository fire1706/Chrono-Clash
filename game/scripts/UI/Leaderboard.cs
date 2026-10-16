using ChronoClash.Game.Net;
using ChronoClash.Game.Presentation;
using Godot;

namespace ChronoClash.Game.UI;

/// <summary>
/// Classement des clans (touche L) : nom, personnalité pour les bots, ère, troupes et ★.
/// Les clans en vie d'abord, triés par points ; les éliminés ensuite, avec leur place.
/// </summary>
public partial class Leaderboard : Control
{
    private GameSession _session = null!;

    public void Setup(GameSession session)
    {
        _session = session;
        MouseFilter = MouseFilterEnum.Ignore;
        Size = new Vector2(360, 300);
    }

    public override void _Process(double delta)
    {
        var size = GetViewportRect().Size;
        Position = new Vector2(size.X - Size.X - 12, 150);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var world = _session.World;
        var rows = world.Armies.Where(a => !a.IsPassive)
            .OrderByDescending(a => a.Alive).ThenByDescending(a => a.Alive ? a.Points : -a.Placement).ToList();
        float h = 30 + rows.Count * 24;
        DrawRect(new Rect2(0, 0, Size.X, h), new Color(0.1f, 0.08f, 0.06f, 0.85f));
        var font = ThemeDB.FallbackFont;
        DrawString(font, new Vector2(12, 21), $"Classement — {world.Armies.Count(a => a.Alive && !a.IsPassive)} clans en vie", HorizontalAlignment.Left, -1, 14, new Color(1, 0.88f, 0.6f));
        float y = 46;
        int rank = 1;
        foreach (var a in rows)
        {
            var color = a.Alive ? CoreConvert.ColorOf(a.ColorRgb) : new Color(0.6f, 0.6f, 0.6f, 0.7f);
            bool mine = a == _session.LocalArmy;
            string name = (mine ? "Toi" : a.Name) + (a.Brain != null ? $" · {a.Brain.Personality.Name}" : "");
            string place = a.Alive ? $"{rank}." : $"{a.Placement}e";
            DrawString(font, new Vector2(12, y), place, HorizontalAlignment.Left, -1, 13, Colors.White);
            DrawCircle(new Vector2(42, y - 5), 5, color);
            DrawString(font, new Vector2(54, y), name, HorizontalAlignment.Left, 170, 13, mine ? new Color(1, 0.9f, 0.5f) : Colors.White);
            string stats = a.Alive ? $"{world.Data.Eras[a.Era].Short} · {a.Units.Count} · {a.Points} ★" : "éliminé";
            DrawString(font, new Vector2(228, y), stats, HorizontalAlignment.Right, 120, 12, new Color(1, 1, 1, 0.8f));
            y += 24;
            if (a.Alive) rank++;
        }
    }
}

using ChronoClash.Core.Entities;
using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Simulation;

namespace ChronoClash.Core.AI;

/// <summary>Ce qu'un bot peut évaluer : force des armées, position d'un clan, et ce qu'il voit vraiment.</summary>
public static class BotSenses
{
    /// <summary>
    /// Force d'une armée : somme de (vie × dégâts ÷ recharge) du chef et des troupes, les tireurs comptant ×1,4.
    /// C'est une estimation simple, la même que dans le prototype.
    /// </summary>
    public static float Strength(World world, Army army)
    {
        var g = army.General;
        float s = g.Dead ? 0 : g.MaxHp * g.Damage / MathF.Max(0.1f, g.Cooldown);
        foreach (var u in army.Units)
        {
            if (u.Dead) continue;
            var st = world.Data.Stats(u.Kind, u.Era);
            s += u.Hp * st.Damage / MathF.Max(0.1f, st.Cooldown) * (st.Range > 0 ? 1.4f : 1);
        }
        return s;
    }

    /// <summary>Force estimée du boss (pour décider s'il vaut la peine de l'attaquer).</summary>
    public static float Strength(Boss boss) => boss.Hp * boss.Damage / boss.Cooldown * 1.2f;

    /// <summary>Position d'un clan : son chef, ou le centre de ses troupes si le chef est mort.</summary>
    public static Vec2? Anchor(Army army)
    {
        if (!army.General.Dead) return army.General.Position;
        int n = 0;
        float x = 0, y = 0;
        foreach (var u in army.Units)
            if (!u.Dead) { x += u.Position.X; y += u.Position.Y; n++; }
        return n > 0 ? new Vec2(x / n, y / n) : null;
    }

    /// <summary>
    /// Vrai si le clan <paramref name="observer"/> voit actuellement au moins un corps du clan <paramref name="other"/>,
    /// d'après son brouillard de guerre. Les bots ne trichent pas : même vision qu'un joueur humain.
    /// </summary>
    public static bool Sees(World world, Army observer, Army other)
    {
        var vision = world.Vision[observer.Id];
        if (!other.General.Dead && vision.IsVisible(other.General.Position)) return true;
        int n = other.Units.Count;
        if (n == 0) return false;
        // On teste quelques troupes réparties dans l'armée (suffisant et beaucoup plus rapide que toutes).
        int step = Math.Max(1, n / 12);
        for (int i = (int)(world.Tick % step); i < n; i += step)
            if (!other.Units[i].Dead && vision.IsVisible(other.Units[i].Position)) return true;
        return false;
    }
}

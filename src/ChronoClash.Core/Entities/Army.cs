using ChronoClash.Core.Commands;
using ChronoClash.Core.Formations;
using ChronoClash.Core.Mathematics;

namespace ChronoClash.Core.Entities;

/// <summary>
/// Un clan : son chef, ses troupes, ses ressources ◆, ses points ★, son ère, ses formations et ses tactiques.
/// Un clan est contrôlé soit par un humain, soit par un bot : dans les deux cas,
/// il reçoit les mêmes ordres (<see cref="PlayerCommand"/>).
/// </summary>
public sealed class Army
{
    /// <summary>Nombre de formations mémorisées par clan (touches 7, 8, 9, 0).</summary>
    public const int FormationCount = 4;
    /// <summary>Dernière ère (0 = Préhistoire … 7 = Futur).</summary>
    public const int MaxEra = 7;

    public Army(int id, int playerId, string name, uint colorRgb, bool isBot, General general)
    {
        Id = id;
        PlayerId = playerId;
        Name = name;
        ColorRgb = colorRgb;
        IsBot = isBot;
        General = general;
        general.Owner = this;
        for (int i = 0; i < FormationCount; i++) Formations[i] = FormationTemplate.Preset(i);
    }

    /// <summary>Numéro du clan dans la partie (0 à 9).</summary>
    public int Id { get; }
    /// <summary>Numéro du joueur qui contrôle ce clan (celui qui figure dans les ordres).</summary>
    public int PlayerId { get; }
    public string Name { get; set; }
    /// <summary>Couleur du clan en hexadécimal RVB, par exemple 0xF0B52A pour l'or.</summary>
    public uint ColorRgb { get; set; }
    public bool IsBot { get; set; }
    /// <summary>Clan de test qui ne bouge pas (touche F6). Les bots l'ignorent pour réfléchir.</summary>
    public bool IsPassive { get; set; }
    /// <summary>« Cerveau » du bot (null pour un humain).</summary>
    public AI.BotBrain? Brain { get; set; }
    public General General { get; }

    // ---- État dans la partie ----
    public bool Alive { get; set; } = true;
    /// <summary>Clan qui l'a touché en dernier (reçoit le bonus d'élimination).</summary>
    public Army? LastHitBy { get; set; }
    /// <summary>Moment de l'élimination (secondes de partie), ou -1.</summary>
    public double EliminatedAt { get; set; } = -1;
    /// <summary>Classement final (1 = vainqueur), 0 tant que le clan est en jeu.</summary>
    public int Placement { get; set; }
    /// <summary>Bonus des sanctuaires tenus (vitesse, soin, dégâts).</summary>
    public ArmyBonus Bonus { get; set; } = ArmyBonus.None;

    // ---- Armée ----
    /// <summary>Les troupes du clan (les mortes sont retirées à la fin de chaque tick).</summary>
    public List<Unit> Units { get; } = new();
    /// <summary>Plus grand nombre de troupes atteint pendant la partie (statistiques).</summary>
    public int PeakUnits { get; set; }
    /// <summary>Nombre d'ennemis tués (troupes et chefs).</summary>
    public int Kills { get; set; }

    // ---- Économie et progression ----
    /// <summary>Ressources ◆ (ramassées dans les bulles et sur les ennemis, dépensées pour acheter des troupes).</summary>
    public int Resources { get; set; }
    /// <summary>Points de compétence ★ (font évoluer les ères).</summary>
    public int Points { get; set; }
    /// <summary>Ère du clan (0 = Préhistoire … 7 = Futur). Pendant une évolution, c'est encore l'ancienne ère.</summary>
    public int Era { get; set; }
    /// <summary>Évolution en cours vers l'ère suivante, ou null.</summary>
    public EraEvolution? Evolution { get; set; }

    // ---- Formation ----
    public FormationTemplate[] Formations { get; } = new FormationTemplate[FormationCount];
    public int FormationIndex { get; set; }
    public FormationTemplate CurrentFormation => Formations[FormationIndex];
    /// <summary>Direction de la formation : suit le chef en tournant doucement.</summary>
    public float FormationAngle;
    /// <summary>Vrai quand les places des troupes doivent être recalculées.</summary>
    public bool SlotsDirty { get; set; } = true;
    /// <summary>Agrandissement actuel du dessin (1 = taille du dessin ; augmente avec le nombre de troupes).</summary>
    public float SlotScale { get; set; } = 1;

    // ---- Tactiques ----
    /// <summary>Temps de recharge restant de chaque tactique (index = TacticKind).</summary>
    public float[] TacticCooldowns { get; } = new float[4];
    public Vec2 ChargeDirection { get; set; }
    /// <summary>Temps de retrait restant (le chef recule, les troupes cessent le combat).</summary>
    public float RetreatTime { get; set; }
    public Vec2 RetreatDirection { get; set; }
    /// <summary>Point visé par toute l'armée, et temps restant.</summary>
    public Vec2 AimPoint { get; set; }
    public float AimTime { get; set; }

    /// <summary>Nombre de troupes vivantes par type, dans l'ordre Guerrier, Distance, Tank.</summary>
    public int[] CountByKind()
    {
        var counts = new int[3];
        foreach (var u in Units) if (!u.Dead) counts[(int)u.Kind]++;
        return counts;
    }

    /// <summary>Nombre de troupes vivantes.</summary>
    public int LivingUnits => Units.Count(u => !u.Dead);
}

/// <summary>
/// Passage d'un clan à l'ère suivante : pendant 20 secondes, les troupes (et le chef) évoluent une par une
/// dans un ordre aléatoire. Pas d'avantage instantané : l'armée est un mélange des deux ères pendant ce temps.
/// </summary>
public sealed class EraEvolution
{
    public EraEvolution(int targetEra, List<Body> queue)
    {
        TargetEra = targetEra;
        Queue = queue;
        Total = queue.Count;
    }

    public int TargetEra { get; }
    /// <summary>Corps qui doivent encore évoluer, dans l'ordre (mélangé au départ).</summary>
    public List<Body> Queue { get; }
    public int Total { get; private set; }
    public float Elapsed { get; set; }
    /// <summary>Évolutions « en attente » (fraction accumulée pour répartir régulièrement dans le temps).</summary>
    public float Accumulator { get; set; }
    public int Done { get; set; }

    /// <summary>Ajoute un corps arrivé pendant l'évolution (troupe ramassée, soldat promu chef).</summary>
    public void Add(Body body)
    {
        if (Queue.Contains(body)) return;
        Queue.Add(body);
        Total++;
    }

    /// <summary>Avancement de 0 à 1.</summary>
    public float Progress(float duration) => MathF.Min(1, Elapsed / duration);
}

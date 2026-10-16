using ChronoClash.Core.Commands;
using ChronoClash.Core.Formations;

namespace ChronoClash.Core.Entities;

/// <summary>
/// Un clan : son chef, ses troupes, ses ressources ◆, ses points ★, son ère et ses formations.
/// Un clan est contrôlé soit par un humain, soit par un bot : dans les deux cas,
/// il reçoit les mêmes ordres (<see cref="PlayerCommand"/>).
/// </summary>
public sealed class Army
{
    /// <summary>Nombre de formations mémorisées par clan (touches 7, 8, 9, 0).</summary>
    public const int FormationCount = 4;

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
    public General General { get; }
    public bool Alive { get; set; } = true;

    // ---- Armée ----
    /// <summary>Les troupes vivantes du clan.</summary>
    public List<Unit> Units { get; } = new();
    /// <summary>Plus grand nombre de troupes atteint pendant la partie (statistiques).</summary>
    public int PeakUnits { get; set; }

    // ---- Économie et progression ----
    /// <summary>Ressources ◆ (ramassées dans les bulles, dépensées pour acheter des troupes).</summary>
    public int Resources { get; set; }
    /// <summary>Points de compétence ★ (font évoluer les ères à l'étape M3).</summary>
    public int Points { get; set; }
    /// <summary>Ère actuelle du clan (0 = Préhistoire … 7 = Futur).</summary>
    public int Era { get; set; }

    // ---- Formation ----
    /// <summary>Les 4 dessins de formation du clan.</summary>
    public FormationTemplate[] Formations { get; } = new FormationTemplate[FormationCount];
    /// <summary>Formation utilisée en ce moment.</summary>
    public int FormationIndex { get; set; }
    public FormationTemplate CurrentFormation => Formations[FormationIndex];
    /// <summary>Direction de la formation : suit le chef en tournant doucement.</summary>
    public float FormationAngle;
    /// <summary>Vrai quand les places des troupes doivent être recalculées (troupe ajoutée, formation changée…).</summary>
    public bool SlotsDirty { get; set; } = true;
    /// <summary>Agrandissement actuel du dessin (1 = taille du dessin ; augmente avec le nombre de troupes).</summary>
    public float SlotScale { get; set; } = 1;

    /// <summary>Nombre de troupes vivantes par type, dans l'ordre Guerrier, Distance, Tank.</summary>
    public int[] CountByKind()
    {
        var counts = new int[3];
        foreach (var u in Units) if (!u.Dead) counts[(int)u.Kind]++;
        return counts;
    }
}

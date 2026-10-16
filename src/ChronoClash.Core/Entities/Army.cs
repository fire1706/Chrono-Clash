namespace ChronoClash.Core.Entities;

/// <summary>
/// Un clan : son chef, et plus tard ses troupes, ressources, points, ère, tactiques…
/// Un clan est contrôlé soit par un humain, soit par un bot : dans les deux cas,
/// il reçoit les mêmes ordres (<see cref="Commands.PlayerCommand"/>).
/// </summary>
public sealed class Army
{
    public Army(int id, int playerId, string name, uint colorRgb, bool isBot, General general)
    {
        Id = id;
        PlayerId = playerId;
        Name = name;
        ColorRgb = colorRgb;
        IsBot = isBot;
        General = general;
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

    /// <summary>Direction de la formation : suit le chef en tournant doucement (utilisée à l'étape M2).</summary>
    public float FormationAngle;

    public bool Alive { get; set; } = true;
}

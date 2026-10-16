using ChronoClash.Core;
using ChronoClash.Core.Simulation;
using Godot;

namespace ChronoClash.Game.Bootstrap;

/// <summary>
/// Point d'entrée du jeu dans Godot (étape M0).
/// Il crée une partie du cœur et affiche le temps simulé : si le texte avance,
/// le projet Godot communique correctement avec ChronoClash.Core.
/// </summary>
public partial class GameRoot : Node2D
{
    private Match _match = null!;
    private Label _label = null!;

    public override void _Ready()
    {
        _match = new Match(new MatchSettings { Seed = GD.Randi() });

        _label = new Label { Position = new Vector2(40, 40) };
        _label.AddThemeFontSizeOverride("font_size", 28);
        AddChild(_label);
    }

    public override void _Process(double delta)
    {
        _match.Advance(delta);
        _label.Text =
            $"{CoreInfo.GameName} — {CoreInfo.Version}\n" +
            $"Ticks : {_match.World.Tick}   Temps de jeu : {_match.World.TimeSeconds:0.0} s\n" +
            "Si ces chiffres avancent, l'étape M0 fonctionne.";
    }
}

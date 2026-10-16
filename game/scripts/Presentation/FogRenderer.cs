using ChronoClash.Game.Net;
using Godot;

namespace ChronoClash.Game.Presentation;

/// <summary>
/// Brouillard de guerre : assombrit tout ce que ton clan ne voit pas. On fabrique une petite image
/// (une case = 80 px du monde, transparente si vue, sombre sinon) que l'on étire sur toute la carte ;
/// le lissage de Godot adoucit les bords. Mise à jour 4 fois par seconde, comme la vision du cœur.
/// Les ennemis cachés ne sont pas dessinés du tout (voir TroopRenderer, ArmyRenderer).
/// </summary>
public partial class FogRenderer : Node2D
{
    /// <summary>Opacité du brouillard (0 = invisible, 255 = noir).</summary>
    private const byte FogAlpha = 150;

    private GameSession _session = null!;
    private Image _image = null!;
    private ImageTexture _texture = null!;
    private byte[] _pixels = Array.Empty<byte>();
    private float _timer;

    public void Setup(GameSession session)
    {
        _session = session;
        int size = session.World.Vision[session.LocalArmy.Id].Size;
        _pixels = new byte[size * size * 4];
        _image = Image.CreateFromData(size, size, false, Image.Format.Rgba8, _pixels);
        _texture = ImageTexture.CreateFromImage(_image);
        TextureFilter = TextureFilterEnum.Linear;
        Visible = session.World.Settings.FogOfWar;
    }

    public override void _Process(double delta)
    {
        if (!_session.World.Settings.FogOfWar) return;
        // Clan éliminé : plus de brouillard (on regarde la fin de la partie).
        Visible = _session.LocalArmy.Alive;
        _timer -= (float)delta;
        if (_timer > 0) return;
        _timer = 0.25f;
        var vision = _session.World.Vision[_session.LocalArmy.Id];
        for (int i = 0; i < vision.Visible.Length; i++)
        {
            int k = i * 4;
            _pixels[k] = 12; _pixels[k + 1] = 14; _pixels[k + 2] = 24; // bleu nuit
            _pixels[k + 3] = vision.Visible[i] ? (byte)0 : FogAlpha;
        }
        _image.SetData(vision.Size, vision.Size, false, Image.Format.Rgba8, _pixels);
        _texture.Update(_image);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var vision = _session.World.Vision[_session.LocalArmy.Id];
        float worldSize = vision.Size * vision.Cell;
        DrawTextureRect(_texture, new Rect2(0, 0, worldSize, worldSize), false);
    }
}

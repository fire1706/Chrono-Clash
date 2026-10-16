using ChronoClash.Core.Events;
using ChronoClash.Core.Mathematics;
using ChronoClash.Game.Localization;
using ChronoClash.Game.Net;
using ChronoClash.Game.Presentation;
using ChronoClash.Game.Settings;
using Godot;

namespace ChronoClash.Game.Audio;

/// <summary>
/// Sons, musique et annonceur.
/// - Effets : joués à leur position dans le monde (plus faibles quand ils sont loin de la caméra), avec une limite
///   par son pour ne pas saturer pendant les grosses batailles. Un fichier game/assets/audio/&lt;nom&gt;.ogg (ou .wav)
///   remplace automatiquement le son synthétisé du même nom (hit, boom, coin, era…).
/// - Musique : une boucle par ère ; elle change en fondu quand ton clan change d'ère.
/// - Annonceur : la synthèse vocale du système lit les grandes annonces (désactivable dans Réglages).
/// Les volumes suivent les Réglages (bus audio « Music » et « Sfx »).
/// </summary>
public partial class AudioManager : Node2D
{
    /// <summary>Distance (pixels du monde) au-delà de laquelle un son n'est plus entendu.</summary>
    private const float HearingDistance = 2200;
    /// <summary>Nombre de sons simultanés au maximum.</summary>
    private const int Voices = 24;

    private GameSession _session = null!;
    private readonly Dictionary<string, AudioStream> _effects = new();
    private readonly Dictionary<string, double> _lastPlayed = new();
    private readonly List<AudioStreamPlayer2D> _pool = new();
    private int _next;
    private AudioStreamPlayer _musicA = null!, _musicB = null!;
    private readonly AudioStream?[] _music = new AudioStream?[8];
    private int _musicEra = -1;
    private float _fade = 1;

    /// <summary>Intervalle minimal entre deux lectures du même son (secondes).</summary>
    private static readonly Dictionary<string, double> MinInterval = new()
    {
        ["hit"] = 0.05, ["shoot_bow"] = 0.05, ["shoot_gun"] = 0.04, ["shoot_plasma"] = 0.06, ["boom"] = 0.08, ["death"] = 0.05, ["pop"] = 0.03,
    };

    public void Setup(GameSession session)
    {
        _session = session;
        EnsureBuses();
        foreach (var (name, wav) in SoundSynth.Effects()) _effects[name] = LoadOverride(name) ?? wav;
        for (int i = 0; i < Voices; i++)
        {
            var p = new AudioStreamPlayer2D { Bus = "Sfx", MaxDistance = HearingDistance, Attenuation = 1.5f };
            AddChild(p);
            _pool.Add(p);
        }
        _musicA = new AudioStreamPlayer { Bus = "Music" };
        _musicB = new AudioStreamPlayer { Bus = "Music" };
        AddChild(_musicA);
        AddChild(_musicB);
        _session.EventRaised += OnEvent;
        GameSettings.Changed += ApplyVolumes;
        ApplyVolumes();
    }

    public override void _ExitTree()
    {
        _session.EventRaised -= OnEvent;
        GameSettings.Changed -= ApplyVolumes;
        if (DisplayServer.TtsIsSpeaking()) DisplayServer.TtsStop();
    }

    /// <summary>Un fichier du graphiste ou du musicien remplace le son synthétisé, s'il existe.</summary>
    private static AudioStream? LoadOverride(string name)
    {
        foreach (var ext in new[] { "ogg", "wav", "mp3" })
        {
            string path = $"res://assets/audio/{name}.{ext}";
            if (ResourceLoader.Exists(path)) return GD.Load<AudioStream>(path);
        }
        return null;
    }

    /// <summary>Crée les bus « Music » et « Sfx » s'ils n'existent pas (réglables séparément).</summary>
    private static void EnsureBuses()
    {
        foreach (var bus in new[] { "Music", "Sfx" })
        {
            if (AudioServer.GetBusIndex(bus) >= 0) continue;
            AudioServer.AddBus();
            int index = AudioServer.BusCount - 1;
            AudioServer.SetBusName(index, bus);
            AudioServer.SetBusSend(index, "Master");
        }
    }

    private static void ApplyVolumes()
    {
        var s = GameSettings.Current;
        SetBus("Music", s.MusicVolume);
        SetBus("Sfx", s.SfxVolume);
    }

    private static void SetBus(string bus, float volume)
    {
        int index = AudioServer.GetBusIndex(bus);
        if (index < 0) return;
        AudioServer.SetBusMute(index, volume <= 0.001f);
        AudioServer.SetBusVolumeDb(index, Mathf.LinearToDb(Mathf.Max(0.001f, volume)));
    }

    // ================= Effets =================

    /// <summary>Joue un son à une position du monde (null = au centre de l'écran, toujours audible).</summary>
    public void Play(string name, Vector2? at = null, float volumeDb = 0, float pitch = 1)
    {
        if (!_effects.TryGetValue(name, out var stream)) return;
        double now = Time.GetTicksMsec() / 1000.0;
        if (MinInterval.TryGetValue(name, out var gap) && _lastPlayed.TryGetValue(name, out var last) && now - last < gap) return;
        _lastPlayed[name] = now;
        var player = _pool[_next];
        _next = (_next + 1) % _pool.Count;
        player.Stream = stream;
        player.VolumeDb = volumeDb;
        player.PitchScale = pitch * (0.94f + GD.Randf() * 0.12f); // petite variation : moins répétitif
        player.GlobalPosition = at ?? GetViewport().GetCamera2D()?.GetScreenCenterPosition() ?? Vector2.Zero;
        player.Play();
    }

    private bool Visible(float x, float y) => _session.CanSee(new Vec2(x, y));

    private void OnEvent(GameEvent e)
    {
        var local = _session.LocalArmy;
        switch (e)
        {
            case MeleeHitEvent m when Visible(m.X, m.Y): Play("hit", new Vector2(m.X, m.Y), -6); break;
            case ProjectileImpactEvent p when p.Splash > 0 && Visible(p.X, p.Y): Play(p.Kind == "plasma" ? "shoot_plasma" : "boom", new Vector2(p.X, p.Y), -3); break;
            case BubbleCollectedEvent b when b.ArmyId == local.Id: Play(b.Kind == Core.Entities.BubbleKind.Star ? "star" : "pop", new Vector2(b.X, b.Y)); break;
            case BodyDiedEvent d when d.IsChief && Visible(d.X, d.Y): Play("chief_down", new Vector2(d.X, d.Y)); break;
            case BodyDiedEvent d when Visible(d.X, d.Y): Play("death", new Vector2(d.X, d.Y), -8); break;
            case TroopRecruitedEvent r when r.ArmyId == local.Id: Play("coin"); break;
            case BuyFailedEvent f when f.PlayerId == _session.LocalPlayerId: Play("deny"); break;
            case TacticFailedEvent f when f.PlayerId == _session.LocalPlayerId: Play("deny"); break;
            case TacticUsedEvent t when t.ArmyId == local.Id: Play(t.Tactic == Core.Commands.TacticKind.WarCry ? "cry" : "tactic"); break;
            case EraStartedEvent es when es.ArmyId == local.Id: Play("era"); break;
            case ZoneShrinkingEvent: Play("zone"); break;
            case BossSpawnedEvent: Play("zone", null, -4, 0.7f); break;
            case ArmyEliminatedEvent: Play("elim", null, -4); break;
            case MeteorImpactEvent mi when Visible(mi.X, mi.Y): Play("boom", new Vector2(mi.X, mi.Y)); break;
            case CrateCollectedEvent or PortalUsedEvent or ShrineCapturedEvent: Play("star", null, -4); break;
            case MatchEndedEvent: Play("era"); break;
        }
    }

    /// <summary>Petits bruits de tir : appelé par la couche d'effets quand un projectile part (pour les sons de tir).</summary>
    public void PlayShot(string projectile, Vector2 at)
    {
        string sound = projectile switch { "stone" or "arrow" => "shoot_bow", "plasma" => "shoot_plasma", "rock" => "shoot_bow", _ => "shoot_gun" };
        Play(sound, at, -10);
    }

    /// <summary>Petit clic pour les boutons de l'interface.</summary>
    public void Click() => Play("ui", null, -6);

    // ================= Annonceur =================

    /// <summary>L'annonceur lit un texte (si la voix est activée et disponible sur l'ordinateur).</summary>
    public void Speak(string text)
    {
        var s = GameSettings.Current;
        if (!s.VoiceEnabled || string.IsNullOrWhiteSpace(text)) return;
        var voices = DisplayServer.TtsGetVoicesForLanguage(Loc.Language);
        if (voices.Length == 0) return;
        DisplayServer.TtsSpeak(text.Replace("\n", ". "), voices[0], (int)(s.VoiceVolume * 100), 1, 1.05f, 0, interrupt: true);
    }

    // ================= Musique =================

    public override void _Process(double delta)
    {
        if (!_session.InMatch) return;
        // Bruits de tir : les projectiles qui viennent de partir (et que l'on voit).
        float fresh = _session.Mode == SessionMode.Client ? 0.1f : 0.05f;
        foreach (var p in _session.World.Projectiles)
            if (p.Elapsed <= fresh && p.Kind != "boss" && _session.CanSee(p.From)) PlayShot(p.Kind, p.From.ToGodot());

        int era = Math.Clamp(_session.LocalArmy.Era, 0, 7);
        if (era != _musicEra)
        {
            // Nouvelle ère : la nouvelle musique arrive en fondu pendant que l'ancienne s'efface.
            _musicEra = era;
            _music[era] ??= LoadOverride($"music_{era}") ?? SoundSynth.Music(era);
            (_musicA, _musicB) = (_musicB, _musicA);
            _musicA.Stream = _music[era];
            _musicA.VolumeDb = -40;
            _musicA.Play();
            _fade = 0;
        }
        if (_fade < 1)
        {
            _fade = Mathf.Min(1, _fade + (float)delta / 2.5f);
            _musicA.VolumeDb = Mathf.LinearToDb(Mathf.Max(0.001f, _fade));
            _musicB.VolumeDb = Mathf.LinearToDb(Mathf.Max(0.001f, 1 - _fade));
            if (_fade >= 1) _musicB.Stop();
        }
    }
}

using Godot;

namespace Expedition.Game;

/// <summary>
/// The Garza's own sounds at sea, from recorded CC0 samples (Kenney, <c>Assets/CREDITS.md</c>):
/// <list type="bullet">
/// <item>the ship's bell striking the half hours of the watch: one bell at half past, two at the hour,
/// up to eight at the change of watch, struck in pairs;</item>
/// <item>a line parting aloft: the crack of it going, then its end flogging;</item>
/// <item>the topsail yard springing: the split of the timber and the rest of it working loose;</item>
/// <item>the sea and the wind under way, by their strength (the port's surf and wind recordings, looped):
/// water along the hull, the wind rising to a roar in a storm, muffled in fog;</item>
/// <item>her timbers working in a seaway, more as the sea gets up; the pumps' clank while they are manned;</item>
/// <item>in fog, the bell rung at intervals, as a ship does to be heard.</item>
/// </list>
/// Purely presentation: Main tells it what happened; it never reads or changes the simulation.
/// </summary>
public partial class ShipSound : Node
{
    /// <summary>Which strikes of the bell to sound.</summary>
    public enum Bells
    {
        /// <summary>None: time is going too fast.</summary>
        None,

        /// <summary>Only the change of watch, every four hours.</summary>
        Watches,

        /// <summary>Every half hour.</summary>
        All,
    }

    private readonly RandomNumberGenerator _random = new() { Seed = 1492 };
    private readonly Dictionary<string, AudioStream[]> _samples = [];
    private readonly List<AudioStreamPlayer> _voices = [];
    private readonly List<(double At, string Kind, float Volume)> _queue = [];
    private double _time;
    private int _lastHalfHour = -1;
    private AudioStreamPlayer? _rain;
    private float _rainLevel;

    public override void _Ready()
    {
        AudioStream[] Load(string folder, params string[] prefixes) => DirAccess.GetFilesAt($"res://Assets/Audio/{folder}")
            .Select(f => f.Replace(".import", ""))
            .Where(f => f.EndsWith(".ogg", StringComparison.Ordinal) && prefixes.Any(p => f.StartsWith(p, StringComparison.Ordinal)))
            .Distinct()
            .Select(f => GD.Load<AudioStream>($"res://Assets/Audio/{folder}/{f}"))
            .ToArray();
        _samples["bell"] = Load("impact", "impactBell_heavy");
        _samples["snap"] = Load("rpg", "clothBelt");
        _samples["flog"] = Load("rpg", "cloth");
        _samples["split"] = Load("impact", "impactWood_heavy");
        _samples["plank"] = Load("impact", "impactPlank_medium");
        _samples["creak"] = Load("rpg", "creak");
        _samples["pump"] = Load("impact", "impactMetal_heavy");
        _sea = Loop("res://Assets/Audio/ambience/surf.ogg");
        _wind = Loop("res://Assets/Audio/ambience/wind.ogg");
        for (var i = 0; i < 6; i++)
        {
            var voice = new AudioStreamPlayer();
            AddChild(voice);
            _voices.Add(voice);
        }

        // Rain on the deck and the sea: the port's recording (Freesound, CC0), looped.
        const string rain = "res://Assets/Audio/ambience/rain.ogg";
        if (ResourceLoader.Exists(rain) && GD.Load<AudioStream>(rain) is AudioStreamOggVorbis recording)
        {
            recording.Loop = true;
            _rain = new AudioStreamPlayer { Stream = recording, VolumeDb = -80 };
            AddChild(_rain);
            _rain.Play();
        }
    }

    private AudioStreamPlayer? _sea;
    private AudioStreamPlayer? _wind;
    private float _seaLevel;
    private float _windLevel;
    private double _nextCreak;
    private double _nextPump;
    private double _nextFogBell;

    private AudioStreamPlayer? Loop(string path)
    {
        if (!ResourceLoader.Exists(path) || GD.Load<AudioStream>(path) is not AudioStreamOggVorbis recording)
        {
            return null;
        }

        recording.Loop = true;
        var player = new AudioStreamPlayer { Stream = recording, VolumeDb = -80 };
        AddChild(player);
        player.Play();
        return player;
    }

    /// <summary>
    /// The sea and wind under way, each frame: <paramref name="seaState"/> 0..3, <paramref name="wind"/> 0..2
    /// (leagues an hour the wind gives), <paramref name="fog"/> 0..1, and whether anyone is at the pumps.
    /// In port, call with zero sea and wind: it falls silent.
    /// </summary>
    public void Sea(float seaState, float wind, float fog, bool pumping, float delta)
    {
        var muffle = 1 - 0.45f * fog;
        _seaLevel = Mathf.MoveToward(_seaLevel, Mathf.Clamp(0.18f + 0.3f * seaState, 0, 1) * muffle * (seaState > 0 ? 1 : 0), delta * 0.2f);
        _windLevel = Mathf.MoveToward(_windLevel, Mathf.Clamp(0.08f + 0.22f * wind + 0.18f * Mathf.Max(0, seaState - 1.5f), 0, 1) * muffle * (wind > 0 ? 1 : 0), delta * 0.2f);
        if (_sea is not null)
        {
            _sea.VolumeDb = _seaLevel <= 0.01f ? -80 : Mathf.LinearToDb(_seaLevel);
            _sea.PitchScale = 0.85f + 0.05f * seaState;
        }

        if (_wind is not null)
        {
            _wind.VolumeDb = _windLevel <= 0.01f ? -80 : Mathf.LinearToDb(_windLevel);
            _wind.PitchScale = 0.9f + 0.12f * Mathf.Clamp(seaState - 1, 0, 2);
        }

        if (seaState <= 0)
        {
            return;
        }

        // Her timbers working: now and then in a calm, often in a seaway.
        if (_time >= _nextCreak)
        {
            _nextCreak = _time + _random.RandfRange(4f, 12f) / Mathf.Max(0.4f, seaState);
            Queue(0, seaState > 2.2f && _random.Randf() < 0.3f ? "plank" : "creak", Mathf.Clamp(0.25f + 0.2f * seaState, 0, 0.9f));
        }

        if (pumping && _time >= _nextPump)
        {
            _nextPump = _time + _random.RandfRange(1.4f, 1.9f);
            Queue(0, "pump", 0.18f);
        }

        if (fog >= 0.5f && _time >= _nextFogBell)
        {
            _nextFogBell = _time + 20;
            Queue(0, "bell", 0.35f);
            Queue(0.45, "bell", 0.35f);
        }
    }

    /// <summary>Rain at sea, 0 none .. 1 heavy: it hisses in and out over a few seconds.</summary>
    public void Rain(float level, float delta)
    {
        _rainLevel = Mathf.MoveToward(_rainLevel, level, delta * 0.3f);
        if (_rain is not null)
        {
            _rain.VolumeDb = _rainLevel <= 0.01f ? -80 : Mathf.LinearToDb(_rainLevel * 1.6f);
        }
    }

    /// <summary>
    /// The clock at sea, in game hours since midnight; strikes the bell on each half hour. At higher game
    /// speeds the half hours go by too fast to hear them all: <paramref name="ring"/> says which sound,
    /// every half hour, only the change of watch (eight bells), or none.
    /// </summary>
    public void Clock(double hour, Bells ring)
    {
        var halfHour = (int)Math.Floor(hour * 2);
        if (halfHour == _lastHalfHour)
        {
            return;
        }

        var first = _lastHalfHour < 0;
        _lastHalfHour = halfHour;
        // Watches of four hours from midnight: half past is one bell, the change of watch eight.
        var bells = ((halfHour - 1) % 8 + 8) % 8 + 1;
        if (first || ring == Bells.None || (ring == Bells.Watches && bells != 8))
        {
            return;
        }

        for (var i = 0; i < bells; i++)
        {
            Queue(0.15 + i / 2 * 1.3 + i % 2 * 0.42, "bell", 0.55f);
        }
    }

    /// <summary>A line has parted aloft.</summary>
    public void LineParted()
    {
        Queue(0, "snap", 0.9f);
        Queue(0.22, "flog", 0.6f);
        Queue(0.5, "flog", 0.45f);
    }

    /// <summary>The topsail yard has been sprung.</summary>
    public void YardSprung()
    {
        Queue(0, "split", 1f);
        Queue(0.08, "plank", 0.8f);
        Queue(0.45, "creak", 0.7f);
        Queue(0.9, "plank", 0.5f);
    }

    /// <summary>Forgets the half hour struck last, so the next one is not rung on arrival at sea.</summary>
    public void Reset() => _lastHalfHour = -1;

    public override void _Process(double delta)
    {
        _time += delta;
        for (var i = _queue.Count - 1; i >= 0; i--)
        {
            if (_queue[i].At <= _time)
            {
                Play(_queue[i].Kind, _queue[i].Volume);
                _queue.RemoveAt(i);
            }
        }
    }

    private void Queue(double after, string kind, float volume) => _queue.Add((_time + after, kind, volume));

    private void Play(string kind, float volume)
    {
        if (!_samples.TryGetValue(kind, out var takes) || takes.Length == 0 || _voices.FirstOrDefault(v => !v.Playing) is not { } voice)
        {
            return;
        }

        voice.Stream = takes[_random.RandiRange(0, takes.Length - 1)];
        voice.VolumeDb = Mathf.LinearToDb(volume);
        voice.PitchScale = kind == "bell" ? 1f : _random.RandfRange(0.9f, 1.08f);
        voice.Play();
    }
}

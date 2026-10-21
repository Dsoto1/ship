using Godot;

namespace Expedition.Game;

/// <summary>
/// The Garza's own sounds at sea, from recorded CC0 samples (Kenney, <c>Assets/CREDITS.md</c>):
/// <list type="bullet">
/// <item>the ship's bell striking the half hours of the watch: one bell at half past, two at the hour,
/// up to eight at the change of watch, struck in pairs;</item>
/// <item>a line parting aloft: the crack of it going, then its end flogging;</item>
/// <item>the topsail yard springing: the split of the timber and the rest of it working loose.</item>
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
        for (var i = 0; i < 6; i++)
        {
            var voice = new AudioStreamPlayer();
            AddChild(voice);
            _voices.Add(voice);
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

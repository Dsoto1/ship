using Godot;

namespace Expedition.Game;

/// <summary>
/// The camp ashore, heard (R7, SETTLEMENT_EXPERIENCE §40), from the recorded CC0 samples already in the game
/// (Kenney and Freesound, <c>Assets/CREDITS.md</c>):
/// <list type="bullet">
/// <item>the surf on the beach, louder in a storm, and the wind in the trees;</item>
/// <item>rain on the sand and the roofs;</item>
/// <item>the work going on, each kind at its own pace and more of it the more hands are at it: axes at the
/// forest's edge and on cleared ground, mallets and pins on posts, frames and staging, the saw at the pit,
/// thatch being cut and laid, stone being shifted, spades in the ground, a tool being mended.</item>
/// </list>
/// At high game speeds the work falls quiet: it would be a din. Purely presentation: Main tells it what is
/// going on; it never reads or changes the simulation.
/// </summary>
public partial class CampSound : Node
{
    // How each kind of work sounds: its samples, the seconds between strikes for one hand, the volume and pitch.
    private static readonly IReadOnlyDictionary<string, (string Kind, float Every, float Volume, float Pitch)> Work =
        new Dictionary<string, (string, float, float, float)>
        {
            ["task.cut_poles"] = ("chop", 1.3f, 0.5f, 1f),
            ["task.clear_ground"] = ("chop", 1.8f, 0.4f, 0.9f),
            ["task.fence_garden"] = ("chop", 1.6f, 0.4f, 1.05f),
            ["task.salvage_timbers"] = ("chop", 2.2f, 0.25f, 0.85f),
            ["task.line_well"] = ("chop", 2f, 0.3f, 1.1f),
            ["task.set_posts"] = ("knock", 1.5f, 0.45f, 0.85f),
            ["task.raise_frame"] = ("knock", 1.2f, 0.45f, 1f),
            ["task.build_staging"] = ("knock", 1.1f, 0.45f, 1.05f),
            ["task.fit_out"] = ("knock", 1.4f, 0.4f, 1.15f),
            ["task.mend_tools"] = ("ring", 2.4f, 0.3f, 1.2f),
            ["task.pit_saw_planks"] = ("saw", 0.55f, 0.3f, 0.7f),
            ["task.cut_thatch"] = ("slash", 1.1f, 0.35f, 1f),
            ["task.thatch_roof"] = ("rustle", 1.4f, 0.4f, 1f),
            ["task.repair_building"] = ("rustle", 1.8f, 0.35f, 1f),
            ["task.gather_stone"] = ("stone", 2.6f, 0.4f, 0.75f),
            ["task.lay_stones"] = ("stone", 1.9f, 0.45f, 0.8f),
            ["task.dig_well"] = ("dig", 1.6f, 0.35f, 0.7f),
            ["task.dig_latrine"] = ("dig", 1.7f, 0.35f, 0.75f),
            ["task.dig_beds"] = ("dig", 1.8f, 0.3f, 0.8f),
        };

    private readonly RandomNumberGenerator _random = new() { Seed = 1500 };
    private readonly Dictionary<string, AudioStream[]> _samples = [];
    private readonly List<AudioStreamPlayer> _voices = [];
    private readonly Dictionary<string, double> _next = [];
    private AudioStreamPlayer? _surf;
    private AudioStreamPlayer? _wind;
    private AudioStreamPlayer? _rain;
    private float _surfLevel;
    private float _windLevel;
    private float _rainLevel;
    private double _time;

    public override void _Ready()
    {
        AudioStream[] Load(string folder, params string[] prefixes) => DirAccess.GetFilesAt($"res://Assets/Audio/{folder}")
            .Select(f => f.Replace(".import", ""))
            .Where(f => f.EndsWith(".ogg", StringComparison.Ordinal) && prefixes.Any(p => f.StartsWith(p, StringComparison.Ordinal)))
            .Distinct()
            .Select(f => GD.Load<AudioStream>($"res://Assets/Audio/{folder}/{f}"))
            .ToArray();
        _samples["chop"] = Load("rpg", "chop");
        _samples["knock"] = [.. Load("impact", "impactWood_medium"), .. Load("impact", "impactPlank_medium")];
        _samples["ring"] = Load("impact", "impactPlate_light");
        _samples["saw"] = Load("rpg", "knifeSlice");
        _samples["slash"] = Load("rpg", "knifeSlice");
        _samples["rustle"] = Load("rpg", "cloth");
        _samples["stone"] = Load("impact", "impactWood_heavy");
        _samples["dig"] = Load("rpg", "clothBelt");
        _surf = Loop("res://Assets/Audio/ambience/surf.ogg");
        _wind = Loop("res://Assets/Audio/ambience/wind.ogg");
        _rain = Loop("res://Assets/Audio/ambience/rain.ogg");
        for (var i = 0; i < 8; i++)
        {
            var voice = new AudioStreamPlayer();
            AddChild(voice);
            _voices.Add(voice);
        }
    }

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
    /// Each frame: whether the camp is being looked at at all, the weather (rain, a storm blowing), the work
    /// going on (task IDs, one per hand at it), and the game's speed in ticks a second.
    /// </summary>
    public void Update(bool ashore, bool raining, bool storm, IEnumerable<string> working, float pace, float delta)
    {
        _time += delta;
        _surfLevel = Mathf.MoveToward(_surfLevel, ashore ? (storm ? 0.75f : 0.3f) : 0, delta * 0.25f);
        _windLevel = Mathf.MoveToward(_windLevel, ashore ? (storm ? 0.7f : raining ? 0.18f : 0.08f) : 0, delta * 0.25f);
        _rainLevel = Mathf.MoveToward(_rainLevel, ashore && raining ? (storm ? 1f : 0.55f) : 0, delta * 0.3f);
        Level(_surf, _surfLevel);
        Level(_wind, _windLevel);
        Level(_rain, _rainLevel * 1.4f);
        if (!ashore || pace <= 0 || pace > 10)
        {
            return;
        }

        foreach (var (task, hands) in working.GroupBy(t => t).Select(g => (g.Key, g.Count())))
        {
            if (!Work.TryGetValue(task, out var sound))
            {
                continue;
            }

            // Strikes at a working pace in real time, whatever the game's speed: more hands, more of them.
            var every = sound.Every / Mathf.Sqrt(hands);
            if (!_next.TryGetValue(task, out var next) || next > _time + every * 3)
            {
                _next[task] = _time + _random.RandfRange(0, every);
                continue;
            }

            if (_time >= next)
            {
                _next[task] = _time + every * _random.RandfRange(0.75f, 1.3f);
                Play(sound.Kind, sound.Volume * (raining ? 0.7f : 1), sound.Pitch * _random.RandfRange(0.92f, 1.08f));
            }
        }
    }

    private static void Level(AudioStreamPlayer? player, float level)
    {
        if (player is not null)
        {
            player.VolumeDb = level <= 0.01f ? -80 : Mathf.LinearToDb(level);
        }
    }

    private void Play(string kind, float volume, float pitch)
    {
        if (!_samples.TryGetValue(kind, out var samples) || samples.Length == 0 || _voices.FirstOrDefault(v => !v.Playing) is not { } voice)
        {
            return;
        }

        voice.Stream = samples[_random.RandiRange(0, samples.Length - 1)];
        voice.VolumeDb = Mathf.LinearToDb(volume);
        voice.PitchScale = pitch;
        voice.Play();
    }
}

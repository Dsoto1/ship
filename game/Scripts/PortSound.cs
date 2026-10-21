using Expedition.Simulation.World;
using Godot;

namespace Expedition.Game;

/// <summary>
/// The port's sound zones (PORT_VISUAL_PASS §3.14, PORT_EXPERIENCE §15). Each zone's loudness follows how
/// close the camera's focus is to it and how much is happening there. The beds (surf, crowd murmur, wind,
/// rain) are looped CC0 recordings from Freesound where fetched, else generated noise; the sounds of work are
/// recorded CC0 samples (Kenney, <c>Assets/CREDITS.md</c>), played when and where the work is done:
/// <list type="bullet">
/// <item>waterfront: surf against the quay, and the Garza's timbers creaking at her lines;</item>
/// <item>market: crowd murmur rising with the people at the stalls, coins, the fishwife's knife;</item>
/// <item>workshop: mallets, planks and the adze for each carpenter and cooper at work, the anvil
/// while the smith works;</item>
/// <item>tavern: voices and cups set down, busier at night;</item>
/// <item>the chapel bell on the hour, by day;</item>
/// <item>street: a quiet bed of wind.</item>
/// </list>
/// Rain adds its own hiss. Purely presentation: it reads the scene, never the other way.
/// </summary>
public partial class PortSound : Node
{
    private const int MixRate = 22050;

    private readonly Dictionary<string, Layer> _layers = [];
    private readonly RandomNumberGenerator _random = new() { Seed = 1519 };
    private Rect2 _market;
    private Rect2 _workshop;
    private Rect2 _tavern;
    private float _waterline;
    private Rect2 _smithy;
    private Rect2 _fish;
    private Vector2 _chapel;
    private readonly Dictionary<string, AudioStream[]> _samples = [];
    private readonly List<AudioStreamPlayer> _voices = [];
    private readonly Dictionary<string, float> _next = [];
    private int _lastHour = -1;
    private int _bellStrikes;
    private float _bellNext;

    /// <summary>
    /// Activity the scene reports each frame: people at the stalls, carpenters and coopers at work,
    /// smiths at the anvil, fish being sold.
    /// </summary>
    public (int Market, int Workers, int Smiths, int Fish) Activity { get; set; }

    /// <summary>0 still .. 1 a strong breeze: the wind bed rises with it.</summary>
    public float Wind { get; set; }

    /// <summary>True while the captain is inside the Three Anchors: its room loud and close, the port muffled.</summary>
    public bool Inside { get; set; }

    /// <summary>True while a game of dice goes round the captain's table: the bones are thrown now and then.</summary>
    public bool Dicing { get; set; }

    private AudioStreamPlayer _singer = null!;
    private float _singing;

    /// <summary>Someone starts a song: the recording for its voice (Audio/tavern/song_*.ogg), once.</summary>
    public void Sing(string voice)
    {
        var path = $"res://Assets/Audio/tavern/song_{voice}.ogg";
        if (_singer is null || !ResourceLoader.Exists(path))
        {
            return;
        }

        _singer.Stream = GD.Load<AudioStream>(path);
        _singing = 1f;
        _singer.VolumeDb = 0;
        _singer.Play();
    }

    /// <summary>The song is over: it fades out if it is still going.</summary>
    public void StopSinging() => _singing = 0f;

    private sealed class Layer
    {
        public AudioStreamPlayer Player = null!;
        public AudioStreamGeneratorPlayback? Playback;
        public float Volume;
        public float Gain = 1f;
        public float Target;
        public float Filter;
        public float Filter2;
        public float Phase;
    }

    /// <summary>Lays out the zones from the place's features.</summary>
    public void Build(LocationDefinition location)
    {
        Rect2 Of(Func<LocationFeature, bool> pick)
        {
            var parts = location.Features.Where(pick).ToList();
            if (parts.Count == 0)
            {
                return new Rect2(-1000, -1000, 1, 1);
            }

            var rect = new Rect2(parts[0].X0, parts[0].Y0, 0, 0);
            foreach (var f in parts)
            {
                rect = rect.Expand(new Vector2(f.X0 - 2, f.Y0 - 2)).Expand(new Vector2(f.X1 + 2, f.Y1 + 3));
            }

            return rect;
        }

        _market = Of(f => f.Kind is "stall" or "well");
        _workshop = Of(f => f.Style is "cooperage" or "smithy" or "sailloft" || f.Kind is "hull_on_stocks" or "timber" or "sawpit" or "ropewalk" or "planks" or "sawhorse");
        _tavern = Of(f => f.Style == "tavern");
        _waterline = location.Spots.Where(s => s.Kind == "BoardShip").Select(s => (float)s.Y).DefaultIfEmpty(34).Max();
        _smithy = Of(f => f.Style == "smithy" || f.Kind == "anvil");
        _fish = Of(f => f.Kind == "stall" && f.Style == "fish");
        var chapel = Of(f => f.Style == "chapel");
        _chapel = chapel.GetCenter();

        // Recorded sounds of work (CC0, Kenney): every take of each kind, picked at random each time.
        AudioStream[] Load(string folder, string prefix) => DirAccess.GetFilesAt($"res://Assets/Audio/{folder}")
            .Where(f => f.StartsWith(prefix, StringComparison.Ordinal) && (f.EndsWith(".ogg", StringComparison.Ordinal) || f.EndsWith(".ogg.import", StringComparison.Ordinal)))
            .Select(f => f.Replace(".import", ""))
            .Distinct()
            .Select(f => GD.Load<AudioStream>($"res://Assets/Audio/{folder}/{f}"))
            .ToArray();
        _samples["mallet"] = [.. Load("impact", "impactWood_heavy"), .. Load("impact", "impactWood_medium"), .. Load("impact", "impactPlank_medium"), .. Load("rpg", "chop")];
        _samples["anvil"] = Load("impact", "impactMetal_heavy");
        _samples["cup"] = Load("impact", "impactPlate_light");
        _samples["coins"] = Load("rpg", "handleCoins");
        _samples["knife"] = Load("rpg", "knifeSlice");
        _samples["creak"] = Load("rpg", "creak");
        _samples["bell"] = Load("impact", "impactBell_heavy");
        _samples["dice"] = Load("tavern", "dice_roll");
        _samples["dicecup"] = Load("tavern", "dice_cup");
        _singer = new AudioStreamPlayer { Name = "Singer" };
        AddChild(_singer);
        for (var i = 0; i < 10; i++)
        {
            var voice = new AudioStreamPlayer();
            AddChild(voice);
            _voices.Add(voice);
        }

        // The beds: a looped CC0 recording (Freesound) where one has been fetched, else generated noise.
        foreach (var (name, file) in new[] { ("surf", "surf"), ("market", "market"), ("tavern", "tavern"), ("street", "wind"), ("rain", "rain") })
        {
            var path = $"res://Assets/Audio/ambience/{file}.ogg";
            if (ResourceLoader.Exists(path) && GD.Load<AudioStream>(path) is AudioStreamOggVorbis recording)
            {
                recording.Loop = true;
                var looped = new AudioStreamPlayer { Stream = recording, VolumeDb = -80 };
                AddChild(looped);
                looped.Play((float)(recording.GetLength() * _random.Randf()));
                // Recordings are levelled to -24 LUFS: lift them to sit with the work sounds (the tavern less, it is loud at night).
                _layers[name] = new Layer { Player = looped, Gain = name == "tavern" ? 1.1f : 2f };
                continue;
            }

            var player = new AudioStreamPlayer { Stream = new AudioStreamGenerator { MixRate = MixRate, BufferLength = 0.25f }, VolumeDb = -80 };
            AddChild(player);
            player.Play();
            _layers[name] = new Layer { Player = player, Playback = player.GetStreamPlayback() as AudioStreamGeneratorPlayback };
        }
    }

    /// <summary>
    /// Sets each layer's loudness from where the camera is looking, the hour, and the rain, then feeds the
    /// generators. <paramref name="listener"/> is in map cells (x, row).
    /// </summary>
    /// <param name="pace">Game minutes per real second: no work is heard while paused, no bell at high speed.</param>
    public void Update(Vector2 listener, float hour, float rain, float delta, float pace = 1f)
    {
        if (_layers.Count == 0)
        {
            return;
        }

        float Near(Rect2 zone, float reach) => Mathf.Clamp(1f - Distance(zone, listener) / reach, 0, 1);
        var day = hour is > 6.5f and < 20.5f ? 1f : 0.25f;
        var night = hour is > 19f or < 2f ? 1f : 0.35f;
        var (market, workers, smiths, fish) = Activity;

        _layers["surf"].Target = 0.35f + 0.45f * Mathf.Clamp(1f - Mathf.Abs(listener.Y - _waterline) / 18f, 0, 1);
        _layers["market"].Target = Near(_market, 18) * Mathf.Clamp(market / 6f, 0.15f, 1f) * day * 0.6f;
        _layers["tavern"].Target = Near(_tavern, 14) * (0.25f + 0.6f * night);
        _layers["street"].Target = 0.12f + Wind * 0.35f;
        _layers["rain"].Target = rain * 0.55f;
        if (Inside)
        {
            // Through the walls the port is a murmur; the room is all round.
            foreach (var name in new[] { "surf", "market", "street", "rain" })
            {
                _layers[name].Target *= 0.3f;
            }

            _layers["tavern"].Target = 0.55f + 0.4f * night;
        }

        // A song: full in the room, faint from outside; the room's talk drops while it is sung.
        if (_singer is { Playing: true })
        {
            var level = _singing * (Inside ? 1f : 0.2f);
            _singer.VolumeDb = Mathf.MoveToward(_singer.VolumeDb, level < 0.01f ? -60f : Mathf.LinearToDb(level), delta * 30f);
            if (_singing <= 0 && _singer.VolumeDb <= -55f)
            {
                _singer.Stop();
            }

            _layers["tavern"].Target *= 0.45f;
        }

        foreach (var (name, layer) in _layers)
        {
            layer.Volume = Mathf.MoveToward(layer.Volume, layer.Target, delta * 0.8f);
            layer.Player.VolumeDb = layer.Volume < 0.01f ? -80 : Mathf.LinearToDb(layer.Volume * layer.Gain);
            Fill(name, layer);
        }

        if (pace <= 0)
        {
            return;
        }

        // The sounds of work, each at its own rhythm and as loud as the listener is near it.
        Now("mallet", delta, workers > 0 ? Mathf.Max(0.6f, 2.2f / workers) : -1, 0.8f, Near(_workshop, 20) * 0.6f);
        Now("anvil", delta, smiths > 0 ? 1.1f : -1, 0.5f, Near(_smithy, 16) * 0.7f);
        Now("dice", delta, Dicing ? 5f : -1, 3f, Inside ? 0.8f : 0.15f);
        Now("cup", delta, Inside ? 2.2f : 3.5f, 2f, (Inside ? 1f : Near(_tavern, 14)) * (0.25f + 0.5f * night) * 0.8f);
        Now("coins", delta, market > 0 ? 18f / market : -1, 4f, Near(_market, 14) * day * 0.6f);
        Now("knife", delta, fish > 0 ? 3f : -1, 2f, Near(_fish, 10) * day * 0.55f);
        Now("creak", delta, 6f, 5f, Mathf.Clamp(1f - Mathf.Abs(listener.Y - _waterline) / 10f, 0, 1) * 0.45f);

        // The chapel bell tells the hour by day: one stroke for each hour of the clock, while time runs gently.
        var whole = (int)hour;
        if (whole != _lastHour)
        {
            if (_lastHour >= 0 && whole is >= 6 and <= 21 && pace <= 10f)
            {
                _bellStrikes = Math.Min((whole + 11) % 12 + 1, 6);
                _bellNext = 0;
            }

            _lastHour = whole;
        }

        if (_bellStrikes > 0 && (_bellNext -= delta) <= 0)
        {
            Play("bell", 0.25f + 0.5f * Mathf.Clamp(1f - _chapel.DistanceTo(listener) / 40f, 0, 1), 1f);
            _bellStrikes--;
            _bellNext = 1.8f;
        }
    }

    // Plays a take of <paramref name="kind"/> when its time comes: every <paramref name="every"/> seconds
    // give or take <paramref name="spread"/>, or never while <paramref name="every"/> is negative.
    private void Now(string kind, float delta, float every, float spread, float volume)
    {
        if (every < 0)
        {
            _next.Remove(kind);
            return;
        }

        if (!_next.TryGetValue(kind, out var wait))
        {
            _next[kind] = every * _random.Randf();
            return;
        }

        wait -= delta;
        if (wait <= 0)
        {
            if (volume > 0.02f)
            {
                Play(kind, volume, _random.RandfRange(0.9f, 1.1f));
            }

            wait = Mathf.Max(0.15f, every + _random.RandfRange(-spread, spread) * 0.5f);
        }

        _next[kind] = wait;
    }

    /// <summary>Plays one take of a recorded sound (mallet, anvil, cup, coins, knife, creak, bell) at once.</summary>
    public void Cue(string kind, float volume) => Play(kind, volume, _random.RandfRange(0.92f, 1.08f));

    private void Play(string kind, float volume, float pitch)
    {
        if (!_samples.TryGetValue(kind, out var takes) || takes.Length == 0 || _voices.FirstOrDefault(v => !v.Playing) is not { } voice)
        {
            return;
        }

        voice.Stream = takes[_random.RandiRange(0, takes.Length - 1)];
        voice.VolumeDb = Mathf.LinearToDb(volume);
        voice.PitchScale = pitch;
        voice.Play();
    }

    private static float Distance(Rect2 zone, Vector2 point)
    {
        var dx = Mathf.Max(Mathf.Max(zone.Position.X - point.X, 0), point.X - zone.End.X);
        var dy = Mathf.Max(Mathf.Max(zone.Position.Y - point.Y, 0), point.Y - zone.End.Y);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    // Generated placeholders: filtered noise shaped for each kind of place.
    private void Fill(string name, Layer layer)
    {
        if (layer.Playback is not { } playback)
        {
            return;
        }

        var frames = playback.GetFramesAvailable();
        for (var i = 0; i < frames; i++)
        {
            var white = _random.Randf() * 2f - 1f;
            float sample;
            switch (name)
            {
                case "surf":
                    // Brown noise swelling and falling like waves against stone.
                    layer.Filter = layer.Filter * 0.995f + white * 0.02f;
                    layer.Phase += 1f / MixRate;
                    sample = layer.Filter * (0.55f + 0.45f * Mathf.Sin(layer.Phase * 0.9f) * Mathf.Sin(layer.Phase * 0.37f + 1f)) * 3f;
                    break;
                case "market":
                case "tavern":
                    // Band-limited noise with a syllabic flutter: many voices at once.
                    layer.Filter = layer.Filter * 0.82f + white * 0.18f;
                    layer.Filter2 = layer.Filter2 * 0.97f + layer.Filter * 0.03f;
                    layer.Phase += 1f / MixRate;
                    var flutter = 0.6f + 0.4f * Mathf.Sin(layer.Phase * 21f + Mathf.Sin(layer.Phase * 3.1f) * 4f);
                    sample = (layer.Filter - layer.Filter2) * flutter * 2.2f;
                    break;
                case "rain":
                    layer.Filter = layer.Filter * 0.4f + white * 0.6f;
                    sample = layer.Filter * 0.5f;
                    break;
                default:
                    layer.Filter = layer.Filter * 0.999f + white * 0.01f;
                    sample = layer.Filter * 4f;
                    break;
            }

            sample = Mathf.Clamp(sample, -1f, 1f) * 0.5f;
            playback.PushFrame(new Vector2(sample, sample));
        }
    }
}

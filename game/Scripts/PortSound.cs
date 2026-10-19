using Expedition.Simulation.World;
using Godot;

namespace Expedition.Game;

/// <summary>
/// The port's sound zones (PORT_VISUAL_PASS §3.14, PORT_EXPERIENCE §15), with generated placeholder
/// sound until CC0 recordings can be fetched (D-026). Each zone is a layer whose loudness follows how
/// close the camera's focus is to it and how much is happening there:
/// <list type="bullet">
/// <item>waterfront: surf against the quay;</item>
/// <item>market: crowd murmur, rising with the number of people at the stalls;</item>
/// <item>workshop: knocks of mallet and saw, timed to the people hammering and sawing;</item>
/// <item>tavern: voices and the clink of cups, busier at night;</item>
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

    /// <summary>Activity the scene reports each frame: people at the stalls, people hammering or sawing.</summary>
    public (int Market, int Workers) Activity { get; set; }

    private sealed class Layer
    {
        public AudioStreamPlayer Player = null!;
        public AudioStreamGeneratorPlayback? Playback;
        public float Volume;
        public float Target;
        public float Filter;
        public float Filter2;
        public float Phase;
        public float Phase2;
        public float Next;
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
        _workshop = Of(f => f.Style is "cooperage" or "smithy" or "sailloft" || f.Kind is "hull_on_stocks" or "timber" or "sawpit" or "ropewalk");
        _tavern = Of(f => f.Style == "tavern");
        _waterline = location.Spots.Where(s => s.Kind == "BoardShip").Select(s => (float)s.Y).DefaultIfEmpty(34).Max();

        foreach (var name in new[] { "surf", "market", "workshop", "tavern", "street", "rain" })
        {
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
    public void Update(Vector2 listener, float hour, float rain, float delta)
    {
        if (_layers.Count == 0)
        {
            return;
        }

        float Near(Rect2 zone, float reach) => Mathf.Clamp(1f - Distance(zone, listener) / reach, 0, 1);
        var day = hour is > 6.5f and < 20.5f ? 1f : 0.25f;
        var night = hour is > 19f or < 2f ? 1f : 0.35f;
        var (market, workers) = Activity;

        _layers["surf"].Target = 0.35f + 0.45f * Mathf.Clamp(1f - Mathf.Abs(listener.Y - _waterline) / 18f, 0, 1);
        _layers["market"].Target = Near(_market, 18) * Mathf.Clamp(market / 6f, 0.15f, 1f) * day * 0.6f;
        _layers["workshop"].Target = Near(_workshop, 20) * Mathf.Clamp(workers / 3f, 0f, 1f) * 0.7f;
        _layers["tavern"].Target = Near(_tavern, 14) * (0.25f + 0.6f * night);
        _layers["street"].Target = 0.12f;
        _layers["rain"].Target = rain * 0.55f;

        foreach (var (name, layer) in _layers)
        {
            layer.Volume = Mathf.MoveToward(layer.Volume, layer.Target, delta * 0.8f);
            layer.Player.VolumeDb = layer.Volume < 0.01f ? -80 : Mathf.LinearToDb(layer.Volume);
            Fill(name, layer, workers);
        }
    }

    private static float Distance(Rect2 zone, Vector2 point)
    {
        var dx = Mathf.Max(Mathf.Max(zone.Position.X - point.X, 0), point.X - zone.End.X);
        var dy = Mathf.Max(Mathf.Max(zone.Position.Y - point.Y, 0), point.Y - zone.End.Y);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    // Generated placeholders: filtered noise shaped for each kind of place.
    private void Fill(string name, Layer layer, int workers)
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
                    if (name == "tavern")
                    {
                        // Now and then a cup set down.
                        layer.Next -= 1f / MixRate;
                        if (layer.Next <= 0)
                        {
                            layer.Next = 0.6f + _random.Randf() * 2.5f;
                            layer.Phase2 = 1f;
                        }

                        layer.Phase2 *= 0.9985f;
                        sample += Mathf.Sin(layer.Phase * 2600f * Mathf.Tau) * layer.Phase2 * 0.25f;
                    }

                    break;
                case "workshop":
                    // Knocks of mallet on wood, faster with more hands at work.
                    layer.Next -= 1f / MixRate;
                    if (layer.Next <= 0 && workers > 0)
                    {
                        layer.Next = (0.5f + _random.Randf() * 0.7f) / Mathf.Max(1, workers * 0.6f);
                        layer.Phase2 = 1f;
                    }

                    layer.Phase2 *= 0.9965f;
                    layer.Phase += 1f / MixRate;
                    sample = (Mathf.Sin(layer.Phase * 180f * Mathf.Tau) * 0.7f + white * 0.3f) * layer.Phase2;
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

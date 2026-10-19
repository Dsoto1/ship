using Expedition.Simulation.Port;
using Expedition.Simulation.World;
using Godot;

namespace Expedition.Game;

/// <summary>
/// Small scenes the port plays out near the player (PORT_EXPERIENCE §12, R2d): sailors arguing outside
/// the tavern, a porter's sack splitting, a procession, a funeral, a thief chased across the square, a
/// cart losing a wheel, a farewell on the pier, a drunk, a dispute on the terrace, children round the
/// well. Each is described in <c>data/port</c> (where, when, in what weather) and drawn here with crowd
/// figures, a few props and recorded sounds. They last about ten to thirty seconds, happen only where
/// the camera is looking and while time runs gently, never twice in a few minutes, and give nothing:
/// no reward, no state, nothing saved. Presentation only, like <see cref="PortCrowd"/>.
/// </summary>
public partial class AmbientScenes : Node3D
{
    private const int Capacity = 14;
    // Clothing indices (CrowdMesh.Clothing) for mourners and clerks.
    private const int Black = 8, DarkBrown = 3, SlateBlue = 11;

    private readonly Random _random = new(1580);
    private readonly Dictionary<string, double> _lastPlayed = [];
    private IReadOnlyList<AmbientScene> _scenes = [];
    private LocationDefinition _location = null!;
    private Func<float, float, float> _height = (_, _) => 0f;
    private MultiMeshInstance3D _view = null!;
    private MultiMeshInstance3D _shadows = null!;
    private Scene? _now;
    private double _clock;
    private double _nextTry = 15;

    /// <summary>Told when a scene begins near the player, with its line.</summary>
    public event Action<string>? Told;

    /// <summary>Plays a recorded sound: kind and volume.</summary>
    public Action<string, float>? Sound { get; set; }

    /// <summary>The scene playing now, or null.</summary>
    public string? Current => _now?.Definition.Id;

    private sealed class Actor
    {
        public Vector2 At;
        public float Yaw;
        public float Phase;
        public int Pose;
        public int Look;
        public float Scale = 0.95f;
        public float Lift;
        public float Shown;
        public bool Gone;
        public Queue<Step> Steps = [];
        public Node3D? Holding;
    }

    // One thing an actor does: walk a route at a speed, or stand (facing, posed, perhaps gesturing),
    // or run round a point; then the next. An actor whose steps run out walks off and is gone.
    private sealed record Step(float Seconds = 0, List<Vector2>? Route = null, float Speed = 1.2f, int Pose = CrowdMesh.Walking,
        Vector2? Face = null, bool Gesture = false, Vector2? Circle = null, float Radius = 0, float Lift = 0, Action? Then = null)
    {
        public int Leg { get; set; }
        public float Along { get; set; }
        public float Spent { get; set; }
    }

    private sealed class Scene(AmbientScene definition)
    {
        public AmbientScene Definition { get; } = definition;
        public List<Actor> Actors { get; } = [];
        public Node3D Props { get; } = new();
        public List<(float At, string Sound, float Volume)> Cues { get; } = [];
        public float Time { get; set; }
    }

    /// <summary>Takes the scenes and the place they happen in.</summary>
    public void Build(LocationDefinition location, IReadOnlyList<AmbientScene> scenes, Func<float, float, float> height)
    {
        _location = location;
        _scenes = scenes;
        _height = height;
        _view?.QueueFree();
        _shadows?.QueueFree();
        _view = CrowdMesh.Create(Capacity);
        _shadows = CrowdMesh.CreateShadows(Capacity);
        AddChild(_view);
        AddChild(_shadows);
    }

    /// <summary>
    /// Moves the scene on by <paramref name="delta"/> real seconds, or starts one near
    /// <paramref name="focus"/> when it is time. <paramref name="gentle"/> is false while paused or at
    /// high speed: nothing moves, and a scene under way at high speed ends.
    /// </summary>
    public void Update(int hour, bool raining, Vector2 focus, float delta, bool paused, bool gentle)
    {
        if (paused)
        {
            return;
        }

        if (!gentle)
        {
            End();
            return;
        }

        _clock += delta;
        if (_now is null && _clock >= _nextTry)
        {
            _nextTry = _clock + 20 + _random.NextDouble() * 30;
            Begin(hour, raining, focus);
        }

        if (_now is { } now)
        {
            now.Time += delta;
            foreach (var cue in now.Cues.Where(c => c.At <= now.Time).ToList())
            {
                Sound?.Invoke(cue.Sound, cue.Volume);
                now.Cues.Remove(cue);
            }

            foreach (var actor in now.Actors)
            {
                Act(actor, delta);
            }

            if (now.Actors.All(a => a.Gone) || now.Time > 60)
            {
                End();
            }
        }

        Draw();
    }

    private void End()
    {
        if (_now is null)
        {
            return;
        }

        _now.Props.QueueFree();
        _now = null;
        _view.Multimesh.VisibleInstanceCount = 0;
        _shadows.Multimesh.VisibleInstanceCount = 0;
    }

    /// <summary>Developer tool for screenshots: starts the scene <paramref name="id"/> at once, wherever the camera is.</summary>
    public void Force(string id)
    {
        if (_scenes.FirstOrDefault(s => s.Id == id) is { } scene)
        {
            End();
            var playing = new Scene(scene);
            AddChild(playing.Props);
            _now = playing;
            Stage(playing);
        }
    }

    private void Begin(int hour, bool raining, Vector2 focus)
    {
        var near = _scenes
            .Where(s => s.AtHour(hour) && (s.Weather == "any" || (s.Weather == "wet") == raining))
            .Where(s => !_lastPlayed.TryGetValue(s.Id, out var last) || _clock - last > 300)
            .Where(s => Cell(s.At).DistanceTo(focus) < 26)
            .OrderBy(s => Cell(s.At).DistanceTo(focus))
            .ToList();
        if (near.Count == 0)
        {
            return;
        }

        var scene = near[_random.Next(Math.Min(2, near.Count))];
        var playing = new Scene(scene);
        AddChild(playing.Props);
        _now = playing;
        _lastPlayed[scene.Id] = _clock;
        Stage(playing);
        Told?.Invoke(scene.Text);
    }

    private static Vector2 Cell(IReadOnlyList<int> cell) => new(cell[0], cell[1]);

    private GridPosition Grid(Vector2 point) => new(Mathf.RoundToInt(point.X), Mathf.RoundToInt(point.Y));

    private List<Vector2> Route(Vector2 from, Vector2 to) =>
        WalkPaths.Between(_location, Grid(from), Grid(to)) ?? [from, to];

    // A walkable cell about <paramref name="distance"/> away, to walk off to and be gone.
    private Vector2 Away(Vector2 from, float distance)
    {
        for (var tries = 0; tries < 12; tries++)
        {
            var angle = (float)(_random.NextDouble() * Mathf.Tau);
            var to = from + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            if (_location.IsWalkable(Grid(to)) && Pathfinder.FindPath(_location, Grid(from), Grid(to)) is not null)
            {
                return to;
            }
        }

        return from;
    }

    private Actor Add(Scene playing, Vector2 at, int look, params Step[] steps)
    {
        var actor = new Actor { At = at, Look = look, Phase = (float)_random.NextDouble() * Mathf.Tau };
        foreach (var step in steps)
        {
            actor.Steps.Enqueue(step);
        }

        playing.Actors.Add(actor);
        return actor;
    }

    // Looks for a clothing colour: any look whose clothing index is the one wanted.
    private int Dressed(int clothing) => clothing + CrowdMesh.Clothing.Length * _random.Next(1, 40);

    private void Stage(Scene playing)
    {
        var scene = playing.Definition;
        var at = Cell(scene.At);
        var to = scene.To is { } t ? Cell(t) : at;
        switch (scene.Kind)
        {
            case "argument":
            {
                // Two sailors face each other, both talking with their hands, then go off different ways.
                Add(playing, at + new Vector2(-0.55f, 0), _random.Next(1000), new Step(18, Face: at + new Vector2(1, 0), Gesture: true));
                Add(playing, at + new Vector2(0.55f, 0), _random.Next(1000), new Step(1.5f, Face: at - new Vector2(1, 0)), new Step(17, Face: at - new Vector2(1, 0), Gesture: true));
                playing.Cues.Add((0.5f, "cup", 0.4f));
                break;
            }

            case "dropped_load":
            {
                // A porter's sack splits halfway along: he stoops over the beans a while, then carries on.
                var route = Route(at, to);
                var half = route.Count / 2;
                var first = route.Take(half + 1).ToList();
                var rest = route.Skip(half).ToList();
                Add(playing, at, _random.Next(1000),
                    new Step(Route: first, Pose: CrowdMesh.Carrying, Then: () =>
                    {
                        var spill = rest[0];
                        Prop(playing, new SphereMesh { Radius = 0.3f, Height = 0.22f, RadialSegments = 8, Rings = 3 }, spill + new Vector2(0.4f, 0.2f), 0.08f, new Color(0.62f, 0.54f, 0.4f));
                        for (var i = 0; i < 9; i++)
                        {
                            var bean = spill + new Vector2((float)_random.NextDouble() * 1.6f - 0.4f, (float)_random.NextDouble() * 1.2f - 0.3f);
                            Prop(playing, new SphereMesh { Radius = 0.025f, Height = 0.04f, RadialSegments = 4, Rings = 2 }, bean, 0.02f, new Color(0.55f, 0.36f, 0.22f));
                        }
                    }),
                    new Step(7, Pose: CrowdMesh.Hauling, Lift: -0.25f),
                    new Step(Route: rest, Pose: CrowdMesh.Walking));
                playing.Cues.Add((Length(first) / 1.2f, "mallet", 0.5f));
                break;
            }

            case "procession":
            {
                // Behind a cross, out of the chapel along the square and back, slowly, in file.
                var route = Route(at, to).Concat(Route(to, at).Skip(1)).ToList();
                for (var i = 0; i < 9; i++)
                {
                    var actor = Add(playing, at, i == 0 ? Dressed(Black) : _random.Next(1000),
                        new Step(i * 1.4f, Face: route.Count > 1 ? route[1] : null), new Step(Route: route, Speed: 0.75f, Pose: i == 0 ? CrowdMesh.Carrying : CrowdMesh.Walking));
                    actor.Shown = 0;
                    if (i == 0)
                    {
                        actor.Holding = Carried(playing, new Vector3(0.14f, 2.0f, 0), new Vector3(0.06f, 1.5f, 0.06f), new Vector3(0.5f, 0.06f, 0.06f), 1.75f);
                    }
                }

                playing.Cues.AddRange([(0f, "bell", 0.5f), (1.8f, "bell", 0.5f), (3.6f, "bell", 0.5f)]);
                break;
            }

            case "funeral":
            {
                // Four men under the bier, mourners in black behind, at a walk, to the burial ground.
                var route = Route(at, to);
                for (var i = 0; i < 8; i++)
                {
                    var bearer = i < 4;
                    var offset = bearer ? new Vector2(i % 2 == 0 ? -0.35f : 0.35f, 0) : Vector2.Zero;
                    var delay = bearer ? (i / 2) * 0.9f : 2.2f + (i - 4) * 1.2f;
                    var actor = Add(playing, at, Dressed(i % 3 == 0 ? DarkBrown : Black),
                        new Step(delay, Face: route.Count > 1 ? route[1] : null), new Step(Route: Shifted(route, offset), Speed: 0.6f, Pose: bearer ? CrowdMesh.Carrying : CrowdMesh.Walking));
                    actor.Shown = 0;
                    if (i == 0)
                    {
                        actor.Holding = Carried(playing, new Vector3(0.35f, 1.68f, -0.45f), new Vector3(0.6f, 0.2f, 1.9f), null, 0);
                    }
                }

                playing.Cues.AddRange([(0f, "bell", 0.45f), (5f, "bell", 0.45f), (10f, "bell", 0.45f)]);
                break;
            }

            case "thief":
            {
                // A boy runs; a woman runs after him, shouting, and gives up.
                var route = Route(at, to);
                var beyond = Route(to, Away(to, 10));
                var boy = Add(playing, at, _random.Next(1000), new Step(Route: route.Concat(beyond.Skip(1)).ToList(), Speed: 3.6f));
                boy.Scale = 0.72f;
                Add(playing, at + new Vector2(-1, 0), Dressed(4), new Step(1.2f, Face: to, Gesture: true), new Step(Route: route, Speed: 2.6f), new Step(5, Face: to, Gesture: true));
                playing.Cues.Add((0.2f, "coins", 0.6f));
                break;
            }

            case "cart_wheel":
            {
                // A handcart with a wheel off, the carter by it; a neighbour comes to help lift it.
                Prop(playing, new BoxMesh { Size = new Vector3(1.6f, 0.12f, 1.0f) }, at + new Vector2(0.9f, 0), 0.42f, new Color(0.45f, 0.33f, 0.21f), new Vector3(0, 0, 0.22f));
                Prop(playing, new CylinderMesh { TopRadius = 0.45f, BottomRadius = 0.45f, Height = 0.08f, RadialSegments = 12, Rings = 1 }, at + new Vector2(0.4f, 0.75f), 0.05f, new Color(0.35f, 0.25f, 0.16f));
                Prop(playing, new CylinderMesh { TopRadius = 0.45f, BottomRadius = 0.45f, Height = 0.08f, RadialSegments = 12, Rings = 1 }, at + new Vector2(1.2f, -0.55f), 0.45f, new Color(0.35f, 0.25f, 0.16f), new Vector3(Mathf.Pi / 2f, 0, 0));
                Prop(playing, new BoxMesh { Size = new Vector3(1.4f, 0.06f, 0.06f) }, at + new Vector2(-0.3f, 0.35f), 0.3f, new Color(0.45f, 0.33f, 0.21f));
                var helper = Away(at, 9);
                Add(playing, at + new Vector2(-0.4f, 0.9f), _random.Next(1000), new Step(9, Face: at + new Vector2(0.9f, 0)), new Step(12, Face: at + new Vector2(0.9f, 0), Pose: CrowdMesh.Hauling, Lift: -0.2f));
                Add(playing, helper, _random.Next(1000), new Step(3, Face: at), new Step(Route: Route(helper, at + new Vector2(1.6f, 0.9f))), new Step(12, Face: at + new Vector2(0.9f, 0), Pose: CrowdMesh.Hauling, Lift: -0.2f));
                playing.Cues.AddRange([(0.1f, "mallet", 0.55f), (14f, "creak", 0.5f)]);
                break;
            }

            case "farewell":
            {
                // An embrace at the pier's end, then he goes down to the boat and she waves.
                Add(playing, at + new Vector2(0, -0.3f), Dressed(10), new Step(8, Face: at + new Vector2(0, 1), Pose: CrowdMesh.Hauling), new Step(10, Face: to, Pose: CrowdMesh.Waving));
                Add(playing, at + new Vector2(0, 0.3f), _random.Next(1000), new Step(8, Face: at - new Vector2(0, 1), Pose: CrowdMesh.Hauling), new Step(Route: [at + new Vector2(0, 0.3f), to], Speed: 0.8f), new Step(2, Lift: -0.9f));
                break;
            }

            case "drunk":
            {
                // He reels out of the tavern, weaving, and sits down hard in the street.
                var route = Route(at, to);
                Add(playing, at, _random.Next(1000), new Step(Route: Weave(route), Speed: 0.8f), new Step(9, Lift: -0.55f, Pose: CrowdMesh.Hauling));
                playing.Cues.Add((0.2f, "cup", 0.5f));
                break;
            }

            case "dispute":
            {
                // A merchant waves his papers at the clerk; two others stand and listen.
                Add(playing, at, _random.Next(1000), new Step(20, Face: at + new Vector2(1, 0), Gesture: true));
                Add(playing, at + new Vector2(1.1f, 0), Dressed(Black), new Step(20, Face: at));
                Add(playing, at + new Vector2(0.4f, -1.6f), _random.Next(1000), new Step(18, Face: at));
                Add(playing, at + new Vector2(1.5f, 1.4f), Dressed(SlateBlue), new Step(16, Face: at));
                break;
            }

            case "children":
            {
                // Three children chase round the well, then scatter.
                for (var i = 0; i < 3; i++)
                {
                    var child = Add(playing, at, _random.Next(1000), new Step(16, Circle: at + new Vector2(-1.5f, -1.5f), Radius: 2.4f + i * 0.3f, Speed: 2.4f));
                    child.Scale = 0.6f + i * 0.04f;
                    child.Phase = i * 2.1f;
                }

                break;
            }
        }

        // When their part is done, anyone left standing walks off and is gone; whoever ends sitting in
        // the street or down in a boat fades where they are.
        foreach (var actor in playing.Actors)
        {
            var last = actor.Steps.Last();
            if (last.Route is null && last.Lift > -0.5f)
            {
                var end = actor.Steps.LastOrDefault(s => s.Route is not null)?.Route?[^1] ?? actor.At;
                actor.Steps.Enqueue(new Step(Route: Route(end, Away(end, 9))));
            }
        }
    }

    private static float Length(List<Vector2> route) => route.Zip(route.Skip(1), (a, b) => a.DistanceTo(b)).Sum();

    private static List<Vector2> Shifted(List<Vector2> route, Vector2 offset) => route.Select(p => p + offset).ToList();

    // A drunk's route: the same way, but swaying from side to side.
    private static List<Vector2> Weave(List<Vector2> route)
    {
        var woven = new List<Vector2> { route[0] };
        for (var i = 1; i < route.Count; i++)
        {
            var a = route[i - 1];
            var b = route[i];
            var steps = Math.Max(1, (int)(a.DistanceTo(b) / 1.2f));
            var side = (b - a).Normalized().Orthogonal();
            for (var s = 1; s <= steps; s++)
            {
                woven.Add(a.Lerp(b, s / (float)steps) + side * (s % 2 == 0 ? 0.35f : -0.35f) * (s == steps ? 0 : 1));
            }
        }

        return woven;
    }

    private void Prop(Scene playing, Mesh mesh, Vector2 at, float height, Color colour, Vector3? rotation = null)
    {
        playing.Props.AddChild(new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = colour, Roughness = 0.9f },
            Position = new Vector3(at.X, _height(at.X, at.Y) + height, at.Y),
            Rotation = rotation ?? Vector3.Zero,
        });
    }

    // A thing carried by an actor (a cross, a bier): a tall piece, and optionally a crosspiece.
    private Node3D Carried(Scene playing, Vector3 offset, Vector3 size, Vector3? cross, float crossHeight)
    {
        var holder = new Node3D();
        playing.Props.AddChild(holder);
        var wood = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.21f, 0.14f), Roughness = 0.85f };
        holder.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = wood, Position = offset });
        if (cross is { } bar)
        {
            holder.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = bar }, MaterialOverride = wood, Position = new Vector3(offset.X, crossHeight + 0.75f, offset.Z) });
        }

        return holder;
    }

    private void Act(Actor actor, float delta)
    {
        if (actor.Gone)
        {
            return;
        }

        if (actor.Steps.Count == 0)
        {
            actor.Shown -= delta * 2f;
            actor.Gone = actor.Shown <= 0;
            return;
        }

        actor.Shown = Mathf.Min(1, actor.Shown + delta * 2.5f);
        var step = actor.Steps.Peek();
        actor.Pose = step.Pose;
        actor.Lift = Mathf.MoveToward(actor.Lift, step.Lift, delta * 1.5f);
        if (step.Route is { Count: > 1 } route)
        {
            var distance = step.Speed * delta;
            while (distance > 0 && step.Leg < route.Count - 1)
            {
                var a = route[step.Leg];
                var b = route[step.Leg + 1];
                var length = a.DistanceTo(b);
                var move = Mathf.Min(distance, length - step.Along);
                step.Along += move;
                distance -= move;
                actor.Phase += move * Mathf.Tau / (step.Speed > 2 ? 2.2f : 1.4f);
                actor.At = a.Lerp(b, length > 0 ? step.Along / length : 1);
                if ((b - a).LengthSquared() > 0.001f)
                {
                    actor.Yaw = Mathf.Atan2(b.X - a.X, b.Y - a.Y);
                }

                if (step.Along >= length - 0.0001f)
                {
                    step.Leg++;
                    step.Along = 0;
                }
            }

            if (step.Leg >= route.Count - 1)
            {
                Next(actor);
            }

            return;
        }

        if (step.Circle is { } centre)
        {
            var angle = (actor.Phase * 0.15f) + step.Spent * step.Speed / step.Radius;
            var next = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * step.Radius;
            actor.Yaw = Mathf.Atan2(next.X - actor.At.X, next.Y - actor.At.Y);
            actor.At = next;
            actor.Phase += step.Speed * delta * Mathf.Tau / 1.6f;
        }
        else if (step.Face is { } face && (face - actor.At).LengthSquared() > 0.01f)
        {
            actor.Yaw = Mathf.Atan2(face.X - actor.At.X, face.Y - actor.At.Y);
        }

        if (step.Gesture)
        {
            // Talking with the hands: an arm up now and then.
            actor.Pose = Mathf.PosMod(step.Spent + actor.Look % 3, 2.4f) < 0.9f ? CrowdMesh.Waving : CrowdMesh.Walking;
        }

        step.Spent += delta;
        if (step.Spent >= step.Seconds)
        {
            Next(actor);
        }
    }

    private static void Next(Actor actor)
    {
        var done = actor.Steps.Dequeue();
        done.Then?.Invoke();
    }

    private void Draw()
    {
        var index = 0;
        if (_now is { } now)
        {
            foreach (var actor in now.Actors)
            {
                if (index >= Capacity || actor.Gone || actor.Shown <= 0.01f)
                {
                    continue;
                }

                var step = actor.Steps.Count > 0 ? actor.Steps.Peek() : null;
                var walking = step is { Route.Count: > 1 } || step?.Circle is not null || actor.Steps.Count == 0;
                var at = new Vector3(actor.At.X, _height(actor.At.X, actor.At.Y) + actor.Lift, actor.At.Y);
                CrowdMesh.Set(_view.Multimesh, index, at, actor.Yaw, actor.Scale * Mathf.SmoothStep(0, 1, actor.Shown), actor.Phase, walking ? 1f : 0f,
                    actor.Pose, actor.Look, _shadows.Multimesh);
                if (actor.Holding is { } held)
                {
                    held.Position = at;
                    held.Rotation = new Vector3(0, actor.Yaw, 0);
                }

                index++;
            }
        }

        _view.Multimesh.VisibleInstanceCount = index;
        _shadows.Multimesh.VisibleInstanceCount = index;
    }
}

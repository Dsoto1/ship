using Expedition.Simulation.World;
using Godot;

namespace Expedition.Game;

/// <summary>
/// The Tier C crowd of the waterfront (PORT_EXPERIENCE §6, §18): passers-by who make the port look far
/// busier than the thirty-odd people simulated in it. Each one makes a trip with a reason: in from a
/// street end or out of a door, sometimes stopping at a stall, the well or the notice board, and off to
/// another door, street or pier, where they are gone. Some carry a sack. There are more in the morning
/// market and fewer at night. They are presentation only: no names, no clicks, nothing saved, never
/// stored in the simulation. They walk at the place's walking pace in game time, so they stop when the
/// game is paused. Each is one of the crowd kits the character pipeline builds (Assets/Characters/Crowd,
/// D-029) drawn from a pool, and every trip it is dressed again from the period's dyes, so two passers-by
/// of the same kit wear different coats, breeches and caps. Without the kits they fall back to one
/// <see cref="CrowdMesh"/> draw.
/// </summary>
public partial class PortCrowd : Node3D
{
    private const int Capacity = 72;
    private const int MostWalking = 40;
    private const string Kits = "res://Assets/Characters/Crowd";

    // The dyes of a working port (identities.py DYES_*): sRGB, with how common each is.
    private static readonly (Color Colour, int Weight)[] DyesMain =
    [
        (new("#5a4330"), 5), (new("#6e4a34"), 4), (new("#6b665c"), 4), (new("#8a7c66"), 4), (new("#8d6b3d"), 3),
        (new("#3f5268"), 2), (new("#8a3324"), 1), (new("#563a24"), 2), (new("#7a5434"), 1), (new("#232120"), 1),
        (new("#4a5a3c"), 1), (new("#6a5a7a"), 1),
    ];

    private static readonly (Color Colour, int Weight)[] DyesLower =
    [
        (new("#5a4330"), 4), (new("#6b665c"), 4), (new("#a9a291"), 3), (new("#8a7c66"), 3), (new("#6e4a34"), 2),
        (new("#cdbf9f"), 2), (new("#3f5268"), 1), (new("#33251a"), 1),
    ];

    // Shirts: mostly linen as it comes, bleached or greyed with wear; now and then a dyed one.
    private static readonly (Color Colour, int Weight)[] DyesShirt =
    [
        (new("#cdbf9f"), 5), (new("#e2dccb"), 3), (new("#a9a291"), 3), (new("#b9a888"), 2), (new("#8d9aa6"), 1),
        (new("#b79a62"), 1), (new("#a0685a"), 1), (new("#7d6650"), 1),
    ];

    private static readonly (Color Colour, int Weight)[] DyesHead =
    [
        (new("#8a3324"), 3), (new("#5a4330"), 3), (new("#6b665c"), 2), (new("#3f5268"), 2), (new("#1f1d1c"), 1),
        (new("#b8995c"), 1), (new("#cdbf9f"), 1), (new("#8d6b3d"), 1),
    ];

    private readonly List<GridPosition> _ends = [];
    private readonly List<(GridPosition Cell, Vector2 Facing)> _stops = [];
    private readonly List<Walker> _walkers = [];
    private readonly List<(Vector3 At, float Yaw, int Pose, int Look)> _gathered = [];
    private readonly List<(Vector3 At, int Pose)> _gatheredHome = [];
    private readonly Random _random = new(1519);
    private readonly List<PersonModel> _free = [];
    private readonly List<PersonModel?> _gatheredFigures = [];
    private readonly List<MeshInstance3D> _sacks = [];
    private readonly List<Vector3[]> _lanes = [];
    private readonly List<(int Lane, float Along, int Way, float Speed, PersonModel Figure)> _onLanes = [];
    private readonly List<(Vector3 At, float Yaw, Pose Pose, PersonModel? Figure)> _idlers = [];
    private Node3D? _figures;
    private float _rate = 1f;
    private float _wetness;
    private float _hour = 12f;
    private MultiMeshInstance3D _view = null!;
    private MultiMeshInstance3D _shadows = null!;
    private LocationDefinition _location = null!;
    private Func<float, float, float> _height = (_, _) => 0f;
    private double _sinceSpawn;
    private bool _populated;

    private sealed class Walker
    {
        public List<Vector2> Path = [];
        public int Segment;
        public float Along;
        public float Speed;
        public float Phase;
        public int Pose;
        public int Look;
        public float Scale;
        public int StopAt = -1;
        public float Pause;
        public Vector2 Facing;
        public float Yaw;
        public PersonModel? Figure;
    }

    /// <summary>1 normally; less in rain (R2d sets it from the weather).</summary>
    public float Density { get; set; } = 1f;

    /// <summary>How many are about just now.</summary>
    public int Count => _walkers.Count + _gathered.Count + _onLanes.Count + _idlers.Count(i => i.Figure?.Visible == true);

    /// <summary>
    /// The walks and the people at work beyond the ends of the map (PortView.BuildOutskirts): there is no
    /// map to find a path on, so they keep to the lanes they are given, end to end.
    /// </summary>
    public void SetOutskirts(IEnumerable<Vector3[]> lanes, IEnumerable<(Vector3 At, float Yaw, Pose Pose)> idlers)
    {
        foreach (var (_, _, _, _, figure) in _onLanes)
        {
            Give(figure);
        }

        foreach (var idler in _idlers)
        {
            Give(idler.Figure);
        }

        _onLanes.Clear();
        _idlers.Clear();
        _lanes.Clear();
        _lanes.AddRange(lanes.Where(l => l.Length > 1));
        foreach (var (at, yaw, pose) in idlers)
        {
            _idlers.Add((at, yaw, pose, _figures is null ? null : Take(false)));
        }
    }

    private static float Length(Vector3[] lane) => Enumerable.Range(1, lane.Length - 1).Sum(i => lane[i - 1].DistanceTo(lane[i]));

    // Where along a lane someone is, and which way the lane runs there.
    private static (Vector3 At, Vector3 Heading) Along(Vector3[] lane, float distance)
    {
        for (var i = 1; i < lane.Length; i++)
        {
            var length = lane[i - 1].DistanceTo(lane[i]);
            if (distance <= length || i == lane.Length - 1)
            {
                var t = Mathf.Clamp(distance / Mathf.Max(length, 0.001f), 0, 1);
                return (lane[i - 1].Lerp(lane[i], t), lane[i] - lane[i - 1]);
            }

            distance -= length;
        }

        return (lane[0], Vector3.Forward);
    }

    // Keeps a few people on each outlying lane, fewer at night; each walks it end to end and is gone.
    private void WalkLanes(float hour, float minutes)
    {
        if (_figures is null)
        {
            return;
        }

        var wanted = Mathf.RoundToInt(3f * Busy(hour) * Density);
        for (var lane = 0; lane < _lanes.Count; lane++)
        {
            var count = _onLanes.Count(w => w.Lane == lane);
            if (count < wanted && _random.NextDouble() < (count == 0 ? 1.0 : minutes * 0.25) && Take(_random.NextDouble() < 0.25) is { } figure)
            {
                var length = Length(_lanes[lane]);
                var way = _random.Next(2) == 0 ? 1 : -1;
                // At the start, anywhere along it, as if they had been walking a while.
                var along = !_lanesStarted ? (float)_random.NextDouble() * length : 0f;
                _onLanes.Add((lane, along, way, (float)((_location.WalkCellsPerMinute ?? 2.5) * (0.85 + _random.NextDouble() * 0.3)), figure));
            }
        }

        _lanesStarted = true;
        for (var i = _onLanes.Count - 1; i >= 0; i--)
        {
            var (lane, along, way, speed, figure) = _onLanes[i];
            along += speed * minutes;
            var length = Length(_lanes[lane]);
            if (along >= length)
            {
                Give(figure);
                _onLanes.RemoveAt(i);
                continue;
            }

            _onLanes[i] = (lane, along, way, speed, figure);
        }
    }

    private bool _lanesStarted;

    /// <summary>Finds the street ends, doors, piers and stalls of <paramref name="location"/>.</summary>
    public void Build(LocationDefinition location, Func<float, float, float> height)
    {
        _location = location;
        _height = height;
        _view?.QueueFree();
        _shadows?.QueueFree();
        _view = CrowdMesh.Create(Capacity);
        _view.Name = "Crowd";
        AddChild(_view);
        _shadows = CrowdMesh.CreateShadows(Capacity);
        AddChild(_shadows);
        foreach (var walker in _walkers)
        {
            Give(walker.Figure);
        }

        _walkers.Clear();
        _ends.Clear();
        BuildFigures();
        _stops.Clear();

        // Street ends: walkable cells on the edge of the map, a few to each opening.
        for (var x = 0; x < location.Width; x += 6)
        {
            AddEnd(new GridPosition(x, 0));
        }

        for (var y = 0; y < location.Height; y += 5)
        {
            AddEnd(new GridPosition(0, y));
            AddEnd(new GridPosition(location.Width - 1, y));
        }

        // Doors: the places people go indoors. The ends of the piers, where boats come and go.
        foreach (var spot in location.Spots.Where(s => s.Indoors))
        {
            AddEnd(spot.Cell);
        }

        for (var x = 0; x < location.Width; x++)
        {
            var y = Enumerable.Range(0, location.Height).LastOrDefault(y => location.IsWalkable(new GridPosition(x, y)));
            if (y > 38)
            {
                AddEnd(new GridPosition(x, y));
            }
        }

        // Stops: in front of the stalls, beside the well, before the notice board.
        foreach (var feature in location.Features.Where(f => f.Kind is "stall" or "well" or "notice_board"))
        {
            var middle = new Vector2((feature.X0 + feature.X1) / 2f, (feature.Y0 + feature.Y1) / 2f);
            for (var x = feature.X0 - 1; x <= feature.X1 + 1; x++)
            {
                foreach (var y in new[] { feature.Y0 - 1, feature.Y1 + 1 })
                {
                    var cell = new GridPosition(x, y);
                    if (location.IsWalkable(cell) && location.Spots.All(s => s.Cell != cell))
                    {
                        _stops.Add((cell, (middle - new Vector2(x, y)).Normalized()));
                    }
                }
            }
        }
    }

    // The pool of crowd figures, one of each kit in turn; none when the kits are not built.
    private void BuildFigures()
    {
        _figures?.QueueFree();
        _figures = null;
        _free.Clear();
        _sacks.Clear();
        _gatheredFigures.Clear();
        var kits = Enumerable.Range(0, 100).Select(i => $"Crowd/kit_{i:00}")
            .Where(k => ResourceLoader.Exists($"res://Assets/Characters/{k}/model.glb")).ToList();
        if (kits.Count == 0)
        {
            return;
        }

        _figures = new Node3D { Name = "CrowdFigures" };
        AddChild(_figures);
        _view.Visible = false;
        var sackMaterial = new StandardMaterial3D { AlbedoColor = new Color("#8f7a58"), Roughness = 0.95f };
        var sackMesh = new SphereMesh { Radius = 0.2f, Height = 0.3f, RadialSegments = 8, Rings = 4, Material = sackMaterial };
        for (var i = 0; i < Capacity; i++)
        {
            var figure = PersonModel.TryLoad(kits[i % kits.Count])!;
            figure.Name = $"Passer{i:00}";
            figure.Visible = false;
            // What a porter carries: a sack held before the chest (walk_carry's arms).
            var sack = new MeshInstance3D { Name = "Sack", Mesh = sackMesh, Position = new Vector3(0, 1.12f, 0.3f), Scale = new Vector3(1.2f, 1f, 0.85f) };
            figure.AddChild(sack);
            _sacks.Add(sack);
            figure.SetMeta("sack", i);
            _figures.AddChild(figure);
            _free.Add(figure);
        }
    }

    // A figure from the pool, dressed afresh: any kit, any dyes, a little taller or shorter.
    private PersonModel? Take(bool carrying)
    {
        if (_free.Count == 0)
        {
            return null;
        }

        var index = _random.Next(_free.Count);
        var figure = _free[index];
        _free.RemoveAt(index);
        Dress(figure, _random);
        figure.Scale = Vector3.One * (0.95f + (float)_random.NextDouble() * 0.09f);
        _sacks[figure.GetMeta("sack").AsInt32()].Visible = carrying;
        figure.Visible = true;
        return figure;
    }

    private void Give(PersonModel? figure)
    {
        if (figure is null || _figures is null)
        {
            return;
        }

        figure.Visible = false;
        _free.Add(figure);
    }

    /// <summary>Dresses a crowd figure afresh from the period's dyes (the lineup uses it too).</summary>
    public static void Dress(PersonModel figure, Random random) =>
        figure.Dye(Dye(DyesMain, random), Dye(DyesLower, random), random.NextDouble() < 0.85 ? Dye(DyesHead, random) : new Color(0, 0, 0, 0), Dye(DyesShirt, random));

    // A dye by how common it is, a shade lighter or darker, as no two dyings came out alike.
    private static Color Dye((Color Colour, int Weight)[] dyes, Random random)
    {
        var roll = random.Next(dyes.Sum(d => d.Weight));
        var colour = dyes[^1].Colour;
        foreach (var (c, weight) in dyes)
        {
            if ((roll -= weight) < 0)
            {
                colour = c;
                break;
            }
        }

        var shade = 0.88f + (float)random.NextDouble() * 0.24f;
        return new Color(Mathf.Clamp(colour.R * shade, 0, 1), Mathf.Clamp(colour.G * shade, 0, 1), Mathf.Clamp(colour.B * shade, 0, 1), 1f);
    }

    private void AddEnd(GridPosition cell)
    {
        if (_location.IsWalkable(cell) && !_ends.Contains(cell))
        {
            _ends.Add(cell);
        }
    }

    /// <summary>
    /// A crowd standing together, facing <paramref name="facing"/>, some waving: people seeing a ship off
    /// (PORT_EXPERIENCE §5, state 5). Null clears it.
    /// </summary>
    public void SetGathering(Vector3? centre, int count, Vector2 facing)
    {
        _gathered.Clear();
        _gatheredHome.Clear();
        foreach (var figure in _gatheredFigures)
        {
            Give(figure);
        }

        _gatheredFigures.Clear();
        if (centre is not { } at)
        {
            return;
        }

        var random = new Random(77);
        var across = new Vector2(-facing.Y, facing.X);
        for (var i = 0; i < count; i++)
        {
            var spot = new Vector2(at.X, at.Z) + across * (float)(random.NextDouble() * 7 - 3.5) - facing * (float)(random.NextDouble() * 2.2);
            var cell = new GridPosition(Mathf.RoundToInt(spot.X), Mathf.RoundToInt(spot.Y));
            if (!_location.IsWalkable(cell))
            {
                continue;
            }

            var yaw = Mathf.Atan2(facing.X, facing.Y) + (float)(random.NextDouble() - 0.5) * 0.8f;
            _gathered.Add((new Vector3(spot.X, _height(spot.X, spot.Y), spot.Y), yaw, random.Next(3) == 0 ? CrowdMesh.Waving : CrowdMesh.Walking, 200 + i));
            _gatheredFigures.Add(_figures is null ? null : Take(false));
        }
    }

    /// <summary>
    /// The departure's farewells (R3 scene D): <paramref name="count"/> of the gathered crowd step to
    /// the quay edge and wave, the rest stand; 0 lets them all go back to how they were gathered.
    /// </summary>
    public void Wave(int count, Vector2 facing)
    {
        for (var i = 0; i < _gathered.Count; i++)
        {
            var (at, yaw, pose, seed) = _gathered[i];
            (Vector3 At, int Pose) home = _gatheredHome.Count > i ? _gatheredHome[i] : (at, pose);
            if (_gatheredHome.Count <= i)
            {
                _gatheredHome.Add(home);
            }

            var waving = i < count;
            _gathered[i] = (waving ? home.At + new Vector3(facing.X, 0, facing.Y) * 1.1f : home.At, yaw, waving ? CrowdMesh.Waving : CrowdMesh.Walking, seed);
        }
    }

    /// <summary>
    /// Moves everyone on by <paramref name="minutes"/> of game time, starts new trips to keep the
    /// numbers right for <paramref name="hour"/>, and draws them; <paramref name="rate"/> is how fast
    /// their clips play (as for the named people), 0 when paused.
    /// </summary>
    public void Update(float hour, float minutes, float wetness, float rate = 1f)
    {
        CrowdMesh.Material.SetShaderParameter("wetness", wetness);
        _wetness = wetness;
        _rate = rate;
        var wanted = Mathf.RoundToInt(MostWalking * Busy(hour) * Density);
        if (!_populated && _ends.Count > 1)
        {
            // On arrival the town is already busy: trips begun before the player looked.
            _populated = true;
            for (var i = 0; i < wanted; i++)
            {
                if (StartTrip(hour) is { } walker)
                {
                    Walk(walker, (float)_random.NextDouble() * walker.Path.Count * 3f / walker.Speed);
                }
            }
        }

        _sinceSpawn += minutes;
        if (_walkers.Count < wanted && _sinceSpawn > 0.35)
        {
            _sinceSpawn = 0;
            StartTrip(hour);
        }

        WalkLanes(hour, minutes);
        _hour = hour;
        foreach (var walker in _walkers.ToList())
        {
            if (!Walk(walker, minutes))
            {
                Give(walker.Figure);
                _walkers.Remove(walker);
            }
        }

        Draw();
    }

    private static float Busy(float hour) => hour switch
    {
        < 4.5f => 0.06f,
        < 6f => 0.25f,
        < 8f => 0.7f,
        < 13f => 1f,
        < 15f => 0.65f,
        < 18.5f => 0.8f,
        < 20.5f => 0.45f,
        < 22.5f => 0.18f,
        _ => 0.06f,
    };

    private Walker? StartTrip(float hour)
    {
        if (_ends.Count < 2 || _walkers.Count >= MostWalking)
        {
            return null;
        }

        var from = _ends[_random.Next(_ends.Count)];
        var to = _ends[_random.Next(_ends.Count)];
        if (Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y) < 18)
        {
            return null;
        }

        var market = hour is >= 7 and < 14;
        (GridPosition Cell, Vector2 Facing)? stop = _stops.Count > 0 && _random.NextDouble() < (market ? 0.45 : 0.12)
            ? _stops[_random.Next(_stops.Count)]
            : null;

        if (stop is { } s)
        {
            var first = Pathfinder.FindPath(_location, from, s.Cell);
            var second = Pathfinder.FindPath(_location, s.Cell, to);
            if (first is null || second is null)
            {
                return null;
            }

            var smoothFirst = WalkPaths.Smooth(_location, first);
            var stopAt = smoothFirst.Count - 1;
            var path = smoothFirst.Concat(WalkPaths.Smooth(_location, second).Skip(1)).ToList();
            return Begin(path, stopAt, s.Facing, hour);
        }

        var route = Pathfinder.FindPath(_location, from, to);
        return route is null ? null : Begin(WalkPaths.Smooth(_location, route), -1, Vector2.Zero, hour);
    }

    private Walker Begin(List<Vector2> path, int stopAt, Vector2 facing, float hour)
    {
        // Walk a little to one side of the centre line, as people do, where there is room.
        var side = (float)(_random.NextDouble() - 0.5) * 0.8f;
        for (var i = 1; i < path.Count - 1; i++)
        {
            if (i == stopAt)
            {
                continue;
            }

            var bend = ((path[i] - path[i - 1]).Normalized() + (path[i + 1] - path[i]).Normalized()).Normalized();
            var moved = path[i] + new Vector2(-bend.Y, bend.X) * side;
            if (_location.IsWalkable(new GridPosition(Mathf.RoundToInt(moved.X), Mathf.RoundToInt(moved.Y))))
            {
                path[i] = moved;
            }
        }

        var carrying = hour is > 6 and < 19 && _random.NextDouble() < 0.28;
        var walker = new Walker
        {
            Path = path,
            Speed = (float)((_location.WalkCellsPerMinute ?? 2.5) * (0.85 + _random.NextDouble() * 0.3)),
            Phase = (float)_random.NextDouble() * Mathf.Tau,
            Pose = carrying ? CrowdMesh.Carrying : CrowdMesh.Walking,
            Look = _random.Next(1000),
            Scale = 0.9f + (float)_random.NextDouble() * 0.15f,
            // A load is carried straight through, not set down at a stall.
            StopAt = carrying ? -1 : stopAt,
            Pause = 2f + (float)_random.NextDouble() * 6f,
            Facing = facing,
            Figure = _figures is null ? null : Take(carrying),
        };
        _walkers.Add(walker);
        return walker;
    }

    // Moves a walker on; false when they have arrived and gone.
    private bool Walk(Walker walker, float minutes)
    {
        var distance = walker.Speed * minutes;
        while (distance > 0 && walker.Segment < walker.Path.Count - 1)
        {
            if (walker.Segment == walker.StopAt && walker.Along == 0 && walker.Pause > 0)
            {
                var waited = Mathf.Min(walker.Pause, distance / walker.Speed);
                walker.Pause -= waited;
                distance -= waited * walker.Speed;
                continue;
            }

            var a = walker.Path[walker.Segment];
            var b = walker.Path[walker.Segment + 1];
            var length = a.DistanceTo(b);
            var step = Mathf.Min(distance, length - walker.Along);
            walker.Along += step;
            walker.Phase += step * Mathf.Tau / 1.4f;
            distance -= step;
            if (walker.Along >= length - 0.0001f)
            {
                walker.Segment++;
                walker.Along = 0;
            }
        }

        return walker.Segment < walker.Path.Count - 1;
    }

    private void Draw()
    {
        var multimesh = _view.Multimesh;
        var index = 0;
        foreach (var walker in _walkers)
        {
            if (index >= Capacity)
            {
                break;
            }

            var a = walker.Path[walker.Segment];
            var b = walker.Path[Math.Min(walker.Segment + 1, walker.Path.Count - 1)];
            var direction = (b - a).Normalized();
            var at = a + direction * walker.Along;
            var stopped = walker.Segment == walker.StopAt && walker.Along == 0 && walker.Pause > 0;
            var heading = stopped ? walker.Facing : direction;
            if (heading.LengthSquared() > 0.01f)
            {
                walker.Yaw = Mathf.Atan2(heading.X, heading.Y);
            }

            var position = new Vector3(at.X, _height(at.X, at.Y), at.Y);
            if (walker.Figure is { } figure)
            {
                Place(figure, position, walker.Yaw, walker.Pose == CrowdMesh.Carrying ? Pose.Carrying : stopped ? Pose.Standing : Pose.Walking,
                    !stopped, _rate * walker.Speed / (float)(_location.WalkCellsPerMinute ?? 2.5));
                _shadows.Multimesh.SetInstanceTransform(index++, new Transform3D(Basis.Identity, position + new Vector3(0, 0.025f, 0)));
                continue;
            }

            if (_figures is not null)
            {
                continue;
            }

            CrowdMesh.Set(multimesh, index++, position, walker.Yaw, walker.Scale,
                walker.Phase, stopped ? 0f : 1f, walker.Pose, walker.Look, _shadows.Multimesh);
        }

        for (var i = 0; i < _gathered.Count; i++)
        {
            var (at, yaw, pose, look) = _gathered[i];
            if (index >= Capacity)
            {
                break;
            }

            if (_gatheredFigures.Count > i && _gatheredFigures[i] is { } figure)
            {
                Place(figure, at, yaw, pose == CrowdMesh.Waving ? Pose.Waving : Pose.Standing, false, _rate);
                _shadows.Multimesh.SetInstanceTransform(index++, new Transform3D(Basis.Identity, at + new Vector3(0, 0.025f, 0)));
                continue;
            }

            if (_figures is not null)
            {
                continue;
            }

            CrowdMesh.Set(multimesh, index++, at, yaw, 0.95f, (float)Time.GetTicksMsec() / 900f + look, 0f, pose, look, _shadows.Multimesh);
        }

        foreach (var (lane, along, way, speed, figure) in _onLanes)
        {
            if (index >= Capacity)
            {
                break;
            }

            var path = _lanes[lane];
            var length = Length(path);
            var (at, heading) = Along(path, way > 0 ? along : length - along);
            heading *= way;
            Place(figure, at, Mathf.Atan2(heading.X, heading.Z), figure == null ? Pose.Walking : _sacks[figure.GetMeta("sack").AsInt32()].Visible ? Pose.Carrying : Pose.Walking,
                true, _rate * speed / (float)(_location.WalkCellsPerMinute ?? 2.5));
            _shadows.Multimesh.SetInstanceTransform(index++, new Transform3D(Basis.Identity, at + new Vector3(0, 0.025f, 0)));
        }

        // The people at work out there keep the day's hours.
        var working = _hour is >= 6.5f and < 20.5f;
        foreach (var (at, yaw, pose, figure) in _idlers)
        {
            if (figure is null)
            {
                continue;
            }

            figure.Visible = working;
            if (working && index < Capacity)
            {
                Place(figure, at, yaw, pose, false, _rate);
                _shadows.Multimesh.SetInstanceTransform(index++, new Transform3D(Basis.Identity, at + new Vector3(0, 0.025f, 0)));
            }
        }

        multimesh.VisibleInstanceCount = _figures is null ? index : 0;
        _shadows.Multimesh.VisibleInstanceCount = index;
    }

    private void Place(PersonModel figure, Vector3 at, float yaw, Pose pose, bool moving, float rate)
    {
        figure.Position = at;
        figure.Rotation = new Vector3(0, yaw, 0);
        figure.Show(pose, moving, rate);
        figure.Condition(_wetness, 0.25f, 0f);
    }
}

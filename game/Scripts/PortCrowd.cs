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
/// game is paused. All of them are one <see cref="CrowdMesh"/> draw.
/// </summary>
public partial class PortCrowd : Node3D
{
    private const int Capacity = 56;
    private const int MostWalking = 40;

    private readonly List<GridPosition> _ends = [];
    private readonly List<(GridPosition Cell, Vector2 Facing)> _stops = [];
    private readonly List<Walker> _walkers = [];
    private readonly List<(Vector3 At, float Yaw, int Pose, int Look)> _gathered = [];
    private readonly Random _random = new(1519);
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
    }

    /// <summary>1 normally; less in rain (R2d sets it from the weather).</summary>
    public float Density { get; set; } = 1f;

    /// <summary>How many are about just now.</summary>
    public int Count => _walkers.Count + _gathered.Count;

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
        _walkers.Clear();
        _ends.Clear();
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
        }
    }

    /// <summary>
    /// Moves everyone on by <paramref name="minutes"/> of game time, starts new trips to keep the
    /// numbers right for <paramref name="hour"/>, and draws them.
    /// </summary>
    public void Update(float hour, float minutes, float wetness)
    {
        CrowdMesh.Material.SetShaderParameter("wetness", wetness);
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

        foreach (var walker in _walkers.ToList())
        {
            if (!Walk(walker, minutes))
            {
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

            var smoothFirst = Smooth(first);
            var stopAt = smoothFirst.Count - 1;
            var path = smoothFirst.Concat(Smooth(second).Skip(1)).ToList();
            return Begin(path, stopAt, s.Facing, hour);
        }

        var route = Pathfinder.FindPath(_location, from, to);
        return route is null ? null : Begin(Smooth(route), -1, Vector2.Zero, hour);
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

        var walker = new Walker
        {
            Path = path,
            Speed = (float)((_location.WalkCellsPerMinute ?? 2.5) * (0.85 + _random.NextDouble() * 0.3)),
            Phase = (float)_random.NextDouble() * Mathf.Tau,
            Pose = hour is > 6 and < 19 && _random.NextDouble() < 0.28 ? CrowdMesh.Carrying : CrowdMesh.Walking,
            Look = _random.Next(1000),
            Scale = 0.9f + (float)_random.NextDouble() * 0.15f,
            StopAt = stopAt,
            Pause = 2f + (float)_random.NextDouble() * 6f,
            Facing = facing,
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

            CrowdMesh.Set(multimesh, index++, new Vector3(at.X, _height(at.X, at.Y), at.Y), walker.Yaw, walker.Scale,
                walker.Phase, stopped ? 0f : 1f, walker.Pose, walker.Look, _shadows.Multimesh);
        }

        foreach (var (at, yaw, pose, look) in _gathered)
        {
            if (index >= Capacity)
            {
                break;
            }

            CrowdMesh.Set(multimesh, index++, at, yaw, 0.95f, (float)Time.GetTicksMsec() / 900f + look, 0f, pose, look, _shadows.Multimesh);
        }

        multimesh.VisibleInstanceCount = index;
        _shadows.Multimesh.VisibleInstanceCount = index;
    }

    // Straightens a grid path: from each point, on to the farthest one in plain sight.
    private List<Vector2> Smooth(IReadOnlyList<GridPosition> cells)
    {
        var points = new List<Vector2> { new(cells[0].X, cells[0].Y) };
        var i = 0;
        while (i < cells.Count - 1)
        {
            var j = cells.Count - 1;
            while (j > i + 1 && !Clear(cells[i], cells[j]))
            {
                j--;
            }

            points.Add(new Vector2(cells[j].X, cells[j].Y));
            i = j;
        }

        return points;
    }

    private bool Clear(GridPosition from, GridPosition to)
    {
        var a = new Vector2(from.X, from.Y);
        var b = new Vector2(to.X, to.Y);
        var steps = Mathf.CeilToInt(a.DistanceTo(b) / 0.2f);
        for (var s = 1; s < steps; s++)
        {
            var p = a.Lerp(b, s / (float)steps);
            foreach (var corner in new[] { new Vector2(-0.3f, -0.3f), new Vector2(0.3f, -0.3f), new Vector2(-0.3f, 0.3f), new Vector2(0.3f, 0.3f) })
            {
                var q = p + corner;
                if (!_location.IsWalkable(new GridPosition(Mathf.RoundToInt(q.X), Mathf.RoundToInt(q.Y))))
                {
                    return false;
                }
            }
        }

        return true;
    }
}

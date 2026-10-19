using Expedition.Simulation.Characters;
using Expedition.Simulation.Content;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Persistence;
using Expedition.Simulation.Scenarios;
using Expedition.Simulation.Tasks;
using Expedition.Simulation.World;
using Godot;

namespace Expedition.Game;

/// <summary>
/// Entry point of the graybox scene. Loads content, starts the test deck scenario, steps simulated
/// time, draws the state and turns player input into simulation commands.
/// The simulation is the only source of truth: this node holds view state only (selection, speed,
/// camera), so saving never needs anything from the scene.
/// </summary>
public partial class Main : Node3D
{
    /// <summary>Simulation ticks (in-game minutes) per real second for each speed; index 0 is paused (D-007).</summary>
    private static readonly int[] TicksPerSecond = [0, 1, 10, 60];

    // Never simulate more than this many ticks in one frame, so a slow frame cannot snowball.
    private const int MaxTicksPerFrame = 600;
    private const string QuickSaveSlot = "quicksave";

    private readonly Dictionary<CharacterId, CrewFigure> _figures = [];
    private GameContent _content = null!;
    private SimulationRunner _runner = null!;
    private SaveStore _saves = null!;
    private CameraRig _camera = null!;
    private Node3D _ship = null!;
    private Node3D _shipContents = null!;
    private ShipView _deck = null!;
    private SeaAndSky _world = null!;
    private readonly List<SeaBird> _birds = [];
    private float _seaState = 1f;
    private float _gloom;
    private float _sink;
    private int _chronicleSeen;
    private bool _endingShown;
    private ChronicleFormatter _formatter = null!;
    private readonly System.Random _flicker = new();
    private Hud _hud = null!;
    private CharacterId? _selected;
    private int _speedIndex;
    private int _lastRunningSpeed = 1;
    private double _tickFraction;
    private double _elapsed;
    private Vector3 _shipCentre;

    public override void _Ready()
    {
        try
        {
            _content = ContentLoader.LoadFromDirectory(DataDirectory());
        }
        catch (ContentValidationException ex)
        {
            ShowFatalError(ex.Message);
            return;
        }

        _saves = new SaveStore(ProjectSettings.GlobalizePath("user://saves"));
        BuildWorld();

        _hud = new Hud();
        AddChild(_hud);
        _hud.SpeedChosen += SetSpeed;
        _hud.SaveRequested += Save;
        _hud.LoadRequested += Load;
        _hud.CharacterChosen += id => _selected = id;
        _hud.TaskOrdered += OrderTask;
        _hud.StopOrdered += () => { if (_selected is { } id) _hud.ShowResult(_runner.CancelTask(id)); };
        _hud.DutyToggled += (taskId, on) =>
        {
            if (_selected is not { } id) return;
            var duties = _runner.Campaign.DutiesOf(id).Where(d => d != taskId).ToList();
            if (on) duties.Add(taskId);
            _hud.ShowResult(_runner.SetDuties(id, duties));
        };
        _hud.ContinueRequested += CarryOn;
        _hud.NewVoyageRequested += () =>
        {
            _hud.HideEnding();
            StartCampaign(TestDeckScenario.Create(_content, (ulong)System.Random.Shared.NextInt64()), "A new voyage. Fair winds.");
        };
        _hud.QueueCleared += () => { if (_selected is { } id) _hud.ShowResult(_runner.ClearQueue(id), "Queued orders cleared."); };

        // Each new voyage gets its own seed; the seed is saved, so a reported problem can be replayed.
        var seed = (ulong)System.Random.Shared.NextInt64();
        ReadDeveloperArguments(ref seed);
        StartCampaign(TestDeckScenario.Create(_content, seed), "New voyage started. Left-click to select, right-click to walk.");
        if (_startZoom is { } size)
        {
            _camera.Size = size;
        }

        if (_advanceOnStart > 0)
        {
            _runner.Advance(_advanceOnStart);
        }

        DeveloperForceVoyage();

        if (!string.IsNullOrEmpty(_screenshotPath))
        {
            TakeScreenshotAndQuit();
        }
    }

    public override void _Process(double delta)
    {
        if (_runner is null)
        {
            return;
        }

        _elapsed += delta;
        var speed = TicksPerSecond[_speedIndex];
        if (speed > 0)
        {
            _tickFraction += delta * speed;
            var ticks = (int)Math.Min(Math.Floor(_tickFraction), MaxTicksPerFrame);
            if (ticks > 0)
            {
                _runner.Advance(ticks);
                _tickFraction = Math.Min(_tickFraction - ticks, 0.999);
            }
        }

        CheckAlerts();

        // Weather sets the sea and sky; a filling bilge makes her ride lower (ART_BIBLE.md 6.3, 7.2).
        var voyage = _runner.Campaign.Voyage;
        var weather = voyage is null ? null : _content.Voyage?.Weathers.FirstOrDefault(w => w.Id == voyage.WeatherId);
        var step = Mathf.Clamp((float)delta * 0.6f, 0, 1);
        _seaState = Mathf.Lerp(_seaState, (float)(weather?.SeaState ?? 0.8), step);
        _gloom = Mathf.Lerp(_gloom, (float)(weather?.Gloom ?? 0), step);
        var targetSink = voyage is null ? 0f : voyage.Outcome == Expedition.Simulation.Voyage.VoyageOutcome.Foundered ? 3.2f : (float)voyage.Bilge / 100f * 0.8f;
        _sink = Mathf.Lerp(_sink, targetSink, Mathf.Clamp((float)delta * 0.4f, 0, 1));
        if (_gloom > 0.75f && speed > 0 && _flicker.NextDouble() < delta * 0.12)
        {
            _world.Flash();
        }

        // The ship rolls, pitches and heaves with the sea (ART_BIBLE.md 12). Visual only.
        var t = (float)_elapsed;
        var swell = 0.35f + _seaState * 0.65f;
        _ship.RotationDegrees = new Vector3(Mathf.Sin(t * 0.45f) * 0.8f * swell, 0, Mathf.Sin(t * 0.6f) * 1.6f * swell);
        _ship.Position = _shipCentre + new Vector3(0, Mathf.Sin(t * 0.7f) * 0.08f * swell - _sink, 0);

        var clock = _runner.Campaign.Clock;
        var hour = (float)((clock.Tick + (speed > 0 ? _tickFraction : 0)) % SimulationClock.TicksPerDay / SimulationClock.TicksPerHour);
        var hull = new Vector4(_deck.CentreX, (_deck.Length.Bow + _deck.Length.Stern) / 2f, _deck.HalfBeam, (_deck.Length.Stern - _deck.Length.Bow) / 2f);
        var making = voyage is { Outcome: Expedition.Simulation.Voyage.VoyageOutcome.UnderWay } ? 0.3f + (float)voyage.SpeedLeaguesPerHour * 1.3f : 0f;
        _world.UpdateWorld(hour, _seaState, making, (float)delta, _camera.Target, hull, _gloom);
        var cooking = _runner.Campaign.ActiveTasks.Values.Any(task => task.DefinitionId == "task.cook");
        // Sails turn see-through at working zoom so they never hide the crew (ART_BIBLE.md 3); zoom out to see them whole.
        _deck.UpdateLook(_world.Darkness, Mathf.Lerp(0.38f, 1f, Mathf.SmoothStep(18f, 32f, _camera.Size)), t, cooking, _seaState, (float)delta);
        foreach (var bird in _birds)
        {
            bird.Centre = _shipCentre + new Vector3(0, 0, -2f);
        }

        var drawTick = _runner.Campaign.Clock.Tick + (speed > 0 ? _tickFraction : 0);
        var animationRate = speed == 0 ? 0f : 1f + Mathf.Log(speed) * 0.25f;
        foreach (var (id, figure) in _figures)
        {
            var character = _runner.Campaign.Characters[id];
            if (_runner.Campaign.Placements.TryGetValue(id, out var placement))
            {
                _runner.Campaign.ActiveTasks.TryGetValue(id, out var task);
                var definition = task is null ? null : _content.Tasks[task.DefinitionId];
                var pose = character.SurvivalState is SurvivalState.Incapacitated or SurvivalState.Dead ? Pose.Lying
                    : placement.IsMoving ? Pose.Walking
                    : definition is { IsRest: true } ? Pose.Resting
                    : definition is not null ? Pose.Working
                    : Pose.Standing;
                figure.Refresh(character, placement, drawTick, id == _selected, Hud.Activity(_runner, character),
                    id == _runner.Campaign.CaptainId, pose, (float)delta, animationRate);
            }
        }

        _hud.Refresh(_runner, _selected, _speedIndex);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_runner is null)
        {
            return;
        }

        if (@event is InputEventMouseButton { Pressed: true } click)
        {
            if (click.ButtonIndex == MouseButton.Left)
            {
                _selected = FigureUnder(click.Position) ?? _selected;
            }
            else if (click.ButtonIndex == MouseButton.Right && _selected is { } id && CellUnder(click.Position) is { } cell)
            {
                _hud.ShowResult(_runner.MoveCharacter(id, cell));
            }
        }
        else if (@event is InputEventKey { Pressed: true, Echo: false } key)
        {
            // Cmd+S / Cmd+L on macOS (Ctrl elsewhere), since F-keys need Fn on a MacBook.
            if (key.IsCommandOrControlPressed() && key.Keycode is Key.S or Key.L)
            {
                if (key.Keycode == Key.S) Save(); else Load();
                return;
            }

            switch (key.Keycode)
            {
                case Key.Space when _hud.AlertShowing: CarryOn(); break;
                case Key.Space: SetSpeed(_speedIndex == 0 ? _lastRunningSpeed : 0); break;
                case Key.Key1: SetSpeed(0); break;
                case Key.Key2: SetSpeed(1); break;
                case Key.Key3: SetSpeed(2); break;
                case Key.Key4: SetSpeed(3); break;
                case Key.F5: Save(); break;
                case Key.F9: Load(); break;
                case Key.Tab: SelectNext(); break;
            }
        }
    }

    private void StartCampaign(SimulationRunner runner, string message)
    {
        _runner = runner;
        _tickFraction = 0;
        foreach (var figure in _figures.Values)
        {
            figure.QueueFree();
        }

        _figures.Clear();
        var location = _content.Locations[TestDeckScenario.LocationId];
        _deck.Build(location);

        // Roll around the middle of the deck rather than its corner.
        var centre = new Vector3((location.Width - 1) / 2f, 0, (location.Height - 1) / 2f);
        _shipCentre = centre;
        _ship.Position = centre;
        _shipContents.Position = -centre;
        _camera.Target = centre;

        foreach (var id in runner.Campaign.Placements.Keys.Order())
        {
            var character = runner.Campaign.Characters[id];
            var officer = id == runner.Campaign.CaptainId || character.StatusId is "status.officer" or "status.gentleman";
            var figure = CrewFigure.Create(character.Appearance, _content.Appearance, officer);
            _shipContents.AddChild(figure);
            _figures[id] = figure;
        }

        _selected = runner.Campaign.CaptainId ?? runner.Campaign.Characters.Keys.Order().FirstOrDefault();
        _formatter = new ChronicleFormatter(runner.Campaign, _content);
        _chronicleSeen = runner.Campaign.Chronicle.Entries.Count;
        _endingShown = runner.Campaign.Voyage is { Outcome: not Expedition.Simulation.Voyage.VoyageOutcome.UnderWay };
        _sink = 0;
        _hud.HideAlert();
        _hud.HideEnding();
        _hud.Bind(runner);
        _hud.ShowStatus(message);
    }

    // Danger stops the clock so the player can act: a storm breaking, the bilge rising, someone hurt.
    private void CheckAlerts()
    {
        var entries = _runner.Campaign.Chronicle.Entries;
        var alerts = new List<string>();
        for (; _chronicleSeen < entries.Count; _chronicleSeen++)
        {
            var entry = entries[_chronicleSeen];
            var urgent = entry.Kind switch
            {
                ChronicleEntryKind.WeatherChanged => entry.Get(ChronicleParameters.To) is "weather.gale" or "weather.storm",
                ChronicleEntryKind.HullStruck or ChronicleEntryKind.BilgeAlarm or ChronicleEntryKind.Injury => true,
                ChronicleEntryKind.SurvivalStateChanged => entry.Get(ChronicleParameters.To) is "Dead" or "Incapacitated",
                ChronicleEntryKind.SupplyShortage => true,
                _ => false,
            };
            if (urgent)
            {
                alerts.Add(_formatter.Format(entry));
            }
        }

        if (alerts.Count > 0 && _speedIndex > 0)
        {
            SetSpeed(0);
            _hud.ShowAlert(string.Join("\n", alerts.TakeLast(3)));
        }

        if (!_endingShown && _runner.Campaign.Voyage is { Outcome: not Expedition.Simulation.Voyage.VoyageOutcome.UnderWay } ship)
        {
            _endingShown = true;
            SetSpeed(0);
            _hud.HideAlert();
            ShowEnding(ship);
        }
    }

    private void ShowEnding(Expedition.Simulation.Voyage.VoyageState ship)
    {
        var crew = _runner.Campaign.Characters.Values.ToList();
        var alive = crew.Count(c => c.SurvivalState is SurvivalState.Alive or SurvivalState.Incapacitated);
        var dead = crew.Where(c => c.SurvivalState == SurvivalState.Dead).Select(c => c.Name).ToList();
        var days = _runner.Campaign.Clock.Tick / (double)SimulationClock.TicksPerDay;
        var (food, water) = _runner.SupplyForecast();
        var losses = dead.Count == 0 ? "Every soul aboard is still alive." : $"Lost on the way: {string.Join(", ", dead)}.";
        if (ship.Outcome == Expedition.Simulation.Voyage.VoyageOutcome.Arrived)
        {
            _hud.ShowEnding("Land!", $"After {days:0.#} days at sea, {ship.ShipName} raises {ship.Destination}.\n\n" +
                $"{losses} {alive} of {crew.Count} come to the coast, with water for about {water:0} days and food for about {food:0}. " +
                $"The hull stands at {ship.Hull:0}% and the rigging at {ship.Rigging:0}%.\n\n" +
                "What waits ashore, and what became of the old factory at Punta Ceniza, is still to be written.");
        }
        else
        {
            var boat = dead.Count == 0 ? $"All {alive} got into the ship's boat as she went." : $"{losses} The {alive} left alive got into the ship's boat as she went.";
            _hud.ShowEnding("Foundered", $"On day {Math.Floor(days) + 1:0}, {ship.LeaguesRemaining:0} leagues short of {ship.Destination}, {ship.ShipName} filled faster " +
                $"than her crew could pump and went down.\n\n{boat} What becomes of them is a story for a later version.\n\n" +
                "Watch the bilge, keep hands on the pumps in heavy weather, and patch the hull while there is oakum and plank to do it.");
        }
    }

    private void CarryOn()
    {
        _hud.HideAlert();
        SetSpeed(_lastRunningSpeed);
    }

    private void OrderTask(string taskId, TaskPriority priority)
    {
        if (_selected is not { } id)
        {
            _hud.ShowStatus("Select someone first.");
            return;
        }

        var name = _runner.Campaign.Characters[id].Name;
        var task = _content.Tasks[taskId].Name;
        var startsNow = !_runner.Campaign.ActiveTasks.ContainsKey(id) && _runner.Campaign.QueueOf(id).Count == 0;
        var message = priority == TaskPriority.Urgent ? $"{name}: {task}, urgently." : startsNow ? $"{name}: {task}." : $"{name} will {task.ToLowerInvariant()} next.";
        _hud.ShowResult(_runner.QueueTask(id, taskId, priority), message);
    }

    private void SetSpeed(int index)
    {
        _speedIndex = index;
        if (index > 0)
        {
            _lastRunningSpeed = index;
        }
    }

    private void Save()
    {
        try
        {
            _saves.Write(QuickSaveSlot, SaveSerializer.Serialize(_runner.Campaign));
            _hud.ShowStatus($"Saved to {_saves.Directory}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _hud.ShowStatus($"Save failed: {ex.Message}");
        }
    }

    private void Load()
    {
        try
        {
            var campaign = SaveSerializer.Deserialize(_saves.Read(QuickSaveSlot), _content);
            var speed = _speedIndex;
            StartCampaign(new SimulationRunner(campaign, _content), "Loaded the quick save.");
            SetSpeed(speed);
        }
        catch (SaveLoadException ex)
        {
            // The current campaign keeps running; a failed load never replaces it.
            _hud.ShowStatus(ex.Message);
        }
    }

    private void SelectNext()
    {
        var ids = _runner.Campaign.Characters.Keys.Order().ToList();
        if (ids.Count > 0)
        {
            var index = _selected is { } current ? ids.IndexOf(current) : -1;
            _selected = ids[(index + 1) % ids.Count];
        }
    }

    private CharacterId? FigureUnder(Vector2 screenPosition)
    {
        // Compare against the ground under the cursor and at chest height, so clicking a figure's
        // body or its feet both work.
        CharacterId? best = null;
        var bestDistance = 0.8f;
        foreach (var height in new[] { 0f, 1f })
        {
            if (_camera.GroundPointUnder(screenPosition, height) is not { } point)
            {
                continue;
            }

            foreach (var (id, figure) in _figures)
            {
                var offset = figure.GlobalPosition - point;
                var distance = new Vector2(offset.X, offset.Z).Length();
                if (distance < bestDistance)
                {
                    best = id;
                    bestDistance = distance;
                }
            }
        }

        return best;
    }

    private GridPosition? CellUnder(Vector2 screenPosition) =>
        _camera.GroundPointUnder(screenPosition) is { } point
            ? new GridPosition(Mathf.RoundToInt(point.X), Mathf.RoundToInt(point.Z))
            : null;

    private void BuildWorld()
    {
        _world = new SeaAndSky { Name = "SeaAndSky" };
        AddChild(_world);

        _ship = new Node3D { Name = "Ship" };
        AddChild(_ship);
        _shipContents = new Node3D { Name = "ShipContents" };
        _ship.AddChild(_shipContents);
        _deck = new ShipView { Name = "ShipView" };
        _shipContents.AddChild(_deck);

        for (var i = 0; i < 3; i++)
        {
            var bird = SeaBird.Create(Vector3.Zero, 9f + i * 4f, (i % 2 == 0 ? 0.35f : -0.28f) + i * 0.03f, i * 2.1f, 9f + i * 2.5f);
            AddChild(bird);
            _birds.Add(bird);
        }

        _camera = new CameraRig { Name = "Camera" };
        AddChild(_camera);
    }

    // Developer options, passed after "--" on the Godot command line, for checking the scene without a
    // person at the screen: --seed=N, --speed=0..3, --advance=MINUTES (simulate before the first frame),
    // --screenshot=PATH (save the screen after a few seconds, then quit), --hour=H (start at that hour).
    private string? _screenshotPath;
    private int _advanceOnStart;
    private float? _startZoom;
    private string? _forceWeather;
    private bool _idleCrew;
    private double? _forceBilge;

    private void ReadDeveloperArguments(ref ulong seed)
    {
        foreach (var argument in OS.GetCmdlineUserArgs())
        {
            var parts = argument.TrimStart('-').Split('=', 2);
            var value = parts.Length > 1 ? parts[1] : "";
            switch (parts[0])
            {
                case "seed" when ulong.TryParse(value, out var number): seed = number; break;
                case "speed" when int.TryParse(value, out var speed): SetSpeed(Math.Clamp(speed, 0, TicksPerSecond.Length - 1)); break;
                case "advance" when int.TryParse(value, out var minutes): _advanceOnStart = minutes; break;
                case "screenshot": _screenshotPath = value; break;
                case "weather": _forceWeather = value; break;
                case "idle": _idleCrew = true; break;
                case "bilge" when double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var bilge): _forceBilge = bilge; break;
                case "zoom" when float.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var zoom): _startZoom = zoom; break;
            }
        }
    }

    // For screenshots of weather and danger without waiting for them.
    private void DeveloperForceVoyage()
    {
        if (_runner.Campaign.Voyage is not { } ship)
        {
            return;
        }

        if (_forceWeather is not null)
        {
            ship.DeveloperSetWeather(_forceWeather);
            _seaState = (float)(_content.Voyage?.Weathers.FirstOrDefault(w => w.Id == _forceWeather)?.SeaState ?? 1);
            _gloom = (float)(_content.Voyage?.Weathers.FirstOrDefault(w => w.Id == _forceWeather)?.Gloom ?? 0);
        }

        if (_idleCrew)
        {
            foreach (var id in _runner.Campaign.Characters.Keys)
            {
                _runner.SetDuties(id, []);
            }
        }

        if (_forceBilge is { } bilge)
        {
            ship.DeveloperSetBilge(bilge);
            _sink = (float)bilge / 100f * 0.8f;
        }
    }

    private async void TakeScreenshotAndQuit()
    {
        await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(_screenshotPath);
        GetTree().Quit();
    }

    private void ShowFatalError(string message)
    {
        GD.PushError(message);
        var layer = new CanvasLayer();
        AddChild(layer);
        layer.AddChild(new Label
        {
            Text = "The game content could not be loaded:\n\n" + message,
            Position = new Vector2(20, 20),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(1000, 0),
        });
    }

    /// <summary>
    /// The repository's <c>data/</c> folder, next to the Godot project. This works when running from the
    /// editor. Exported builds will need the data packaged with them, which is a later ticket.
    /// </summary>
    private static string DataDirectory() =>
        Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "data"));
}

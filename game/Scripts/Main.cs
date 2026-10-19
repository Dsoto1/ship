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
/// Entry point of the game scene. Loads content, starts a campaign in port (or the test deck), steps
/// simulated time, draws the state and turns player input into simulation commands. In port the
/// waterfront and the moored Garza are drawn together and the captain walks the town (R2a); at sea the
/// Garza is drawn alone on the ocean.
/// The simulation is the only source of truth: this node holds view state only (selection, speed,
/// camera), so saving never needs anything from the scene.
/// </summary>
public partial class Main : Node3D
{
    /// <summary>Simulation ticks (in-game minutes) per real second for each speed; index 0 is paused (D-007).</summary>
    private static readonly double[] VoyageSpeeds = [0, 1, 10, 60, 240, 720];

    /// <summary>Slower in port, so people can be watched going about their day (D-023).</summary>
    private static readonly double[] PortSpeeds = [0, 0.5, 2, 10, 60, 240];

    private static readonly string[] VoyageSpeedNames = ["Pause", "1×", "10×", "60×", "240×", "720×"];
    private static readonly string[] PortSpeedNames = ["Pause", "Watch", "2×", "10×", "60×", "240×"];

    private double[] TicksPerSecond => _runner?.Campaign.Port is not null ? PortSpeeds : VoyageSpeeds;

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
    private PortHud _portHud = null!;
    private PortView _port = null!;
    private Node3D _portFigures = null!;
    private bool _portBuilt;
    private bool _follow = true;
    private CharacterId? _hovered;
    private PendingAction? _pending;
    private int _crewSeen;

    // Something the captain walks over to do: talk, ask, test or offer.
    private sealed record PendingAction(CharacterId Target, string Description, Action Act, long GiveUpTick)
    {
        public GridPosition? Heading { get; set; }
    }
    private CharacterId? _selected;
    private int _speedIndex = 1;
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
        _hud.EventChoiceMade += choice =>
        {
            var (result, resolution) = _runner.ResolveEvent(choice);
            if (resolution is null)
            {
                _hud.ShowResult(result);
                return;
            }

            // The decision's own consequences are in the outcome text; do not repeat them as alerts.
            _chronicleSeen = _runner.Campaign.Chronicle.Entries.Count;
            _hud.ShowEventOutcome(resolution.Choice, resolution.Succeeded, resolution.Text);
        };
        _hud.EventClosed += () =>
        {
            _hud.HideEvent();
            CarryOn();
        };
        _hud.SailPlanChosen += plan => _hud.ShowResult(_runner.SetSailPlan(plan), plan switch
        {
            Expedition.Simulation.Voyage.SailPlan.PressOn => "Every sail set: press on.",
            Expedition.Simulation.Voyage.SailPlan.HoveTo => "Hove to: riding it out.",
            _ => "Sail set for the weather.",
        });
        _hud.RationingToggled += on => _hud.ShowResult(_runner.SetRationing(on), on ? "Short rations ordered." : "Full rations restored.");
        _hud.NewVoyageRequested += () =>
        {
            _hud.HideEnding();
            StartCampaign(ExpeditionScenario.Create(_content, (ulong)System.Random.Shared.NextInt64()), "A new voyage. Choose your crew.");
        };
        _hud.QueueCleared += () => { if (_selected is { } id) _hud.ShowResult(_runner.ClearQueue(id), "Queued orders cleared."); };

        _portHud = new PortHud();
        AddChild(_portHud);
        _portHud.CardClosed += () => _selected = _runner.Campaign.CaptainId;
        _portHud.TalkRequested += id => WalkOverTo(id, "talk", () =>
        {
            var (result, said) = _runner.TalkTo(id);
            _portHud.Say(result.Succeeded ? said : result.Reason);
        });
        _portHud.AskRequested += (informant, about) => WalkOverTo(informant, $"ask about {_runner.Campaign.Characters[about].Name}", () =>
        {
            var (result, said) = _runner.AskAbout(informant, about);
            _portHud.Say(result.Succeeded ? said : result.Reason);
        });
        _portHud.DemonstrationRequested += (id, skill) => WalkOverTo(id, $"ask for a show of {_content.Skills[skill].Name.ToLowerInvariant()}", () =>
        {
            var result = _runner.RequestDemonstration(id, skill);
            _portHud.Say(result.Succeeded ? $"{_runner.Campaign.Characters[id].Name} nods and goes to show you. Stay and watch it done." : result.Reason);
        });
        _portHud.OfferRequested += (id, advance) => WalkOverTo(id, $"offer a berth for {advance} sueldos", () =>
        {
            var name = _runner.Campaign.Characters[id].Name;
            var result = _runner.Offer(id, advance);
            _portHud.Say(result.Succeeded ? $"{name} spits in a palm and shakes on it, and goes to stow a sea chest aboard the Garza." : result.Reason);
        });
        _portHud.BuyRequested += lot => _hud.ShowResult(_runner.Buy(lot), "Bought. The chandler's boy will carry it aboard.");
        _portHud.SetSailRequested += () =>
        {
            var result = ExpeditionScenario.SetSail(_runner);
            if (!result.Succeeded)
            {
                _portHud.Say(result.Reason);
                return;
            }

            ShowWorld();
            _hud.Bind(_runner);
            _hud.SetSpeedNames(VoyageSpeedNames);
            _hud.ShowStatus("Lines cast off. La Bocana falls astern.");
        };

        // Each new voyage gets its own seed; the seed is saved, so a reported problem can be replayed.
        var seed = (ulong)System.Random.Shared.NextInt64();
        ReadDeveloperArguments(ref seed);
        StartCampaign(_testDeck ? TestDeckScenario.Create(_content, seed) : ExpeditionScenario.Create(_content, seed),
            "La Bocana. Right-click to walk; click anyone to look at them.");
        if (_startZoom is { } size)
        {
            _camera.Size = size;
        }

        if (_advanceOnStart > 0)
        {
            _runner.Advance(_advanceOnStart);
        }

        DeveloperPort();

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
        CheckEvent();
        CarryOutPending();
        if (_runner.Campaign.Crew.Count() != _crewSeen)
        {
            _crewSeen = _runner.Campaign.Crew.Count();
            _hud.Bind(_runner);
        }

        SyncFigures();
        var inPort = _runner.Campaign.Port is not null;
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
        var swell = inPort ? 0.15f : 0.35f + _seaState * 0.65f;
        _ship.RotationDegrees = new Vector3(Mathf.Sin(t * 0.45f) * 0.8f * swell, inPort ? -90f : 0, Mathf.Sin(t * 0.6f) * 1.6f * swell);
        _ship.Position = (inPort ? _port.Berth : _shipCentre) + new Vector3(0, Mathf.Sin(t * 0.7f) * 0.08f * swell - _sink, 0);

        var clock = _runner.Campaign.Clock;
        var hour = (float)((clock.Tick + (speed > 0 ? _tickFraction : 0)) % SimulationClock.TicksPerDay / SimulationClock.TicksPerHour);
        var hull = inPort
            ? new Vector4(9999, 9999, 0.1f, 0.1f)
            : new Vector4(_deck.CentreX, (_deck.Length.Bow + _deck.Length.Stern) / 2f, _deck.HalfBeam, (_deck.Length.Stern - _deck.Length.Bow) / 2f);
        if (inPort)
        {
            _seaState = 0.35f;
            FollowCaptain((float)delta);
            _port.UpdateLook(_world.Darkness, t);
        }
        var making = voyage is { Outcome: Expedition.Simulation.Voyage.VoyageOutcome.UnderWay } ? 0.3f + (float)voyage.SpeedLeaguesPerHour * 1.3f : 0f;
        _world.UpdateWorld(hour, _seaState, making, (float)delta, _camera.Target, hull, _gloom);
        var cooking = _runner.Campaign.ActiveTasks.Values.Any(task => task.DefinitionId == "task.cook");
        // Sails turn see-through at working zoom so they never hide the crew (ART_BIBLE.md 3); zoom out to see them whole.
        _deck.UpdateLook(_world.Darkness, Mathf.Lerp(0.38f, 1f, Mathf.SmoothStep(18f, 32f, _camera.Size)), t, cooking, inPort ? 3.2f : SailSetting(voyage), (float)delta);
        foreach (var bird in _birds)
        {
            bird.Centre = inPort ? _port.Berth + new Vector3(0, 4f, -4f) : _shipCentre + new Vector3(0, 0, -2f);
        }

        var drawTick = _runner.Campaign.Clock.Tick + (speed > 0 ? _tickFraction : 0);
        var animationRate = speed == 0 ? 0f : Mathf.Max(0.8f, 1f + Mathf.Log((float)speed) * 0.25f);
        foreach (var (id, figure) in _figures)
        {
            var character = _runner.Campaign.Characters[id];
            if (_runner.Campaign.Placements.TryGetValue(id, out var placement))
            {
                _runner.Campaign.ActiveTasks.TryGetValue(id, out var task);
                var definition = task is null ? null : _content.Tasks[task.DefinitionId];
                var location = _content.Locations[placement.LocationId];
                var spot = task?.SpotId is { } spotId ? location.FindSpot(spotId) : null;
                var carrying = definition?.Carries;
                var pose = character.SurvivalState is SurvivalState.Incapacitated or SurvivalState.Dead ? Pose.Lying
                    : carrying is not null ? Pose.Carrying
                    : placement.IsMoving ? Pose.Walking
                    : definition is { IsRest: true } ? Pose.Resting
                    : definition is not null ? WorkPose(definition.Id, spot?.Kind)
                    : Pose.Standing;
                var inside = character.Membership == Membership.Local && _runner.IsIndoors(id);
                figure.Visible = character.SurvivalState != SurvivalState.Missing && !inside;
                var onPort = placement.LocationId == _runner.Content.Port?.Location;
                var isCaptain = id == _runner.Campaign.CaptainId;
                figure.Refresh(character, placement, drawTick, id == _selected, Hud.Activity(_runner, character),
                    isCaptain, pose, (float)delta, animationRate,
                    onPort ? _port.HeightAt : null, carrying,
                    showLabel: !inPort || isCaptain || id == _hovered, shownName: inPort ? PortHud.KnownName(_runner, id) : null);
            }
        }

        _hud.Refresh(_runner, _selected, _speedIndex);
        _portHud.Refresh(_runner, _selected);
    }

    // How someone looks while working, from the kind of place and the work (PORT_EXPERIENCE §8).
    private static Pose WorkPose(string task, string? kind) => task switch
    {
        "task.drink" or "task.gamble" => kind == "SitPoint" ? Pose.Sitting : Pose.Drinking,
        "task.pray" => Pose.Praying,
        "task.sell_fish" or "task.trade_goods" or "task.dun_debtor" or "task.serve_drinks" or "task.play" or "task.meet_in_secret" => Pose.Talking,
        "task.watch_water" or "task.stand_guard" => Pose.Watching,
        "task.read_notices" => Pose.Standing,
        "task.wait_in_irons" or "task.treat_patients" or "task.keep_accounts" or "task.study_charts" => kind == "SitPoint" ? Pose.Sitting : Pose.Working,
        _ => kind switch
        {
            "HammerStation" => Pose.Hammering,
            "SawStation" => Pose.Sawing,
            "RopeWork" => Pose.Roping,
            "SitPoint" => Pose.Sitting,
            "LeanPoint" or "WatchWater" => Pose.Watching,
            _ => Pose.Working,
        },
    };

    // The camera keeps the captain in view in port until the player pans away; F brings it back.
    private void FollowCaptain(float delta)
    {
        if (_camera.Panned)
        {
            _camera.Panned = false;
            _follow = false;
        }

        if (!_follow || _runner.Campaign.CaptainId is not { } captain || !_figures.TryGetValue(captain, out var figure))
        {
            return;
        }

        _camera.Target = _camera.Target.Lerp(figure.GlobalPosition, Mathf.Clamp(delta * 2.5f, 0, 1));
    }

    // Walks the captain over to someone, then does what was asked once within speaking distance.
    private void WalkOverTo(CharacterId target, string description, Action act)
    {
        var name = PortHud.KnownName(_runner, target);
        if (_runner.IsIndoors(target))
        {
            _portHud.Say($"{name} is indoors, out of reach. Try later.");
            return;
        }

        _pending = new PendingAction(target, description, act, _runner.Campaign.Clock.Tick + 3 * SimulationClock.TicksPerHour);
        _portHud.Say("");
        CarryOutPending();
    }

    private void CarryOutPending()
    {
        if (_pending is not { } pending || _runner.Campaign.CaptainId is not { } captain)
        {
            _portHud.ShowPending(null);
            return;
        }

        var name = PortHud.KnownName(_runner, pending.Target);
        if (_runner.WhyCannotSpeak(pending.Target) is null)
        {
            _pending = null;
            _portHud.ShowPending(null);
            pending.Act();
            return;
        }

        if (!_runner.Campaign.Placements.TryGetValue(pending.Target, out var there) || _runner.IsIndoors(pending.Target)
            || _runner.Campaign.Clock.Tick > pending.GiveUpTick)
        {
            _pending = null;
            _portHud.ShowPending(null);
            _portHud.Say($"You lose sight of {name}.");
            return;
        }

        // Head for a free cell beside them, and re-plan if they have moved on (a docker with a crate).
        var tick = _runner.Campaign.Clock.Tick;
        var cell = there.CellAt(tick);
        var here = _runner.Campaign.Placements[captain];
        if (pending.Heading is { } heading && here.IsMoving && Math.Max(Math.Abs(heading.X - cell.X), Math.Abs(heading.Y - cell.Y)) <= 1)
        {
            _portHud.ShowPending($"Walking over to {pending.Description}…");
            return;
        }

        var location = _content.Locations[there.LocationId];
        var taken = _runner.Campaign.Placements.Where(p => p.Key != captain && p.Value.LocationId == there.LocationId)
            .Select(p => p.Value.CellAt(tick)).ToHashSet();
        var here0 = here.CellAt(tick);
        var beside = new[] { (0, 1), (1, 0), (-1, 0), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1) }
            .Select(d => new GridPosition(cell.X + d.Item1, cell.Y + d.Item2))
            .Where(c => location.IsWalkable(c) && !taken.Contains(c))
            .OrderBy(c => Math.Abs(c.X - here0.X) + Math.Abs(c.Y - here0.Y));
        foreach (var next in beside)
        {
            if (_runner.MoveCharacter(captain, next).Succeeded)
            {
                break;
            }
        }

        pending.Heading = cell;
        _portHud.ShowPending($"Walking over to {pending.Description}…");
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
            else if (click.ButtonIndex == MouseButton.Right && _runner.Campaign.Port is not null && _runner.Campaign.CaptainId is { } captain
                     && CellUnder(click.Position) is { } portCell)
            {
                // In port, right-click always walks the captain; it also cancels walking over to someone.
                _pending = null;
                _follow = true;
                _hud.ShowResult(_runner.MoveCharacter(captain, portCell));
            }
            else if (click.ButtonIndex == MouseButton.Right && _selected is { } id && CellUnder(click.Position) is { } cell)
            {
                _hud.ShowResult(_runner.MoveCharacter(id, cell));
            }
        }
        else if (@event is InputEventMouseMotion motion)
        {
            _hovered = FigureUnder(motion.Position);
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
                case Key.Space when _hud.EventShowing: break;
                case Key.Space when _hud.AlertShowing: CarryOn(); break;
                case Key.Space: SetSpeed(_speedIndex == 0 ? _lastRunningSpeed : 0); break;
                case Key.Key1: SetSpeed(0); break;
                case Key.Key2: SetSpeed(1); break;
                case Key.Key3: SetSpeed(2); break;
                case Key.Key4: SetSpeed(3); break;
                case Key.Key5: SetSpeed(4); break;
                case Key.Key6: SetSpeed(5); break;
                case Key.F5: Save(); break;
                case Key.F9: Load(); break;
                case Key.Tab: SelectNext(); break;
                case Key.F: _follow = true; break;
                case Key.Escape: _selected = _runner.Campaign.CaptainId; _pending = null; break;
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
        _pending = null;
        var location = _content.Locations[TestDeckScenario.LocationId];
        _deck.Build(location);
        if (_content.Port is { } rules && !_portBuilt)
        {
            _port.Build(_content.Locations[rules.Location]);
            _portBuilt = true;
        }

        // Roll around the middle of the deck rather than its corner.
        var centre = new Vector3((location.Width - 1) / 2f, 0, (location.Height - 1) / 2f);
        _shipCentre = centre;
        _ship.Position = centre;
        _shipContents.Position = -centre;
        _camera.Target = centre;
        ShowWorld();
        _follow = true;
        if (runner.Campaign.Port is not null && runner.Campaign.CaptainId is { } captain && runner.Campaign.Placements.TryGetValue(captain, out var start))
        {
            _camera.Target = new Vector3(start.Cell.X, 0, start.Cell.Y);
            _camera.Size = 24f;
        }

        _crewSeen = runner.Campaign.Crew.Count();
        SyncFigures();

        _selected = runner.Campaign.CaptainId ?? runner.Campaign.Characters.Keys.Order().FirstOrDefault();
        _formatter = new ChronicleFormatter(runner.Campaign, _content);
        _chronicleSeen = runner.Campaign.Chronicle.Entries.Count;
        _endingShown = runner.Campaign.Voyage is { Outcome: not Expedition.Simulation.Voyage.VoyageOutcome.UnderWay };
        _sink = 0;
        _hud.HideAlert();
        _hud.HideEnding();
        _hud.HideEvent();
        _hud.Bind(runner);
        _portHud.Bind(runner);
        _hud.SetSpeedNames(runner.Campaign.Port is not null ? PortSpeedNames : VoyageSpeedNames);
        _hud.ShowStatus(message);
    }

    // The waterfront is drawn while in port, and the sea alone once she has sailed.
    private void ShowWorld()
    {
        var inPort = _runner.Campaign.Port is not null;
        _port.Visible = inPort;
        _portFigures.Visible = inPort;
        if (!inPort)
        {
            _ship.RotationDegrees = Vector3.Zero;
            _ship.Position = _shipCentre;
            _camera.Target = _shipCentre;
            _follow = false;
        }
    }

    // A figure for everyone placed somewhere, drawn where they are: on the waterfront or aboard. Someone
    // hired moves from the quay to the Garza's deck; someone who left the port is no longer drawn.
    private void SyncFigures()
    {
        foreach (var id in _figures.Keys.Where(id => !_runner.Campaign.Placements.ContainsKey(id)).ToList())
        {
            _figures[id].QueueFree();
            _figures.Remove(id);
        }

        foreach (var (id, placement) in _runner.Campaign.Placements.OrderBy(p => p.Key))
        {
            if (!_figures.TryGetValue(id, out var figure))
            {
                var character = _runner.Campaign.Characters[id];
                var officer = id == _runner.Campaign.CaptainId || character.StatusId is "status.officer" or "status.gentleman";
                figure = CrewFigure.Create(character.Appearance, _content.Appearance, officer);
                _figures[id] = figure;
            }

            var parent = placement.LocationId == _content.Port?.Location ? _portFigures : _shipContents;
            if (figure.GetParent() != parent)
            {
                figure.GetParent()?.RemoveChild(figure);
                parent.AddChild(figure);
            }
        }
    }


    // How much sail shows: hove to, nearly everything is furled; pressing on, the topsails stay up longer.
    private float SailSetting(Expedition.Simulation.Voyage.VoyageState? voyage) => voyage?.SailPlan switch
    {
        Expedition.Simulation.Voyage.SailPlan.HoveTo => 3.2f,
        Expedition.Simulation.Voyage.SailPlan.PressOn => Mathf.Max(0.2f, _seaState - 0.9f),
        _ => _seaState,
    };

    // An event stops the clock until the captain decides.
    private void CheckEvent()
    {
        if (_runner.Campaign.PendingEvent is null || _hud.EventShowing)
        {
            return;
        }

        if (_runner.DescribePendingEvent() is not { } description)
        {
            return;
        }

        SetSpeed(0);
        _hud.HideAlert();
        var choices = _runner.OfferedChoices().Select(offered =>
        {
            var text = EventText.Fill(offered.Choice.Text, _runner.Campaign, _runner.Campaign.PendingEvent!);
            var note = !offered.Available ? $"needs {offered.Choice.RequiresAmount} {_content.Items[offered.Choice.RequiresItem!].Name}"
                : offered.Chance is { } chance ? SimulationRunner.ChanceInWords(chance) : null;
            return (offered.Choice.Id, text, offered.Available, note);
        }).ToList();
        _hud.ShowEvent(description.Title, description.Text, choices);
        if (_developerChoice is { } developerChoice)
        {
            _developerChoice = null;
            _hud.DeveloperChoose(developerChoice);
        }
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
        var dead = crew.Where(c => c.SurvivalState is SurvivalState.Dead or SurvivalState.Missing)
            .Select(c => c.SurvivalState == SurvivalState.Missing ? $"{c.Name} (lost at sea)" : c.Name).ToList();
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
        else if (ship.Outcome == Expedition.Simulation.Voyage.VoyageOutcome.AllHandsLost)
        {
            _hud.ShowEnding("All hands lost", $"On day {Math.Floor(days) + 1:0}, {ship.LeaguesRemaining:0} leagues short of {ship.Destination}, the last of " +
                $"{ship.ShipName}'s company is gone. She drifts on, empty.\n\n{losses}\n\n" +
                "Keep the water and food coming, rest the exhausted before they collapse, and think twice about the choices that cost lives.");
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

        if (index > 0 && _runner?.Campaign.PendingEvent is not null)
        {
            _hud.ShowStatus("The captain must decide first.");
            return;
        }

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
        var ids = _runner.Campaign.Crew.Select(c => c.Id).Order().ToList();
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
                if (!figure.IsVisibleInTree())
                {
                    continue;
                }

                var offset = figure.GlobalPosition - new Vector3(0, figure.GlobalPosition.Y - height, 0) - point;
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

    private GridPosition? CellUnder(Vector2 screenPosition)
    {
        if (_camera.GroundPointUnder(screenPosition) is not { } point)
        {
            return null;
        }

        // On the raised terrace, look again at its height.
        if (_runner.Campaign.Port is not null && _camera.GroundPointUnder(screenPosition, 2.6f) is { } high
            && _port.HeightAt(high.X, high.Z) > 2.5f)
        {
            point = high;
        }

        return new GridPosition(Mathf.RoundToInt(point.X), Mathf.RoundToInt(point.Z));
    }

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

        _port = new PortView { Name = "PortView" };
        AddChild(_port);
        _portFigures = new Node3D { Name = "PortFigures" };
        AddChild(_portFigures);

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
    private bool _testDeck;
    private string? _developerChoice;
    private Expedition.Simulation.Voyage.SailPlan? _developerSail;
    private double? _forceBilge;
    private string? _developerSelect;
    private string? _developerLook;
    private bool _developerTalk;
    private int? _developerOffer;
    private float? _developerYaw;

    // For screenshots of the port: --select=person.velho opens their card (and --talk walks the captain
    // over and talks, at once; --offer=N then offers that advance); --look=x,z points the camera; --yaw=degrees turns it.
    private void DeveloperPort()
    {
        if (_developerSelect is { } person && _runner.CharacterFor(person) is { } character)
        {
            _selected = character.Id;
            if (_developerTalk && _runner.Campaign.CaptainId is { } captain && _runner.Campaign.Placements.TryGetValue(character.Id, out var there))
            {
                var cell = there.CellAt(_runner.Campaign.Clock.Tick);
                var location = _content.Locations[there.LocationId];
                foreach (var d in new[] { (0, 1), (1, 0), (-1, 0), (0, -1) })
                {
                    var next = new GridPosition(cell.X + d.Item1, cell.Y + d.Item2);
                    if (location.IsWalkable(next) && _runner.MoveCharacter(captain, next).Succeeded)
                    {
                        break;
                    }
                }

                for (var i = 0; i < 60 && _runner.WhyCannotSpeak(character.Id) is not null; i++)
                {
                    _runner.Advance(1);
                }

                var (_, said) = _runner.TalkTo(character.Id);
                _portHud.Say(said);
                if (_developerOffer is { } advance)
                {
                    var offered = _runner.Offer(character.Id, advance);
                    _portHud.Say(offered.Succeeded ? $"{character.Name} signs on." : offered.Reason);
                    _runner.Advance(5);
                }
            }
        }

        if (_developerLook?.Split(',') is [var x, var z] && float.TryParse(x, System.Globalization.CultureInfo.InvariantCulture, out var lookX)
            && float.TryParse(z, System.Globalization.CultureInfo.InvariantCulture, out var lookZ))
        {
            _camera.Target = new Vector3(lookX, 0, lookZ);
            _follow = false;
        }

        if (_developerYaw is { } yaw)
        {
            _camera.SetYaw(yaw);
        }
    }

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
                case "testdeck": _testDeck = true; break;
                case "choose": _developerChoice = value; break;
                case "sail" when Enum.TryParse<Expedition.Simulation.Voyage.SailPlan>(value, out var plan): _developerSail = plan; break;
                case "bilge" when double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var bilge): _forceBilge = bilge; break;
                case "zoom" when float.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var zoom): _startZoom = zoom; break;
                case "select": _developerSelect = value; break;
                case "look": _developerLook = value; break;
                case "talk": _developerTalk = true; break;
                case "offer" when int.TryParse(value, out var advance): _developerOffer = advance; break;
                case "yaw" when float.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var yaw): _developerYaw = yaw; break;
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

        if (_developerSail is { } sail)
        {
            _runner.SetSailPlan(sail);
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

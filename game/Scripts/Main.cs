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

    private static readonly string[] VoyageSpeedTips =
    [
        "Paused: give orders, read, plan", "One game minute a second: watch the work", "Ten minutes a second",
        "An hour a second: routine sailing", "Four hours a second", "Twelve hours a second: open ocean, nothing near",
    ];

    private static readonly string[] PortSpeedTips =
    [
        "Paused: give orders, read, plan", "Watch: half a game minute a second, people at walking pace", "Two minutes a second",
        "Ten minutes a second", "An hour a second", "Four hours a second",
    ];

    // After an interruption the game never resumes faster than this (UI_UX §16).
    private const int SafeResumeSpeed = 2;

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
    private float _portFog;
    private float _portWind;
    private string? _weatherShown;
    private float _portWet;
    private float _sink;
    private int _chronicleSeen;
    private bool _endingShown;
    private ChronicleFormatter _formatter = null!;
    private readonly System.Random _flicker = new();
    private Hud _hud = null!;
    private PortHud _portHud = null!;
    private PortView _port = null!;
    private PortSound _portSound = null!;
    private Node3D _portFigures = null!;
    private PortCrowd _crowd = null!;
    private HarbourView _harbour = null!;
    private AmbientScenes _ambient = null!;
    private DepartureSequence _departure = null!;
    private FrameProfiler _profiler = null!;
    private string? _stageShown;
    private Vector3 _profileCentre;

    // The four validation scenes of PORT_EXPERIENCE §21 (R3), each staged the same way every time: a
    // fixed seed, the hour, the weather, the hands and stores, and where the camera looks.
    // Hire: person IDs signed on; Depart: play the departure once staged (scene D is the departure).
    private sealed record Stage(string Id, string Title, int Minutes, string Weather, Vector2 Look, float Zoom, string Hire, string Buy, bool Depart = false);

    private static readonly Stage[] Stages =
    [
        new("dawn-quay", "Scene A: the quay at dawn", 1335, "mist", new Vector2(42, 34), 22f, "", ""),
        // Late morning, not noon: at noon everyone named goes indoors to eat and drink.
        new("midday-market", "Scene B: the market at midday", 195, "fair", new Vector2(40, 23), 22f, "person.leonor,person.tome", "0,1"),
        new("rainy-workshops", "Scene C: the workshop street in rain", 450, "rain", new Vector2(65, 17), 18f, "", ""),
        new("departure-morning", "Scene D: departure morning", 5620, "fair", new Vector2(32, 33), 14f,
            "person.velho,person.pons,person.bartolo,person.rodrigo,person.gil,person.wat,person.pascual", "0,0,0,0,0,1,1,1,1,2,2,3,3,4,5,6", Depart: true),
    ];
    private PreparationView _preparation = null!;
    private bool _farewells;
    private bool _portBuilt;
    private bool _follow = true;
    private CharacterId? _hovered;
    private PendingAction? _pending;
    private int _crewSeen;

    // Something the captain walks over to do: talk, ask, test or offer; or, with no target, read the
    // notice board at a fixed cell.
    private sealed record PendingAction(CharacterId? Target, string Description, Action Act, long GiveUpTick, GridPosition? Place = null)
    {
        public GridPosition? Heading { get; set; }
    }
    private CharacterId? _selected;
    private int _speedIndex = 1;
    private ConversationView _conversation = null!;
    private CharacterId? _singerHeard;
    private int? _speedBeforeTalk;
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
        _portHud.Attach(_hud);
        _hud.FocusRequested += Focus;
        _portHud.CardClosed += () => _selected = _runner.Campaign.CaptainId;
        // Talking: walk over, then the conversation, with the port standing still until it is done.
        _conversation = new ConversationView { Name = "Conversation" };
        var talkLayer = new CanvasLayer { Name = "ConversationLayer", Layer = 6 };
        AddChild(talkLayer);
        talkLayer.AddChild(_conversation);
        _portHud.TalkRequested += id => WalkOverTo(id, "talk", () =>
        {
            var (result, said) = _runner.BeginConversation(id);
            if (!result.Succeeded)
            {
                _portHud.Say(result.Reason);
                return;
            }

            _portHud.Say("");
            _speedBeforeTalk ??= _speedIndex;
            SetSpeed(0);
            _conversation.Open(_runner, id, said);
        });
        _conversation.TopicChosen += (id, topic, asked) =>
        {
            var (result, said, ends) = _runner.Discuss(id, topic);
            _conversation.Add(_runner, asked, result.Succeeded ? said : result.Reason, ends);
        };
        _conversation.AskAboutChosen += (informant, about) =>
        {
            var (result, said) = _runner.AskAbout(informant, about);
            _conversation.Add(_runner, $"What do you know of {_runner.Campaign.Characters[about].Name}?", result.Succeeded ? said : result.Reason);
        };
        _conversation.OfferChosen += (id, advance) =>
        {
            var name = _runner.Campaign.Characters[id].Name;
            var result = _runner.Offer(id, advance);
            _conversation.Add(_runner, advance < 0 ? $"Passage for {-advance} sueldos." : $"A berth, for {advance} sueldos in advance.",
                result.Succeeded ? $"{name} spits in a palm and shakes on it, and goes to stow a sea chest aboard the Garza." : result.Reason, result.Succeeded);
        };
        _conversation.Closed += () =>
        {
            SetSpeed(_speedBeforeTalk ?? 1);
            _speedBeforeTalk = null;
        };
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
        _portHud.DiceRequested += stake =>
        {
            var (result, said) = _runner.PlayDice(stake);
            _portHud.Say(result.Succeeded ? said : result.Reason);
        };
        _portHud.SongRequested += () =>
        {
            var (result, said) = _runner.CallForSong();
            _portHud.Say(result.Succeeded ? said : result.Reason);
        };
        _portHud.RoundRequested += () =>
        {
            var (result, said) = _runner.BuyRound();
            _portHud.Say(result.Succeeded ? said : result.Reason);
        };
        _portHud.NoticesRequested += () =>
        {
            if (_content.Port?.NoticeBoard is not { } board || _content.Locations[_content.Port.Location].FindSpot(board) is not { } spot)
            {
                return;
            }

            _pending = new PendingAction(null, "read the Chamber's notices", () =>
            {
                var (result, said) = _runner.ReadNotices();
                _portHud.Say(result.Succeeded ? said : result.Reason);
            }, _runner.Campaign.Clock.Tick + 3 * SimulationClock.TicksPerHour, spot.Cell);
            _follow = true;
            _hud.ShowStatus("Walking to the Chamber's notice board…");
            CarryOutPending();
        };
        _portHud.BuyRequested += lot => _hud.ShowResult(_runner.Buy(lot, _portHud.OnCredit),
            _portHud.OnCredit ? "Bought on the account. It will be carried aboard." : "Bought. It will be carried aboard.");
        _portHud.SettleRequested += seller => _hud.ShowResult(_runner.PayDebt(seller), "Account settled.");
        _portHud.InspectRequested += seller =>
        {
            var (result, seen) = _runner.Inspect(seller);
            if (result.Succeeded)
            {
                _portHud.Say(seen);
            }
            else
            {
                _hud.ShowResult(result, "");
            }
        };
        _portHud.ClaimAnswered += (creditor, pay) =>
        {
            var result = pay ? _runner.PayClaim(creditor) : _runner.RefuseClaim(creditor);
            _portHud.Say(!result.Succeeded ? result.Reason
                : pay ? $"{_runner.Campaign.Characters[creditor].Name} counts the coins twice and goes off satisfied."
                : $"{_runner.Campaign.Characters[creditor].Name} says the Chamber will hear of this.");
        };

        // Sailing plays the departure first (R2e): the real crew, cargo and claims, then the lines cast off.
        _departure = new DepartureSequence { Name = "Departure" };
        AddChild(_departure);
        _profiler = new FrameProfiler { Name = "Profiler", Crowd = () => _crowd.Count };
        AddChild(_profiler);
        _profiler.Finished += (_, saved) =>
        {
            _hud.Toast($"Profile saved: {saved}");
            if (_developerProfile && _screenshotPath is null)
            {
                GetTree().Quit();
            }
        };
        _departure.Beat += kind =>
        {
            // Those in the crowd who wave to this hand step to the edge; for anyone else, none do.
            _crowd.Wave(kind == "farewell" ? _departure.Current?.Wavers ?? 0 : 0, new Vector2(0, 1));
            switch (kind)
            {
                case "deadline":
                    _portSound.Cue("bell", 0.5f);
                    break;
                case "gangway":
                    _port.Gangway.Visible = false;
                    _portSound.Cue("mallet", 0.45f);
                    break;
                case "lines":
                    _port.Lines.Visible = false;
                    _portSound.Cue("creak", 0.6f);
                    break;
            }
        };
        _departure.Finished += () =>
        {
            var result = ExpeditionScenario.SetSail(_runner);
            _hud.Visible = true;
            _portHud.Visible = true;
            _port.Gangway.Visible = true;
            _port.Lines.Visible = true;
            if (!result.Succeeded)
            {
                _portHud.Say(result.Reason);
                return;
            }

            ShowWorld();
            _hud.Bind(_runner);
            _hud.SetSpeedNames(VoyageSpeedNames, VoyageSpeedTips);
            _hud.ClosePanel();
            _hud.ShowStatus("Lines cast off. La Bocana falls astern.");
        };
        _portHud.SetSailRequested += BeginDeparture;

        // Each new voyage gets its own seed; the seed is saved, so a reported problem can be replayed.
        var seed = (ulong)System.Random.Shared.NextInt64();
        ReadDeveloperArguments(ref seed);
        StartCampaign(_testDeck ? TestDeckScenario.Create(_content, seed) : ExpeditionScenario.Create(_content, seed),
            _testDeck ? "At sea. Left-click a crew member, right-click to send them." : "La Bocana. Right-click to walk; click anyone to look at them.");
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
        if (_runner.Campaign.Port is not null)
        {
            // What people say to the captain of their own accord (R2e): greetings in passing are a
            // passing line; someone who has come looking for him is shown and selected.
            foreach (var line in _runner.TakeLines())
            {
                if (line.Approach)
                {
                    _selected = line.Who;
                    _portHud.Say(line.Text);
                }
                else if (!_departure.Playing)
                {
                    _hud.Toast(line.Text);
                }
            }
        }
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
        // The port's weather (R2d): stone wets quickly in rain and dries slowly, fog and wind come and go.
        var portWeather = _runner.PortWeather;
        var raining = (float)(portWeather?.Rain ?? 0);
        _portWet = Mathf.MoveToward(_portWet, raining, (float)delta * (raining > _portWet ? 0.25f : 0.03f));
        _portFog = Mathf.MoveToward(_portFog, (float)(portWeather?.Fog ?? 0), (float)delta * 0.08f);
        _portWind = Mathf.MoveToward(_portWind, (float)(portWeather?.Wind ?? 0), (float)delta * 0.2f);
        _gloom = Mathf.Lerp(_gloom, _runner.Campaign.Port is not null ? (float)(portWeather?.Gloom ?? 0) : (float)(weather?.Gloom ?? 0), step);
        var targetSink = voyage is null ? 0f : voyage.Outcome == Expedition.Simulation.Voyage.VoyageOutcome.Foundered ? 3.2f : (float)voyage.Bilge / 100f * 0.8f;
        _sink = Mathf.Lerp(_sink, targetSink, Mathf.Clamp((float)delta * 0.4f, 0, 1));
        if (_gloom > 0.75f && speed > 0 && _flicker.NextDouble() < delta * 0.12)
        {
            _world.Flash();
        }

        // The ship rolls, pitches and heaves with the sea (ART_BIBLE.md 12). Visual only.
        var t = (float)_elapsed;
        var swell = inPort ? 0.15f : 0.35f + _seaState * 0.65f;
        var leaving = inPort && _departure.Playing ? _departure.Ship : (Offset: Vector3.Zero, Turn: 0f);
        _ship.RotationDegrees = new Vector3(Mathf.Sin(t * 0.45f) * 0.8f * swell, inPort ? -90f + leaving.Turn : 0, Mathf.Sin(t * 0.6f) * 1.6f * swell);
        _ship.Position = (inPort ? _port.Berth + leaving.Offset : _shipCentre) + new Vector3(0, Mathf.Sin(t * 0.7f) * 0.08f * swell - _sink, 0);

        var clock = _runner.Campaign.Clock;
        var hour = (float)((clock.Tick + (speed > 0 ? _tickFraction : 0)) % SimulationClock.TicksPerDay / SimulationClock.TicksPerHour);
        var hull = inPort
            ? new Vector4(9999, 9999, 0.1f, 0.1f)
            : new Vector4(_deck.CentreX, (_deck.Length.Bow + _deck.Length.Stern) / 2f, _deck.HalfBeam, (_deck.Length.Stern - _deck.Length.Bow) / 2f);
        // The camera keeps to the port and a little past its ends, but not off into the country; the
        // departure and the profiler's circle are free to go where they need.
        _camera.Bounds = inPort && _departure.Camera is null && !_profiler.Capturing ? new Rect2(-10, -6, 100, 54) : null;
        if (inPort)
        {
            _seaState = 0.35f;
            if (_profiler.Capturing)
            {
                // A slow circle over the scene, the same in every run.
                var angle = _profiler.Progress * Mathf.Tau;
                _camera.Target = _profileCentre + new Vector3(Mathf.Cos(angle) - 1f, 0, Mathf.Sin(angle)) * 5f;
            }
            else if (_departure.Camera is { } shot)
            {
                // The departure moves the camera itself: to the gangway, then after the ship.
                _follow = false;
                // While a beat is about someone (a hand and who came to see them off), close in on them.
                var (target, size) = _departure.Subject is { } who && _figures.TryGetValue(who, out var subject) && subject.Visible
                    ? (subject.GlobalPosition, 8f)
                    : shot;
                if (_departure.Current is { } told && (told.SeenOffBy.Count > 0 || told.Wavers > 0) && _departure.Subject is not null)
                {
                    // Frame the hand and those seeing them off together, across the gangway.
                    target = target.Lerp(new Vector3(_port.Berth.X, 0, 32.9f), 0.5f);
                    size = 11f;
                }
                _camera.Target = _camera.Target.Lerp(target, Mathf.Clamp((float)delta * 1.5f, 0, 1));
                _camera.Size = Mathf.Lerp(_camera.Size, size, Mathf.Clamp((float)delta * 1.2f, 0, 1));
            }
            else
            {
                FollowCaptain((float)delta);
            }
            _port.UpdateLook(_world.Darkness, t, _portWet);
            _port.OpenRoom(_runner.CaptainRoom?.Id, (float)delta);
            FadeForeground((float)delta);
            ListenToThePort(hour, (float)delta);
            // Rain and fog thin the crowd; stall covers go on, the ships at anchor vanish into fog.
            _crowd.Density = 1f - 0.7f * _portWet - 0.3f * _portFog;
            SceneryMaterial.Wind = 0.15f + _portWind;
            _crowd.Update(hour, (float)(delta * speed), _portWet, speed == 0 ? 0f : Mathf.Max(0.8f, 1f + Mathf.Log((float)speed) * 0.25f));
            _harbour.Update(hour, (float)(delta * speed), (float)delta, _world.Darkness, _portFog);
            _port.Covered = raining >= 0.5f;
            _ambient.Update((int)hour, _runner.RainingInPort, new Vector2(_camera.Target.X, _camera.Target.Z), (float)delta, speed == 0, speed <= 2);
            if (portWeather is not null && portWeather.Id != _weatherShown)
            {
                if (_weatherShown is not null)
                {
                    _hud.Toast(portWeather.Says);
                }

                _weatherShown = portWeather.Id;
            }
        }

        // The Garza's preparation as it stands (R2c): stores on the quay and deck, the stage's dressing.
        if (Engine.GetProcessFrames() % 6 == 0)
        {
            _preparation.Show(Expedition.Simulation.Port.ShipPreparation.Of(_runner.Campaign, _content), _runner.RainingInPort);

            // Departure morning: from first light, people come down to the quay to see her off (PORT_EXPERIENCE §5).
            var farewells = inPort && (_departure.Playing || (_preparation.Stage == Expedition.Simulation.Port.PreparationStage.DepartureMorning && hour >= 4.5f));
            if (farewells != _farewells)
            {
                _farewells = farewells;
                _crowd.SetGathering(farewells ? new Vector3(33.5f, 0, 32.6f) : null, 12, new Vector2(0, 1));
            }
        }
        var making = voyage is { Outcome: Expedition.Simulation.Voyage.VoyageOutcome.UnderWay } ? 0.3f + (float)voyage.SpeedLeaguesPerHour * 1.3f : 0f;
        _world.UpdateWorld(hour, _seaState, making, (float)delta, _camera.Target, hull, _gloom, inPort ? _portFog : 0f);
        var cooking = _runner.Campaign.ActiveTasks.Values.Any(task => task.DefinitionId == "task.cook");
        // Sails turn see-through at working zoom so they never hide the crew (ART_BIBLE.md 3); zoom out to see them whole.
        // Set sails fade at working zoom so they never hide the crew; furled at the quay they hide no one.
        _deck.UpdateLook(_world.Darkness, inPort ? 1f : Mathf.Lerp(0.38f, 1f, Mathf.SmoothStep(18f, 32f, _camera.Size)), t, cooking, inPort ? 3.2f : SailSetting(voyage), (float)delta, furled: inPort);
        foreach (var bird in _birds)
        {
            bird.Centre = inPort ? _port.Berth + new Vector3(0, 4f, -4f) : _shipCentre + new Vector3(0, 0, -2f);
        }

        var drawTick = _runner.Campaign.Clock.Tick + (speed > 0 ? _tickFraction : 0);
        var animationRate = speed == 0 ? 0f : Mathf.Max(0.8f, 1f + Mathf.Log((float)speed) * 0.25f);
        CrewFigure.FarView = _camera.Size > 26f;
        var captainRoom = _runner.CaptainRoom;
        foreach (var (id, figure) in _figures)
        {
            var character = _runner.Campaign.Characters[id];
            if (_runner.Campaign.Placements.TryGetValue(id, out var placement))
            {
                _runner.Campaign.ActiveTasks.TryGetValue(id, out var task);
                var definition = task is null ? null : _content.Tasks[task.DefinitionId];
                var location = _content.Locations[placement.LocationId];
                var spot = task?.SpotId is { } spotId ? location.FindSpot(spotId) : null;
                // A cask is rolled, not carried; everything else the task carries is held.
                var carrying = definition?.Carries == "cask" ? null : definition?.Carries;
                // The departure's farewells, staged (R3 scene D): whoever has come to see this hand off
                // stands at the quay edge by the gangway and waves.
                Vector3? standAt = null;
                var signing = false;
                if (_departure.Playing && _departure.Current is { Kind: "farewell" or "sponsor" } beat && beat.SeenOffBy.ToList().IndexOf(id) is var giver and >= 0)
                {
                    standAt = new Vector3(_port.Berth.X - 1.2f + giver * 1.4f, 0, 32.9f);
                    signing = beat.Kind == "sponsor";
                }

                var pose = signing ? Pose.Talking
                    : _runner.Singing == id ? Pose.Waving
                    : _runner.Dicing.Contains(id) ? Pose.Talking
                    : standAt is not null ? Pose.Waving
                    : character.SurvivalState is SurvivalState.Incapacitated or SurvivalState.Dead ? Pose.Lying
                    : definition?.Carries == "cask" ? (placement.IsMoving ? Pose.Rolling : Pose.Lifting)
                    : carrying is not null ? Pose.Carrying
                    : placement.IsMoving ? Pose.Walking
                    : definition is { IsRest: true } ? Pose.Resting
                    : definition is not null ? WorkPose(definition.Id, spot?.Kind)
                    : Pose.Standing;
                // Indoors out of sight, or in a room the captain is not in (the roof is on).
                var inside = character.Membership == Membership.Local && (_runner.IsIndoors(id) || (_runner.RoomOf(id) is { } room && room != captainRoom));
                // In the departure the captain leaves the quay when he goes up the gangway (R2e).
                figure.Visible = standAt is not null || character.SurvivalState != SurvivalState.Missing && !inside && !(id == _runner.Campaign.CaptainId && _departure.Boarded && _departure.Playing);
                var onPort = placement.LocationId == _runner.Content.Port?.Location;
                var isCaptain = id == _runner.Campaign.CaptainId;
                figure.Refresh(character, placement, drawTick, id == _selected, Hud.Activity(_runner, character),
                    isCaptain, pose, (float)delta, animationRate,
                    onPort ? _port.HeightAt : null, carrying,
                    showLabel: !_departure.Playing && (!inPort || isCaptain || id == _hovered), shownName: inPort ? PortHud.KnownName(_runner, id) : null,
                    facing: spot is null ? null : FacingAt(location, spot),
                    inRain: onPort && _runner.RainingInPort && definition?.Id != "task.shelter", standAt: standAt);
                figure.Outline(inPort && !_departure.Playing ? RecruitRim(id) : Colors.Transparent);
            }
        }

        _hud.Refresh(_runner, _selected, _speedIndex);
        _portHud.Refresh(_runner, _selected);
        var pointer = GetViewport().GetMousePosition();
        // --hover=person.x: as if the pointer were over them, for screenshots.
        if (_developerHover is { } hoverId && _runner.CharacterFor(hoverId) is { } hoverOf && _figures.TryGetValue(hoverOf.Id, out var hoverFigure))
        {
            _hovered = hoverOf.Id;
            pointer = _camera.UnprojectPosition(hoverFigure.GlobalPosition + new Vector3(0, 1.2f, 0));
        }

        _portHud.Hover(_runner, inPort && !_departure.Playing && _hovered != _runner.Campaign.CaptainId ? _hovered : null, pointer);
    }

    // Which way someone at a spot faces: the spot's own facing, or else the nearest thing beside them
    // (the stall, the bench, the stack), so people turn to their work (PORT_VISUAL_PASS §3.6–7).
    private static Vector2? FacingAt(LocationDefinition location, WorkSpotDefinition spot)
    {
        if (spot.Face is { } face)
        {
            return face switch
            {
                "N" => new Vector2(0, -1), "NE" => new Vector2(0.7f, -0.7f), "E" => new Vector2(1, 0), "SE" => new Vector2(0.7f, 0.7f),
                "S" => new Vector2(0, 1), "SW" => new Vector2(-0.7f, 0.7f), "W" => new Vector2(-1, 0), _ => new Vector2(-0.7f, -0.7f),
            };
        }

        foreach (var (dx, dy) in new[] { (0, -1), (1, 0), (-1, 0), (0, 1), (1, -1), (-1, -1), (1, 1), (-1, 1) })
        {
            if (!location.IsWalkable(new GridPosition(spot.X + dx, spot.Y + dy)))
            {
                return new Vector2(dx, dy);
            }
        }

        return null;
    }

    // How someone looks while working, from the kind of place and the work (PORT_EXPERIENCE §8).
    private static Pose WorkPose(string task, string? kind) => task switch
    {
        "task.drink" or "task.gamble" => kind == "SitPoint" ? Pose.Sitting : Pose.Drinking,
        "task.pray" => Pose.Praying,
        "task.sell_fish" => Pose.Gutting,
        "task.trade_goods" or "task.dun_debtor" or "task.serve_drinks" or "task.play" or "task.meet_in_secret" or "task.buy_at_market" or "task.gossip" => Pose.Talking,
        "task.inspect_ship" => Pose.Inspecting,
        "task.watch_water" => Pose.Watching,
        "task.stand_guard" or "task.read_notices" or "task.shelter" => Pose.Leaning,
        "task.steady_plank" => Pose.Steadying,
        "task.catch_breath" => Pose.Sitting,
        "task.wait_in_irons" or "task.treat_patients" or "task.keep_accounts" or "task.study_charts" => kind == "SitPoint" ? Pose.Sitting : Pose.Working,
        _ => kind switch
        {
            "CarryPickup" => Pose.Lifting,
            "HammerStation" => Pose.Hammering,
            "SawStation" => Pose.Sawing,
            "RopeWork" => Pose.Roping,
            "SitPoint" => Pose.Sitting,
            "LeanPoint" or "WatchWater" => Pose.Watching,
            _ => Pose.Working,
        },
    };

    // The port's sound follows where the camera looks and how busy each place is (PORT_VISUAL_PASS §3.14).
    private void ListenToThePort(float hour, float delta)
    {
        var market = 0;
        var workers = 0;
        var smiths = 0;
        var fish = 0;
        foreach (var (id, task) in _runner.Campaign.ActiveTasks)
        {
            if (task.DefinitionId is "task.sell_fish" or "task.buy_at_market" or "task.trade_goods" or "task.gossip" or "task.play")
            {
                market++;
                fish += task.DefinitionId == "task.sell_fish" ? 1 : 0;
            }
            else if (task.DefinitionId == "task.smithing")
            {
                smiths++;
            }
            else if (_content.Tasks[task.DefinitionId].Skill is "skill.carpentry" or "skill.crafting")
            {
                workers++;
            }
        }

        _portSound.Activity = (market, workers, smiths, fish);
        _portSound.Wind = _portWind;
        _portSound.Inside = _runner.CaptainRoom is not null;
        // The tavern's evening: a song sung while it lasts, the bones thrown while a game goes round.
        var singer = _runner.Singing;
        if (singer != _singerHeard)
        {
            if (singer is { } who && _runner.PersonOf(who)?.Song is { } song)
            {
                _portSound.Sing(song.Voice);
            }
            else
            {
                _portSound.StopSinging();
            }

            _singerHeard = singer;
        }

        var dicing = _runner.Dicing.Count > 0;
        if (dicing && !_portSound.Dicing)
        {
            _portSound.Cue("dicecup", 0.8f);
        }

        _portSound.Dicing = dicing;
        _portSound.Update(new Vector2(_camera.Target.X, _camera.Target.Z), hour, _portWet, delta, (float)TicksPerSecond[_speedIndex]);
    }

    // Foreground pieces (the hoist, a moored ship's rig) thin out while they stand between the camera and
    // the captain or the person selected, so they add depth without hiding people (PORT_VISUAL_PASS §3.12).
    private void FadeForeground(float delta)
    {
        var watched = new[] { _runner.Campaign.CaptainId, _selected }.OfType<CharacterId>().Distinct()
            .Where(_figures.ContainsKey).Select(id => _figures[id].GlobalPosition + new Vector3(0, 0.9f, 0)).ToList();
        foreach (var (node, material, bounds) in _port.Foreground)
        {
            var covering = false;
            foreach (var person in watched)
            {
                if (_camera.IsPositionBehind(person))
                {
                    continue;
                }

                var screen = _camera.UnprojectPosition(person);
                var rect = new Rect2(_camera.UnprojectPosition(bounds.Position), Vector2.Zero);
                for (var i = 0; i < 8; i++)
                {
                    rect = rect.Expand(_camera.UnprojectPosition(bounds.GetEndpoint(i)));
                }

                // In front of them (nearer the camera) and over them on screen.
                var nearer = _camera.GlobalTransform.Basis.Z.Dot(bounds.GetCenter() - person) > 0;
                covering |= nearer && rect.Grow(12).HasPoint(screen);
            }

            var alpha = material.AlbedoColor.A;
            material.AlbedoColor = new Color(1, 1, 1, Mathf.MoveToward(alpha, covering ? 0.28f : 1f, delta * 3f));
        }
    }

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

    // Walks to a fixed place (the notice board) and acts on arriving within speaking distance of it.
    private void CarryOutAtPlace(PendingAction pending, CharacterId captain)
    {
        var place = pending.Place!.Value;
        var here = _runner.Campaign.Placements[captain];
        var cell = here.CellAt(_runner.Campaign.Clock.Tick);
        if (Math.Max(Math.Abs(cell.X - place.X), Math.Abs(cell.Y - place.Y)) <= SimulationRunner.SpeakingDistance && !here.IsMoving)
        {
            _pending = null;
            _portHud.ShowPending(null);
            pending.Act();
            return;
        }

        if (_runner.Campaign.Clock.Tick > pending.GiveUpTick)
        {
            _pending = null;
            return;
        }

        if (!here.IsMoving)
        {
            var location = _content.Locations[here.LocationId];
            foreach (var d in new[] { (0, 1), (1, 1), (-1, 1), (1, 0), (-1, 0), (0, 2), (1, 2), (-1, 2) })
            {
                var next = new GridPosition(place.X + d.Item1, place.Y + d.Item2);
                if (location.IsWalkable(next) && _runner.MoveCharacter(captain, next).Succeeded)
                {
                    break;
                }
            }
        }

        _portHud.ShowPending($"Walking over to {pending.Description}…");
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

        if (pending.Target is not { } target)
        {
            CarryOutAtPlace(pending, captain);
            return;
        }

        var name = PortHud.KnownName(_runner, target);
        if (_runner.WhyCannotSpeak(target) is null)
        {
            _pending = null;
            _portHud.ShowPending(null);
            pending.Act();
            return;
        }

        if (!_runner.Campaign.Placements.TryGetValue(target, out var there) || _runner.IsIndoors(target)
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
        if (_runner is null || _departure.Playing)
        {
            return;
        }

        if (@event is InputEventMouseButton { Pressed: true } click)
        {
            if (click.ButtonIndex == MouseButton.Left)
            {
                _selected = FigureUnder(click.Position) ?? _selected;
            }
            else if (click.ButtonIndex == MouseButton.Right && _runner.Campaign.Port is not null && FigureUnder(click.Position) is { } person
                     && person != _runner.Campaign.CaptainId && _runner.Campaign.Characters[person].Membership == Membership.Local && _runner.PersonOf(person) is not null)
            {
                // In port, right-click on someone walks over and talks with them.
                _selected = person;
                _follow = true;
                _portHud.RequestTalk(person);
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

            // Ctrl+1..4 in debug builds: stage validation scene A..D (R3).
            if (key.CtrlPressed && OS.IsDebugBuild() && key.Keycode is >= Key.Key1 and <= Key.Key4)
            {
                StageScene(Stages[(int)key.Keycode - (int)Key.Key1].Id, profile: false);
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
                case Key.F10:
                    // A frame-time capture of the view (R3 profiling).
                    StartProfile();
                    break;
                case Key.R when key.ShiftPressed && OS.IsDebugBuild():
                    _runner.DeveloperSetPortWeather(_runner.RainingInPort ? "weather.fair" : "weather.rain");
                    break;
                case Key.P: _hud.TogglePanel(PanelId.People); break;
                case Key.X: _hud.TogglePanel(PanelId.Expedition); break;
                case Key.C: _hud.TogglePanel(PanelId.Cargo); break;
                case Key.H: _hud.TogglePanel(PanelId.Ship); break;
                case Key.L: _hud.TogglePanel(PanelId.Chronicle); break;
                case Key.Escape:
                    _profiler.Close();
                    // Back out one layer at a time: the open screen, then the card (§48).
                    if (!_hud.ClosePanel())
                    {
                        _selected = _runner.Campaign.CaptainId;
                        _pending = null;
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Stages one of the four validation scenes (R3): a new campaign on a fixed seed, run on to the hour,
    /// with the scene's weather, hands and stores, and the camera on it. Optionally profiles it.
    /// </summary>
    private void StageScene(string id, bool profile)
    {
        if (Stages.FirstOrDefault(s => s.Id == id) is not { } stage || _content.Port is not { } rules)
        {
            GD.PushWarning($"No validation scene '{id}'. Scenes: {string.Join(", ", Stages.Select(s => s.Id))}.");
            return;
        }

        StartCampaign(ExpeditionScenario.Create(_content, 1519UL), stage.Title);
        _runner.Advance(stage.Minutes);
        foreach (var person in stage.Hire.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (_runner.CharacterFor(person) is { } hand && _runner.Campaign.Placements.ContainsKey(hand.Id))
            {
                _runner.DeveloperHire(hand.Id);
            }
        }

        foreach (var lot in stage.Buy.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            _runner.Buy(int.Parse(lot, System.Globalization.CultureInfo.InvariantCulture));
        }

        if (_runner.DeveloperSetPortWeather("weather." + stage.Weather).Succeeded)
        {
            _portWet = (float)(_runner.PortWeather?.Rain ?? 0);
            _portFog = (float)(_runner.PortWeather?.Fog ?? 0);
            _portWind = (float)(_runner.PortWeather?.Wind ?? 0);
            _gloom = (float)(_runner.PortWeather?.Gloom ?? 0);
            _weatherShown = _runner.PortWeather?.Id;
        }

        _camera.Target = new Vector3(stage.Look.X, 0, stage.Look.Y);
        _camera.Size = stage.Zoom;
        _follow = false;
        SetSpeed(1);
        _stageShown = stage.Id;
        if (profile)
        {
            StartProfile();
        }
        else if (stage.Depart)
        {
            BeginDeparture();
        }
    }

    // A frame-time capture of what is in view, the camera drifting slowly round it.
    private void StartProfile()
    {
        _profileCentre = _camera.Target;
        _follow = false;
        _profiler.Start(_stageShown ?? "view");
    }

    // Sailing from port plays the departure first (R2e); the departure sails when it ends.
    private void BeginDeparture()
    {
        if (_content.Port is { } rules && _runner.Campaign.Port is { } port && port.Hired.Count < rules.MinimumCrew)
        {
            _portHud.Say($"The Garza needs at least {rules.MinimumCrew} hands to sail; {port.Hired.Count} are signed on.");
            return;
        }

        _hud.ClosePanel();
        _hud.Visible = false;
        _portHud.Visible = false;
        _selected = _runner.Campaign.CaptainId;
        SetSpeed(1);
        _departure.Play(_runner, new Vector3(_port.Berth.X, 0, 33.6f), _port.Berth);
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
            _port.Build(_content.Locations[rules.Location], _content.Tasks);
            _portSound.Build(_content.Locations[rules.Location]);
            _crowd.Build(_content.Locations[rules.Location], _port.HeightAt);
            _crowd.SetOutskirts(_port.Lanes, _port.Idlers);
            _harbour.Build();
            _ambient.Build(_content.Locations[rules.Location], rules.Ambient, _port.HeightAt);
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
            // Close enough that people read as people (the owner's board), not as a crowd of pins.
            _camera.Size = 17f;
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
        if (runner.Campaign.Port is not null)
        {
            _hud.SetSpeedNames(PortSpeedNames, PortSpeedTips);
        }
        else
        {
            _hud.SetSpeedNames(VoyageSpeedNames, VoyageSpeedTips);
        }
        _hud.ShowStatus(message);
    }

    // The waterfront is drawn while in port, and the sea alone once she has sailed.
    private void ShowWorld()
    {
        var inPort = _runner.Campaign.Port is not null;
        _port.Visible = inPort;
        _portSound.ProcessMode = inPort ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
        foreach (var player in _portSound.GetChildren().OfType<AudioStreamPlayer>())
        {
            player.StreamPaused = !inPort;
        }
        _portFigures.Visible = inPort;
        if (!inPort)
        {
            _ship.RotationDegrees = Vector3.Zero;
            _ship.Position = _shipCentre;
            _camera.Target = _shipCentre;
            _follow = false;
        }
    }

    // The rim round people the captain could sign on, from what he knows (not from the truth): brass
    // for someone who has said they want a berth and is still free; a pale blue for someone he has
    // heard of but not yet spoken to, worth finding out. Crew, strangers and the gone have none.
    private Color RecruitRim(CharacterId id)
    {
        if (_runner.Campaign.Port is not { } port || port.Hired.Contains(id) || port.Gone.Contains(id) || id == _runner.Campaign.CaptainId)
        {
            return Colors.Transparent;
        }

        var person = _runner.Campaign.Characters[id].PersonId is { } pid ? _content.Port?.People.FirstOrDefault(p => p.Id == pid) : null;
        if (port.TalkedTo.Contains(id))
        {
            return person?.Seeking is not null ? Ui.Brass with { A = 0.95f } : Colors.Transparent;
        }

        return _runner.LeadsAbout(id).Any() ? Ui.Uncertain with { A = 0.8f } : Colors.Transparent;
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
                // The captain is no person of the port, but has a model of his own (Assets/Characters/person.captain).
                figure = CrewFigure.Create(character.Appearance, _content.Appearance, officer,
                    character.PersonId ?? (id == _runner.Campaign.CaptainId ? "person.captain" : null));
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
        // Each danger becomes an alert with its severity, cause and who it concerns (UI_UX §17); those in
        // the categories the player chose stop the clock (§18).
        var entries = _runner.Campaign.Chronicle.Entries;
        var pausing = new List<string>();
        for (; _chronicleSeen < entries.Count; _chronicleSeen++)
        {
            var entry = entries[_chronicleSeen];
            (Severity Severity, AutoPause Category, string Title)? alert = entry.Kind switch
            {
                ChronicleEntryKind.WeatherChanged when entry.Get(ChronicleParameters.To) is "weather.storm" => (Severity.Critical, AutoPause.Weather, "A storm breaks"),
                ChronicleEntryKind.WeatherChanged when entry.Get(ChronicleParameters.To) is "weather.gale" => (Severity.Urgent, AutoPause.Weather, "A gale is blowing"),
                ChronicleEntryKind.HullStruck => (Severity.Urgent, AutoPause.ShipDamage, "The hull is struck"),
                ChronicleEntryKind.BilgeAlarm => (Severity.Critical, AutoPause.ShipDamage, "The bilge is rising"),
                ChronicleEntryKind.Injury => (Severity.Urgent, AutoPause.InjuryAndDeath, "Someone is hurt"),
                ChronicleEntryKind.SurvivalStateChanged when entry.Get(ChronicleParameters.To) is "Dead" => (Severity.Critical, AutoPause.InjuryAndDeath, "A death aboard"),
                ChronicleEntryKind.SurvivalStateChanged when entry.Get(ChronicleParameters.To) is "Incapacitated" => (Severity.Critical, AutoPause.InjuryAndDeath, "Someone has collapsed"),
                ChronicleEntryKind.SupplyShortage => (Severity.Critical, AutoPause.Shortage, $"Out of {entry.Get(ChronicleParameters.Need)}"),
                _ => null,
            };
            if (alert is not { } found)
            {
                continue;
            }

            CharacterId? who = entry.Characters.Count > 0 ? entry.Characters[0] : null;
            _hud.PushAlert(found.Severity, found.Title, _formatter.Format(entry), who, new SimulationClock(entry.Tick).ToString());
            if (_hud.Pauses(found.Category))
            {
                pausing.Add($"{Ui.IconOf(found.Severity)} {found.Title}");
            }
        }

        if (pausing.Count > 0 && _speedIndex > 0)
        {
            SetSpeed(0);
            _hud.ShowAlert("Paused for: " + string.Join(" · ", pausing.Distinct().TakeLast(3)));
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
        SetSpeed(Math.Min(_lastRunningSpeed, SafeResumeSpeed));
    }

    // Looks at someone: selects them and brings the camera over (alerts, the roster). In port the camera
    // stops following the captain until F.
    private void Focus(CharacterId id)
    {
        if (!_figures.TryGetValue(id, out var figure))
        {
            return;
        }

        _selected = id;
        _follow = false;
        _camera.Target = figure.GlobalPosition;
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
            // Reported only once the file is written (UI_UX §46).
            _saves.Write(QuickSaveSlot, SaveSerializer.Serialize(_runner.Campaign));
            var where = _runner.Campaign.Port is not null ? "in port at La Bocana" : _runner.Campaign.Voyage is { } ship ? $"at sea, bound for {ship.Destination}" : "";
            _hud.ShowResult(CommandResult.Ok(), $"Saved: {_runner.Campaign.Clock}, {where}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _hud.ShowResult(CommandResult.Rejected($"Not saved: {ex.Message}"));
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
        _portSound = new PortSound { Name = "PortSound" };
        AddChild(_portSound);
        _portFigures = new Node3D { Name = "PortFigures" };
        AddChild(_portFigures);
        _crowd = new PortCrowd { Name = "PortCrowd" };
        _portFigures.AddChild(_crowd);
        _harbour = new HarbourView { Name = "Harbour" };
        _portFigures.AddChild(_harbour);
        _ambient = new AmbientScenes { Name = "AmbientScenes" };
        _portFigures.AddChild(_ambient);
        _ambient.Told += text => _hud.Toast(text);
        _ambient.Sound = (kind, volume) => _portSound.Cue(kind, volume);
        _preparation = new PreparationView { Name = "PreparationView" };
        AddChild(_preparation);
        _preparation.Attach(_shipContents, _portFigures);

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
    private string? _developerInspect;
    private string? _developerLook;
    private bool _developerTalk;
    private string? _developerTopics;
    private GridPosition? _developerCaptainAt;
    private string? _developerTavern;
    private string? _developerHover;
    private bool _developerJournal;
    private bool _developerPerf;
    private PanelId? _developerPanel;
    private int? _developerOffer;
    private float? _developerYaw;
    private string? _developerBuy;
    private int _developerHire;
    private int _developerAfter;
    private string? _developerScene;
    private bool _developerDepart;
    private string? _developerStage;
    private bool _developerProfile;
    private double _shotDelay = 3.0;

    // For screenshots of the port: --select=person.velho opens their card (and --talk walks the captain
    // over and talks, at once; --offer=N then offers that advance); --look=x,z points the camera (--look=selected at
    // the one selected); --yaw=degrees turns it.
    private void DeveloperPort()
    {
        // For the preparation stages (R2c): --hire=N signs on the first N who want a berth, --buy=0,0,4
        // buys those lots, --after=MINUTES then lets time pass.
        if (_developerHire > 0 && _content.Port is { } rules)
        {
            foreach (var seeker in rules.People.Where(p => p.Seeking is not null).Take(_developerHire))
            {
                if (_runner.CharacterFor(seeker.Id) is { } hand)
                {
                    _runner.DeveloperHire(hand.Id);
                }
            }
        }

        foreach (var lot in (_developerBuy ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(lot, out var index))
            {
                _runner.Buy(index);
            }
        }

        if (_developerAfter > 0)
        {
            _runner.Advance(_developerAfter);
        }

        // --captain=x,y walks the captain there first (into the Three Anchors: --captain=28,9).
        if (_developerCaptainAt is { } walkTo && _runner.Campaign.CaptainId is { } walker && _runner.MoveCharacter(walker, walkTo).Succeeded)
        {
            for (var i = 0; i < 180 && _runner.Campaign.Placements[walker].IsMoving; i++)
            {
                _runner.Advance(1);
            }

            // --tavern=song or --tavern=dice: an evening's entertainment, once the captain is in.
            var (done, told) = _developerTavern switch
            {
                "song" => _runner.CallForSong(),
                "dice" => _runner.PlayDice(20),
                _ => (Expedition.Simulation.Core.CommandResult.Ok(), ""),
            };
            _portHud.Say(done.Succeeded ? told : done.Reason);
        }

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

                // --talk opens the conversation; --topics=a,b asks those, in order.
                var (_, said) = _runner.BeginConversation(character.Id);
                _speedBeforeTalk ??= _speedIndex;
                SetSpeed(0);
                _conversation.Open(_runner, character.Id, said);
                foreach (var topic in _developerTopics?.Split(',') ?? [])
                {
                    var asked = _runner.Topics(character.Id).FirstOrDefault(t => t.Id == topic)?.Ask ?? topic;
                    var (_, answer, ends) = _runner.Discuss(character.Id, topic);
                    _conversation.Add(_runner, asked, answer, ends);
                }

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
        else if (_developerLook == "selected" && _selected is { } chosen && _runner.Campaign.Placements.TryGetValue(chosen, out var at))
        {
            // --look=selected: the camera on whoever --select chose.
            var cell = at.CellAt(_runner.Campaign.Clock.Tick);
            _camera.Target = new Vector3(cell.X, 0, cell.Y);
            _follow = false;
        }

        // --inspect=seller.hob: the captain stands by that seller and looks closely at the goods.
        if (_developerInspect is { } sellerId && _content.Port?.Sellers.FirstOrDefault(x => x.Id == sellerId) is { } seller
            && _runner.SellerCharacter(seller) is { } sells && _runner.Campaign.Placements.TryGetValue(sells.Id, out var stall)
            && _runner.Campaign.CaptainId is { } captainId)
        {
            var at = stall.CellAt(_runner.Campaign.Clock.Tick);
            var map = _content.Locations[stall.LocationId];
            var beside = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Select(d => new GridPosition(at.X + d.Item1, at.Y + d.Item2)).FirstOrDefault(map.IsWalkable);
            _runner.PlaceCharacter(captainId, stall.LocationId, beside);
            var (_, seen) = _runner.Inspect(sellerId);
            _portHud.Say(seen);
            _camera.Target = new Vector3(at.X, 0, at.Y);
            _follow = false;
        }

        if (_developerJournal)
        {
            _portHud.ShowJournal();
        }

        // --weather=rain (or fog, breezy, overcast) sets the port's weather at once, already settled in.
        if (_forceWeather is { } forced && _runner.Campaign.Port is not null && _runner.DeveloperSetPortWeather("weather." + forced).Succeeded)
        {
            _portWet = (float)(_runner.PortWeather?.Rain ?? 0);
            _portFog = (float)(_runner.PortWeather?.Fog ?? 0);
            _gloom = (float)(_runner.PortWeather?.Gloom ?? 0);
            _weatherShown = _runner.PortWeather?.Id;
        }

        if (_developerPanel is { } open)
        {
            _hud.OpenPanel(open);
        }

        if (_developerYaw is { } yaw)
        {
            _camera.SetYaw(yaw);
        }

        // --scene=ambient.procession starts that scene at once (with --shotdelay to catch it under way).
        if (_developerScene is { } scene)
        {
            _ambient.Force(scene);
        }

        // --stage=dawn-quay (midday-market, rainy-workshops, departure-morning) stages a validation scene;
        // --profile profiles it (or the view) for 20 seconds, saves the report and quits.
        if (_developerStage is { } staged)
        {
            StageScene(staged, _developerProfile);
        }
        else if (_developerProfile)
        {
            StartProfile();
        }

        // --depart plays the departure at once (with --shotdelay to catch a moment of it).
        if (_developerDepart && _runner.Campaign.Port is not null)
        {
            BeginDeparture();
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
                case "topics": _developerTopics = value; break;
                case "tavern": _developerTavern = value; break;
                case "hover": _developerHover = value; break;
                case "captain" when value.Split(',') is [var cx, var cy] && int.TryParse(cx, out var x) && int.TryParse(cy, out var y): _developerCaptainAt = new GridPosition(x, y); break;
                case "journal": _developerJournal = true; break;
                case "perf": _developerPerf = true; break;
                case "panel" when Enum.TryParse<PanelId>(value, true, out var panel): _developerPanel = panel; break;
                case "offer" when int.TryParse(value, out var advance): _developerOffer = advance; break;
                case "buy": _developerBuy = value; break;
                case "inspect": _developerInspect = value; break;
                case "scene": _developerScene = value; break;
                case "depart": _developerDepart = true; break;
                case "stage": _developerStage = value; break;
                case "profile": _developerProfile = true; break;
                case "shotdelay" when double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var wait): _shotDelay = wait; break;
                case "hire" when int.TryParse(value, out var hands): _developerHire = hands; break;
                case "after" when int.TryParse(value, out var later): _developerAfter = later; break;
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
        await ToSignal(GetTree().CreateTimer(_shotDelay), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        if (_developerPerf)
        {
            // Render cost of the frame just drawn (--perf): what the scene asks of the GPU.
            GD.Print($"PERF crowd={_crowd.Count} fps={Engine.GetFramesPerSecond():0} drawCalls={Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame):0} " +
                $"objects={Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame):0} primitives={Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame):0} " +
                $"nodes={Performance.GetMonitor(Performance.Monitor.ObjectNodeCount):0} processMs={Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000:0.0} " +
                $"videoMemMB={Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed) / 1048576:0}");
        }

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

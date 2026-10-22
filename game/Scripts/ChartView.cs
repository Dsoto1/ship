using Expedition.Simulation.Core;
using Expedition.Simulation.Voyage;
using Expedition.Simulation.World;
using Godot;

namespace Expedition.Game;

/// <summary>
/// The working chart at sea: the coasts, a graticule of degrees, the reckoned track with the latitude of
/// each noon sight, where the captain reckons she is and how far that may be out, and the course to
/// steer. Beside it the reckoning in words, who keeps it, and the noon sight. It shows only the
/// captain's reckoning, never the truth: at sea no one knows the longitude.
/// </summary>
public partial class ChartView : Control
{
    private PanelContainer _panel = null!;
    private ChartCanvas _canvas = null!;
    private Label _reckoning = null!;
    private Label _sight = null!;
    private Button _take = null!;
    private OptionButton _navigator = null!;
    private readonly List<CharacterId?> _navigatorChoices = [];
    private VBoxContainer _coastBox = null!;
    private VBoxContainer _sightBox = null!;
    private VBoxContainer _islandBox = null!;
    private Label _islands = null!;
    private VBoxContainer _islandButtons = null!;
    private string _islandState = "";
    private string? _listedFor;
    private Label _coast = null!;
    private Button _lead = null!;
    private VBoxContainer _knowBox = null!;
    private OptionButton _landmark = null!;
    private Label _directions = null!;
    private Button _north = null!;
    private Button _south = null!;
    private SimulationRunner? _runner;
    private Label _legs = null!;
    private Button _plot = null!;
    private Button _clearLegs = null!;
    private LineEdit _noteText = null!;
    private Button _placeNote = null!;
    private Button _rubOut = null!;

    /// <summary>The captain goes to take the noon sight himself.</summary>
    public event Action? SightRequested;

    /// <summary>The chart is closed.</summary>
    public event Action? Closed;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Theme = Ui.BuildTheme();
        Visible = false;
        _panel = Ui.Frame(Ui.SpaceL, Ui.SpaceL);
        _panel.SetAnchorsPreset(LayoutPreset.Center);
        _panel.OffsetLeft = -600;
        _panel.OffsetRight = 600;
        _panel.OffsetTop = -360;
        _panel.OffsetBottom = 340;
        AddChild(_panel);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", Ui.SpaceL);
        _panel.AddChild(row);
        _canvas = new ChartCanvas { CustomMinimumSize = new Vector2(820, 660), SizeFlagsVertical = SizeFlags.ExpandFill, ClipContents = true };
        row.AddChild(_canvas);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(330, 0) };
        column.AddThemeConstantOverride("separation", Ui.SpaceM);
        row.AddChild(column);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        column.AddChild(scroll);
        var side = new VBoxContainer { CustomMinimumSize = new Vector2(320, 0) };
        side.AddThemeConstantOverride("separation", Ui.SpaceM);
        scroll.AddChild(side);
        side.AddChild(Ui.Text("The chart", Ui.TextHeading + 2, Ui.Brass));
        _reckoning = Ui.Text("", Ui.TextBody, Ui.Ink, 320);
        side.AddChild(_reckoning);
        _sightBox = new VBoxContainer();
        _sightBox.AddThemeConstantOverride("separation", Ui.SpaceM);
        side.AddChild(_sightBox);
        _sightBox.AddChild(new HSeparator());
        _sightBox.AddChild(Ui.Text("The noon sight", Ui.TextHeading, Ui.Brass));
        _sight = Ui.Text("", Ui.TextBody, Ui.Ink, 320);
        _sightBox.AddChild(_sight);
        _take = Ui.Button(Ui.ButtonKind.Action, "Take the sun with the astrolabe", "Take the noon sight yourself: the latitude, as true as your hand is steady", () => SightRequested?.Invoke());
        _sightBox.AddChild(_take);
        side.AddChild(Ui.Text("Navigator (takes the sight, keeps the lead going near the coast, and speaks to what is seen)", Ui.TextMeta, Ui.InkSoft, 320));
        _navigator = new OptionButton { FocusMode = FocusModeEnum.None, ClipText = true };
        _navigator.ItemSelected += index =>
        {
            if (_runner is not null && index >= 0 && index < _navigatorChoices.Count)
            {
                _runner.SetNavigator(_navigatorChoices[(int)index]);
                Refresh();
            }
        };
        side.AddChild(_navigator);

        // Laying off a course in legs, and writing on the chart (NAVIGATION §6, §22).
        var course = new VBoxContainer();
        course.AddThemeConstantOverride("separation", Ui.SpaceS);
        side.AddChild(course);
        course.AddChild(new HSeparator());
        course.AddChild(Ui.Text("The course", Ui.TextHeading, Ui.Brass));
        _legs = Ui.Text("", Ui.TextBody, Ui.Ink, 320);
        course.AddChild(_legs);
        var legButtons = new HBoxContainer();
        legButtons.AddThemeConstantOverride("separation", Ui.SpaceS);
        course.AddChild(legButtons);
        _plot = Ui.Button("Lay off a leg", "Then click the chart where she is to steer for: legs are steered in turn, by the reckoning", null, toggle: true);
        _plot.Toggled += on => { if (on) _placeNote.SetPressedNoSignal(false); };
        legButtons.AddChild(_plot);
        _clearLegs = Ui.Button("Clear the legs", "Back to the passage's own course", () => { _runner?.ClearLegs(); Refresh(); });
        legButtons.AddChild(_clearLegs);
        _noteText = new LineEdit { PlaceholderText = "A note for the chart…", MaxLength = 60, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        course.AddChild(_noteText);
        var noteButtons = new HBoxContainer();
        noteButtons.AddThemeConstantOverride("separation", Ui.SpaceS);
        course.AddChild(noteButtons);
        _placeNote = Ui.Button("Write it on the chart", "Then click the chart where it belongs", null, toggle: true);
        _placeNote.Toggled += on => { if (on) _plot.SetPressedNoSignal(false); };
        noteButtons.AddChild(_placeNote);
        _rubOut = Ui.Button("Rub out my last", "Rub out the last note you wrote (the voyage's own marks stay)", RubOutLast);
        noteButtons.AddChild(_rubOut);
        _canvas.Clicked += OnChartClicked;

        // The islands: make for one to water, or go back to the passage.
        _islandBox = new VBoxContainer();
        _islandBox.AddThemeConstantOverride("separation", Ui.SpaceS);
        side.AddChild(_islandBox);
        _islandBox.AddChild(new HSeparator());
        _islandBox.AddChild(Ui.Text("Islands", Ui.TextHeading, Ui.Brass));
        _islands = Ui.Text("", Ui.TextBody, Ui.Ink, 320);
        _islandBox.AddChild(_islands);
        _islandButtons = new VBoxContainer();
        _islandButtons.AddThemeConstantOverride("separation", Ui.SpaceS);
        _islandBox.AddChild(_islandButtons);

        // The coast: the lead, and once it is raised, what it is.
        _coastBox = new VBoxContainer { Visible = false };
        _coastBox.AddThemeConstantOverride("separation", Ui.SpaceS);
        side.AddChild(_coastBox);
        _coastBox.AddChild(new HSeparator());
        _coastBox.AddChild(Ui.Text("The coast", Ui.TextHeading, Ui.Brass));
        _coast = Ui.Text("", Ui.TextBody, Ui.Ink, 320);
        _coastBox.AddChild(_coast);
        _lead = Ui.Button("Heave the lead", "Sound: the depth, and what the tallow brings up off the bottom", HeaveTheLead);
        _coastBox.AddChild(_lead);
        _knowBox = new VBoxContainer { Visible = false };
        _knowBox.AddThemeConstantOverride("separation", Ui.SpaceS);
        _coastBox.AddChild(_knowBox);
        _landmark = new OptionButton { FocusMode = FocusModeEnum.None, ClipText = true };
        _landmark.ItemSelected += _ => Refresh();
        _knowBox.AddChild(_landmark);
        _directions = Ui.Text("", Ui.TextMeta, Ui.InkSoft, 320);
        _knowBox.AddChild(_directions);
        _knowBox.AddChild(Ui.Button("That is what we see", "Take the coast in view for this mark: the reckoning goes there, and she runs for Punta Ceniza by it", Identify));
        var along = new HBoxContainer();
        along.AddThemeConstantOverride("separation", Ui.SpaceS);
        _coastBox.AddChild(along);
        _north = Ui.Button("Run north", "Run north along the coast by eye", () => _runner?.CoastAlong(1));
        _south = Ui.Button("Run south", "Run south along the coast by eye", () => _runner?.CoastAlong(-1));
        along.AddChild(_north);
        along.AddChild(_south);
        column.AddChild(Ui.Button(Ui.ButtonKind.Primary, "Close the chart", "Close the chart  (M or Esc)", Close));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && @event.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Opens the chart on the voyage.</summary>
    public void Open(SimulationRunner runner)
    {
        _runner = runner;
        _navigator.Clear();
        _navigatorChoices.Clear();
        _navigator.AddItem("No one: you take it, or it is missed");
        _navigatorChoices.Add(null);
        foreach (var person in runner.Campaign.Crew.Where(c => c.SurvivalState == Expedition.Simulation.Characters.SurvivalState.Alive).OrderBy(c => c.Name))
        {
            _navigator.AddItem(person.Id == runner.Campaign.CaptainId ? $"{person.Name} (yourself)" : person.Name);
            _navigatorChoices.Add(person.Id);
        }

        _landmark.Clear();
        foreach (var mark in runner.Coast?.Landmarks ?? [])
        {
            _landmark.AddItem(Capitalise(mark.Name));
        }

        Visible = true;
        Refresh();
        Ui.FadeIn(_panel);
    }

    private static string Capitalise(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    // The lead is hove by the navigator if there is one, else by the first able hand.
    private void OnChartClicked(ChartPoint at)
    {
        if (_runner is null)
        {
            return;
        }

        if (_plot.ButtonPressed)
        {
            var result = _runner.PlotLeg(at);
            if (!result.Succeeded)
            {
                _legs.Text = result.Reason;
                return;
            }
        }
        else if (_placeNote.ButtonPressed)
        {
            var result = _runner.WriteOnChart(at, _noteText.Text);
            if (!result.Succeeded)
            {
                _legs.Text = result.Reason;
                return;
            }

            _noteText.Text = "";
            _placeNote.SetPressedNoSignal(false);
        }

        Refresh();
    }

    private void RubOutLast()
    {
        if (_runner?.Campaign.Voyage?.Navigation is { } navigation)
        {
            var last = navigation.Notes.ToList().FindLastIndex(n => n.ByCaptain);
            _runner.RubOut(last);
            Refresh();
        }
    }

    private void HeaveTheLead()
    {
        if (_runner?.Campaign.Voyage?.Navigation is not { } navigation)
        {
            return;
        }

        var who = navigation.Navigator is { } appointed && _runner.Campaign.Characters[appointed].CanWork
            ? appointed
            : _runner.Campaign.Crew.Where(c => c.CanWork && c.Id != _runner.Campaign.CaptainId).Select(c => (CharacterId?)c.Id).FirstOrDefault()
              ?? _runner.Campaign.CaptainId;
        if (who is { } hand)
        {
            _runner.HeaveTheLead(hand);
        }

        Refresh();
    }

    private void Identify()
    {
        if (_runner?.Coast is { } coast && _landmark.Selected >= 0 && _landmark.Selected < coast.Landmarks.Count)
        {
            _runner.IdentifyCoast(coast.Landmarks[_landmark.Selected].Id);
            Refresh();
        }
    }

    // The wind as the sailors would say it, from how far it is off her bow.
    private static string PointOfSail(double offWind) => Math.Abs(offWind) switch
    {
        < 80 => "close-hauled",
        < 110 => "on the beam",
        < 160 => "on the quarter",
        _ => "dead astern: she runs before it",
    };

    /// <summary>Redraws from the voyage; called each frame while open.</summary>
    public void Refresh()
    {
        if (_runner?.Campaign.Voyage is not { Navigation: { } navigation } ship)
        {
            return;
        }

        var day = _runner.Campaign.Clock.Day;
        var (alongX, alongY) = Uncertainty(_runner, navigation);
        var speed = ship.SpeedLeaguesPerHour * 24;
        _reckoning.Text =
            $"By your reckoning she lies at {_runner.Latitude(navigation.Reckoned.Y)}, {_runner.Longitude(navigation.Reckoned.X, navigation.Reckoned.Y)}.\n" +
            $"Course {SimulationRunner.CompassPoint(navigation.Heading)} ({navigation.Heading:0}°), " +
            $"making about {speed:0} leagues a day by the log.\n" +
            $"The wind is from the {SimulationRunner.CompassPoint(navigation.WindFrom)}" +
            (navigation.Beating
                ? $": too near her course to steer it, so she beats up to windward, {(navigation.Tack > 0 ? "on the port tack" : "on the starboard tack")}.\n"
                : $", {PointOfSail(navigation.OffWind)}.\n") +
            (navigation.Sighted
                ? $"The coast is in sight, {navigation.Offshore:0.0} leagues off."
                : $"About {ship.ReckonedRemaining:0} leagues to {ship.Destination}" + (navigation.RunningDown ? ", running down her latitude." : ", making for her latitude.")) +
            $"\nYour latitude may be out by {alongY:0} leagues; your longitude, by {alongX:0}.";
        var last = navigation.Sights.LastOrDefault();
        var window = _runner.Content.Voyage?.Route;
        var lastText = last is null
            ? "No sight has been taken yet."
            : $"Last sight: day {last.Day + 1}, by {_runner.Campaign.Characters[last.Taker].Name}: {_runner.Latitude(last.Y)}.";
        var today = navigation.LastSightDay == day
            ? "Today's sight is taken."
            : _runner.CanTakeNoonSight ? "The sun is near noon: take it now."
            : _runner.NoonNow && _runner.SunHidden ? "Noon, but the sun is hidden: there is no sight to be had today." : $"Taken between {Hour(window?.SightFrom ?? 11.5)} and {Hour(window?.SightUntil ?? 12.5)}.";
        _sight.Text = $"{lastText}\n{today}";
        _take.Disabled = !_runner.CanTakeNoonSight;
        // With the coast in sight the sun has nothing more to tell: the land gives the place.
        _sightBox.Visible = !navigation.Sighted;
        var selected = _navigatorChoices.IndexOf(navigation.Navigator);
        if (_navigator.Selected != selected)
        {
            _navigator.Select(Math.Max(selected, 0));
        }

        RefreshCoast(navigation);
        RefreshIslands(ship, navigation);
        var plan = _runner.LegPlan();
        _legs.Text = (navigation.Legs.Count == 0 ? "The passage's own course: for the offing, then down the latitude.\n" : "")
                     + string.Join("\n", plan.Select((leg, i) => $"{(i < plan.Count - 1 ? $"Leg {i + 1}" : navigation.Legs.Count == 0 ? "To the coast" : "Then the passage")}: " +
                                                                  $"{leg.Leagues:0} leagues, ~{Math.Floor(leg.LowDays):0}–{Math.Ceiling(leg.HighDays):0} d"))
                     + (_plot.ButtonPressed ? "\nClick the chart to lay off a leg." : _placeNote.ButtonPressed ? "\nClick the chart where the note belongs." : "");
        _plot.Disabled = navigation.Sighted;
        _clearLegs.Disabled = navigation.Legs.Count == 0;
        _rubOut.Disabled = !navigation.Notes.Any(n => n.ByCaptain);
        _placeNote.Disabled = _noteText.Text.Trim().Length == 0;
        _canvas.Show(_runner, navigation, alongX, alongY);
    }

    // The islands section: where each lies from her by the reckoning, and the orders that fit.
    private void RefreshIslands(VoyageState ship, Navigation navigation)
    {
        var islands = _runner?.Islands ?? [];
        _islandBox.Visible = islands.Count > 0 && !navigation.Sighted;
        if (!_islandBox.Visible || _runner is null)
        {
            return;
        }

        var lines = new List<string>();
        var call = navigation.Call;
        var bound = call is null ? null : islands.FirstOrDefault(i => i.Id == call.Island);
        if (bound is not null && call is not null)
        {
            lines.Add(call.Anchored ? $"At anchor off {bound.Name}. Send the boat in: Water the ship ashore, Forage ashore." +
                                      (bound.Sells.Count > 0 ? $" Its people will sell to her: the purse holds {ship.Funds} sueldos." : "")
                : call.Sighted ? $"{Capitalise(bound.Name)} in sight: running in to anchor."
                : $"Making for {bound.Name}, about {navigation.Reckoned.DistanceTo(bound.Charted):0} leagues by the reckoning" +
                  (bound.Reported ? ". It is only reported: it may not be there." : "."));
        }
        else
        {
            foreach (var island in islands)
            {
                var off = navigation.Reckoned.DistanceTo(island.Charted);
                lines.Add($"{Capitalise(island.Name)}{(island.Reported ? " (reported)" : "")}: about {off:0} leagues, {SimulationRunner.CompassPoint(Bearing(navigation.Reckoned, island.Charted))}. " +
                          (island.Reported ? "Said to have birds and fruit, and water of a sort." : island.Settled ? "Charted; a spring and a few houses." : "Charted."));
            }

            lines.Add($"Water for about {_runner.SupplyForecast().WaterDays:0} days.");
        }

        _islands.Text = string.Join("\n", lines);
        // Rebuild the buttons only when what they offer changes.
        var state = call is null ? "passage"
            : $"{call.Island}:{call.Anchored}:{ship.Funds}:{string.Join(",", bound?.Sells.Select(g => _runner.LotsLeft(g)) ?? [])}";
        if (state == _islandState)
        {
            return;
        }

        _islandState = state;
        foreach (var child in _islandButtons.GetChildren())
        {
            child.QueueFree();
        }

        if (call is null)
        {
            foreach (var island in islands)
            {
                var id = island.Id;
                _islandButtons.AddChild(Ui.Button(island.Reported ? $"Search for {island.Name}" : $"Make for {island.Name}",
                    "Leave the passage and steer for it, to water and forage. It costs days.", () => { _runner?.MakeForIsland(id); Refresh(); }));
            }
        }
        else
        {
            if (call.Anchored && bound is not null)
            {
                foreach (var goods in bound.Sells)
                {
                    var item = goods.Item;
                    var name = _runner.Content.Items.TryGetValue(item, out var definition) ? definition.Name.ToLowerInvariant() : item;
                    var left = _runner.LotsLeft(goods);
                    var buy = Ui.Button($"Buy {goods.Quantity} {name}: {goods.Price} sueldos ({left} left)", goods.Looks, () =>
                    {
                        _runner?.BuyFromIslanders(item);
                        _islandState = "";
                        Refresh();
                    });
                    buy.Disabled = left <= 0 || ship.Funds < goods.Price;
                    _islandButtons.AddChild(buy);
                }
            }

            _islandButtons.AddChild(Ui.Button(call.Anchored ? "Weigh anchor: back to the passage" : "Give it up: back to the passage",
                "Steer for the Ember Coast again", () => { _runner?.ResumePassage(); Refresh(); }));
        }
    }

    private static double Bearing(ChartPoint from, ChartPoint to) => (Math.Atan2(to.X - from.X, to.Y - from.Y) * 180 / Math.PI + 360) % 360;

    // The coast section: the last cast of the lead, and with the coast raised what is in view, what the
    // best hand aboard makes of it, and the sailing directions for the mark chosen to compare.
    private void RefreshCoast(Navigation navigation)
    {
        if (_runner?.Coast is not { } coast)
        {
            _coastBox.Visible = false;
            return;
        }

        _coastBox.Visible = navigation.NearWarned || navigation.Sighted || navigation.Soundings.Count > 0;
        var lines = new List<string>();
        if (navigation.Soundings.LastOrDefault() is { } cast)
        {
            var when = new SimulationClock(cast.Tick).TimeOfDay;
            lines.Add(cast.Fathoms is { } fathoms
                ? $"Last cast at {when.Hours:00}:{when.Minutes:00}: {fathoms:0} fathoms, {cast.Bottom}."
                : $"Last cast at {when.Hours:00}:{when.Minutes:00}: no bottom at {coast.ShelfFathoms:0} fathoms.");
        }
        else
        {
            lines.Add($"The bank runs out some {coast.ShelfLeagues:0} leagues from the coast: inside it the lead finds bottom, shoaling as she closes.");
        }

        if (navigation.Sighted)
        {
            var view = coast.Find(navigation.Seen);
            lines.Add(view is null ? "In view: a plain stretch of shore, nothing to know it by." : $"In view: {view.Looks}");
            if (navigation.Pending && navigation.Opinion is { } opinion && navigation.OpinionBy is { } by)
            {
                lines.Add($"{_runner.Campaign.Characters[by].Name} takes it for {coast.Find(opinion)?.Name}.");
            }

            lines.Add(navigation.CoastDirection == 0
                ? "She stands off and on, waiting for your word."
                : $"Running {(navigation.CoastDirection > 0 ? "north" : "south")} along the coast" +
                  (coast.Find(navigation.Identified) is { } taken ? $", taking it for {taken.Name}: by that, {Math.Abs(navigation.Reckoned.Y - navigation.Destination.Y):0} leagues to go." : ", by eye."));
        }

        _coast.Text = string.Join("\n", lines);
        _lead.Disabled = _runner.Campaign.Voyage?.Outcome != VoyageOutcome.UnderWay;
        _knowBox.Visible = navigation.Sighted && navigation.Seen is not null;
        _north.Visible = _south.Visible = navigation.Sighted;
        // A new mark in view: the list opens on what the best hand makes of it.
        if (_knowBox.Visible && navigation.Seen != _listedFor)
        {
            _listedFor = navigation.Seen;
            var guess = coast.Landmarks.ToList().FindIndex(l => l.Id == navigation.Opinion);
            if (guess >= 0)
            {
                _landmark.Select(guess);
            }
        }

        if (_knowBox.Visible && _landmark.Selected >= 0 && _landmark.Selected < coast.Landmarks.Count)
        {
            _directions.Text = $"The sailing directions for {coast.Landmarks[_landmark.Selected].Name}: {coast.Landmarks[_landmark.Selected].Looks}";
        }
    }

    // How far out the reckoning may be (the simulation's measure).
    internal static (double X, double Y) Uncertainty(SimulationRunner runner, Navigation navigation) => runner.ReckoningDoubt();

    private static string Hour(double hour) => $"{(int)hour:00}:{(int)Math.Round(hour % 1 * 60):00}";

    private void Close()
    {
        if (!Visible)
        {
            return;
        }

        Visible = false;
        Closed?.Invoke();
    }

    public override void _Process(double delta)
    {
        if (Visible)
        {
            Refresh();
        }
    }
}

/// <summary>The chart itself, drawn: coasts, graticule, track, fixes, the reckoned place and its doubt, the course.</summary>
public partial class ChartCanvas : Control
{
    private static readonly Color InkLine = new(0.25f, 0.18f, 0.1f);
    private static readonly Color Faint = new(0.25f, 0.18f, 0.1f, 0.22f);
    private static readonly Color Land = new("#D9C28F");
    private static readonly Color Sea = new(0.36f, 0.56f, 0.66f, 0.32f);
    private static readonly Color Red = new(0.6f, 0.15f, 0.1f);
    private SimulationRunner? _runner;
    private Navigation? _navigation;
    private double _doubtX;
    private double _doubtY;
    private Rect2 _world;
    private bool _coastSheet;

    /// <summary>The chart was clicked: where, in the chart's leagues east and north of the port.</summary>
    public event Action<ChartPoint>? Clicked;

    public override void _Ready() => MouseFilter = MouseFilterEnum.Stop;

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click && _world.Size.X > 0)
        {
            var scale = Zoom;
            var origin = (Size - _world.Size * scale) / 2;
            var local = click.Position - origin;
            Clicked?.Invoke(new ChartPoint(_world.Position.X + local.X / scale, _world.End.Y - local.Y / scale));
            AcceptEvent();
        }
    }

    /// <summary>Sets what to draw.</summary>
    public void Show(SimulationRunner runner, Navigation navigation, double doubtX, double doubtY)
    {
        (_runner, _navigation, _doubtX, _doubtY) = (runner, navigation, doubtX, doubtY);
        // The whole passage, with a margin: from a little east and north of the port to past the coast.
        // Near the coast, the coast sheet: the approach to Punta Ceniza, large enough to work on.
        var d = navigation.Destination;
        _coastSheet = runner.Coast is not null && (navigation.NearWarned || navigation.Sighted);
        _world = _coastSheet
            ? new Rect2((float)d.X - 14, (float)d.Y - 42, 62, 86)
            : new Rect2((float)d.X - 45, (float)d.Y - 70, (float)-d.X + 85, (float)-d.Y + 130);
        QueueRedraw();
    }

    private Vector2 At(double x, double y)
    {
        var scale = Mathf.Min(Size.X / _world.Size.X, Size.Y / _world.Size.Y);
        var origin = (Size - _world.Size * scale) / 2;
        return origin + new Vector2((float)(x - _world.Position.X) * scale, (float)(_world.End.Y - y) * scale);
    }

    private float Zoom => Mathf.Min(Size.X / _world.Size.X, Size.Y / _world.Size.Y);

    public override void _Draw()
    {
        // Parchment, washed blue where it is sea; the land is laid over it by the coasts.
        Ui.DrawParchment(this, new Rect2(Vector2.Zero, Size));
        DrawRect(new Rect2(new Vector2(14, 14), Size - new Vector2(28, 28)), Sea);
        if (_runner is null || _navigation is not { } navigation || _runner.Content.Voyage?.Route is not { } route)
        {
            return;
        }

        var font = Ui.TitleFont;
        var italic = Ui.TitleItalic;
        var d = navigation.Destination;

        // Graticule: every two degrees, labelled at the edges.
        var perDegree = route.LeaguesPerDegree;
        for (var lat = Math.Ceiling(route.StartLatitude + (_world.Position.Y) / perDegree); lat <= route.StartLatitude + _world.End.Y / perDegree; lat++)
        {
            if (!_coastSheet && (int)lat % 2 != 0)
            {
                continue;
            }

            var y = (lat - route.StartLatitude) * perDegree;
            DrawLine(At(_world.Position.X, y), At(_world.End.X, y), Faint, 1);
            DrawString(font, At(_world.End.X, y) + new Vector2(-44, -3), $"{lat:0}° N", HorizontalAlignment.Left, -1, 13, InkLine);
        }

        var middle = (route.StartLatitude + d.Y / 2 / perDegree) * Math.PI / 180;
        var perLonDegree = perDegree * Math.Cos(middle);
        for (var lon = Math.Ceiling(route.StartLongitude + _world.Position.X / perLonDegree); lon <= route.StartLongitude + _world.End.X / perLonDegree; lon++)
        {
            if (!_coastSheet && (int)lon % 2 != 0)
            {
                continue;
            }

            var x = (lon - route.StartLongitude) * perLonDegree;
            DrawLine(At(x, _world.Position.Y), At(x, _world.End.Y), Faint, 1);
            DrawString(font, At(x, _world.Position.Y) + new Vector2(3, -4), $"{Math.Abs(lon):0}° W", HorizontalAlignment.Left, -1, 13, InkLine);
        }

        // The coasts: Castrena about the port, running north-west and south; the Ember Coast north and
        // south through Punta Ceniza, the destination, with its bar standing off to the north.
        // Castrena turns east at its foot and runs off the chart, as the Company's charts have it.
        Coast([(25, 70), (12, 30), (6, 8), (0, 0), (4, -10), (-2, -30), (8, -60), (30, -90), (70, -102)], "Castrena");
        var ember = new List<(double, double)>();
        var charted = Math.Min(d.Y + 130, _world.End.Y);
        for (var y = Math.Max(d.Y - 70, _world.Position.Y); y <= charted; y += _coastSheet ? 1 : 5)
        {
            ember.Add((d.X + CoastBulge(y, d), y));
        }

        // North of what the sailing directions cover, no one has followed the Ember Coast: its run is guessed.
        var guessed = new List<(double, double)> { ember[^1] };
        for (var y = charted + 5; y <= _world.End.Y + 10; y += 5)
        {
            guessed.Add((d.X - (y - charted) * 0.12 + CoastBulge(y, d), y));
        }

        if (guessed.Count > 1)
        {
            Coast(guessed, "", west: true, conjectured: true);
        }

        Coast(ember, "the Ember Coast", west: true);
        Mark(0, 0, "La Bocana");
        // The marks of the sailing directions, along the coast; the destination in red.
        foreach (var mark in _runner.Coast?.Landmarks ?? [])
        {
            var at = At(d.X + CoastBulge(d.Y + mark.North, d), d.Y + mark.North);
            var colour = mark.Id == _runner.Coast!.Destination ? Red : InkLine;
            DrawLine(at, at + new Vector2(8, 0), colour, 2);
            DrawCircle(at, 3, colour);
            var name = mark.Name;
            _late.Add(() => Backed(italic, at + new Vector2(11, 5), name, 14, colour));
        }

        // The islands: a charted one drawn as land, a reported one dashed, with a query.
        foreach (var island in _runner.Islands)
        {
            var at = At(island.X, island.Y);
            var radius = Mathf.Max(5f, 2.5f * Zoom);
            if (island.Reported)
            {
                for (var a = 0; a < 16; a += 2)
                {
                    DrawArc(at, radius, a * Mathf.Tau / 16, (a + 1) * Mathf.Tau / 16, 4, InkLine, 1.2f);
                }

                DrawString(italic, at + new Vector2(radius + 4, 4), $"{island.Name} (reported) ?", HorizontalAlignment.Left, -1, 13, InkLine);
            }
            else
            {
                var blob = new Vector2[12];
                for (var k = 0; k < 12; k++)
                {
                    var a = k * Mathf.Tau / 12;
                    blob[k] = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * (0.8f + 0.25f * Mathf.Sin(k * 2.3f));
                }

                DrawColoredPolygon(blob, new Color(Land, 0.9f));
                DrawPolyline([.. blob, blob[0]], InkLine, 1.5f);
                DrawString(italic, at + new Vector2(radius + 4, 5), island.Name, HorizontalAlignment.Left, -1, 14, InkLine);
            }
        }

        // The soundings, where the reckoning put her: fathoms, or a ring with a bar for no bottom.
        foreach (var cast in navigation.Soundings)
        {
            var at = At(cast.At.X, cast.At.Y);
            if (cast.Fathoms is { } fathoms)
            {
                DrawString(font, at + new Vector2(-6, 4), $"{fathoms:0}", HorizontalAlignment.Left, -1, 10, SoundingInk);
            }
            else
            {
                DrawArc(at, 2.5f, 0, Mathf.Tau, 10, SoundingInk, 1);
                DrawLine(at + new Vector2(-3, -5), at + new Vector2(3, -5), SoundingInk, 1);
            }
        }

        // The course: to the offing on the destination's latitude, then down it to the coast.
        var reckoned = navigation.Reckoned;
        var offing = new ChartPoint(d.X + navigation.Offing, d.Y);
        if (navigation.Call is { Anchored: false } call && _runner.Islands.FirstOrDefault(i => i.Id == call.Island) is { } bound)
        {
            var aim = call.SeenAt ?? bound.Charted;
            Dashed(At(reckoned.X, reckoned.Y), At(aim.X, aim.Y));
        }
        else if (!navigation.Sighted && navigation.Call is null)
        {
            // The legs laid off, numbered, then the passage's own course from the last of them.
            var from = reckoned;
            for (var i = 0; i < navigation.Legs.Count; i++)
            {
                var leg = navigation.Legs[i];
                Dashed(At(from.X, from.Y), At(leg.X, leg.Y), Red);
                DrawArc(At(leg.X, leg.Y), 6, 0, Mathf.Tau, 16, Red, 1.5f);
                DrawString(font, At(leg.X, leg.Y) + new Vector2(-3, 4), $"{i + 1}", HorizontalAlignment.Left, -1, 10, Red);
                from = leg;
            }

            var runningDown = navigation.Legs.Count == 0 ? navigation.RunningDown : from.X <= offing.X;
            if (!runningDown)
            {
                Dashed(At(from.X, from.Y), At(offing.X, offing.Y));
            }

            Dashed(At(runningDown ? from.X : offing.X, d.Y), At(d.X, d.Y));
        }

        // What is written on the chart: the captain's notes in ink, the voyage's own marks in blue.
        foreach (var note in navigation.Notes)
        {
            var at = At(note.At.X, note.At.Y);
            var ink = note.ByCaptain ? InkLine : SoundingInk;
            DrawLine(at, at + new Vector2(0, -12), ink, 1.2f);
            DrawColoredPolygon([at + new Vector2(0, -12), at + new Vector2(7, -9.5f), at + new Vector2(0, -7)], ink);
            DrawString(font, at + new Vector2(9, -7), note.Text, HorizontalAlignment.Left, -1, 10, ink);
        }

        // The reckoned track, noon by noon, and each sight's latitude as a short bar across it.
        var track = navigation.Track.Concat([reckoned]).ToList();
        for (var i = 1; i < track.Count; i++)
        {
            DrawLine(At(track[i - 1].X, track[i - 1].Y), At(track[i].X, track[i].Y), InkLine, 2);
        }

        foreach (var sight in navigation.Sights)
        {
            var nearest = track.MinBy(p => Math.Abs(p.Y - sight.Y));
            DrawLine(At(nearest.X - 5, sight.Y), At(nearest.X + 5, sight.Y), Red, 2);
        }

        // Where she may be: an ellipse of doubt about the reckoned place, and the place itself.
        var centre = At(reckoned.X, reckoned.Y);
        var points = new Vector2[33];
        for (var i = 0; i <= 32; i++)
        {
            var a = i * Mathf.Tau / 32;
            points[i] = centre + new Vector2(Mathf.Cos(a) * (float)_doubtX, -Mathf.Sin(a) * (float)_doubtY) * Zoom;
        }

        DrawColoredPolygon(points[..32], new Color(Red, 0.12f));
        DrawPolyline(points, new Color(Red, 0.6f), 1.2f);
        DrawCircle(centre, 4.5f, Red);
        DrawString(font, centre + new Vector2(8, 16), _runner.Campaign.Voyage!.ShipName, HorizontalAlignment.Left, -1, 15, Red);

        // The coast's names over the lines; the rose in the open sea, clear of the latitudes down the east
        // side and above the scale.
        foreach (var write in _late)
        {
            write();
        }

        _late.Clear();
        Rose(new Vector2(Mathf.Min(Size.X - 90, At(_world.End.X, 0).X - 44 - 80), Size.Y - 175), 50);
        ScaleBar(new Vector2(Size.X / 2 - 90, Size.Y - 40));
        Cartouche(_coastSheet ? "The Ember Coast" : "The Ocean Sea", _coastSheet ? "the approach to Punta Ceniza" : "the passage to the Ember Coast");
    }

    private static readonly Color SoundingInk = new(0.2f, 0.32f, 0.5f);

    // The drawn coast's wander about the straight line the simulation uses, small enough not to mislead:
    // the headlands of Ceniza and the false point stand out a little.
    private static double CoastBulge(double y, ChartPoint destination)
    {
        var north = y - destination.Y;
        return 1.2 * Math.Sin(y * 0.21) + 2.2 * Math.Exp(-north * north / 8) + 1.8 * Math.Exp(-(north + 13) * (north + 13) / 8);
    }

    private readonly List<Action> _late = [];

    // Words on the chart with a wash of parchment under them, so the lines drawn after do not strike them out.
    private void Backed(Font font, Vector2 at, string text, int size, Color colour)
    {
        var extent = font.GetStringSize(text, HorizontalAlignment.Left, -1, size);
        DrawRect(new Rect2(at + new Vector2(-2, -extent.Y + 4), extent + new Vector2(4, 0)), new Color(0.93f, 0.87f, 0.72f, 0.8f));
        DrawString(font, at, text, HorizontalAlignment.Left, -1, size, colour);
    }

    private void Coast(IReadOnlyList<(double X, double Y)> line, string name, bool west = false, bool conjectured = false)
    {
        var points = line.Select(p => At(p.X, p.Y)).ToArray();
        // The land, from the coast to the chart's edge, with a darker band along the shore. A coast only
        // guessed at is drawn faint and dashed, as the old charts left what no one had seen.
        var edge = west ? 14 : Size.X - 14;
        var shade = points.Concat(points.AsEnumerable().Reverse().Select(p => new Vector2(edge, p.Y))).ToArray();
        if (conjectured)
        {
            DrawColoredPolygon(shade, new Color(Land, 0.35f));
            for (var i = 1; i < points.Length; i++)
            {
                Dashed(points[i - 1], points[i]);
            }

            var note = points[points.Length / 2] + new Vector2(-26, 0);
            DrawSetTransform(note, -Mathf.Pi / 2);
            DrawString(Ui.TitleItalic, Vector2.Zero, "not yet charted", HorizontalAlignment.Left, -1, 14, new Color(InkLine, 0.7f));
            DrawSetTransform(Vector2.Zero);
            return;
        }

        DrawColoredPolygon(shade, new Color(Land, 0.92f));
        var band = points.Concat(points.AsEnumerable().Reverse().Select(p => p + new Vector2(west ? -10 : 10, 0))).ToArray();
        DrawColoredPolygon(band, new Color(0.55f, 0.42f, 0.25f, 0.25f));
        DrawPolyline(points, InkLine, 2);
        // The name on the land, near the top of the drawn coast and clear of the graticule's labels.
        var label = points[west ? points.Length - Math.Max(4, points.Length / 4) : 1] + new Vector2(west ? -36 : 22, 0);
        if (west)
        {
            DrawSetTransform(label, -Mathf.Pi / 2);
            DrawString(Ui.TitleItalic, Vector2.Zero, name, HorizontalAlignment.Left, -1, 22, InkLine);
            DrawSetTransform(Vector2.Zero);
        }
        else
        {
            DrawString(Ui.TitleItalic, label, name, HorizontalAlignment.Left, -1, 22, InkLine);
        }
    }

    private void Mark(double x, double y, string name)
    {
        var at = At(x, y);
        DrawCircle(at, 3.5f, InkLine);
        DrawString(Ui.TitleItalic, at + new Vector2(7, -6), name, HorizontalAlignment.Left, -1, 16, InkLine);
    }

    private void Dashed(Vector2 from, Vector2 to, Color? colour = null)
    {
        var length = from.DistanceTo(to);
        for (var s = 0f; s < length; s += 10)
        {
            DrawLine(from.Lerp(to, s / length), from.Lerp(to, Mathf.Min(s + 5, length) / length), colour ?? InkLine, 1.2f);
        }
    }

    private static readonly Color WindInk = new(0.2f, 0.32f, 0.5f);

    // The scale: a bar in leagues, black and white by turns.
    private void ScaleBar(Vector2 at)
    {
        var step = _coastSheet ? 5 : 50;
        var segment = step * Zoom;
        for (var i = 0; i < 4; i++)
        {
            var box = new Rect2(at + new Vector2(i * segment, 0), new Vector2(segment, 5));
            DrawRect(box, i % 2 == 0 ? InkLine : new Color(0.95f, 0.9f, 0.78f));
            DrawRect(box, InkLine, false, 1);
            DrawString(Ui.TitleFont, at + new Vector2(i * segment - 4, -5), $"{i * step}", HorizontalAlignment.Left, -1, 12, InkLine);
        }

        DrawString(Ui.TitleFont, at + new Vector2(4 * segment - 6, -5), $"{4 * step} leagues", HorizontalAlignment.Left, -1, 12, InkLine);
    }

    // The title of the sheet in a framed cartouche, top left.
    private void Cartouche(string title, string subtitle)
    {
        var box = new Rect2(new Vector2(34, 30), new Vector2(250, 64));
        DrawRect(box, new Color(0.95f, 0.9f, 0.77f, 0.95f));
        DrawRect(box, InkLine, false, 1.5f);
        DrawRect(box.Grow(-4), new Color(InkLine, 0.6f), false, 1);
        var titleSize = Ui.TitleItalic.GetStringSize(title, HorizontalAlignment.Left, -1, 26);
        DrawString(Ui.TitleItalic, new Vector2(box.GetCenter().X - titleSize.X / 2, box.Position.Y + 32), title, HorizontalAlignment.Left, -1, 26, InkLine);
        var subSize = Ui.TitleItalic.GetStringSize(subtitle, HorizontalAlignment.Left, -1, 15);
        DrawString(Ui.TitleItalic, new Vector2(box.GetCenter().X - subSize.X / 2, box.Position.Y + 53), subtitle, HorizontalAlignment.Left, -1, 15, new Color(InkLine, 0.85f));
    }

    // A compass rose: the cardinal points, north marked, the course steered as a red needle, and the wind.
    private void Rose(Vector2 centre, float radius)
    {
        DrawCircle(centre, radius, new Color(0.94f, 0.88f, 0.74f, 0.7f));
        DrawArc(centre, radius, 0, Mathf.Tau, 48, InkLine, 1.5f);
        DrawArc(centre, radius * 0.82f, 0, Mathf.Tau, 48, Faint, 1);
        var font = Ui.TitleFont;
        // The eight points, each half lit and half shaded.
        for (var p = 0; p < 8; p++)
        {
            var a = Mathf.DegToRad(p * 45f);
            var dir = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
            var side = new Vector2(-dir.Y, dir.X);
            var length = radius * (p % 2 == 0 ? 0.95f : 0.6f);
            var width = radius * (p % 2 == 0 ? 0.14f : 0.1f);
            DrawColoredPolygon([centre, centre + dir * length, centre + side * width], new Color(0.86f, 0.74f, 0.5f));
            DrawColoredPolygon([centre, centre - side * width, centre + dir * length], new Color(0.36f, 0.26f, 0.14f));
        }

        foreach (var (angle, letter) in new[] { (0f, "N"), (90f, "E"), (180f, "S"), (270f, "W") })
        {
            var dir = new Vector2(Mathf.Sin(Mathf.DegToRad(angle)), -Mathf.Cos(Mathf.DegToRad(angle)));
            DrawString(font, centre + dir * (radius + 12) + new Vector2(-6, 6), letter, HorizontalAlignment.Left, -1, 17, InkLine);
        }

        if (_navigation is { } navigation)
        {
            var heading = new Vector2(Mathf.Sin(Mathf.DegToRad((float)navigation.Heading)), -Mathf.Cos(Mathf.DegToRad((float)navigation.Heading)));
            DrawLine(centre, centre + heading * radius * 0.92f, Red, 3);
            DrawCircle(centre, 3, Red);

            // The wind: an arrow flying in from the quarter it blows from.
            var from = new Vector2(Mathf.Sin(Mathf.DegToRad((float)navigation.WindFrom)), -Mathf.Cos(Mathf.DegToRad((float)navigation.WindFrom)));
            var tail = centre + from * radius * 1.55f;
            var tip = centre + from * radius * 1.08f;
            var side = new Vector2(-from.Y, from.X);
            DrawLine(tail, tip, WindInk, 2.5f);
            DrawColoredPolygon([tip, tip + from * 9 + side * 5, tip + from * 9 - side * 5], WindInk);
            foreach (var k in new[] { 0f, 7f })
            {
                DrawLine(tail + from * -k, tail + from * (6 - k) + side * 6, WindInk, 1.5f);
            }
        }
    }
}

using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.Shore;
using Expedition.Simulation.World;
using Godot;

namespace Expedition.Game;

/// <summary>
/// The coast around the camp, as the expedition knows it (R8, docs/R8_THE_WORLD_AROUND.md): places drawn by
/// how well they are known (told of, seen, been to), the routes walked or told of, parties on the way, and
/// the peoples' villages with how they stand. Choosing a place shows what is known of it and who said so,
/// and a party can be sent there. Nothing is drawn that no one has found or been told of.
/// </summary>
public partial class RegionView : Control
{
    private PanelContainer _panel = null!;
    private RegionCanvas _canvas = null!;
    private Label _place = null!;
    private VBoxContainer _partyBox = null!;
    private VBoxContainer _members = null!;
    private OptionButton _purpose = null!;
    private Button _send = null!;
    private Label _way = null!;
    private Label _away = null!;
    private Label _peoples = null!;
    private SimulationRunner? _runner;
    private string? _selected;
    private readonly List<(CheckBox Box, CharacterId Id)> _checks = [];
    private static readonly string[] Purposes = ["explore", "visit", "wood", "fetch"];
    private Label _survey = null!;
    private Button _move = null!;

    /// <summary>The map is closed.</summary>
    public event Action? Closed;

    /// <summary>Something to say to the captain: a party refused, or sent.</summary>
    public event Action<CommandResult, string>? Said;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Theme = Ui.BuildTheme();
        Visible = false;
        _panel = Ui.Frame(Ui.SpaceL, Ui.SpaceL);
        _panel.SetAnchorsPreset(LayoutPreset.Center);
        _panel.OffsetLeft = -590;
        _panel.OffsetRight = 590;
        _panel.OffsetTop = -330;
        _panel.OffsetBottom = 310;
        AddChild(_panel);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", Ui.SpaceL);
        _panel.AddChild(row);
        _canvas = new RegionCanvas { CustomMinimumSize = new Vector2(730, 600), SizeFlagsVertical = SizeFlags.ExpandFill, ClipContents = true };
        _canvas.Chosen += id =>
        {
            _selected = id;
            Refresh();
        };
        row.AddChild(_canvas);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(380, 0) };
        column.AddThemeConstantOverride("separation", Ui.SpaceM);
        row.AddChild(column);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        column.AddChild(scroll);
        var side = new VBoxContainer { CustomMinimumSize = new Vector2(370, 0) };
        side.AddThemeConstantOverride("separation", Ui.SpaceM);
        scroll.AddChild(side);
        side.AddChild(Ui.Text("The coast", Ui.TextHeading + 2, Ui.Brass));
        side.AddChild(Ui.Text("As far as it is known: told of (dashed, ?), seen, or been to. Click a place.", Ui.TextMeta, Ui.InkSoft, 360));
        _place = Ui.Text("", Ui.TextBody, Ui.Ink, 360);
        side.AddChild(_place);
        // A landing site looked over (step 2, the move): what it would be to settle there, and the order to go.
        _survey = Ui.Text("", Ui.TextMeta + 1, Ui.Ink, 360);
        side.AddChild(_survey);
        _move = Ui.Button("Move the camp here", "Everyone goes, the sick carried, each with a pack of the stores, the boat taking more along the shore. "
            + "What cannot be carried stays where it lies, to be fetched; what was built stays standing.", Move);
        side.AddChild(_move);

        _partyBox = new VBoxContainer();
        _partyBox.AddThemeConstantOverride("separation", Ui.SpaceS);
        side.AddChild(_partyBox);
        _partyBox.AddChild(new HSeparator());
        _partyBox.AddChild(Ui.Text("Send a party", Ui.TextHeading, Ui.Brass));
        _members = new VBoxContainer();
        _partyBox.AddChild(_members);
        _purpose = new OptionButton { FocusMode = FocusModeEnum.None };
        _purpose.AddItem("To see what is there");
        _purpose.AddItem("To call on its people");
        _purpose.AddItem("To cut embrawood");
        _purpose.AddItem("To fetch what was left at the old camp");
        _purpose.ItemSelected += _ => Refresh();
        _partyBox.AddChild(_purpose);
        _way = Ui.Text("", Ui.TextMeta + 1, Ui.InkSoft, 360);
        _partyBox.AddChild(_way);
        _send = Ui.Button("Send them", "They take food for the days there and back from the stores, and do no camp work while away", Send);
        _partyBox.AddChild(_send);

        side.AddChild(new HSeparator());
        side.AddChild(Ui.Text("Away", Ui.TextHeading, Ui.Brass));
        _away = Ui.Text("", Ui.TextBody, Ui.Ink, 360);
        side.AddChild(_away);
        side.AddChild(new HSeparator());
        side.AddChild(Ui.Text("The peoples", Ui.TextHeading, Ui.Brass));
        _peoples = Ui.Text("", Ui.TextBody, Ui.Ink, 360);
        side.AddChild(_peoples);
        column.AddChild(Ui.Button("Close", "Close the map  (M or Esc)", Close));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && @event.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Opens the map ashore.</summary>
    public void Open(SimulationRunner runner, string? select = null)
    {
        _runner = runner;
        _selected = select ?? runner.CampPlace?.Id;
        Visible = true;
        RebuildMembers();
        Refresh();
    }

    private void Close()
    {
        Visible = false;
        Closed?.Invoke();
    }

    private void RebuildMembers()
    {
        foreach (var child in _members.GetChildren())
        {
            child.QueueFree();
        }

        _checks.Clear();
        if (_runner is null)
        {
            return;
        }

        foreach (var person in _runner.Campaign.Crew.Where(c => c.CanWork && !_runner.Away(c.Id) && _runner.Campaign.Placements.ContainsKey(c.Id)).OrderBy(c => c.Name))
        {
            var tongue = person.Languages.Get("language.ahuneri") is var level and > 0 ? $", Ahuneri {level}" : "";
            var tracker = person.Skills.Get("skill.tracking") is var track and >= 2 ? ", tracker" : "";
            var box = new CheckBox { Text = person.Name + tongue + tracker, FocusMode = FocusModeEnum.None };
            box.Toggled += _ => Refresh();
            _members.AddChild(box);
            _checks.Add((box, person.Id));
        }
    }

    private void Send()
    {
        if (_runner is null || _selected is null)
        {
            return;
        }

        var party = _checks.Where(c => c.Box.ButtonPressed).Select(c => c.Id).ToList();
        var result = _runner.SendParty(party, _selected, Purposes[Math.Max(0, _purpose.Selected)]);
        Said?.Invoke(result, "The party has set out.");
        if (result.Succeeded)
        {
            RebuildMembers();
        }

        Refresh();
    }

    private void Move()
    {
        if (_runner is null || _selected is null)
        {
            return;
        }

        var result = _runner.MoveCamp(_selected);
        Said?.Invoke(result, "The camp is struck and on the move.");
        RebuildMembers();
        Refresh();
    }

    /// <summary>Redraws the map and the column from the simulation.</summary>
    public void Refresh()
    {
        if (_runner is null || _runner.Region is not { } region || _runner.Campaign.World is not { } world)
        {
            return;
        }

        _canvas.Show(_runner, _selected);
        var place = _selected is null ? null : region.Place(_selected);
        var level = place is null ? PlaceKnowledge.Unknown : _runner.PlaceKnown(place.Id);
        if (place is null || level == PlaceKnowledge.Unknown)
        {
            _place.Text = "Nothing chosen.";
            _partyBox.Visible = false;
            _survey.Text = "";
            _move.Visible = false;
        }
        else
        {
            var source = world.Places.TryGetValue(place.Id, out var known) ? known.Source : "";
            var told = source.StartsWith("people.", StringComparison.Ordinal) ? region.Community(source)?.Name ?? source : source;
            var how = level switch
            {
                PlaceKnowledge.Visited => "Been to.",
                PlaceKnowledge.Seen => "Seen, not yet been to.",
                _ => $"Told of, by {told}.",
            };
            var text = level == PlaceKnowledge.Visited ? place.Text : place.Report;
            var people = place.People is { } peopleId && region.Community(peopleId) is { } community
                ? $"\n{community.Name} ({community.People}): {_runner.Stance(peopleId)}."
                : "";
            var ground = place.Ground is { } owners && (_runner.Learned("know.ground") || level == PlaceKnowledge.Visited)
                ? $"\nThe ground is {region.Community(owners)?.Name ?? owners}'s." : "";
            var warn = place.Sacred ? "\nNot to be entered: going there would be a grave offence." : "";
            _place.Text = $"{place.Name}\n{how}\n{text}{people}{ground}{warn}";
            var isCamp = place.Id == _runner.CampPlace?.Id;
            _partyBox.Visible = !isCamp;
            var survey = _runner.Survey(place.Id);
            var left = _runner.LeftCampAt(place.Id) is { } old
                ? $"\nThe old camp: {old.Buildings.Count} buildings standing; {_runner.StoresOf(old).Sum(q => q.Value * (_runner.Content.Items.TryGetValue(q.Key, out var item) ? item.WeightKg : 0)):0} kg of stores left there"
                  + (old.Afloat.Count > 0 ? ", and more still aboard" : "") + "."
                : "";
            _survey.Text = survey.Count == 0
                ? place.Site is not null ? "A landing place. Send a party to look it over as a place to settle." + left : left.TrimStart('\n')
                : "As a place to settle:\n" + string.Join("\n", survey.Select(l => $"{l.Topic}: {l.Text}")) + left;
            _move.Visible = place.Site is not null && !isCamp;
            _move.Disabled = world.Journeys.Any(j => j.Purpose == "move");
            var purpose = Purposes[Math.Max(0, _purpose.Selected)];
            var chosen = _checks.Count(c => c.Box.ButtonPressed);
            if (_runner.Way(place.Id) is { } way)
            {
                _way.Text = $"About {way.Days:0.#} days each way{(way.Guessed ? ", the way guessed: it may take longer" : "")}."
                            + (purpose == "wood" && !place.Wood ? " No embrawood grows there." : "");
            }
            else
            {
                _way.Text = "No one knows the way there.";
            }

            _send.Disabled = chosen is < 1 or > 4;
        }

        var now = _runner.Campaign.Clock.Tick;
        _away.Text = world.Journeys.Count == 0
            ? "No one is away."
            : string.Join("\n", world.Journeys.Select(j =>
                $"{string.Join(", ", j.Members.Select(m => _runner.Campaign.Characters[m].Name))}: {(j.Arrived ? "coming back from" : "on the way to")} {region.Place(j.Destination)?.Name}, "
                + $"back in about {Math.Max(0, (j.Returns - now) / (double)SimulationClock.TicksPerDay):0.#} days."));
        _peoples.Text = string.Join("\n\n", region.Communities.Select(c =>
        {
            var state = world.Communities.TryGetValue(c.Id, out var s) ? s : null;
            if (state is null || (!state.Met && state.Watched == 0))
            {
                return $"{c.Name}: not met.";
            }

            var owed = state.GiftOwedSince > 0 ? " Their gift is unanswered." : "";
            var leave = state.Leave.Count == 0 ? "" : $" Leave given: {string.Join(", ", state.Leave.Select(l => l == "wood" ? "to cut the red wood" : "to stay on their ground"))}.";
            return $"{c.Name} ({c.People}): {_runner.Stance(c.Id)}, {SimulationRunner.TrustWords(state.Trust)}.{owed}{leave}";
        }));
    }
}

/// <summary>The coast drawn as it is known.</summary>
public partial class RegionCanvas : Control
{
    private static readonly Color Sea = new(0.4f, 0.6f, 0.7f, 0.7f);
    private static readonly Color InkLine = new(0.28f, 0.2f, 0.12f);
    private static readonly Color Faint = new(0.28f, 0.2f, 0.12f, 0.35f);
    private static readonly Color Red = new(0.6f, 0.15f, 0.1f);
    private static readonly Color Green = new(0.2f, 0.42f, 0.18f);
    private static readonly Color Amber = new(0.72f, 0.45f, 0.1f);
    private SimulationRunner? _runner;
    private string? _selected;
    // Leagues shown: west, south, east, north.
    private static readonly Rect2 World = new(-10, -9, 22, 26);

    /// <summary>A place was clicked.</summary>
    public event Action<string>? Chosen;

    public override void _Ready() => MouseFilter = MouseFilterEnum.Stop;

    /// <summary>Sets what to draw.</summary>
    public void Show(SimulationRunner runner, string? selected)
    {
        (_runner, _selected) = (runner, selected);
        QueueRedraw();
    }

    private float Leagues => Mathf.Min(Size.X / World.Size.X, Size.Y / World.Size.Y);

    private Vector2 At(double x, double y)
    {
        var origin = (Size - World.Size * Leagues) / 2;
        return origin + new Vector2((float)(x - World.Position.X) * Leagues, (float)(World.End.Y - y) * Leagues);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click || _runner?.Region is not { } region)
        {
            return;
        }

        var nearest = region.Places.Where(p => _runner.PlaceKnown(p.Id) != PlaceKnowledge.Unknown)
            .OrderBy(p => At(p.X, p.Y).DistanceTo(click.Position)).FirstOrDefault();
        if (nearest is not null && At(nearest.X, nearest.Y).DistanceTo(click.Position) < 30)
        {
            Chosen?.Invoke(nearest.Id);
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        Ui.DrawParchment(this, new Rect2(Vector2.Zero, Size));
        if (_runner?.Region is not { } region || _runner.Campaign.World is not { } world)
        {
            return;
        }

        var font = Ui.TitleItalic;
        // The sea to the west of the coast, as the Company's charts have it; the lagoon once anyone has seen it.
        (double X, double Y)[] coast = [(9, -10), (6, -6), (2, -2.2), (0, 0), (-1, 2), (-2, 3.5), (-3, 6), (-4, 9), (-6, 11), (-8, 17)];
        var shore = coast.Select(c => At(c.X, c.Y)).ToArray();
        var sea = shore.Concat([At(-40, 18), At(-40, -10)]).ToArray();
        DrawColoredPolygon(sea, Sea);
        DrawPolyline(shore, InkLine, 2);
        if (_runner.PlaceKnown("place.lagoon_shore") >= PlaceKnowledge.Seen || _runner.PlaceKnown("place.mahenu") >= PlaceKnowledge.Seen)
        {
            var lagoon = Enumerable.Range(0, 24).Select(i => At(2.8 + 2.2 * Math.Cos(i * Math.Tau / 24), 2.6 + 1.6 * Math.Sin(i * Math.Tau / 24))).ToArray();
            DrawColoredPolygon(lagoon, Sea.Lightened(0.15f));
            DrawPolyline([.. lagoon, lagoon[0]], Faint, 1);
        }

        // A scale of leagues.
        DrawLine(At(-9, -8), At(-6, -8), InkLine, 2);
        DrawString(font, At(-9, -8) + new Vector2(0, -6), "3 leagues", HorizontalAlignment.Left, -1, 11, InkLine);

        // The routes known: walked or told of.
        foreach (var route in region.Routes.Where(r => world.Routes.Contains(r.Key)))
        {
            var (a, b) = (region.Place(route.From)!, region.Place(route.To)!);
            DrawDashedLine(At(a.X, a.Y), At(b.X, b.Y), route.Way == "lagoon" || route.Way == "river" ? Sea.Darkened(0.45f) : InkLine, 1.5f, 6);
        }

        // Labels already drawn, so one close by is set below the other rather than over it.
        var labels = new List<Rect2>();
        foreach (var place in region.Places)
        {
            var level = _runner.PlaceKnown(place.Id);
            if (level == PlaceKnowledge.Unknown)
            {
                continue;
            }

            var at = At(place.X, place.Y);
            var colour = place.People is { } people ? StanceColour(_runner.Stance(people)) : place.Sacred ? Red : InkLine;
            if (place.Id == _selected)
            {
                DrawArc(at, 14, 0, Mathf.Tau, 24, Amber, 3);
            }

            switch (level)
            {
                case PlaceKnowledge.Visited:
                    DrawCircle(at, 7, colour);
                    break;
                case PlaceKnowledge.Seen:
                    DrawArc(at, 7, 0, Mathf.Tau, 18, colour, 2.5f);
                    break;
                default:
                    for (var i = 0; i < 8; i += 2)
                    {
                        DrawArc(at, 7, i * Mathf.Tau / 8, (i + 1) * Mathf.Tau / 8, 4, colour, 2);
                    }

                    DrawString(font, at + new Vector2(-3, 4), "?", HorizontalAlignment.Left, -1, 12, colour);
                    break;
            }

            if (place.Id == _runner.CampPlace?.Id)
            {
                DrawRect(new Rect2(at + new Vector2(-5, -18), new Vector2(10, 6)), Red);
            }

            var label = new Rect2(at + new Vector2(11, -8), font.GetStringSize(place.Name, HorizontalAlignment.Left, -1, 16) + new Vector2(0, 2));
            while (labels.Any(l => l.Intersects(label)))
            {
                label.Position += new Vector2(0, 17);
            }

            labels.Add(label);
            DrawString(font, label.Position + new Vector2(0, 14), place.Name, HorizontalAlignment.Left, -1, 16, InkLine);
        }

        // Parties on the way, where they should be by now.
        var now = _runner.Campaign.Clock.Tick;
        foreach (var journey in world.Journeys)
        {
            var share = journey.Arrived
                ? 1 - Math.Clamp((now - journey.Arrives) / (double)Math.Max(1, journey.Returns - journey.Arrives), 0, 1)
                : Math.Clamp((now - journey.Left) / (double)Math.Max(1, journey.Arrives - journey.Left), 0, 1);
            var points = journey.Path.Select(p => region.Place(p)!).Select(p => At(p.X, p.Y)).ToList();
            var along = share * (points.Count - 1);
            var leg = Math.Min(points.Count - 2, (int)along);
            var here = points.Count < 2 ? points[0] : points[leg].Lerp(points[leg + 1], (float)(along - leg));
            DrawCircle(here, 5, Red);
            DrawString(font, here + new Vector2(8, -6), $"{journey.Members.Count}", HorizontalAlignment.Left, -1, 11, Red);
        }
    }

    private static Color StanceColour(string stance) => stance switch
    {
        "friendly" or "trading" => Green,
        "curious" or "watching" => InkLine,
        "wary" => Amber,
        _ => Red,
    };
}

using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Tasks;
using Expedition.Simulation.World;
using Godot;

namespace Expedition.Game;

/// <summary>The management screens reachable from the navigation bar (UI_UX_DESIGN_SYSTEM.md §12).</summary>
public enum PanelId
{
    People,
    Expedition,
    Cargo,
    Ship,
    Chart,
    Chronicle,
}

/// <summary>What may stop the clock on its own (UI_UX §18): the player chooses in the menu.</summary>
public enum AutoPause
{
    InjuryAndDeath,
    Weather,
    ShipDamage,
    Shortage,
    Events,
    Landfall,
}

/// <summary>
/// The interface shell shared by every phase (UI_UX_DESIGN_SYSTEM.md §2, §12–19). The world gets the
/// screen; only what is needed at a glance stays up:
/// <list type="bullet">
/// <item>top left: day, time and speed;</item>
/// <item>top centre: the management screens (People, Expedition, Cargo, Ship, Chart, Chronicle) and the menu;</item>
/// <item>top right: the phase's few critical gauges, as chips;</item>
/// <item>bottom left: who is selected, and the current objective;</item>
/// <item>alerts under the bar, short notices at the bottom.</item>
/// </list>
/// Management screens open one at a time in a drawer on the left. The HUD shows only what the captain
/// knows (§20–23) and holds no game state (§55).
/// </summary>
public partial class Hud : CanvasLayer
{
    private static readonly string[] DefaultSpeedNames = ["Pause", "1×", "10×", "60×", "240×", "720×"];

    private readonly List<(Button Button, int Speed)> _speedButtons = [];
    private readonly List<(Button Button, CharacterId Id)> _crewButtons = [];
    private readonly List<(Button Button, string TaskId)> _taskButtons = [];
    private readonly List<(CheckBox Box, string TaskId)> _dutyBoxes = [];
    private readonly Dictionary<string, ProgressBar> _needBars = [];
    private readonly Dictionary<PanelId, Button> _navButtons = [];
    private readonly Dictionary<PanelId, VBoxContainer> _panelBodies = [];
    private readonly HashSet<AutoPause> _autoPause = [AutoPause.InjuryAndDeath, AutoPause.Weather, AutoPause.ShipDamage, AutoPause.Shortage, AutoPause.Events, AutoPause.Landfall];

    private Control _root = null!;
    private Label _clock = null!;
    private Label _place = null!;
    private HBoxContainer _status = null!;
    private HBoxContainer _sailOrders = null!;
    private PanelContainer _statusPanel = null!;
    private PanelContainer _selectedPanel = null!;
    private Label _selectedName = null!;
    private Label _selectedDoing = null!;
    private Label _selectedWarnings = null!;
    private Label _objective = null!;
    private PanelContainer _objectivePanel = null!;
    private VBoxContainer _alerts = null!;
    private PanelContainer _paused = null!;
    private Label _pausedText = null!;
    private VBoxContainer _toasts = null!;
    private PanelContainer _drawer = null!;
    private Label _drawerTitle = null!;
    private PanelId? _open;
    private PopupMenu _menu = null!;
    private PanelContainer _help = null!;
    private float _uiScale = 1f;

    // People screen
    private VBoxContainer _crewList = null!;
    private Label _name = null!;
    private Label _doing = null!;
    private Label _details = null!;
    private Label _pack = null!;
    private VBoxContainer _taskList = null!;
    private Label _why = null!;
    private Label _queue = null!;
    private Label _dutyNote = null!;
    private VBoxContainer _dutyList = null!;
    private CheckBox _urgent = null!;

    // Cargo, Ship, Chronicle screens
    private Label _manifest = null!;
    private Label _shipReport = null!;
    private Button _rations = null!;
    private bool _rationsOn;
    private readonly List<(Button Button, Expedition.Simulation.Voyage.SailPlan Plan)> _sailButtons = [];
    private RichTextLabel _chronicle = null!;
    private string _chronicleFilter = "all";
    private int _chronicleCount = -1;
    private int _chronicleSeen;
    private int _unread;

    // Event window and ending
    private PanelContainer _event = null!;
    private Label _eventTitle = null!;
    private Label _eventText = null!;
    private VBoxContainer _eventChoices = null!;
    private Control _ending = null!;
    private Label _endingTitle = null!;
    private Label _endingText = null!;

    private ChronicleFormatter _formatter = null!;
    private SimulationRunner? _runner;

    /// <summary>The player chose a time speed (index into the speeds Main defines).</summary>
    public event Action<int>? SpeedChosen;

    /// <summary>Save now.</summary>
    public event Action? SaveRequested;

    /// <summary>Load the quick save.</summary>
    public event Action? LoadRequested;

    /// <summary>The player picked someone in a list.</summary>
    public event Action<CharacterId>? CharacterChosen;

    /// <summary>An order for the selected crew member.</summary>
    public event Action<string, TaskPriority>? TaskOrdered;

    /// <summary>A standing duty turned on or off.</summary>
    public event Action<string, bool>? DutyToggled;

    /// <summary>Carry on after an alert paused the game.</summary>
    public event Action? ContinueRequested;

    /// <summary>A choice in an event window.</summary>
    public event Action<string>? EventChoiceMade;

    /// <summary>The event window was closed after its outcome.</summary>
    public event Action? EventClosed;

    /// <summary>A sail order.</summary>
    public event Action<Expedition.Simulation.Voyage.SailPlan>? SailPlanChosen;

    /// <summary>Short rations on or off.</summary>
    public event Action<bool>? RationingToggled;

    /// <summary>Start a new campaign.</summary>
    public event Action? NewVoyageRequested;

    /// <summary>Clear the selected crew member's queued orders.</summary>
    public event Action? QueueCleared;

    /// <summary>Stop the selected crew member's current task.</summary>
    public event Action? StopOrdered;

    /// <summary>An alert or record asked the camera to go to someone (§17, §49).</summary>
    public event Action<CharacterId>? FocusRequested;

    public override void _Ready()
    {
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = Ui.BuildTheme() };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        BuildClock();
        BuildChecklist();
        BuildNavigation();
        BuildStatus();
        BuildSelected();
        BuildAlerts();
        BuildToasts();
        BuildDrawer();
        BuildHelp();
        BuildEvent();
        BuildEnding();
    }

    // ================================================================ public interface used by Main

    /// <summary>Whether an event window is open.</summary>
    public bool EventShowing => _event.Visible;

    /// <summary>Whether the game is paused by an alert waiting to be acknowledged.</summary>
    public bool AlertShowing => _paused.Visible;

    /// <summary>Whether a management screen is open.</summary>
    public bool PanelOpen => _open is not null;

    /// <summary>Whether this kind of event stops the clock (menu, §18).</summary>
    public bool Pauses(AutoPause category) => _autoPause.Contains(category);

    /// <summary>The body of a management screen, for phase-specific content (the port adds its journal).</summary>
    public VBoxContainer PanelBody(PanelId panel) => _panelBodies[panel];

    /// <summary>Opens a management screen, or closes it if it is already open.</summary>
    public void TogglePanel(PanelId panel)
    {
        // The chart is a view of its own, not a drawer: Main opens it.
        if (panel == PanelId.Chart)
        {
            if (!_navButtons[panel].Disabled)
            {
                ChartRequested?.Invoke();
            }

            return;
        }

        if (_open == panel)
        {
            ClosePanel();
            return;
        }

        if (!_navButtons[panel].Disabled)
        {
            OpenPanel(panel);
        }
    }

    /// <summary>Opens a management screen.</summary>
    public void OpenPanel(PanelId panel)
    {
        foreach (var (id, body) in _panelBodies)
        {
            body.Visible = id == panel;
        }

        foreach (var (id, button) in _navButtons)
        {
            button.SetPressedNoSignal(id == panel);
        }

        _drawerTitle.Text = panel switch
        {
            PanelId.People => "People — the roster",
            PanelId.Expedition => "Expedition — objectives and papers",
            PanelId.Cargo => "Cargo — the manifest",
            PanelId.Ship => "Ship — condition and orders",
            PanelId.Chart => "Chart",
            _ => "Chronicle — the record of the expedition",
        };
        if (panel == PanelId.Chronicle)
        {
            _unread = 0;
            _chronicleCount = -1;
        }

        _open = panel;
        Ui.FadeIn(_drawer);
    }

    /// <summary>Closes the open management screen; false if none was open.</summary>
    public bool ClosePanel()
    {
        if (_open is null)
        {
            return false;
        }

        _open = null;
        _drawer.Visible = false;
        foreach (var button in _navButtons.Values)
        {
            button.SetPressedNoSignal(false);
        }

        return true;
    }

    /// <summary>Renames the speed buttons, for example for the slower speeds in port (D-023).</summary>
    public void SetSpeedNames(IReadOnlyList<string> names, IReadOnlyList<string>? tips = null)
    {
        foreach (var (button, speed) in _speedButtons)
        {
            button.Text = speed == 0 ? "❚❚" : names[speed];
            button.TooltipText = (tips is null ? names[speed] : tips[speed]) + $"  (key {speed + 1}; Space pauses)";
        }
    }

    /// <summary>
    /// Pauses for something that needs the captain, with the reasons, until the player carries on (§17).
    /// </summary>
    public void ShowAlert(string text)
    {
        _pausedText.Text = text;
        Ui.FadeIn(_paused);
    }

    /// <summary>Clears the paused notice.</summary>
    public void HideAlert() => _paused.Visible = false;

    /// <summary>
    /// Adds an alert: severity, cause, and who it concerns, with a Focus button where there is someone to
    /// look at (§17). Critical alerts stay until dismissed; others fade.
    /// </summary>
    public void PushAlert(Severity severity, string title, string detail = "", CharacterId? focus = null, string when = "")
    {
        var panel = Ui.PanelOf(new Color(0.07f, 0.05f, 0.04f, 0.88f), Ui.Of(severity), Ui.SpaceL, Ui.SpaceS);
        var row = new HBoxContainer();
        panel.AddChild(row);
        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 0);
        row.AddChild(column);
        column.AddChild(Ui.Text($"{Ui.IconOf(severity)} {severity.ToString().ToUpperInvariant()} — {title}", Ui.TextBody, Ui.Of(severity), 380));
        if (!string.IsNullOrEmpty(detail) || !string.IsNullOrEmpty(when))
        {
            column.AddChild(Ui.Text(string.Join(" · ", new[] { detail, when }.Where(s => s.Length > 0)), Ui.TextMeta, Ui.InkSoft, 380));
        }

        if (focus is { } who)
        {
            row.AddChild(Ui.Button("Focus", "Look at them", () => FocusRequested?.Invoke(who)));
        }

        row.AddChild(Ui.Button("×", "Dismiss", () => panel.QueueFree()));
        // Newest first, under the paused notice and above the routine notices; at most four kept.
        _alerts.AddChild(panel);
        _alerts.MoveChild(panel, 1);
        while (_alerts.GetChildCount() > 6)
        {
            var oldest = _alerts.GetChild(_alerts.GetChildCount() - 2);
            _alerts.RemoveChild(oldest);
            oldest.QueueFree();
        }

        Ui.FadeIn(panel);
        if (severity != Severity.Critical)
        {
            var timer = GetTree().CreateTimer(severity == Severity.Urgent ? 25 : 12);
            timer.Timeout += () => { if (IsInstanceValid(panel)) panel.QueueFree(); };
        }
    }

    /// <summary>A short notice at the bottom that fades on its own (§18): routine news, confirmations.</summary>
    public void Toast(string text, Severity severity = Severity.Information)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var panel = Ui.PanelOf(Ui.Panel, null, Ui.SpaceL, Ui.SpaceS);
        panel.AddChild(Ui.Text(severity == Severity.Information ? text : $"{Ui.IconOf(severity)} {text}", Ui.TextBody, Ui.Of(severity), 560));
        _toasts.AddChild(panel);
        while (_toasts.GetChildCount() > 3)
        {
            var oldest = _toasts.GetChild(0);
            _toasts.RemoveChild(oldest);
            oldest.QueueFree();
        }

        Ui.FadeIn(panel);
        var tween = panel.CreateTween();
        tween.TweenInterval(5.5);
        tween.TweenProperty(panel, "modulate:a", 0f, 0.6f);
        tween.TweenCallback(Callable.From(() => { if (IsInstanceValid(panel)) panel.QueueFree(); }));
    }

    /// <summary>Shows a one-line message as a notice.</summary>
    public void ShowStatus(string message) => Toast(message);

    /// <summary>Shows the result of a command: the success message, or the reason it was refused (§44).</summary>
    public void ShowResult(CommandResult result, string successMessage = "")
    {
        if (result.Succeeded)
        {
            Toast(successMessage);
        }
        else
        {
            Toast(result.Reason, Severity.Attention);
        }
    }

    /// <summary>Opens an event: its story and the captain's choices, with their odds. A rare modal (§54).</summary>
    public void ShowEvent(string title, string text, IReadOnlyList<(string Id, string Text, bool Available, string? Note)> choices)
    {
        _eventTitle.Text = title;
        _eventText.Text = text;
        foreach (var child in _eventChoices.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var (id, label, available, note) in choices)
        {
            var button = Ui.Button(note is null ? label : $"{label}   ({note})", available ? "" : $"Not possible: {note}", () => EventChoiceMade?.Invoke(id));
            button.Disabled = !available;
            button.Alignment = HorizontalAlignment.Left;
            button.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _eventChoices.AddChild(button);
        }

        Ui.FadeIn(_event);
    }

    /// <summary>Shows what came of the choice, with a button to carry on.</summary>
    public void ShowEventOutcome(string choice, bool succeeded, string text)
    {
        _eventText.Text = text;
        foreach (var child in _eventChoices.GetChildren())
        {
            child.QueueFree();
        }

        _eventChoices.AddChild(Ui.Button("Carry on", "", () => EventClosed?.Invoke()));
    }

    /// <summary>For tests and screenshots: make a choice without clicking.</summary>
    public void DeveloperChoose(string id) => EventChoiceMade?.Invoke(id);

    /// <summary>Closes the event window.</summary>
    public void HideEvent() => _event.Visible = false;

    /// <summary>Shows the end of the voyage, won or lost.</summary>
    public void ShowEnding(string title, string text)
    {
        _endingTitle.Text = title;
        _endingText.Text = text;
        _ending.Visible = true;
    }

    /// <summary>Hides the end screen.</summary>
    public void HideEnding() => _ending.Visible = false;

    /// <summary>Rebuilds the roster and order buttons for a newly started or loaded campaign.</summary>
    public void Bind(SimulationRunner runner)
    {
        _runner = runner;
        foreach (var (button, _) in _crewButtons) button.QueueFree();
        foreach (var (button, _) in _taskButtons) button.QueueFree();
        foreach (var (box, _) in _dutyBoxes) box.QueueFree();
        _crewButtons.Clear();
        _taskButtons.Clear();
        _dutyBoxes.Clear();
        _chronicleCount = -1;
        _chronicleSeen = runner.Campaign.Chronicle.Entries.Count;
        _unread = 0;
        _formatter = new ChronicleFormatter(runner.Campaign, runner.Content);

        // Only the expedition's own people are listed; the port's people are met in the world.
        foreach (var character in runner.Campaign.Characters.Values.Where(c => c.Membership == Membership.Crew).OrderBy(c => c.Id))
        {
            var id = character.Id;
            var button = Ui.Button("", "Select; Focus shows them on deck", () => CharacterChosen?.Invoke(id));
            button.Alignment = HorizontalAlignment.Left;
            button.ClipText = true;
            _crewList.AddChild(button);
            _crewButtons.Add((button, id));
        }

        // Work that is only done ashore (at the forge, in the tavern) is not ordered aboard.
        var aboard = runner.Content.Locations.Values.Where(l => l.Id != runner.Content.Port?.Location).SelectMany(l => l.Spots).SelectMany(s => s.Tasks).ToHashSet();
        foreach (var task in runner.Content.Tasks.Values.Where(t => !t.NeedsWorkSpot || aboard.Contains(t.Id)).OrderBy(t => t.Name))
        {
            var taskId = task.Id;
            var button = Ui.Button(task.Name, "", () => TaskOrdered?.Invoke(taskId, CurrentPriority));
            button.Alignment = HorizontalAlignment.Left;
            button.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _taskList.AddChild(button);
            _taskButtons.Add((button, taskId));

            // Rest is not a duty: everyone rests of their own accord when tired.
            if (task.IsRest)
            {
                continue;
            }

            var box = new CheckBox { Text = DutyLabel(task), FocusMode = Control.FocusModeEnum.None };
            box.Toggled += on => DutyToggled?.Invoke(taskId, on);
            _dutyList.AddChild(box);
            _dutyBoxes.Add((box, taskId));
        }

        _navButtons[PanelId.Ship].Disabled = false;
    }

    /// <summary>Refreshes the visible parts from the simulation. Called once per frame; hidden screens are skipped (§57).</summary>
    public void Refresh(SimulationRunner runner, CharacterId? selected, int speedIndex)
    {
        _runner = runner;
        var campaign = runner.Campaign;
        var clock = campaign.Clock;
        var day = clock.Tick / SimulationClock.TicksPerDay + 1;
        var minute = clock.Tick % SimulationClock.TicksPerDay;
        _clock.Text = $"DAY {day} · {minute / 60:00}:{minute % 60:00}";
        _place.Text = campaign.Port is not null ? "In port · La Bocana"
            : campaign.Voyage is { } at ? $"At sea · bound for {at.Destination}"
            : campaign.Phase.ToString();
        foreach (var (button, speed) in _speedButtons)
        {
            button.SetPressedNoSignal(speed == speedIndex);
        }

        RefreshStatus(runner);
        RefreshSelectedCard(runner, selected);
        RefreshChecklist(runner);
        RefreshChronicleNews(runner);

        switch (_open)
        {
            case PanelId.People:
                RefreshRoster(runner, selected);
                RefreshRecord(runner, selected);
                break;
            case PanelId.Cargo:
                RefreshManifest(runner);
                break;
            case PanelId.Ship:
                RefreshShip(runner);
                break;
            case PanelId.Chronicle:
                RefreshChronicle(runner);
                break;
        }
    }

    /// <summary>Short text for what a character is doing.</summary>
    public static string Activity(SimulationRunner runner, Character character)
    {
        if (character.SurvivalState != SurvivalState.Alive)
        {
            return character.SurvivalState.ToString();
        }

        var walking = runner.Campaign.Placements.TryGetValue(character.Id, out var placement) && placement.IsMoving;
        if (runner.Campaign.ActiveTasks.TryGetValue(character.Id, out var task))
        {
            var name = runner.Content.Tasks[task.DefinitionId].Name;
            var why = task.Origin switch
            {
                TaskOrigin.Duty => " (duty)",
                TaskOrigin.SelfCare => " (tired)",
                _ => "",
            };
            return (walking ? $"going to {name.ToLowerInvariant()}" : name) + why;
        }

        return walking ? "walking" : "idle";
    }

    /// <summary>Needs worth mentioning, in words (§63, §64): "exhausted", "hungry". Empty when all is well.</summary>
    public static IReadOnlyList<(string Text, Severity Severity)> Concerns(Character character)
    {
        var concerns = new List<(string, Severity)>();
        void Check(double value, string moderate, string severe)
        {
            if (value >= 85) concerns.Add((severe, Severity.Urgent));
            else if (value >= 65) concerns.Add((moderate, Severity.Attention));
        }

        if (character.SurvivalState == SurvivalState.Incapacitated) concerns.Add(("incapacitated", Severity.Critical));
        if (character.Health.Value < 40) concerns.Add(("badly hurt", Severity.Critical));
        else if (character.Health.Value < 75) concerns.Add(("hurt", Severity.Attention));
        Check(character.Needs.Thirst, "thirsty", "parched");
        Check(character.Needs.Hunger, "hungry", "starving");
        Check(character.Needs.Fatigue, "tired", "exhausted");
        Check(character.Needs.Stress, "strained", "at the end of their tether");
        return concerns;
    }

    // ================================================================ persistent HUD

    private void BuildClock()
    {
        var panel = Ui.PanelOf(Ui.Panel, null, Ui.SpaceL, Ui.SpaceS);
        panel.Position = new Vector2(Ui.SpaceM, Ui.SpaceM);
        _root.AddChild(panel);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 0);
        panel.AddChild(column);
        var top = new HBoxContainer();
        column.AddChild(top);
        _clock = Ui.Text("DAY 1 · 08:00", Ui.TextHeading + 1, Ui.Ink);
        _clock.TooltipText = "Day of the expedition and the time";
        _clock.MouseFilter = Control.MouseFilterEnum.Stop;
        top.AddChild(_clock);
        var speeds = new HBoxContainer();
        speeds.AddThemeConstantOverride("separation", 2);
        top.AddChild(speeds);
        for (var i = 0; i < DefaultSpeedNames.Length; i++)
        {
            var speed = i;
            var button = Ui.Button(i == 0 ? "❚❚" : DefaultSpeedNames[i], "", () => SpeedChosen?.Invoke(speed), toggle: true);
            speeds.AddChild(button);
            _speedButtons.Add((button, speed));
        }

        _place = Ui.Text("", Ui.TextMeta, Ui.InkSoft);
        column.AddChild(_place);
        SetSpeedNames(DefaultSpeedNames);
    }

    // Top left, under the clock, in port: what preparing the expedition comes to, ticked off as it is done
    // (the owner's port board: "Prepare the expedition"). Built from the simulation each refresh.
    private PanelContainer _checklist = null!;
    private VBoxContainer _checks = null!;
    private string _checkSignature = "";

    private void BuildChecklist()
    {
        _checklist = Ui.PanelOf(Ui.Panel, null, Ui.SpaceL, Ui.SpaceS);
        _checklist.Position = new Vector2(Ui.SpaceM, 78);
        _checklist.Visible = false;
        _root.AddChild(_checklist);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 3);
        _checklist.AddChild(column);
        column.AddChild(Ui.Heading("Prepare the expedition"));
        _checks = new VBoxContainer();
        _checks.AddThemeConstantOverride("separation", 2);
        column.AddChild(_checks);
    }

    private void RefreshChecklist(SimulationRunner runner)
    {
        if (runner.Campaign.Port is not { } port || runner.Content.Port is not { } rules || _open is not null)
        {
            _checklist.Visible = false;
            return;
        }

        _checklist.Visible = true;
        var crew = runner.Campaign.Crew.Where(c => c.Id != runner.Campaign.CaptainId).ToList();
        int Knowing(string skill) => crew.Count(c => c.Skills.Get(skill) >= 3);
        var sailing = Math.Max(runner.Campaign.Crew.Count(), rules.MinimumCrew + 1);
        var (food, water) = runner.SupplyForecast(sailing);
        var (low, _) = Expedition.Simulation.Scenarios.ExpeditionScenario.PassageDays;
        var hours = (rules.Departure.Tick - runner.Campaign.Clock.Tick) / (double)SimulationClock.TicksPerHour;
        var items = new List<(bool Done, string Text, string Tip)>
        {
            (port.Hired.Count >= rules.MinimumCrew, $"Sign on hands ({port.Hired.Count}/{rules.MinimumCrew})", "The Garza will not sail with fewer. Talk with people; the journal (X) says who to find."),
            (Knowing("skill.navigation") > 0, $"Find someone who knows the way ({Math.Min(1, Knowing("skill.navigation"))}/1)", "A pilot or a navigator: no one aboard has been to the Ember Coast."),
            (Knowing("skill.first_aid") + Knowing("skill.surgery") > 0, "Find someone to treat the sick (optional)", "A barber-surgeon keeps the flux and the fever from emptying the berths."),
            (Math.Min(food, water) >= low, $"Buy stores for the passage ({Math.Floor(Math.Min(food, water)):0}/{low} days)", $"Water and food for {sailing} aboard, from the chandler and the cooper (Cargo, C)."),
        };
        if (port.Owed.Count > 0)
        {
            items.Add((false, $"Settle with the sellers ({port.Owed.Values.Sum()} owed)", "What is owed on credit is claimed at the gangway."));
        }

        items.Add((false, hours > 0 ? $"Sail with the convoy (in {Math.Floor(hours / 24):0} d {Math.Floor(hours % 24):0} h)" : "The convoy has sailed", "Set sail from Expedition (X) when ready."));
        var signature = string.Join("|", items.Select(i => $"{i.Done}{i.Text}"));
        if (signature == _checkSignature)
        {
            return;
        }

        _checkSignature = signature;
        foreach (var child in _checks.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var (done, text, tip) in items)
        {
            var row = new HBoxContainer { TooltipText = tip, MouseFilter = Control.MouseFilterEnum.Stop };
            row.AddThemeConstantOverride("separation", Ui.SpaceS);
            var box = new PanelContainer { CustomMinimumSize = new Vector2(18, 18), MouseFilter = Control.MouseFilterEnum.Ignore };
            box.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0, 0, 0, 0.25f), done ? Ui.Of(Severity.Positive) : Ui.Rule, 1, 1));
            if (done && ResourceLoader.Exists("res://Assets/UI/icons/tick.png"))
            {
                box.AddChild(new TextureRect
                {
                    Texture = GD.Load<Texture2D>("res://Assets/UI/icons/tick.png"),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    CustomMinimumSize = new Vector2(14, 14),
                    Modulate = Ui.Of(Severity.Positive),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                });
            }

            row.AddChild(box);
            var label = Ui.Text(text, Ui.TextBody, done ? Ui.InkSoft : Ui.Ink);
            label.MouseFilter = Control.MouseFilterEnum.Ignore;
            row.AddChild(label);
            _checks.AddChild(row);
        }
    }

    private void BuildNavigation()
    {
        // Bottom right, where the world matters least, clear of the clock and the phase's gauges.
        var panel = Ui.PanelOf(Ui.Panel, null, Ui.SpaceM, Ui.SpaceS);
        panel.AnchorLeft = 1;
        panel.AnchorRight = 1;
        panel.AnchorTop = 1;
        panel.AnchorBottom = 1;
        panel.OffsetRight = -Ui.SpaceM;
        panel.OffsetBottom = -Ui.SpaceM;
        panel.GrowHorizontal = Control.GrowDirection.Begin;
        panel.GrowVertical = Control.GrowDirection.Begin;
        _root.AddChild(panel);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 2);
        panel.AddChild(row);
        foreach (var (id, label, key, tip) in new[]
                 {
                     (PanelId.People, "People", "P", "The roster: who you have, what they can do, who is tired or hurt"),
                     (PanelId.Expedition, "Expedition", "X", "Objectives, the charter, and the papers of the voyage"),
                     (PanelId.Cargo, "Cargo", "C", "What is in the hold and how long it will last"),
                     (PanelId.Ship, "Ship", "H", "The Garza's condition and the captain's orders for her"),
                     (PanelId.Chart, "Chart", "M", "The working chart: your reckoning, the noon sight, the course (at sea)"),
                     (PanelId.Chronicle, "Chronicle", "L", "The record of everything that has happened"),
                 })
        {
            var panelId = id;
            var button = Ui.Button(label, $"{tip}  ({key})", () => TogglePanel(panelId), toggle: true);
            button.Disabled = id == PanelId.Chart;
            Ui.WithIcon(button, id.ToString().ToLowerInvariant());
            row.AddChild(button);
            _navButtons[id] = button;
        }

        var menuButton = Ui.Button("Menu", "Menu: new voyage, save, load, controls, interface size, auto-pause");
        Ui.WithIcon(menuButton, "menu");
        row.AddChild(menuButton);
        _menu = new PopupMenu();
        _root.AddChild(_menu);
        menuButton.Pressed += () =>
        {
            RebuildMenu();
            _menu.ResetSize();
            _menu.Position = (Vector2I)(menuButton.GetScreenPosition() - new Vector2(_menu.Size.X - menuButton.Size.X, _menu.Size.Y + 4));
            _menu.Popup();
        };
        _menu.IdPressed += OnMenu;
    }

    private void RebuildMenu()
    {
        _menu.Clear();
        _menu.AddItem("New voyage", 1);
        _menu.AddItem("Save   (Cmd+S)", 2);
        _menu.AddItem("Load   (Cmd+L)", 3);
        _menu.AddSeparator();
        _menu.AddItem("Controls", 4);
        _menu.AddItem($"Interface size: {_uiScale:P0}", 5);
        _menu.AddSeparator("Pause automatically for");
        foreach (var (category, label, id) in new[]
                 {
                     (AutoPause.InjuryAndDeath, "Injury and death", 10),
                     (AutoPause.Weather, "Storms", 11),
                     (AutoPause.ShipDamage, "Damage and the bilge", 12),
                     (AutoPause.Shortage, "Running short of water or food", 13),
                     (AutoPause.Events, "Decisions for the captain", 14),
                     (AutoPause.Landfall, "The coast: land sighted, marks to know", 15),
                 })
        {
            _menu.AddCheckItem(label, id);
            _menu.SetItemChecked(_menu.GetItemIndex(id), _autoPause.Contains(category));
            if (category == AutoPause.Events)
            {
                // A decision always waits for the captain; this only says so.
                _menu.SetItemDisabled(_menu.GetItemIndex(id), true);
            }
        }
    }

    private void OnMenu(long id)
    {
        switch (id)
        {
            case 1: NewVoyageRequested?.Invoke(); break;
            case 2: SaveRequested?.Invoke(); break;
            case 3: LoadRequested?.Invoke(); break;
            case 4: _help.Visible = !_help.Visible; break;
            case 5:
                _uiScale = _uiScale switch { < 0.95f => 1f, < 1.1f => 1.15f, < 1.25f => 1.3f, _ => 0.9f };
                GetTree().Root.ContentScaleFactor = _uiScale;
                Toast($"Interface size {_uiScale:P0}.");
                break;
            case (>= 10 and <= 13) or 15:
                var category = (AutoPause)(id - 10);
                if (!_autoPause.Remove(category)) _autoPause.Add(category);
                Toast(_autoPause.Contains(category) ? "Will pause for this." : "Will notify without pausing.");
                break;
        }
    }

    private void BuildStatus()
    {
        _statusPanel = Ui.PanelOf(Ui.Panel, null, Ui.SpaceL, Ui.SpaceS);
        _statusPanel.AnchorLeft = 1;
        _statusPanel.AnchorRight = 1;
        _statusPanel.OffsetTop = Ui.SpaceM;
        _statusPanel.OffsetRight = -Ui.SpaceM;
        _statusPanel.GrowHorizontal = Control.GrowDirection.Begin;
        _root.AddChild(_statusPanel);
        var column = new VBoxContainer();
        _statusPanel.AddChild(column);
        _status = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        _status.AddThemeConstantOverride("separation", Ui.SpaceL);
        column.AddChild(_status);

        // Sail orders stay one click away at sea: they matter most when things go wrong (VOYAGE §21).
        _sailOrders = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        _sailOrders.AddThemeConstantOverride("separation", 2);
        column.AddChild(_sailOrders);
        foreach (var (plan, label, tip) in new[]
                 {
                     (Expedition.Simulation.Voyage.SailPlan.PressOn, "Press on", "Every sail set: about a fifth faster, but the hull and rigging wear half as fast again and she leaks more."),
                     (Expedition.Simulation.Voyage.SailPlan.Normal, "Normal sail", "Sail set for the weather."),
                     (Expedition.Simulation.Voyage.SailPlan.HoveTo, "Heave to", "Ride out heavy weather: almost no headway, but a third of the wear and about half the leaking."),
                 })
        {
            var button = Ui.Button(label, tip, () => SailPlanChosen?.Invoke(plan), toggle: true);
            _sailOrders.AddChild(button);
            _sailButtons.Add((button, plan));
        }
    }

    /// <summary>The captain asks for the chart.</summary>
    public event Action? ChartRequested;

    private void RefreshStatus(SimulationRunner runner)
    {
        var campaign = runner.Campaign;
        if (_navButtons.TryGetValue(PanelId.Chart, out var chartButton))
        {
            chartButton.Disabled = campaign.Voyage?.Navigation is null;
        }

        var chips = new List<(string Icon, string Text, Severity Severity, string Tip)>();
        if (campaign.Port is { } port && runner.Content.Port is { } rules)
        {
            var hours = (rules.Departure.Tick - campaign.Clock.Tick) / (double)SimulationClock.TicksPerHour;
            chips.Add(hours > 0
                ? (Ui.IconTime, $"Departure in {Math.Floor(hours):0} h", hours < 6 ? Severity.Urgent : hours < 12 ? Severity.Attention : Severity.Information,
                    $"The convoy sails at dawn on day {rules.Departure.Day}. After that, every half day in port costs {rules.LateFeePerHalfDay} sueldos.")
                : (Ui.IconTime, "The convoy has sailed", Severity.Critical, $"Every half day in port now costs {rules.LateFeePerHalfDay} sueldos."));
            var signed = port.Hired.Count;
            chips.Add(signed < rules.MinimumCrew
                ? (Ui.IconPeople, $"Crew {signed}/{rules.MinimumCrew} minimum", Severity.Attention, $"The Garza needs at least {rules.MinimumCrew} hands to sail. Find people through the journal (X).")
                : (Ui.IconPeople, $"Crew {signed}/{rules.Berths}", Severity.Positive, $"Enough hands to sail; {rules.Berths - signed} berths still free."));
            chips.Add((Ui.IconPurse, $"{port.Funds}", port.Funds < 500 ? Severity.Attention : Severity.Information, "Sueldos left in the purse, for advances and stores"));
            if (runner.PortWeather is { } weather)
            {
                chips.Add((Ui.IconWind, weather.Name, Severity.Information, weather.Raining
                    ? "Rain: people caught outside take shelter in doorways and the vault arches, and the market thins."
                    : weather.Fog > 0.5 ? "Fog: you can see only a few paces, so people further off are out of sight."
                    : "The weather in port. It changes through the day."));
            }
        }
        else if (campaign.Voyage is { } ship)
        {
            var weather = runner.Content.Voyage?.Weathers.FirstOrDefault(w => w.Id == ship.WeatherId);
            var sea = weather?.SeaState ?? 1;
            chips.Add((Ui.IconWind, Capital(_formatter.WeatherName(ship.WeatherId)), sea >= 2.5 ? Severity.Critical : sea >= 1.8 ? Severity.Urgent : Severity.Information,
                "The wind and sea. Heavy weather strains the hull and rigging and makes her leak; heaving to eases it."));
            if (ship.Navigation is { } navigation)
            {
                // The compass: the course steered, and at noon a reminder to take the sun.
                chips.Add(("✦", $"{SimulationRunner.CompassPoint(navigation.Heading)} · wind {SimulationRunner.CompassPoint(navigation.WindFrom)}" + (navigation.Beating ? " · beating" : ""),
                    Severity.Information,
                    $"Course {navigation.Heading:0}° by the compass, steered from your reckoning; the wind from the {SimulationRunner.CompassPoint(navigation.WindFrom)}. " +
                    (navigation.Beating ? "It is too near the course she wants, so she beats to windward, tack upon tack. " : "") +
                    "The chart (M) shows where you reckon she is."));
                if (runner.Raining)
                {
                    var catching = runner.Campaign.ActiveTasks.Values.Count(t => t.DefinitionId == "task.catch_rain");
                    chips.Add(("☂", catching > 0 ? $"Rain: {catching} catching it" : "Rain: catch it", catching > 0 ? Severity.Information : Severity.Attention,
                        "It is raining. Set hands to Catch the rain (People, or a standing duty): the courses slacked off into the casks fill them while it lasts."));
                }

                if (navigation.Sighted)
                {
                    chips.Add(("⚓", navigation.Pending ? "Land: what coast is this?" : navigation.CoastDirection == 0 ? "Land: standing off" : $"Coasting {(navigation.CoastDirection > 0 ? "north" : "south")}",
                        navigation.Pending || navigation.CoastDirection == 0 ? Severity.Attention : Severity.Information,
                        "The coast is in sight. Open the chart (M) to say what it is, by the sailing directions, or to run along it by eye."));
                }
                else if (navigation.NearWarned)
                {
                    chips.Add(("⚓", "Coast near: sound", Severity.Attention,
                        "By your reckoning the coast is near. Heave the lead from the chart (M): the bank shoals as she closes. Keep a lookout, above all at night."));
                }

                if (runner.CanTakeNoonSight)
                {
                    chips.Add(("☀", "Noon: take the sun", Severity.Attention, "The sun is near its height. Open the chart (M) and take the noon sight, or leave it to your navigator."));
                }
            }

            var (part, value, severity) = WorstPart(ship);
            chips.Add((Ui.IconShip, part, severity, $"Hull {ship.Hull:0}%, rigging {ship.Rigging:0}%, bilge {ship.Bilge:0}% water. Details in Ship (H)."));
            var (food, water) = runner.SupplyForecast();
            chips.Add((Ui.IconWater, $"Water {Range(water)}", water < 5 ? Severity.Critical : water < 10 ? Severity.Urgent : Severity.Information,
                "Days of water at the current use, as an estimate. Short rations stretch it."));
            chips.Add((Ui.IconFood, $"Food {Range(food)}", food < 5 ? Severity.Critical : food < 10 ? Severity.Urgent : Severity.Information,
                "Days of food at the current use, as an estimate."));
            if (!ship.WatchKept)
            {
                chips.Add(("▲", "No one on watch", Severity.Urgent, "Without a watch she sails slowly and blind. Give someone the Stand watch duty."));
            }
        }

        while (_status.GetChildCount() > chips.Count)
        {
            var last = _status.GetChild(_status.GetChildCount() - 1);
            _status.RemoveChild(last);
            last.QueueFree();
        }

        for (var i = 0; i < chips.Count; i++)
        {
            if (i >= _status.GetChildCount())
            {
                _status.AddChild(Ui.Chip("", "", Severity.Information));
            }

            var (icon, text, severity, tip) = chips[i];
            Ui.SetChip((Label)_status.GetChild(i), icon, text, severity, tip);
        }

        _statusPanel.Visible = chips.Count > 0;
        _sailOrders.Visible = campaign.Voyage is { Outcome: Expedition.Simulation.Voyage.VoyageOutcome.UnderWay };
        foreach (var (button, plan) in _sailButtons)
        {
            button.SetPressedNoSignal(campaign.Voyage?.SailPlan == plan);
        }
    }

    // What squalls have carried away, for the ship panel: work on the rigging mends the yard first, then the lines.
    private static string CarriedAway(Expedition.Simulation.Voyage.VoyageState ship) =>
        (ship.YardSprung ? "  The main topsail yard is sprung: no topsail until it is fished.\n" : "")
        + (ship.PartedLines.Count > 0 ? $"  Lines parted aloft: {ship.PartedLines.Count}.\n" : "")
        + (ship.YardSprung || ship.PartedLines.Count > 0 ? "  Repairing the rigging mends the yard first, then the lines.\n" : "");

    // The ship's most worrying system, in a word or two (§62: no number soup).
    private static (string Text, double Value, Severity Severity) WorstPart(Expedition.Simulation.Voyage.VoyageState ship)
    {
        if (ship.Bilge > 60) return ($"Bilge {ship.Bilge:0}% — pump!", ship.Bilge, Severity.Critical);
        if (ship.Hull < 40) return ($"Hull failing ({ship.Hull:0}%)", ship.Hull, Severity.Critical);
        if (ship.Bilge > 30) return ($"Bilge rising ({ship.Bilge:0}%)", ship.Bilge, Severity.Urgent);
        if (ship.YardSprung) return ("Topsail yard sprung", ship.Rigging, Severity.Urgent);
        if (ship.Rigging < 40) return ($"Rigging failing ({ship.Rigging:0}%)", ship.Rigging, Severity.Urgent);
        if (ship.PartedLines.Count > 0) return (ship.PartedLines.Count == 1 ? "A line parted aloft" : $"{ship.PartedLines.Count} lines parted aloft", ship.Rigging, Severity.Attention);
        if (ship.Hull < 70 || ship.Rigging < 70) return ("Worn: needs repair", Math.Min(ship.Hull, ship.Rigging), Severity.Attention);
        return ("Sound", 100, Severity.Positive);
    }

    // An estimate as a range, not false precision (§31): "~42–51 d".
    private static string Range(double days)
    {
        if (double.IsInfinity(days) || days > 300)
        {
            return "plenty";
        }

        var low = Math.Max(0, Math.Floor(days * 0.9));
        var high = Math.Ceiling(days * 1.1);
        return low == high ? $"~{low:0} d" : $"~{low:0}–{high:0} d";
    }

    private void BuildSelected()
    {
        var column = new VBoxContainer { AnchorTop = 1, AnchorBottom = 1, OffsetLeft = Ui.SpaceM, OffsetBottom = -Ui.SpaceM, GrowVertical = Control.GrowDirection.Begin };
        column.AddThemeConstantOverride("separation", Ui.SpaceS);
        column.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(column);

        _selectedPanel = Ui.PanelOf(Ui.Panel, null, Ui.SpaceL, Ui.SpaceS);
        column.AddChild(_selectedPanel);
        var card = new VBoxContainer();
        card.AddThemeConstantOverride("separation", 1);
        _selectedPanel.AddChild(card);
        var top = new HBoxContainer();
        card.AddChild(top);
        _selectedName = Ui.Text("", Ui.TextHeading, Ui.Brass);
        _selectedName.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        top.AddChild(_selectedName);
        top.AddChild(Ui.Button("Record", "Their full record, orders and duties  (P)", () => OpenPanel(PanelId.People)));
        _selectedDoing = Ui.Text("", Ui.TextBody, Ui.Ink, 300);
        card.AddChild(_selectedDoing);
        _selectedWarnings = Ui.Text("", Ui.TextBody, Ui.Of(Severity.Attention), 300);
        card.AddChild(_selectedWarnings);

        _objectivePanel = Ui.PanelOf(Ui.Panel, null, Ui.SpaceL, Ui.SpaceS);
        column.AddChild(_objectivePanel);
        _objective = Ui.Text("", Ui.TextBody, Ui.Ink, 300);
        _objective.TooltipText = "What probably matters next. All objectives are in Expedition (X).";
        _objective.MouseFilter = Control.MouseFilterEnum.Stop;
        _objectivePanel.AddChild(_objective);
    }

    private void RefreshSelectedCard(SimulationRunner runner, CharacterId? selected)
    {
        var campaign = runner.Campaign;
        var show = selected is { } id && campaign.Characters.TryGetValue(id, out var character) && character.Membership == Membership.Crew && _open is null;
        _selectedPanel.Visible = show;
        if (show)
        {
            character = campaign.Characters[selected!.Value];
            _selectedName.Text = selected == campaign.CaptainId ? $"{character.Name} · captain" : character.Name;
            var progress = campaign.ActiveTasks.TryGetValue(character.Id, out var task)
                ? $" · {Math.Min(1, task.Progress / runner.Content.Tasks[task.DefinitionId].DurationMinutes):P0}"
                : "";
            _selectedDoing.Text = Capital(Activity(runner, character)) + progress;
            var concerns = Concerns(character);
            _selectedWarnings.Visible = concerns.Count > 0;
            _selectedWarnings.Text = string.Join("  ", concerns.Select(c => $"{Ui.IconOf(c.Severity)} {Capital(c.Text)}"));
            if (concerns.Count > 0)
            {
                _selectedWarnings.AddThemeColorOverride("font_color", Ui.Of(concerns.Max(c => c.Severity)));
            }
        }

        var objective = Objective(runner);
        // In port the checklist at the top left says it; this panel is for the voyage.
        _objectivePanel.Visible = objective is not null && _open is null && runner.Campaign.Port is null;
        _objective.Text = objective ?? "";
    }

    // The one thing that probably matters next (§3, §36): immediate first.
    private static string? Objective(SimulationRunner runner)
    {
        var campaign = runner.Campaign;
        if (campaign.Port is { } port && runner.Content.Port is { } rules)
        {
            var leads = rules.AllLeads().ToDictionary(l => l.Lead.Id, l => l.Lead);
            var open = port.Leads.Select(l => leads[l.LeadId]).LastOrDefault(l => runner.CharacterFor(l.About) is { } who && !port.TalkedTo.Contains(who.Id) && !port.Gone.Contains(who.Id));
            var need = rules.MinimumCrew - port.Hired.Count;
            var first = need > 0 ? $"{Ui.IconLead} Sign on {need} more {(need == 1 ? "hand" : "hands")} before the convoy sails" : $"{Ui.IconLead} Buy stores, then set sail (X)";
            return open is null
                ? first + (port.Leads.Count == 0 ? "\n   Ask around: the landlady, the chandler, the Chamber's notices." : "")
                : first + $"\n   Lead: {open.Where}";
        }

        if (campaign.Voyage is not { Outcome: Expedition.Simulation.Voyage.VoyageOutcome.UnderWay } ship)
        {
            return null;
        }

        return ship.Navigation switch
        {
            { Sighted: true, Identified: null } => $"{Ui.IconLead} Know the coast: which mark is in view? (Chart, M)",
            { Sighted: true } => $"{Ui.IconLead} Run along the coast for Punta Ceniza: about {ship.ReckonedRemaining:0} leagues, by what you took it for",
            _ => $"{Ui.IconLead} Reach {ship.Destination}: about {ship.ReckonedRemaining:0} leagues to go",
        };
    }

    private void BuildAlerts()
    {
        _alerts = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetTop = 84, OffsetLeft = -240, OffsetRight = 240,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _root.AddChild(_alerts);

        _paused = Ui.PanelOf(new Color(0.18f, 0.06f, 0.04f, 0.92f), Ui.Of(Severity.Urgent), Ui.SpaceL, Ui.SpaceM);
        _paused.Visible = false;
        _alerts.AddChild(_paused);
        var column = new VBoxContainer();
        _paused.AddChild(column);
        _pausedText = Ui.Text("", Ui.TextBody + 1, Ui.Ink, 440);
        column.AddChild(_pausedText);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        column.AddChild(row);
        row.AddChild(Ui.Text("Paused. Give your orders, then", Ui.TextMeta, Ui.InkSoft));
        row.AddChild(Ui.Button("Carry on (Space)", "Resume at a safe speed", () => ContinueRequested?.Invoke()));
    }

    private void BuildToasts()
    {
        // Notices sit under the alerts at the top, clear of the conversation strip and the selected card.
        _toasts = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _toasts.AddThemeConstantOverride("separation", Ui.SpaceXs);
        _alerts.AddChild(_toasts);
    }

    // New chronicle entries: notable ones surface as short notices; all count as unread (§35).
    private void RefreshChronicleNews(SimulationRunner runner)
    {
        var entries = runner.Campaign.Chronicle.Entries;
        for (; _chronicleSeen < entries.Count; _chronicleSeen++)
        {
            var entry = entries[_chronicleSeen];
            if (_open != PanelId.Chronicle)
            {
                _unread++;
            }

            // Leads are already said in conversation and kept in the journal; no notice for them.
            if (entry.Kind is ChronicleEntryKind.CrewHired or ChronicleEntryKind.Noticed or ChronicleEntryKind.LeftPort
                or ChronicleEntryKind.ConvoyWarning or ChronicleEntryKind.LateFee or ChronicleEntryKind.Landfall or ChronicleEntryKind.RationingChanged)
            {
                Toast(_formatter.Format(entry), entry.Kind is ChronicleEntryKind.LeftPort or ChronicleEntryKind.ConvoyWarning or ChronicleEntryKind.LateFee
                    ? Severity.Attention : Severity.Information);
            }
        }

        _navButtons[PanelId.Chronicle].Text = _unread > 0 ? $"Chronicle ({_unread})" : "Chronicle";
    }

    private void BuildHelp()
    {
        _help = Ui.PanelOf(Ui.Drawer, Ui.Rule);
        _help.AnchorLeft = 0.5f;
        _help.AnchorRight = 0.5f;
        _help.AnchorTop = 0.5f;
        _help.AnchorBottom = 0.5f;
        _help.OffsetLeft = -240;
        _help.OffsetRight = 240;
        _help.OffsetTop = -170;
        _help.GrowVertical = Control.GrowDirection.Both;
        _help.Visible = false;
        _root.AddChild(_help);
        var column = new VBoxContainer();
        _help.AddChild(column);
        var top = new HBoxContainer();
        column.AddChild(top);
        var title = Ui.Heading("Controls");
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        top.AddChild(title);
        top.AddChild(Ui.Button("×", "Close", () => _help.Visible = false));
        column.AddChild(Ui.Text(
            "Right-click: walk (in port the captain; at sea the selected crew member)\n" +
            "Left-click: look at someone · Esc: close · F: follow the captain\n" +
            "Space: pause or carry on · 1–6: speeds\n" +
            "P People · X Expedition · C Cargo · H Ship · L Chronicle\n" +
            "WASD or arrows: pan · Wheel: zoom · Q / E: turn\n" +
            "Cmd+S / Cmd+L (Ctrl elsewhere): save / load · Tab: next crew member",
            Ui.TextBody, Ui.Ink, 440));
    }

    // ================================================================ management drawer

    private void BuildDrawer()
    {
        _drawer = Ui.PanelOf(Ui.Drawer, Ui.Rule, Ui.SpaceL, Ui.SpaceM);
        _drawer.AnchorBottom = 1;
        _drawer.OffsetLeft = Ui.SpaceM;
        _drawer.OffsetRight = Ui.SpaceM + 400;
        _drawer.OffsetTop = 70;
        _drawer.OffsetBottom = -Ui.SpaceM;
        _drawer.Visible = false;
        _root.AddChild(_drawer);
        var column = new VBoxContainer();
        _drawer.AddChild(column);
        var top = new HBoxContainer();
        column.AddChild(top);
        _drawerTitle = Ui.Heading("");
        _drawerTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        top.AddChild(_drawerTitle);
        top.AddChild(Ui.Button("×", "Close  (Esc)", () => ClosePanel()));
        column.AddChild(new HSeparator());
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        column.AddChild(scroll);
        var bodies = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(bodies);
        foreach (var id in Enum.GetValues<PanelId>())
        {
            var body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Visible = false };
            body.AddThemeConstantOverride("separation", Ui.SpaceM);
            bodies.AddChild(body);
            _panelBodies[id] = body;
        }

        BuildPeople(_panelBodies[PanelId.People]);
        BuildCargo(_panelBodies[PanelId.Cargo]);
        BuildShip(_panelBodies[PanelId.Ship]);
        BuildChronicle(_panelBodies[PanelId.Chronicle]);
    }

    private void BuildPeople(VBoxContainer body)
    {
        body.AddChild(Ui.Text("Your people. Skills are what you have seen or been told, never more.", Ui.TextMeta, Ui.InkSoft, 370));
        _crewList = new VBoxContainer();
        _crewList.AddThemeConstantOverride("separation", 2);
        body.AddChild(_crewList);
        body.AddChild(new HSeparator());

        _name = Ui.Heading("");
        body.AddChild(_name);
        _doing = Ui.Text("", Ui.TextBody, Ui.Ink, 370);
        body.AddChild(_doing);
        foreach (var need in new[] { "Hunger", "Thirst", "Fatigue", "Stress" })
        {
            var line = new HBoxContainer();
            line.AddChild(Ui.Text(need, Ui.TextMeta, Ui.InkSoft));
            line.GetChild<Label>(0).CustomMinimumSize = new Vector2(64, 0);
            var bar = new ProgressBar
            {
                MinValue = 0, MaxValue = 100, ShowPercentage = false,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 10),
            };
            line.AddChild(bar);
            body.AddChild(line);
            _needBars[need] = bar;
        }

        var tabs = new TabContainer { CustomMinimumSize = new Vector2(0, 360) };
        body.AddChild(tabs);
        var orders = AddTab(tabs, "Orders");
        _queue = Wrapped();
        orders.AddChild(_queue);
        orders.AddChild(Ui.Text("Click a task to order it. It starts when the current work is done.", Ui.TextMeta, Ui.InkSoft, 350));
        _urgent = new CheckBox { Text = "Urgent: pause current work and do this first", FocusMode = Control.FocusModeEnum.None };
        orders.AddChild(_urgent);
        _taskList = new VBoxContainer();
        orders.AddChild(_taskList);
        orders.AddChild(new HSeparator());
        orders.AddChild(Ui.Button("Stop current task", "", () => StopOrdered?.Invoke()));
        orders.AddChild(Ui.Button("Clear queued orders", "", () => QueueCleared?.Invoke()));

        var duties = AddTab(tabs, "Duties");
        duties.AddChild(Ui.Text("When idle with no orders, this person takes up these duties in turn. Everyone rests when tired; your orders come first.", Ui.TextMeta, Ui.InkSoft, 350));
        _dutyList = new VBoxContainer();
        duties.AddChild(_dutyList);
        _dutyNote = Wrapped();
        duties.AddChild(_dutyNote);

        var record = AddTab(tabs, "Record");
        _details = Wrapped();
        record.AddChild(_details);

        var needs = AddTab(tabs, "Needs");
        _why = Wrapped();
        needs.AddChild(_why);
        _pack = Wrapped();
        needs.AddChild(_pack);
    }

    private void RefreshRoster(SimulationRunner runner, CharacterId? selected)
    {
        foreach (var (button, id) in _crewButtons)
        {
            var character = runner.Campaign.Characters[id];
            var captain = id == runner.Campaign.CaptainId ? " · captain" : "";
            var concerns = Concerns(character);
            button.Text = $"{character.Name}{captain} — {Activity(runner, character)}" + (concerns.Count > 0 ? $"   {Ui.IconOf(concerns.Max(c => c.Severity))} {concerns[0].Text}" : "");
            button.SetPressedNoSignal(id == selected);
            button.Modulate = id == selected ? Ui.Brass : Colors.White;
        }
    }

    private TaskPriority CurrentPriority => _urgent.ButtonPressed ? TaskPriority.Urgent : TaskPriority.Normal;

    private void RefreshRecord(SimulationRunner runner, CharacterId? selected)
    {
        if (selected is not { } id || !runner.Campaign.Characters.TryGetValue(id, out var character) || character.Membership != Membership.Crew)
        {
            _name.Text = "Choose someone above";
            _doing.Text = "";
            _details.Text = "";
            _queue.Text = "";
            _dutyNote.Text = "";
            _why.Text = "";
            _pack.Text = "";
            return;
        }

        var isCaptain = id == runner.Campaign.CaptainId;
        _name.Text = isCaptain ? $"{character.Name} · captain" : character.Name;
        var progress = runner.Campaign.ActiveTasks.TryGetValue(id, out var task)
            ? $" ({Math.Min(1, task.Progress / runner.Content.Tasks[task.DefinitionId].DurationMinutes):P0})"
            : "";
        _doing.Text = $"Doing: {Activity(runner, character)}{progress}";
        _needBars["Hunger"].Value = character.Needs.Hunger;
        _needBars["Thirst"].Value = character.Needs.Thirst;
        _needBars["Fatigue"].Value = character.Needs.Fatigue;
        _needBars["Stress"].Value = character.Needs.Stress;

        // The captain knows their own self; everyone else is what the captain has learned (§20–23).
        string Self(IEnumerable<KeyValuePair<string, int>> levels, Func<string, string> name) =>
            string.Join(", ", levels.OrderByDescending(p => p.Value).Select(p => $"{name(p.Key)} — {KnowledgeText.LevelWord(p.Value)}"));
        var known = KnowledgeText.Lines(runner.Campaign.Knowledge, id, runner.Content, runner.Campaign.Characters);
        string Known(Func<string, bool> kind) => string.Join("\n", known.Where(l => kind(l.Topic))
            .Select(l => $"  {l.Name} — {(l.Status is "Observed" or "Confirmed" ? Capital(l.Value) : $"{l.Status} {l.Value}")} · {l.Confidence} · {l.Source}"));
        var skills = isCaptain ? Self(character.Skills.Levels, k => runner.Content.Skills[k].Name) : Known(runner.Content.Skills.ContainsKey);
        var physique = isCaptain ? Self(runner.Content.Attributes.Values.ToDictionary(a => a.Id, a => character.Attributes.Get(a.Id)), k => runner.Content.Attributes[k].Name)
            : Known(runner.Content.Attributes.ContainsKey);
        var traits = isCaptain
            ? string.Join(", ", character.Traits.Select(t => runner.Content.Traits.TryGetValue(t, out var trait) ? trait.Name : t))
            : string.Join(", ", known.Where(l => runner.Content.Traits.ContainsKey(l.Topic) && l.Value == "yes").Select(l => l.Name));
        var tongues = isCaptain ? Self(character.Languages.Levels, k => runner.Content.Languages.TryGetValue(k, out var language) ? language.Name : k)
            : Known(runner.Content.Languages.ContainsKey);
        var status = character.StatusId is not null && runner.Content.Statuses.TryGetValue(character.StatusId, out var standing) ? standing.Name.ToLowerInvariant() : "standing unknown";
        var health = character.Health.Value >= 90 ? "well" : character.Health.Value >= 60 ? "hurt" : character.Health.Value >= 30 ? "badly hurt" : "close to death";
        _details.Text =
            $"{character.Age}, {status}, from {character.Origin}\n" +
            (character.Appearance is { } looks ? $"{AppearanceText.Describe(looks, runner.Content.Appearance)}\n" : "") +
            $"{character.SurvivalState} · {health}\n\n" +
            $"{(isCaptain ? "Skills" : "Skills, as far as you know")}:\n{(skills.Length == 0 ? "  not yet known: watch them work, or ask" : (isCaptain ? "  " + skills : skills))}\n" +
            $"Physique:\n{(physique.Length == 0 ? "  not yet seen" : (isCaptain ? "  " + physique : physique))}\n" +
            $"Languages:\n{(tongues.Length == 0 ? "  not yet known" : (isCaptain ? "  " + tongues : tongues))}\n" +
            $"Character: {(traits.Length == 0 ? "not yet known" : traits)}\n" +
            (character.Health.Conditions.Count > 0
                ? $"Conditions: {string.Join(", ", character.Health.Conditions.Select(c => runner.Content.Conditions.TryGetValue(c, out var condition) ? condition.Name : c))}\n"
                : "") +
            Views(runner, id);

        _why.Text = "Why (last day):\n" + string.Join("\n", new[] { TrackedNeed.Fatigue, TrackedNeed.Hunger, TrackedNeed.Thirst, TrackedNeed.Health }
            .Select(need => (Need: need, Causes: runner.ExplainNeed(id, need).Take(3).ToList()))
            .Where(line => line.Causes.Count > 0)
            .Select(line => $"  {line.Need}: " + string.Join(", ", line.Causes.Select(c => $"{c.Amount:+0;-0} {_formatter.DescribeNeedSource(c.Source)}"))));

        var pack = runner.Campaign.Inventories[character.InventoryId].Quantities;
        _pack.Text = "Carrying: " + (pack.Count == 0 ? "nothing" : string.Join(", ", pack.Select(p => $"{runner.Content.Items[p.Key].Name} ×{p.Value}")));

        var queue = runner.Campaign.QueueOf(id);
        _queue.Text = queue.Count == 0
            ? "Next: nothing queued"
            : "Next:\n" + string.Join("\n", queue.Select((queued, i) =>
            {
                var name = runner.Content.Tasks[queued.DefinitionId].Name;
                var urgent = queued.Priority == TaskPriority.Urgent ? " (urgent)" : queued.Origin == TaskOrigin.Duty ? " (duty)" : "";
                var paused = queued.Progress > 0 ? $" (paused at {queued.Progress / runner.Content.Tasks[queued.DefinitionId].DurationMinutes:P0})" : "";
                return $"  {i + 1}. {name}{urgent}{paused}";
            }));

        var duties = runner.Campaign.DutiesOf(id);
        foreach (var (box, taskId) in _dutyBoxes)
        {
            box.SetPressedNoSignal(duties.Contains(taskId));
        }

        var idle = !runner.Campaign.ActiveTasks.ContainsKey(id) && character.CanWork;
        _dutyNote.Text = duties.Count == 0
            ? "No duties: idle until ordered."
            : idle && runner.WhyNoDuty(id) is { } blocked ? $"Idle: {blocked}" : "";

        // Orders stay clickable so the player can see why one is refused (§45).
        foreach (var (button, taskId) in _taskButtons)
        {
            var eligibility = runner.CheckOrder(id, taskId, CurrentPriority);
            var name = runner.Content.Tasks[taskId].Name;
            button.Text = eligibility.Succeeded ? name : $"{name}: {eligibility.Reason}";
            button.Modulate = eligibility.Succeeded ? Colors.White : new Color(1, 1, 1, 0.5f);
        }
    }

    // "Duty pump the bilge (when it is above 15%)": what the duty is and when it is taken up.
    private static string DutyLabel(TaskDefinition task)
    {
        var when = new List<string>();
        if (task.DutyWhen?.BilgeAbove is { } bilge) when.Add($"bilge above {bilge:0}%");
        if (task.DutyWhen?.HullBelow is { } hull) when.Add($"hull below {hull:0}%");
        if (task.DutyWhen?.RiggingBelow is { } rigging) when.Add($"rigging below {rigging:0}%");
        if (task.DutyWhen?.StartBetween is { } windows) when.Add("at " + string.Join(" and ", windows.Select(w => $"{w[0]:00}:00")));
        return when.Count == 0 ? task.Name : $"{task.Name} ({string.Join(", ", when)})";
    }

    // How they regard others, in words, with the latest reason: shipmates see this much of each other.
    private static string Views(SimulationRunner runner, CharacterId id)
    {
        static string Word(string dimension, int value) => (dimension, value) switch
        {
            ("trust", >= 25) => "trusts", ("trust", <= -25) => "distrusts",
            ("respect", >= 25) => "respects", ("respect", <= -25) => "looks down on",
            ("affection", >= 25) => "likes", ("affection", <= -25) => "dislikes",
            (_, > 0) => $"warms a little to ({dimension})", _ => $"has cooled a little on ({dimension})",
        };

        var views = runner.Campaign.Relationships.Values
            .Where(r => r.From == id && runner.Campaign.Characters.TryGetValue(r.To, out var other) && other.Membership == Membership.Crew)
            .OrderBy(r => r.To)
            .Select(r =>
            {
                var feelings = new[] { ("trust", r.Trust), ("respect", r.Respect), ("affection", r.Affection) }
                    .Where(v => v.Item2 != 0)
                    .Select(v => Word(v.Item1, v.Item2));
                return $"  {string.Join(", ", feelings)} {runner.Campaign.Characters[r.To].Name} — {r.History[^1].Cause}";
            })
            .ToList();
        return views.Count == 0 ? "" : "\nAmong the crew:\n" + string.Join("\n", views) + "\n";
    }

    private void BuildCargo(VBoxContainer body)
    {
        _manifest = Ui.Text("", Ui.TextBody, Ui.Ink, 370);
        body.AddChild(_manifest);
    }

    private void RefreshManifest(SimulationRunner runner)
    {
        var content = runner.Content;
        var store = runner.Campaign.SharedStore.Quantities;
        var (food, water) = runner.SupplyForecast();
        var tools = content.Tasks.Values.SelectMany(t => t.RequiredTools).ToHashSet();
        var materials = content.Tasks.Values.SelectMany(t => t.Consumes.Keys).ToHashSet();
        string Line(KeyValuePair<string, int> pair) => $"  {content.Items[pair.Key].Name} × {pair.Value}";
        var groups = new[]
        {
            ("Water", store.Where(p => content.Items[p.Key].ThirstRelief > 0), $"lasts {Range(water)} for {runner.Campaign.Crew.Count()} aboard"),
            ("Provisions", store.Where(p => content.Items[p.Key].HungerRelief > 0 && content.Items[p.Key].ThirstRelief <= 0), $"lasts {Range(food)}"),
            ("Repair materials", store.Where(p => materials.Contains(p.Key) && content.Items[p.Key].HungerRelief <= 0 && content.Items[p.Key].ThirstRelief <= 0), "used up by repairs"),
            ("Tools", store.Where(p => tools.Contains(p.Key) && !materials.Contains(p.Key)), "needed for some work, not used up"),
            ("Other", store.Where(p => !tools.Contains(p.Key) && !materials.Contains(p.Key) && content.Items[p.Key].HungerRelief <= 0 && content.Items[p.Key].ThirstRelief <= 0), ""),
        };
        var hold = runner.Campaign.SharedStore;
        var room = hold.Capacity.MaxVolumeLitres is { } litres
            ? $" — {hold.TotalVolumeLitres(content.Items):0} of {litres:0} litres, {hold.TotalWeightKg(content.Items):0} of {hold.Capacity.MaxWeightKg ?? 0:0} kg"
            : "";
        _manifest.Text = $"The Garza's hold{room}\n" + string.Join("\n", groups.Where(g => g.Item2.Any())
            .Select(g => $"\n{g.Item1}{(g.Item3.Length > 0 ? $" — {g.Item3}" : "")}\n" + string.Join("\n", g.Item2.OrderBy(p => content.Items[p.Key].Name).Select(Line))));
    }

    private void BuildShip(VBoxContainer body)
    {
        _shipReport = Ui.Text("", Ui.TextBody, Ui.Ink, 370);
        body.AddChild(_shipReport);
        _rations = Ui.Button("", "Short rations: water once a day at dawn, food once at noon. The stores last longer, but the crew grow hungry, thirsty and strained.",
            () => RationingToggled?.Invoke(!_rationsOn));
        body.AddChild(_rations);
    }

    private void RefreshShip(SimulationRunner runner)
    {
        if (runner.Campaign.Voyage is not { } ship)
        {
            _shipReport.Text = "The Garza lies alongside the quay at La Bocana, sails furled, taking on stores and crew.\n\nInspection and repairs before sailing come with the port's later steps.";
            _rations.Visible = false;
            return;
        }

        var repairs = runner.RepairsLeft();
        var hullRepairs = repairs.GetValueOrDefault(Expedition.Simulation.Voyage.ShipPart.Hull);
        var riggingRepairs = repairs.GetValueOrDefault(Expedition.Simulation.Voyage.ShipPart.Rigging);
        var daysLeft = ship.SpeedLeaguesPerHour > 0.05 ? ship.ReckonedRemaining / (ship.SpeedLeaguesPerHour * 24) : double.PositiveInfinity;
        string Condition(double value) => value >= 85 ? "sound" : value >= 70 ? "worn" : value >= 40 ? "damaged" : "failing";
        _shipReport.Text =
            $"{Capital(ship.ShipName)}, bound for {ship.Destination}\n" +
            $"  About {ship.ReckonedRemaining:0} leagues to go{(double.IsInfinity(daysLeft) || daysLeft > 99 ? ", becalmed" : $", {Range(daysLeft)} at this pace")}\n" +
            $"  Wind: {_formatter.WeatherName(ship.WeatherId)} · sail: {ship.SailPlan switch { Expedition.Simulation.Voyage.SailPlan.PressOn => "pressing on", Expedition.Simulation.Voyage.SailPlan.HoveTo => "hove to", _ => "normal" }}\n" +
            $"  Watch: {(ship.WatchKept ? "kept" : "nobody on watch — she sails slowly and blind")}\n\n" +
            $"Hull — {Condition(ship.Hull)} ({ship.Hull:0}%)\n  Leaks worsen as the hull wears; heavy seas strike it.\n  Patches left: {hullRepairs} (oakum and plank)\n" +
            $"Bilge — {ship.Bilge:0}% water\n  The pumps must keep ahead of the leaks; above 80% she founders.\n" +
            $"Rigging — {Condition(ship.Rigging)} ({ship.Rigging:0}%)\n  Worn rigging slows her.\n{CarriedAway(ship)}  Repairs left: {riggingRepairs} (rope)\n";
        _rations.Visible = true;
        _rations.Text = runner.Campaign.Rationing ? "Short rations — restore full" : "Full rations — cut to short";
        _rations.Modulate = runner.Campaign.Rationing ? Ui.Of(Severity.Attention) : Colors.White;
        _rationsOn = runner.Campaign.Rationing;
    }

    private void BuildChronicle(VBoxContainer body)
    {
        var filters = new HBoxContainer();
        filters.AddThemeConstantOverride("separation", 2);
        body.AddChild(filters);
        foreach (var (key, label) in new[] { ("all", "All"), ("people", "People"), ("ship", "Ship"), ("port", "Port"), ("decisions", "Decisions") })
        {
            var filter = key;
            var button = Ui.Button(label, "", () => { _chronicleFilter = filter; _chronicleCount = -1; }, toggle: true);
            button.ButtonGroup ??= null;
            filters.AddChild(button);
        }

        _chronicle = new RichTextLabel
        {
            FitContent = true, SelectionEnabled = true, ScrollActive = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(370, 0),
        };
        _chronicle.AddThemeFontSizeOverride("normal_font_size", Ui.TextBody - 1);
        body.AddChild(_chronicle);
    }

    private void RefreshChronicle(SimulationRunner runner)
    {
        var entries = runner.Campaign.Chronicle.Entries;
        if (entries.Count == _chronicleCount)
        {
            return;
        }

        _chronicleCount = entries.Count;
        bool Keep(ChronicleEntry e) => _chronicleFilter switch
        {
            "people" => e.Kind is ChronicleEntryKind.Injury or ChronicleEntryKind.SurvivalStateChanged or ChronicleEntryKind.RelationshipChanged
                or ChronicleEntryKind.CrewHired or ChronicleEntryKind.Noticed or ChronicleEntryKind.LeftPort or ChronicleEntryKind.LeadHeard
                or ChronicleEntryKind.ClaimTested,
            "ship" => e.Kind is ChronicleEntryKind.WeatherChanged or ChronicleEntryKind.HullStruck or ChronicleEntryKind.RiggingCarriedAway or ChronicleEntryKind.BilgeAlarm
                or ChronicleEntryKind.Foundered or ChronicleEntryKind.SailPlanChanged or ChronicleEntryKind.Landfall,
            "port" => e.Kind is ChronicleEntryKind.CrewHired or ChronicleEntryKind.LeadHeard or ChronicleEntryKind.Noticed
                or ChronicleEntryKind.LeftPort or ChronicleEntryKind.ConvoyWarning or ChronicleEntryKind.LateFee,
            "decisions" => e.Kind is ChronicleEntryKind.EventResolved or ChronicleEntryKind.RationingChanged or ChronicleEntryKind.SailPlanChanged,
            _ => true,
        };

        // Newest first, so the latest news is at the top of the record.
        _chronicle.Text = string.Join("\n", entries.Where(Keep).Reverse().Take(200)
            .Select(e => $"[color=#c9b48a]{new SimulationClock(e.Tick)}[/color] — {_formatter.Format(e)}"));
        _chronicle.BbcodeEnabled = true;
    }

    // ================================================================ modal event and ending

    private void BuildEvent()
    {
        _event = Ui.PanelOf(new Color(0.11f, 0.08f, 0.055f, 0.96f), Ui.Brass, Ui.SpaceXl, Ui.SpaceL);
        _event.AnchorLeft = 0.5f;
        _event.AnchorRight = 0.5f;
        _event.AnchorTop = 0.5f;
        _event.AnchorBottom = 0.5f;
        _event.OffsetLeft = -290;
        _event.OffsetRight = 290;
        _event.OffsetTop = -220;
        _event.GrowVertical = Control.GrowDirection.End;
        _event.Visible = false;
        _root.AddChild(_event);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", Ui.SpaceL);
        _event.AddChild(column);
        _eventTitle = Ui.Text("", Ui.TextDisplay, Ui.Brass);
        _eventTitle.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(_eventTitle);
        _eventText = Ui.Text("", Ui.TextBody + 2, Ui.Ink, 540);
        column.AddChild(_eventText);
        column.AddChild(new HSeparator());
        _eventChoices = new VBoxContainer();
        _eventChoices.AddThemeConstantOverride("separation", Ui.SpaceS);
        column.AddChild(_eventChoices);
    }

    private void BuildEnding()
    {
        _ending = new ColorRect { Color = new Color(0, 0, 0, 0.55f), Visible = false };
        _ending.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_ending);
        var panel = Ui.PanelOf(Ui.Drawer, Ui.Brass, Ui.SpaceXl, Ui.SpaceL);
        panel.AnchorLeft = 0.5f;
        panel.AnchorRight = 0.5f;
        panel.AnchorTop = 0.5f;
        panel.AnchorBottom = 0.5f;
        panel.OffsetLeft = -300;
        panel.OffsetRight = 300;
        panel.OffsetTop = -170;
        panel.OffsetBottom = 170;
        _ending.AddChild(panel);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", Ui.SpaceL);
        panel.AddChild(column);
        _endingTitle = Ui.Text("", Ui.TextDisplay + 4, Ui.Brass);
        _endingTitle.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(_endingTitle);
        _endingText = Ui.Text("", Ui.TextBody + 1, Ui.Ink, 560);
        _endingText.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        column.AddChild(_endingText);
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", Ui.SpaceL);
        column.AddChild(buttons);
        buttons.AddChild(Ui.Button("New voyage", "", () => NewVoyageRequested?.Invoke()));
        buttons.AddChild(Ui.Button("Look around", "", HideEnding));
    }

    // ================================================================ helpers

    private static VBoxContainer AddTab(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer { Name = title, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        tabs.AddChild(scroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(content);
        return content;
    }

    private static Label Wrapped() => Ui.Text("", Ui.TextBody, Ui.Ink, 350);

    private static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}

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
    private readonly Dictionary<string, Gauge> _needBars = [];
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
    private int _scene = -1;
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
            PanelId.Expedition => _runner?.Campaign.Voyage is null ? "Expedition — objectives and papers" : "The voyage — at a glance",
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
    /// look at (§17). Alerts stack in a column on the right, under the status pills, newest on top, three
    /// at most; the same alert again is counted on its card (×2) rather than stacked. Critical alerts
    /// stay until dismissed; others go after a while. All are in the Chronicle.
    /// </summary>
    public void PushAlert(Severity severity, string title, string detail = "", CharacterId? focus = null, string when = "")
    {
        var lifetime = severity == Severity.Critical ? ulong.MaxValue : severity == Severity.Urgent ? 25_000UL : 12_000UL;
        var now = Time.GetTicksMsec();
        if (_alertCards.FirstOrDefault(a => a.Title == title && IsInstanceValid(a.Panel)) is { } same)
        {
            same.Count++;
            same.Heading.Text = $"{Ui.IconOf(severity)} {title}  ×{same.Count}";
            same.Detail.Text = string.Join(" · ", new[] { detail, when }.Where(t => t.Length > 0));
            same.Expires = lifetime == ulong.MaxValue ? lifetime : now + lifetime;
            _alerts.MoveChild(same.Panel, 0);
            return;
        }

        var panel = Ui.PanelOf(new Color(0.08f, 0.06f, 0.045f, 0.95f), new Color(Ui.Of(severity), 0.8f), Ui.SpaceM, Ui.SpaceS);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", Ui.SpaceS);
        panel.AddChild(row);
        row.AddChild(new ColorRect { Color = Ui.Of(severity), CustomMinimumSize = new Vector2(3, 0), MouseFilter = Control.MouseFilterEnum.Ignore });
        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 0);
        row.AddChild(column);
        var heading = Ui.Text($"{Ui.IconOf(severity)} {title}", Ui.TextBody, Ui.Of(severity), 230);
        column.AddChild(heading);
        var text = Ui.Text(string.Join(" · ", new[] { detail, when }.Where(t => t.Length > 0)), Ui.TextMeta, Ui.InkSoft, 230);
        column.AddChild(text);
        if (focus is { } who)
        {
            row.AddChild(Ui.Button("Look", "Show them on deck", () => FocusRequested?.Invoke(who)));
        }

        var card = new AlertCard(panel, heading, text, title, severity) { Expires = lifetime == ulong.MaxValue ? lifetime : now + lifetime };
        row.AddChild(Ui.Button("×", "Dismiss (it stays in the Chronicle)", () => DropAlert(card)));
        _alerts.AddChild(panel);
        _alerts.MoveChild(panel, 0);
        _alertCards.Add(card);
        // Three at most: the oldest of the least severe goes first.
        while (_alertCards.Count > 3)
        {
            DropAlert(_alertCards.OrderBy(a => a.Severity).ThenByDescending(a => a.Panel.GetIndex()).First());
        }

        Ui.FadeIn(panel);
    }

    private sealed class AlertCard(PanelContainer panel, Label heading, Label detail, string title, Severity severity)
    {
        public PanelContainer Panel { get; } = panel;
        public Label Heading { get; } = heading;
        public Label Detail { get; } = detail;
        public string Title { get; } = title;
        public Severity Severity { get; } = severity;
        public int Count { get; set; } = 1;
        public ulong Expires { get; set; }
    }

    private readonly List<AlertCard> _alertCards = [];
    private ulong _lastSweep;

    private void DropAlert(AlertCard card)
    {
        _alertCards.Remove(card);
        if (IsInstanceValid(card.Panel))
        {
            card.Panel.QueueFree();
        }
    }

    // Once a frame: alerts that have had their time go, and the column sits under the status pills.
    private void SweepAlerts()
    {
        // While the game waits on the captain, the alerts wait too: their time runs only while it runs.
        var now = Time.GetTicksMsec();
        var elapsed = now - _lastSweep;
        _lastSweep = now;
        if (_paused.Visible)
        {
            foreach (var card in _alertCards.Where(a => a.Expires != ulong.MaxValue))
            {
                card.Expires += elapsed;
            }
        }

        foreach (var card in _alertCards.Where(a => a.Expires <= now || !IsInstanceValid(a.Panel)).ToList())
        {
            DropAlert(card);
        }

        _alerts.OffsetTop = _statusPanel.Visible ? _statusPanel.Position.Y + _statusPanel.Size.Y + Ui.SpaceS : Ui.SpaceM;
        // The paused notice and the notices keep clear of an open screen: beside it, not over it.
        if (_drawer.Visible)
        {
            _centre.AnchorLeft = _centre.AnchorRight = 0;
            _centre.OffsetLeft = _drawer.Position.X + _drawer.Size.X + Ui.SpaceL;
            _centre.OffsetRight = _centre.OffsetLeft + 480;
        }
        else
        {
            _centre.AnchorLeft = _centre.AnchorRight = 0.5f;
            _centre.OffsetLeft = -240;
            _centre.OffsetRight = 240;
        }
    }

    /// <summary>Clears every alert and notice: when the scene changes (port to sea), the old ones are stale.</summary>
    public void ClearNotices()
    {
        foreach (var card in _alertCards.ToList())
        {
            DropAlert(card);
        }

        foreach (var toast in _toasts.GetChildren())
        {
            toast.QueueFree();
        }
    }

    /// <summary>A short notice at the top that fades on its own (§18): routine news, confirmations.</summary>
    public void Toast(string text, Severity severity = Severity.Information)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        // The same notice twice in a row is said once.
        if (_toasts.GetChildCount() > 0 && _toasts.GetChild(_toasts.GetChildCount() - 1).GetMeta("text", "").AsString() == text)
        {
            return;
        }

        var panel = Ui.PanelOf(new Color(0.07f, 0.055f, 0.045f, 0.9f), null, Ui.SpaceL, Ui.SpaceS);
        panel.SetMeta("text", text);
        panel.AddChild(Ui.Text(severity == Severity.Information ? text : $"{Ui.IconOf(severity)} {text}", Ui.TextBody, Ui.Of(severity), 460));
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

        // Port to sea, or sea to shore: the old scene's notices are stale.
        var scene = campaign.Port is not null ? 0 : campaign.Voyage is { Outcome: Expedition.Simulation.Voyage.VoyageOutcome.UnderWay } ? 1 : 2;
        if (scene != _scene)
        {
            if (_scene >= 0)
            {
                ClearNotices();
            }

            _scene = scene;
        }

        RefreshStatus(runner);
        SweepAlerts();
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
            case PanelId.Expedition:
                RefreshVoyage(runner);
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
        _statusPanel = Ui.PanelOf(new Color(0, 0, 0, 0), null, 0, 0);
        _statusPanel.AnchorLeft = 1;
        _statusPanel.AnchorRight = 1;
        _statusPanel.OffsetTop = Ui.SpaceM;
        _statusPanel.OffsetRight = -Ui.SpaceM;
        _statusPanel.GrowHorizontal = Control.GrowDirection.Begin;
        _root.AddChild(_statusPanel);
        var column = new VBoxContainer();
        _statusPanel.AddChild(column);
        _status = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        _status.AddThemeConstantOverride("separation", Ui.SpaceS);
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

        var chips = new List<(string Icon, string Text, Severity Severity, string Tip, PanelId? Go)>();
        if (campaign.Port is { } port && runner.Content.Port is { } rules)
        {
            var hours = (rules.Departure.Tick - campaign.Clock.Tick) / (double)SimulationClock.TicksPerHour;
            chips.Add(hours > 0
                ? (Ui.IconTime, $"Sails in {Math.Floor(hours):0} h", hours < 6 ? Severity.Urgent : hours < 12 ? Severity.Attention : Severity.Information,
                    $"The convoy sails at dawn on day {rules.Departure.Day}. After that, every half day in port costs {rules.LateFeePerHalfDay} sueldos.", PanelId.Expedition)
                : (Ui.IconTime, "The convoy has sailed", Severity.Critical, $"Every half day in port now costs {rules.LateFeePerHalfDay} sueldos.", PanelId.Expedition));
            var signed = port.Hired.Count;
            chips.Add(signed < rules.MinimumCrew
                ? (Ui.IconPeople, $"Crew {signed}/{rules.MinimumCrew}", Severity.Attention, $"The Garza needs at least {rules.MinimumCrew} hands to sail. Find people through the journal (X).", PanelId.Expedition)
                : (Ui.IconPeople, $"Crew {signed}/{rules.Berths}", Severity.Positive, $"Enough hands to sail; {rules.Berths - signed} berths still free.", PanelId.People));
            chips.Add((Ui.IconPurse, $"{port.Funds}", port.Funds < 500 ? Severity.Attention : Severity.Information, "Sueldos left in the purse, for advances and stores", PanelId.Cargo));
            if (runner.PortWeather is { } weather)
            {
                chips.Add((Ui.IconWind, weather.Name, Severity.Information, weather.Raining
                    ? "Rain: people caught outside take shelter in doorways and the vault arches, and the market thins."
                    : weather.Fog > 0.5 ? "Fog: you can see only a few paces, so people further off are out of sight."
                    : "The weather in port. It changes through the day.", null));
            }
        }
        else if (campaign.Voyage is { Outcome: Expedition.Simulation.Voyage.VoyageOutcome.UnderWay } ship)
        {
            var weather = runner.Content.Voyage?.Weathers.FirstOrDefault(w => w.Id == ship.WeatherId);
            var sea = weather?.SeaState ?? 1;
            var course = ship.Navigation is { } navigation ? $" · {SimulationRunner.CompassPoint(navigation.Heading)}" + (navigation.Beating ? ", beating" : "") : "";
            chips.Add((Ui.IconWind, Capital(_formatter.WeatherName(ship.WeatherId)) + course, sea >= 2.5 ? Severity.Critical : sea >= 1.8 ? Severity.Urgent : Severity.Information,
                "The wind and sea, and the course steered. Heavy weather strains the hull and rigging and makes her leak; heaving to eases it." +
                (ship.Navigation is { } n ? $"\nCourse {n.Heading:0}° by the compass; the wind from the {SimulationRunner.CompassPoint(n.WindFrom)}." +
                    (n.Beating ? " It is too near the course she wants, so she beats to windward, tack upon tack." : "") : "") +
                "\nClick for the chart (M).", ship.Navigation is null ? null : PanelId.Chart));

            var togo = DaysToGo(ship);
            chips.Add((Ui.IconLead, $"{ship.ReckonedRemaining:0} lg · {DaysText(togo)}", Severity.Information,
                $"About {ship.ReckonedRemaining:0} leagues to {ship.Destination} by your reckoning: {DaysText(togo)} at the passage's usual pace. Click for the voyage at a glance (X).", PanelId.Expedition));
            var (food, water) = runner.SupplyForecast();
            var waterVerdict = Verdict("Water", water, togo);
            var foodVerdict = Verdict("Food", food, togo);
            chips.Add((Ui.IconWater, $"Water {Range(water)}", waterVerdict.Severity, $"Water: {waterVerdict.Text}\nClick for the hold (C).", PanelId.Cargo));
            chips.Add((Ui.IconFood, $"Food {Range(food)}", foodVerdict.Severity, $"Food: {foodVerdict.Text}\nClick for the hold (C).", PanelId.Cargo));
            var (part, _, severity) = WorstPart(ship);
            chips.Add((Ui.IconShip, part, severity, $"Hull {ship.Hull:0}%, rigging {ship.Rigging:0}%, bilge {ship.Bilge:0}% water. Click for the ship (H).", PanelId.Ship));

            // Everything else that wants the captain, most pressing first, in one pill: the voyage screen lists them.
            // (Water and food have their own pills, coloured by whether they will last.)
            var matters = Matters(runner).Where(m => m.Severity >= Severity.Attention && !m.Short.StartsWith("Water") && !m.Short.StartsWith("Food")).ToList();
            if (matters.Count > 0)
            {
                var top = matters[0];
                chips.Add((Ui.IconOf(top.Severity), matters.Count > 1 ? $"{top.Short}  +{matters.Count - 1}" : top.Short, top.Severity,
                    string.Join("\n", matters.Select(m => $"{Ui.IconOf(m.Severity)} {m.Short}: {m.Text}")) + "\nClick for what needs seeing to (X).", PanelId.Expedition));
            }
        }

        while (_status.GetChildCount() > chips.Count)
        {
            var last = _status.GetChild(_status.GetChildCount() - 1);
            _status.RemoveChild(last);
            last.QueueFree();
        }

        _pillTargets.Clear();
        for (var i = 0; i < chips.Count; i++)
        {
            if (i >= _status.GetChildCount())
            {
                var pill = Ui.Pill();
                var index = i;
                pill.Pressed += () =>
                {
                    if (index < _pillTargets.Count && _pillTargets[index] is { } go)
                    {
                        if (go == PanelId.Chart || _open != go) TogglePanel(go);
                    }
                };
                _status.AddChild(pill);
            }

            var (icon, text, severity, tip, target) = chips[i];
            Ui.SetPill((Button)_status.GetChild(i), icon, text, severity, tip);
            _pillTargets.Add(target);
        }

        _statusPanel.Visible = chips.Count > 0;
        _sailOrders.Visible = campaign.Voyage is { Outcome: Expedition.Simulation.Voyage.VoyageOutcome.UnderWay };
        foreach (var (button, plan) in _sailButtons)
        {
            button.SetPressedNoSignal(campaign.Voyage?.SailPlan == plan);
        }
    }

    private readonly List<PanelId?> _pillTargets = [];

    // Days still to go, as a range at the passage's usual pace: steadier than the last hour's speed,
    // which a calm makes infinite and a gale doubles.
    private static (double Low, double High) DaysToGo(Expedition.Simulation.Voyage.VoyageState ship)
    {
        var (fast, slow) = Expedition.Simulation.Scenarios.ExpeditionScenario.PassageDays;
        var share = ship.TotalLeagues > 0 ? Math.Clamp(ship.ReckonedRemaining / ship.TotalLeagues, 0, 1.5) : 0;
        return (share * fast, share * slow);
    }

    private static string DaysText((double Low, double High) days) =>
        days.High < 1 ? "under a day" : $"~{Math.Floor(days.Low):0}–{Math.Ceiling(days.High):0} d";

    // Will it last? Stores against the passage still to go, in a word and a sentence.
    private static (Severity Severity, string Word, string Text) Verdict(string what, double days, (double Low, double High) togo)
    {
        if (double.IsInfinity(days) || days >= togo.High * 1.15)
        {
            return (Severity.Positive, "enough", $"lasts {Range(days)}; the passage still to go is {DaysText(togo)}. Enough, with some over.");
        }

        if (days >= togo.Low)
        {
            return (Severity.Attention, "tight", $"lasts {Range(days)}; the passage still to go is {DaysText(togo)}. Enough only on a good passage.");
        }

        var shortBy = Math.Max(1, Math.Round(togo.Low - days));
        return (days < 5 ? Severity.Critical : Severity.Urgent, "short",
            $"lasts {Range(days)}; the passage still to go is {DaysText(togo)}. It runs out {shortBy:0} days or more before landfall.");
    }

    /// <summary>Something that wants the captain: how much, in a few words, why and what to do, and where.</summary>
    private readonly record struct Matter(Severity Severity, string Short, string Text, PanelId Go, CharacterId? Who = null);

    // Everything at sea that wants the captain now, most pressing first (§3, §36).
    private List<Matter> Matters(SimulationRunner runner)
    {
        var matters = new List<Matter>();
        if (runner.Campaign.Voyage is not { Outcome: Expedition.Simulation.Voyage.VoyageOutcome.UnderWay } ship)
        {
            return matters;
        }

        if (ship.Bilge > 60)
        {
            matters.Add(new(Severity.Critical, $"Bilge {ship.Bilge:0}%: pump!", "She is filling; above 80% she founders. Every hand to the pumps: call all hands.", PanelId.Ship));
        }
        else if (ship.Bilge > 30)
        {
            matters.Add(new(Severity.Urgent, $"Bilge {ship.Bilge:0}%", "The leaks are gaining on the pumps. Put more hands to pumping, plug the leaks.", PanelId.Ship));
        }

        var open = ship.Leaks.Count(l => l.Repair == Expedition.Simulation.Voyage.LeakRepair.Open);
        if (open > 0)
        {
            matters.Add(new(Severity.Urgent, open == 1 ? "A leak is open" : $"{open} leaks open", "Plug it now, from the Ship screen; patch it with plank when you can.", PanelId.Ship));
        }

        if (runner.StormComing && ship.Prepared.Count < Expedition.Simulation.Voyage.Preparations.All.Count)
        {
            matters.Add(new(Severity.Urgent, "Prepare for heavy weather", "A blow is coming or on her: batten down, lash the cargo, strike the topmasts.", PanelId.Ship));
        }

        if (!ship.WatchKept)
        {
            matters.Add(new(Severity.Urgent, "No one on watch", "Without a watch she sails slowly and blind. Draw up the watch bill or give someone the Stand watch duty.", PanelId.Ship));
        }

        var togo = DaysToGo(ship);
        var (food, water) = runner.SupplyForecast();
        foreach (var (what, days, remedy) in new[]
                 {
                     ("Water", water, "Cut to short rations, catch the rain when it comes, or make for an island to water."),
                     ("Food", food, "Cut to short rations, put hands to fishing, or make for an island to forage and buy."),
                 })
        {
            var verdict = Verdict(what, days, togo);
            if (verdict.Severity >= Severity.Attention)
            {
                matters.Add(new(verdict.Severity, verdict.Word == "short" ? $"{what} will run out" : $"{what} is tight", $"{Capital(verdict.Text)} {remedy}", PanelId.Ship));
            }
        }

        foreach (var hand in runner.Campaign.Crew.OrderBy(c => c.Name))
        {
            var concerns = Concerns(hand);
            if (concerns.Count > 0 && concerns.Max(c => c.Severity) >= Severity.Urgent)
            {
                var worst = concerns.OrderByDescending(c => c.Severity).First();
                matters.Add(new(worst.Severity, $"{hand.Name}: {worst.Text}", string.Join(", ", concerns.Select(c => c.Text)) + ". Rest them, feed them, or see the surgeon.", PanelId.People, hand.Id));
            }
        }

        if (ship.Navigation is { } navigation)
        {
            if (navigation.Sighted && navigation.Pending)
            {
                matters.Add(new(Severity.Attention, "Land: which coast?", "The coast is in sight. Say what it is by the sailing directions on the chart.", PanelId.Chart));
            }
            else if (!navigation.Sighted && navigation.NearWarned)
            {
                matters.Add(new(Severity.Attention, "Coast near: sound", "By your reckoning the coast is near. Heave the lead from the chart, and keep a lookout.", PanelId.Chart));
            }

            if (runner.CanTakeNoonSight)
            {
                matters.Add(new(Severity.Attention, "Noon: take the sun", "The sun is near its height: take the noon sight on the chart, or leave it to your navigator.", PanelId.Chart));
            }

            if (navigation.Call is { Anchored: true })
            {
                matters.Add(new(Severity.Information, "At anchor", "Send hands to water and forage ashore, buy from the islanders, then weigh from the chart.", PanelId.Chart));
            }
        }

        if (runner.Raining && !runner.Campaign.ActiveTasks.Values.Any(t => t.DefinitionId == "task.catch_rain"))
        {
            matters.Add(new(Severity.Attention, "Rain: catch it", "It is raining and no one is catching it. Order hands to Catch the rain while it lasts.", PanelId.People));
        }

        var repairs = runner.RepairsLeft();
        if ((ship.Hull < 70 && repairs.GetValueOrDefault(Expedition.Simulation.Voyage.ShipPart.Hull) > 0)
            || ((ship.Rigging < 70 || ship.YardSprung) && repairs.GetValueOrDefault(Expedition.Simulation.Voyage.ShipPart.Rigging) > 0))
        {
            matters.Add(new(Severity.Attention, "Repairs wanted", $"Hull {ship.Hull:0}%, rigging {ship.Rigging:0}%. Order a hand to patch the hull or repair the rigging while the weather allows.", PanelId.People));
        }

        return matters.OrderByDescending(m => m.Severity).ToList();
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
            _selectedName.Text = selected == campaign.CaptainId && !character.Name.StartsWith("Captain") ? $"{character.Name} · captain" : character.Name;
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
            { Call.Anchored: true } => $"{Ui.IconLead} Water and forage ashore, then weigh anchor (Chart, M)",
            { Call: { } } => $"{Ui.IconLead} Make for the island, then send the boat in to water",
            { Sighted: true, Identified: null } => $"{Ui.IconLead} Know the coast: which mark is in view? (Chart, M)",
            { Sighted: true } => $"{Ui.IconLead} Run along the coast for Punta Ceniza: about {ship.ReckonedRemaining:0} leagues, by what you took it for",
            _ => $"{Ui.IconLead} Reach {ship.Destination}: about {ship.ReckonedRemaining:0} leagues to go",
        };
    }

    private VBoxContainer _centre = null!;

    private void BuildAlerts()
    {
        // Alerts: a column on the right, under the status pills (SweepAlerts keeps it there).
        _alerts = new VBoxContainer
        {
            AnchorLeft = 1, AnchorRight = 1, OffsetLeft = -360, OffsetRight = -Ui.SpaceM, OffsetTop = 84,
            GrowHorizontal = Control.GrowDirection.Begin, MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _alerts.AddThemeConstantOverride("separation", Ui.SpaceS);
        _root.AddChild(_alerts);

        // The paused notice and the routine notices: top centre, under the clock's line.
        _centre = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetTop = 84, OffsetLeft = -240, OffsetRight = 240,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _centre.AddThemeConstantOverride("separation", Ui.SpaceS);
        _root.AddChild(_centre);
        _paused = Ui.PanelOf(new Color(0.14f, 0.07f, 0.04f, 0.94f), Ui.Of(Severity.Attention), Ui.SpaceL, Ui.SpaceS);
        _paused.Visible = false;
        _centre.AddChild(_paused);
        var row = new HBoxContainer();
        _paused.AddChild(row);
        row.AddChild(Ui.Text("❚❚", Ui.TextHeading, Ui.Of(Severity.Attention)));
        _pausedText = Ui.Text("", Ui.TextBody, Ui.Ink, 300);
        _pausedText.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(_pausedText);
        row.AddChild(Ui.Button("Carry on  (Space)", "Resume at a safe speed", () => ContinueRequested?.Invoke()));
    }

    private void BuildToasts()
    {
        _toasts = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _toasts.AddThemeConstantOverride("separation", Ui.SpaceXs);
        _centre.AddChild(_toasts);
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

        BuildVoyage(_panelBodies[PanelId.Expedition]);
        BuildPeople(_panelBodies[PanelId.People]);
        BuildCargo(_panelBodies[PanelId.Cargo]);
        BuildShip(_panelBodies[PanelId.Ship]);
        BuildChronicle(_panelBodies[PanelId.Chronicle]);
    }

    // The voyage at a glance (Expedition at sea): how far, whether the stores will last, the ship and the
    // crew in a line each, and what wants the captain, each with the way to it.
    private VBoxContainer _voyageView = null!;
    private Label _passageText = null!;
    private Gauge _passageGauge = null!;
    private Gauge _waterGauge = null!;
    private Gauge _foodGauge = null!;
    private Label _storesText = null!;
    private Gauge _overHull = null!;
    private Gauge _overBilge = null!;
    private Gauge _overRigging = null!;
    private Label _crewText = null!;
    private VBoxContainer _matterList = null!;
    private string _matterSignature = "";

    private void BuildVoyage(VBoxContainer body)
    {
        _voyageView = new VBoxContainer { Visible = false };
        _voyageView.AddThemeConstantOverride("separation", Ui.SpaceM);
        body.AddChild(_voyageView);

        var attention = Ui.Card(_voyageView, "Needs your attention");
        _matterList = new VBoxContainer();
        _matterList.AddThemeConstantOverride("separation", Ui.SpaceS);
        attention.AddChild(_matterList);

        var passage = Ui.Card(_voyageView, "The passage");
        _passageText = Ui.Text("", Ui.TextBody, Ui.Ink, 340);
        passage.AddChild(_passageText);
        _passageGauge = new Gauge(passage, "Made good");

        var stores = Ui.Card(_voyageView, "Stores against the passage");
        _waterGauge = new Gauge(stores, $"{Ui.IconWater} Water");
        _foodGauge = new Gauge(stores, $"{Ui.IconFood} Food");
        _storesText = Ui.Text("", Ui.TextMeta + 1, Ui.InkSoft, 340);
        stores.AddChild(_storesText);

        var ship = Ui.Card(_voyageView, "The ship");
        _overHull = new Gauge(ship, "Hull");
        _overBilge = new Gauge(ship, "Bilge");
        _overRigging = new Gauge(ship, "Rigging");

        var crew = Ui.Card(_voyageView, "The crew");
        _crewText = Ui.Text("", Ui.TextBody, Ui.Ink, 340);
        crew.AddChild(_crewText);

        var charter = Ui.Card(_voyageView, "The charter");
        charter.AddChild(Ui.Text("Reach Punta Ceniza on the Ember Coast, learn what became of the factory, re-establish it and hold it.", Ui.TextBody, Ui.InkSoft, 340));
    }

    private void RefreshVoyage(SimulationRunner runner)
    {
        if (runner.Campaign.Voyage is not { Outcome: Expedition.Simulation.Voyage.VoyageOutcome.UnderWay } ship)
        {
            _voyageView.Visible = false;
            return;
        }

        _voyageView.Visible = true;
        var togo = DaysToGo(ship);
        var made = ship.TotalLeagues > 0 ? 100 * (1 - ship.ReckonedRemaining / ship.TotalLeagues) : 0;
        _passageText.Text = $"{Capital(ship.ShipName)}, bound for {ship.Destination}.\nAbout {ship.ReckonedRemaining:0} leagues to go by your reckoning: {DaysText(togo)} at the usual pace."
                            + (Objective(runner) is { } next ? $"\n{next}" : "");
        _passageGauge.Set(made, $"{Math.Max(0, made):0}% of {ship.TotalLeagues:0} lg", Severity.Information,
            "How much of the passage is behind her, by your reckoning. The reckoning drifts; the noon sight and the land put it right.");

        var (food, water) = runner.SupplyForecast();
        void Stores(Gauge gauge, string what, double days)
        {
            var verdict = Verdict(what, days, togo);
            // Full bar: enough for the slowest passage still to go.
            gauge.Set(togo.High > 0 ? 100 * days / togo.High : 100, $"{Range(days)} · {verdict.Word}", verdict.Severity, $"{what}: {verdict.Text}");
        }

        Stores(_waterGauge, "Water", water);
        Stores(_foodGauge, "Food", food);
        _storesText.Text = $"A full bar is enough for the slowest passage still to go ({DaysText(togo)}). " +
                           (runner.Campaign.Rationing ? "On short rations." : "On full rations.") + $"  Purse: {ship.Funds} sueldos.";

        SetShipGauges(ship, _overHull, _overBilge, _overRigging);

        var crew = runner.Campaign.Crew.ToList();
        var aboard = crew.Count(c => c.SurvivalState is SurvivalState.Alive or SurvivalState.Incapacitated);
        var concerns = crew.Select(c => (Hand: c, Concerns: Concerns(c))).ToList();
        var fit = concerns.Count(c => c.Concerns.Count == 0 && c.Hand.SurvivalState == SurvivalState.Alive);
        string Count(params string[] words) => concerns.Count(c => c.Concerns.Any(k => words.Contains(k.Text))).ToString();
        _crewText.Text = $"{aboard} aboard · {fit} fit\n" +
                         $"Tired {Count("tired", "exhausted")} · Hungry {Count("hungry", "starving")} · Thirsty {Count("thirsty", "parched")} · Hurt {Count("hurt", "badly hurt", "incapacitated")} · Strained {Count("strained", "at the end of their tether")}";

        var matters = Matters(runner);
        var signature = string.Join("|", matters.Select(m => $"{m.Severity}{m.Short}{m.Text}"));
        if (signature == _matterSignature)
        {
            return;
        }

        _matterSignature = signature;
        foreach (var child in _matterList.GetChildren())
        {
            child.QueueFree();
        }

        if (matters.Count == 0)
        {
            _matterList.AddChild(Ui.Text($"{Ui.IconOf(Severity.Positive)} Nothing pressing. She is on her course and the hands at their work.", Ui.TextBody, Ui.Of(Severity.Positive), 340));
            return;
        }

        foreach (var matter in matters)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", Ui.SpaceS);
            _matterList.AddChild(row);
            row.AddChild(new ColorRect { Color = Ui.Of(matter.Severity), CustomMinimumSize = new Vector2(3, 0) });
            var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            column.AddThemeConstantOverride("separation", 0);
            row.AddChild(column);
            column.AddChild(Ui.Text($"{Ui.IconOf(matter.Severity)} {matter.Short}", Ui.TextBody, Ui.Of(matter.Severity), 250));
            column.AddChild(Ui.Text(matter.Text, Ui.TextMeta, Ui.InkSoft, 250));
            var go = matter.Go;
            var who = matter.Who;
            var name = go switch { PanelId.Ship => "Ship", PanelId.People => "People", PanelId.Chart => "Chart", PanelId.Cargo => "Cargo", _ => "Open" };
            var button = Ui.Button($"{name} ›", $"Go to the {name} screen to deal with it", () =>
            {
                if (who is { } id) CharacterChosen?.Invoke(id);
                if (go == PanelId.Chart) TogglePanel(go);
                else OpenPanel(go);
            });
            button.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            row.AddChild(button);
        }
    }

    // The hull, bilge and rigging gauges, wherever they are shown.
    private static void SetShipGauges(Expedition.Simulation.Voyage.VoyageState ship, Gauge hull, Gauge bilge, Gauge rigging)
    {
        static string Word(double value) => value >= 85 ? "sound" : value >= 70 ? "worn" : value >= 40 ? "damaged" : "failing";
        static Severity Of(double value) => value >= 85 ? Severity.Positive : value >= 70 ? Severity.Information : value >= 40 ? Severity.Attention : Severity.Critical;
        hull.Set(ship.Hull, $"{Word(ship.Hull)} · {ship.Hull:0}%", Of(ship.Hull), "The hull: leaks worsen as it wears, and heavy seas strike it. Patch it with oakum and plank.");
        bilge.Set(ship.Bilge, ship.Bilge > 60 ? $"filling · {ship.Bilge:0}%" : ship.Bilge > 30 ? $"rising · {ship.Bilge:0}%" : $"pumped · {ship.Bilge:0}%",
            ship.Bilge > 60 ? Severity.Critical : ship.Bilge > 30 ? Severity.Urgent : ship.Bilge > 15 ? Severity.Attention : Severity.Positive,
            "Water in the bilge. The pumps must keep ahead of the leaks; above 80% she founders.");
        rigging.Set(ship.Rigging, $"{Word(ship.Rigging)} · {ship.Rigging:0}%", ship.YardSprung ? Severity.Urgent : Of(ship.Rigging),
            "Masts, yards, sails and ropes. Worn rigging slows her; squalls carry it away. Repair it with rope.");
    }

    private void BuildPeople(VBoxContainer body)
    {
        var roster = Ui.Card(body, "The roster", "click to choose");
        roster.AddChild(Ui.Text("Skills are what you have seen or been told, never more.", Ui.TextMeta, Ui.InkSoft, 340));
        _crewList = new VBoxContainer();
        _crewList.AddThemeConstantOverride("separation", 2);
        roster.AddChild(_crewList);

        var person = Ui.Card(body);
        _name = Ui.Heading("");
        person.AddChild(_name);
        _doing = Ui.Text("", Ui.TextBody, Ui.Ink, 340);
        person.AddChild(_doing);
        foreach (var need in new[] { "Health", "Hunger", "Thirst", "Fatigue", "Stress" })
        {
            _needBars[need] = new Gauge(person, need, 64, 110);
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
            var captain = id == runner.Campaign.CaptainId && !character.Name.StartsWith("Captain") ? " (captain)" : "";
            var concerns = Concerns(character);
            var worst = concerns.Count > 0 ? concerns.Max(c => c.Severity) : Severity.Positive;
            var watch = runner.Campaign.Voyage is { } ship && ship.Watches.ContainsKey(id) ? $" · {WatchShort(ship.WatchOf(id))}" : "";
            // Their state at a glance: a mark and colour for the worst of it, then what they are doing.
            button.Text = $"{Ui.IconOf(worst)} {character.Name}{captain}{watch} — {Activity(runner, character)}" + (concerns.Count > 0 ? $"  · {string.Join(", ", concerns.Select(c => c.Text))}" : "");
            button.SetPressedNoSignal(id == selected);
            button.AddThemeColorOverride("font_color", concerns.Count > 0 ? Ui.Of(worst) : Ui.Ink);
            button.AddThemeColorOverride("font_hover_color", concerns.Count > 0 ? Ui.Of(worst) : Ui.Ink);
        }
    }

    private static string WatchShort(Expedition.Simulation.Voyage.Watch watch) => watch switch
    {
        Expedition.Simulation.Voyage.Watch.Starboard => "stbd",
        Expedition.Simulation.Voyage.Watch.Larboard => "larb",
        Expedition.Simulation.Voyage.Watch.Day => "day",
        _ => "no watch",
    };

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
        _name.Text = isCaptain && !character.Name.StartsWith("Captain") ? $"{character.Name} · captain" : character.Name;
        var progress = runner.Campaign.ActiveTasks.TryGetValue(id, out var task)
            ? $" ({Math.Min(1, task.Progress / runner.Content.Tasks[task.DefinitionId].DurationMinutes):P0})"
            : "";
        _doing.Text = $"Doing: {Activity(runner, character)}{progress}";
        void Need(string key, double value, string mild, string severe, string tip)
        {
            var severity = value >= 85 ? Severity.Urgent : value >= 65 ? Severity.Attention : Severity.Information;
            _needBars[key].Set(value, $"{(value >= 85 ? severe : value >= 65 ? mild : "fine")} · {value:0}", severity, tip);
        }

        var vigour = character.Health.Value;
        _needBars["Health"].Set(vigour, $"{(vigour >= 90 ? "well" : vigour >= 60 ? "hurt" : vigour >= 30 ? "badly hurt" : "near death")} · {vigour:0}",
            vigour >= 90 ? Severity.Positive : vigour >= 75 ? Severity.Information : vigour >= 40 ? Severity.Attention : Severity.Critical, "Health: injuries and sickness bring it down; rest and a surgeon bring it back.");
        Need("Hunger", character.Needs.Hunger, "hungry", "starving", "Hunger: eating brings it down. Short rations feed once a day.");
        Need("Thirst", character.Needs.Thirst, "thirsty", "parched", "Thirst: drinking brings it down. Short rations water once a day.");
        Need("Fatigue", character.Needs.Fatigue, "tired", "exhausted", "Fatigue: work and the night watches raise it; sleep below brings it down. The tired make mistakes.");
        Need("Stress", character.Needs.Stress, "strained", "at the end of their tether", "Stress: danger, hunger and hard words raise it; rest, food and good company ease it.");

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

    private VBoxContainer _cargoView = null!;
    private string _cargoSignature = "";

    private void BuildCargo(VBoxContainer body)
    {
        _cargoView = new VBoxContainer();
        _cargoView.AddThemeConstantOverride("separation", Ui.SpaceM);
        body.AddChild(_cargoView);
    }

    // The manifest as cards: the hold's room, then water and provisions against the passage, then the
    // rest. Rebuilt only when something in it changes.
    private void RefreshManifest(SimulationRunner runner)
    {
        var content = runner.Content;
        var store = runner.Campaign.SharedStore.Quantities;
        var (food, water) = runner.SupplyForecast();
        var togo = runner.Campaign.Voyage is { Outcome: Expedition.Simulation.Voyage.VoyageOutcome.UnderWay } ship ? DaysToGo(ship) : ((double, double)?)null;
        var signature = string.Join(",", store.Select(p => $"{p.Key}{p.Value}")) + $"|{Range(food)}|{Range(water)}|{(togo is { } t ? DaysText(t) : "")}";
        if (signature == _cargoSignature)
        {
            return;
        }

        _cargoSignature = signature;
        foreach (var child in _cargoView.GetChildren())
        {
            child.QueueFree();
        }

        var tools = content.Tasks.Values.SelectMany(t => t.RequiredTools).ToHashSet();
        var materials = content.Tasks.Values.SelectMany(t => t.Consumes.Keys).ToHashSet();
        var hold = runner.Campaign.SharedStore;
        if (hold.Capacity.MaxVolumeLitres is { } litres)
        {
            var room = Ui.Card(_cargoView, "The Garza's hold");
            var volume = hold.TotalVolumeLitres(content.Items);
            var weight = hold.TotalWeightKg(content.Items);
            var maxWeight = hold.Capacity.MaxWeightKg ?? 0;
            new Gauge(room, "Space", 64, 150).Set(100 * volume / litres, $"{volume:0} of {litres:0} l", volume > litres * 0.95 ? Severity.Attention : Severity.Information, "How full the hold is by volume.");
            if (maxWeight > 0)
            {
                new Gauge(room, "Weight", 64, 150).Set(100 * weight / maxWeight, $"{weight:0} of {maxWeight:0} kg", weight > maxWeight * 0.95 ? Severity.Attention : Severity.Information, "How much she carries against what she can.");
            }
        }

        string Lasts(string what, double days) => togo is { } passage ? $"{Range(days)} · {Verdict(what, days, passage).Word}" : $"lasts {Range(days)}";
        var groups = new[]
        {
            ("Water", store.Where(p => content.Items[p.Key].ThirstRelief > 0), Lasts("Water", water), togo is { } a ? Verdict("Water", water, a).Severity : Severity.Information),
            ("Provisions", store.Where(p => content.Items[p.Key].HungerRelief > 0 && content.Items[p.Key].ThirstRelief <= 0), Lasts("Food", food), togo is { } b ? Verdict("Food", food, b).Severity : Severity.Information),
            ("Repair materials", store.Where(p => materials.Contains(p.Key) && content.Items[p.Key].HungerRelief <= 0 && content.Items[p.Key].ThirstRelief <= 0), "used up by repairs", Severity.Information),
            ("Tools", store.Where(p => tools.Contains(p.Key) && !materials.Contains(p.Key)), "not used up", Severity.Information),
            ("Other", store.Where(p => !tools.Contains(p.Key) && !materials.Contains(p.Key) && content.Items[p.Key].HungerRelief <= 0 && content.Items[p.Key].ThirstRelief <= 0), "", Severity.Information),
        };
        foreach (var (title, items, note, severity) in groups)
        {
            var list = items.OrderBy(p => content.Items[p.Key].Name).ToList();
            if (list.Count == 0 && title is not ("Water" or "Provisions"))
            {
                continue;
            }

            var card = Ui.Card(_cargoView, title, note);
            if (severity != Severity.Information)
            {
                var header = card.GetChild(0).GetChild(0);
                ((Label)header.GetChild(1)).AddThemeColorOverride("font_color", Ui.Of(severity));
            }

            if (list.Count == 0)
            {
                card.AddChild(Ui.Text($"{Ui.IconOf(Severity.Critical)} None left aboard.", Ui.TextBody, Ui.Of(Severity.Critical)));
                continue;
            }

            var grid = new GridContainer { Columns = 2 };
            grid.AddThemeConstantOverride("h_separation", Ui.SpaceL);
            grid.AddThemeConstantOverride("v_separation", 1);
            card.AddChild(grid);
            foreach (var (item, count) in list)
            {
                var name = Ui.Text(content.Items[item].Name, Ui.TextBody, Ui.Ink);
                name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                grid.AddChild(name);
                var amount = Ui.Text($"× {count}", Ui.TextBody, Ui.Brass);
                amount.HorizontalAlignment = HorizontalAlignment.Right;
                grid.AddChild(amount);
            }
        }
    }

    private VBoxContainer _stormBox = null!;
    private Label _stormReport = null!;
    private readonly Dictionary<string, Button> _stormButtons = [];
    private VBoxContainer _watchBox = null!;
    private Label _watchReport = null!;
    private Button _allHands = null!;
    private GridContainer _watchRows = null!;
    private readonly Dictionary<CharacterId, OptionButton> _watchPickers = [];
    private SimulationRunner? _watchRunner;

    private Control _conditionCard = null!;
    private Gauge _hullGauge = null!;
    private Gauge _bilgeGauge = null!;
    private Gauge _riggingGauge = null!;
    private Label _repairsText = null!;
    private Label _carriedText = null!;
    private Control _provisionsCard = null!;
    private Label _rationsText = null!;
    private readonly Dictionary<string, Label> _prepLabels = [];
    private Label _leaksText = null!;

    private void BuildShip(VBoxContainer body)
    {
        var under = Ui.Card(body, "Under way");
        _shipReport = Ui.Text("", Ui.TextBody, Ui.Ink, 340);
        under.AddChild(_shipReport);

        var condition = Ui.Card(body, "Condition");
        _conditionCard = condition.GetParent<Control>();
        _hullGauge = new Gauge(condition, "Hull");
        _bilgeGauge = new Gauge(condition, "Bilge");
        _riggingGauge = new Gauge(condition, "Rigging");
        _carriedText = Ui.Text("", Ui.TextMeta + 1, Ui.Of(Severity.Attention), 340);
        condition.AddChild(_carriedText);
        _repairsText = Ui.Text("", Ui.TextMeta + 1, Ui.InkSoft, 340);
        condition.AddChild(_repairsText);

        var provisions = Ui.Card(body, "Rations");
        _provisionsCard = provisions.GetParent<Control>();
        var rationRow = new HBoxContainer();
        provisions.AddChild(rationRow);
        _rationsText = Ui.Text("", Ui.TextBody, Ui.Ink, 200);
        _rationsText.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        rationRow.AddChild(_rationsText);
        _rations = Ui.Button("", "Short rations: water once a day at dawn, food once at noon. The stores last longer, but the crew grow hungry, thirsty and strained.",
            () => RationingToggled?.Invoke(!_rationsOn));
        _rations.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        rationRow.AddChild(_rations);

        // Heavy weather: the signs, the preparations as a checklist with the order beside each, the leaks.
        var storm = Ui.Card(body, "Heavy weather");
        _stormBox = storm;
        _stormReport = Ui.Text("", Ui.TextBody, Ui.Ink, 340);
        storm.AddChild(_stormReport);
        foreach (var (work, label, tip) in new[]
                 {
                     ("hatches", "Batten down", "Less water below in a seaway, and the stores kept dry"),
                     ("cargo", "Lash cargo", "Nothing breaks loose in the hold to stove casks and strain the hull"),
                     ("aloft", "Strike topmasts", "Far less wear and carrying away aloft; she sails slower until they are swayed up"),
                     ("plug", "Plug a leak", "Fast, with oakum and a wedge; the next blow may burst it. A proper patch needs plank."),
                 })
        {
            var row = new HBoxContainer();
            storm.AddChild(row);
            var state = Ui.Text("", Ui.TextBody, Ui.Ink);
            state.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(state);
            _prepLabels[work] = state;
            var order = work;
            var button = Ui.Button(label, tip, () =>
            {
                if (_watchRunner is { } runner)
                {
                    ShowResult(runner.OrderStormWork(order), $"{label}: a hand is on it.");
                }
            });
            button.CustomMinimumSize = new Vector2(120, Ui.MinHit);
            row.AddChild(button);
            _stormButtons[work] = button;
        }

        _leaksText = Ui.Text("", Ui.TextMeta + 1, Ui.InkSoft, 340);
        storm.AddChild(_leaksText);

        // The watch bill: who stands which watch, which has the deck, and all hands.
        var watch = Ui.Card(body, "The watch bill");
        _watchBox = watch;
        _watchReport = Ui.Text("", Ui.TextBody, Ui.Ink, 340);
        _watchBox.AddChild(_watchReport);
        _allHands = Ui.Button("", "All hands: both watches on deck for the work in hand, at the cost of their sleep", () =>
        {
            if (_watchRunner?.Campaign.Voyage is { } voyage)
            {
                _watchRunner.CallAllHands(!voyage.AllHands);
            }
        });
        _watchBox.AddChild(_allHands);
        _watchRows = new GridContainer { Columns = 2 };
        _watchRows.AddThemeConstantOverride("h_separation", Ui.SpaceM);
        _watchBox.AddChild(_watchRows);
        _watchBox.AddChild(Ui.Text("Four hours on and four below through the evening and night, the dog watches shifting the round each day; by day both turn to. The watch on deck keeps the helm; the watch below sleeps once tired.",
            Ui.TextMeta, Ui.InkSoft, 340));
    }

    private static readonly (Expedition.Simulation.Voyage.Watch Watch, string Name)[] WatchChoices =
    [
        (Expedition.Simulation.Voyage.Watch.Starboard, "Starboard watch"), (Expedition.Simulation.Voyage.Watch.Larboard, "Larboard watch"),
        (Expedition.Simulation.Voyage.Watch.Day, "Day-worker"), (Expedition.Simulation.Voyage.Watch.None, "No watch"),
    ];

    // The heavy weather section: what is coming, what is done, and every leak with how it was met.
    private void RefreshStorm(SimulationRunner runner, Expedition.Simulation.Voyage.VoyageState ship)
    {
        _stormBox.GetParent<Control>().Visible = true;
        var coming = ship.NextWeatherId is { } next ? runner.Content.Voyage?.Weathers.FirstOrDefault(w => w.Id == next) : null;
        var storm = runner.StormComing;
        _stormReport.Text = storm
            ? coming is { SeaState: >= 1.8 } ? $"{Ui.IconOf(Severity.Urgent)} Heavy weather is coming: {_formatter.WeatherName(coming.Id)}. Prepare while there is time." : $"{Ui.IconOf(Severity.Urgent)} It is blowing hard."
            : "Fair enough for now. The signs of a blow are seen some hours ahead; prepare then.";
        _stormReport.AddThemeColorOverride("font_color", storm ? Ui.Of(Severity.Urgent) : Ui.InkSoft);
        void Line(string key, string yes, string no)
        {
            var done = ship.Prepared.Contains(key);
            _prepLabels[key].Text = done ? $"✓ {yes}" : $"○ {no}";
            _prepLabels[key].AddThemeColorOverride("font_color", done ? Ui.Of(Severity.Positive) : storm ? Ui.Of(Severity.Attention) : Ui.Ink);
        }

        Line("hatches", "Hatches battened", "Hatches open");
        Line("cargo", "Cargo lashed", "Cargo not lashed");
        Line("aloft", "Topmasts struck (slower)", "Topmasts up");
        var open = ship.Leaks.Count(l => l.Repair == Expedition.Simulation.Voyage.LeakRepair.Open);
        _prepLabels["plug"].Text = open > 0 ? $"▲ {(open == 1 ? "A leak is open" : $"{open} leaks open")}" : "✓ No leak open";
        _prepLabels["plug"].AddThemeColorOverride("font_color", open > 0 ? Ui.Of(Severity.Urgent) : Ui.Of(Severity.Positive));
        var lines = new List<string>();
        foreach (var leak in ship.Leaks)
        {
            var day = new SimulationClock(leak.Tick).Day + 1;
            var how = leak.Repair switch
            {
                Expedition.Simulation.Voyage.LeakRepair.Open => "open",
                Expedition.Simulation.Voyage.LeakRepair.Plugged => $"plugged day {new SimulationClock(leak.RepairedTick).Day + 1}; may burst",
                _ => $"made good day {new SimulationClock(leak.RepairedTick).Day + 1}",
            };
            lines.Add($"{Capital(leak.Where)}, sprung day {day}: {how}");
        }

        _leaksText.Text = lines.Count == 0 ? "" : "Leaks: \n" + string.Join("\n", lines);
        _leaksText.Visible = lines.Count > 0;
        foreach (var (work, button) in _stormButtons)
        {
            button.Disabled = work == "plug" ? open == 0 : ship.Prepared.Contains(work);
        }
    }

    // The watch bill section: rebuilt when the crew changes, refreshed every frame the panel is open.
    private void RefreshWatchBill(SimulationRunner runner, Expedition.Simulation.Voyage.VoyageState ship)
    {
        _watchRunner = runner;
        _watchBox.GetParent<Control>().Visible = true;
        var tick = runner.Campaign.Clock.Tick;
        var onDeck = Expedition.Simulation.Voyage.WatchBill.OnDeck(tick);
        var hour = tick % SimulationClock.TicksPerDay / SimulationClock.TicksPerHour;
        var daysWork = hour >= Expedition.Simulation.Voyage.WatchBill.DaysWorkBegins && hour < Expedition.Simulation.Voyage.WatchBill.DaysWorkEnds;
        _watchReport.Text =
            $"{Capital(Expedition.Simulation.Voyage.WatchBill.NameAt(tick))}: " +
            (ship.AllHands ? "all hands on deck." : ship.TurnedOut ? "the watch below turned out to the pumps." : daysWork ? "both watches at the day's work." : $"the {(onDeck == Expedition.Simulation.Voyage.Watch.Starboard ? "starboard" : "larboard")} watch has the deck; the other sleeps below.");
        _allHands.Text = ship.AllHands ? "Dismiss all hands: the watch below goes below" : "All hands on deck!";
        _allHands.Modulate = ship.AllHands ? Ui.Of(Severity.Attention) : Colors.White;
        var crew = runner.Campaign.Crew.Where(c => c.Id != runner.Campaign.CaptainId).OrderBy(c => c.Name).ToList();
        if (crew.Count * 2 != _watchRows.GetChildCount())
        {
            foreach (var child in _watchRows.GetChildren())
            {
                child.QueueFree();
            }

            _watchPickers.Clear();
            foreach (var hand in crew)
            {
                var who = Ui.Text(hand.Name, Ui.TextBody, Ui.Ink);
                who.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                _watchRows.AddChild(who);
                var picker = new OptionButton { FocusMode = Control.FocusModeEnum.None, TooltipText = "Starboard and larboard stand the night watches by turns; a day-worker works by day and sleeps all night; no watch: works when needed." };
                foreach (var (_, name) in WatchChoices)
                {
                    picker.AddItem(name);
                }

                var id = hand.Id;
                picker.ItemSelected += index => _watchRunner?.SetWatch(id, WatchChoices[(int)index].Watch);
                _watchRows.AddChild(picker);
                _watchPickers[id] = picker;
            }
        }

        foreach (var (id, picker) in _watchPickers)
        {
            var index = Array.FindIndex(WatchChoices, c => c.Watch == ship.WatchOf(id));
            if (picker.Selected != index && !picker.HasFocus())
            {
                picker.Select(index);
            }
        }
    }

    private void RefreshShip(SimulationRunner runner)
    {
        if (runner.Campaign.Voyage is not { } ship)
        {
            _shipReport.Text = "The Garza lies alongside the quay at La Bocana, sails furled, taking on stores and crew.\n\nInspection and repairs before sailing come with the port's later steps.";
            foreach (var card in new[] { _conditionCard, _provisionsCard, _stormBox.GetParent<Control>(), _watchBox.GetParent<Control>() })
            {
                card.Visible = false;
            }

            return;
        }

        _conditionCard.Visible = _provisionsCard.Visible = true;
        RefreshWatchBill(runner, ship);
        RefreshStorm(runner, ship);

        var repairs = runner.RepairsLeft();
        var togo = DaysToGo(ship);
        var sail = ship.SailPlan switch { Expedition.Simulation.Voyage.SailPlan.PressOn => "pressing on", Expedition.Simulation.Voyage.SailPlan.HoveTo => "hove to", _ => "normal sail" };
        _shipReport.Text =
            $"{Capital(ship.ShipName)}, bound for {ship.Destination}: about {ship.ReckonedRemaining:0} leagues, {DaysText(togo)}.\n" +
            $"{Ui.IconWind} {Capital(_formatter.WeatherName(ship.WeatherId))} · {sail} · {ship.SpeedLeaguesPerHour * 24:0} leagues a day at this pace\n" +
            (ship.WatchKept ? $"{Ui.IconPeople} The watch is kept." : $"{Ui.IconOf(Severity.Urgent)} Nobody on watch: she sails slowly and blind.") +
            $"   {Ui.IconPurse} {ship.Funds} sueldos";

        SetShipGauges(ship, _hullGauge, _bilgeGauge, _riggingGauge);
        _carriedText.Text = CarriedAway(ship).Replace("  ", "").TrimEnd();
        _carriedText.Visible = _carriedText.Text.Length > 0;
        _repairsText.Text = $"Patches left: {repairs.GetValueOrDefault(Expedition.Simulation.Voyage.ShipPart.Hull)} (oakum and plank) · " +
                            $"rigging repairs left: {repairs.GetValueOrDefault(Expedition.Simulation.Voyage.ShipPart.Rigging)} (rope). Order them in People.";

        var (food, water) = runner.SupplyForecast();
        _rationsOn = runner.Campaign.Rationing;
        _rationsText.Text = (_rationsOn ? "Short rations." : "Full rations.") + $"  Water {Range(water)} · food {Range(food)}; {DaysText(togo)} to go.";
        _rations.Text = _rationsOn ? "Restore full" : "Cut to short";
        _rations.Modulate = _rationsOn ? Ui.Of(Severity.Attention) : Colors.White;
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

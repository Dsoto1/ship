using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Tasks;
using Expedition.Simulation.World;
using Godot;

namespace Expedition.Game;

/// <summary>
/// Graybox interface: time and save controls, crew list, the selected character's record, task orders
/// and the chronicle. It only displays simulation state and raises events. <see cref="Main"/> turns
/// those events into simulation commands, so the HUD never changes game state itself.
/// </summary>
public partial class Hud : CanvasLayer
{
    private static readonly string[] SpeedNames = ["Pause", "1×", "10×", "60×"];

    private readonly List<(Button Button, int Speed)> _speedButtons = [];
    private readonly List<(Button Button, CharacterId Id)> _crewButtons = [];
    private readonly List<(Button Button, string TaskId)> _taskButtons = [];
    private readonly List<(CheckBox Box, string TaskId)> _dutyBoxes = [];
    private readonly Dictionary<string, ProgressBar> _needBars = [];

    private Label _time = null!;
    private Label _status = null!;
    private VBoxContainer _crewList = null!;
    private Label _hold = null!;
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
    private RichTextLabel _chronicle = null!;
    private ChronicleFormatter _formatter = null!;
    private int _chronicleCount = -1;

    /// <summary>The player chose a time speed (index into the speeds Main defines).</summary>
    public event Action<int>? SpeedChosen;

    /// <summary>The player asked to save.</summary>
    public event Action? SaveRequested;

    /// <summary>The player asked to load.</summary>
    public event Action? LoadRequested;

    /// <summary>The player selected a character from the list.</summary>
    public event Action<CharacterId>? CharacterChosen;

    /// <summary>The player gave the selected character an order: task ID and priority.</summary>
    public event Action<string, TaskPriority>? TaskOrdered;

    /// <summary>The player turned one of the selected character's standing duties on or off.</summary>
    public event Action<string, bool>? DutyToggled;

    /// <summary>The player cleared the selected character's waiting orders.</summary>
    public event Action? QueueCleared;

    /// <summary>The player ordered the selected character to stop.</summary>
    public event Action? StopOrdered;

    public override void _Ready()
    {
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = BuildTheme() };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        BuildTopBar(root);
        BuildLeftPanel(root);
        BuildRightPanel(root);
        BuildChronicle(root);
    }

    // Smaller text and see-through dark panels, so the interface frames the scene instead of covering it.
    private static Theme BuildTheme()
    {
        var theme = new Theme { DefaultFontSize = 13 };
        var panel = new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.06f, 0.05f, 0.72f),
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 6, ContentMarginBottom = 6,
        };
        theme.SetStylebox("panel", "PanelContainer", panel);
        foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
        {
            var button = new StyleBoxFlat
            {
                BgColor = state switch
                {
                    "hover" => new Color(0.3f, 0.25f, 0.18f, 0.85f),
                    "pressed" => new Color(0.45f, 0.35f, 0.2f, 0.9f),
                    _ => new Color(0.18f, 0.15f, 0.12f, 0.75f),
                },
                CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
                ContentMarginLeft = 6, ContentMarginRight = 6, ContentMarginTop = 3, ContentMarginBottom = 3,
            };
            theme.SetStylebox(state, "Button", button);
        }

        theme.SetColor("font_color", "Label", new Color(0.93f, 0.9f, 0.84f));
        theme.SetColor("font_color", "Button", new Color(0.93f, 0.9f, 0.84f));
        return theme;
    }

    /// <summary>Rebuilds the crew list and task buttons for a newly started or loaded campaign.</summary>
    public void Bind(SimulationRunner runner)
    {
        foreach (var (button, _) in _crewButtons) button.QueueFree();
        foreach (var (button, _) in _taskButtons) button.QueueFree();
        foreach (var (box, _) in _dutyBoxes) box.QueueFree();
        _crewButtons.Clear();
        _taskButtons.Clear();
        _dutyBoxes.Clear();
        _chronicleCount = -1;
        _formatter = new ChronicleFormatter(runner.Campaign, runner.Content);

        foreach (var character in runner.Campaign.Characters.Values.OrderBy(c => c.Id))
        {
            var id = character.Id;
            var button = new Button { Alignment = HorizontalAlignment.Left, ClipText = true, FocusMode = Control.FocusModeEnum.None };
            button.Pressed += () => CharacterChosen?.Invoke(id);
            _crewList.AddChild(button);
            _crewButtons.Add((button, id));
        }

        foreach (var task in runner.Content.Tasks.Values.OrderBy(t => t.Name))
        {
            var taskId = task.Id;
            var button = new Button
            {
                Text = task.Name, Alignment = HorizontalAlignment.Left, FocusMode = Control.FocusModeEnum.None,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            button.Pressed += () => TaskOrdered?.Invoke(taskId, CurrentPriority);
            _taskList.AddChild(button);
            _taskButtons.Add((button, taskId));

            // Rest is not a duty: everyone rests of their own accord when tired.
            if (task.IsRest)
            {
                continue;
            }

            var box = new CheckBox { Text = task.Name, FocusMode = Control.FocusModeEnum.None };
            box.Toggled += on => DutyToggled?.Invoke(taskId, on);
            _dutyList.AddChild(box);
            _dutyBoxes.Add((box, taskId));
        }
    }

    private TaskPriority CurrentPriority => _urgent.ButtonPressed ? TaskPriority.Urgent : TaskPriority.Normal;

    /// <summary>Shows a one-line message, such as why an order was refused.</summary>
    public void ShowStatus(string message) => _status.Text = message;

    /// <summary>Shows the result of a command: nothing on success, the reason on refusal.</summary>
    public void ShowResult(CommandResult result, string successMessage = "")
    {
        _status.Text = result.Succeeded ? successMessage : result.Reason;
    }

    /// <summary>Refreshes every display from the simulation. Called once per frame.</summary>
    public void Refresh(SimulationRunner runner, CharacterId? selected, int speedIndex)
    {
        var campaign = runner.Campaign;
        _time.Text = $"{campaign.Clock}   ·   {campaign.Phase}";
        foreach (var (button, speed) in _speedButtons)
        {
            button.ButtonPressed = speed == speedIndex;
        }

        foreach (var (button, id) in _crewButtons)
        {
            var character = campaign.Characters[id];
            var captain = id == campaign.CaptainId ? " (captain)" : "";
            button.Text = $"{character.Name}{captain} — {Activity(runner, character)}";
            button.Modulate = id == selected ? new Color(1f, 0.93f, 0.7f) : Colors.White;
        }

        _hold.Text = "Hold: " + string.Join(" · ", campaign.SharedStore.Quantities
            .Select(pair => $"{runner.Content.Items[pair.Key].Name} {pair.Value}"));

        RefreshSelected(runner, selected);

        if (campaign.Chronicle.Entries.Count != _chronicleCount)
        {
            _chronicleCount = campaign.Chronicle.Entries.Count;
            _chronicle.Text = string.Join("\n", campaign.Chronicle.Entries.TakeLast(30)
                .Select(e => $"[{new SimulationClock(e.Tick)}] {_formatter.Format(e)}"));
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

    // What the captain has learned about this person (TECHNICAL_SPEC.md §5.2). For the crew already aboard
    // the true values are shown above; this line shows what the captain has confirmed, and for recruits
    // (Ticket 5c) it will be all the HUD shows.
    private static string Known(SimulationRunner runner, CharacterId id)
    {
        var lines = KnowledgeText.Describe(runner.Campaign.Knowledge, id, runner.Content);
        return lines.Count == 0 ? "" : $"Captain knows: {string.Join(", ", lines)}\n";
    }

    private void RefreshSelected(SimulationRunner runner, CharacterId? selected)
    {
        if (selected is not { } id || !runner.Campaign.Characters.TryGetValue(id, out var character))
        {
            _name.Text = "No one selected";
            _doing.Text = "Click a crew member on deck or in the crew list.";
            _details.Text = "";
            _queue.Text = "";
            _dutyNote.Text = "";
            _why.Text = "";
            _pack.Text = "";
            return;
        }

        _name.Text = id == runner.Campaign.CaptainId ? $"{character.Name} (captain)" : character.Name;
        var skills = character.Skills.Levels.Count == 0
            ? "none"
            : string.Join(", ", character.Skills.Levels.OrderByDescending(p => p.Value)
                .Select(p => $"{runner.Content.Skills[p.Key].Name} {p.Value}"));
        var progress = runner.Campaign.ActiveTasks.TryGetValue(id, out var task)
            ? $" ({task.Progress / runner.Content.Tasks[task.DefinitionId].DurationMinutes:P0})"
            : "";
        var attributes = runner.Content.Attributes.Values
            .Select(a => $"{a.Name} {character.Attributes.Get(a.Id)}");
        var status = character.StatusId is not null && runner.Content.Statuses.TryGetValue(character.StatusId, out var standing) ? standing.Name.ToLowerInvariant() : "standing unknown";
        var sex = character.Sex == Sex.Unrecorded ? "" : $"{character.Sex.ToString().ToLowerInvariant()}, ";
        var tongues = string.Join(", ", character.Languages.Levels.OrderByDescending(l => l.Value)
            .Select(l => $"{(runner.Content.Languages.TryGetValue(l.Key, out var language) ? language.Name : l.Key)} {l.Value}"));
        _details.Text =
            $"{sex}{character.Age}, {status}, from {character.Origin}\n" +
            (tongues.Length > 0 ? $"Speaks: {tongues}\n" : "") +
            $"{character.SurvivalState} · health {character.Health.Value:0}\n" +
            (character.Appearance is { } looks ? $"Looks: {AppearanceText.Describe(looks, runner.Content.Appearance)}\n" : "") +
            $"Skills: {skills}\n" +
            $"Physique: {string.Join(", ", attributes)}\n" +
            $"Traits: {(character.Traits.Count == 0 ? "none" : string.Join(", ", character.Traits.Select(t => runner.Content.Traits.TryGetValue(t, out var trait) ? trait.Name : t)))}\n" +
            (character.Health.Conditions.Count > 0
                ? $"Conditions: {string.Join(", ", character.Health.Conditions.Select(c => runner.Content.Conditions.TryGetValue(c, out var condition) ? condition.Name : c))}\n"
                : "") +
            Known(runner, id) +
            Views(runner, id);

        _doing.Text = $"Doing: {Activity(runner, character)}{progress}";
        _needBars["Hunger"].Value = character.Needs.Hunger;
        _needBars["Thirst"].Value = character.Needs.Thirst;
        _needBars["Fatigue"].Value = character.Needs.Fatigue;
        _needBars["Stress"].Value = character.Needs.Stress;

        _why.Text = "Why (last day):\n" + string.Join("\n", new[] { TrackedNeed.Fatigue, TrackedNeed.Hunger, TrackedNeed.Thirst, TrackedNeed.Health }
            .Select(need => (Need: need, Causes: runner.ExplainNeed(id, need).Take(3).ToList()))
            .Where(line => line.Causes.Count > 0)
            .Select(line => $"  {line.Need}: " + string.Join(", ", line.Causes.Select(c => $"{c.Amount:+0;-0} {_formatter.DescribeNeedSource(c.Source)}"))));

        var pack = runner.Campaign.Inventories[character.InventoryId].Quantities;
        _pack.Text = "Carrying: " + (pack.Count == 0 ? "nothing" : string.Join(", ", pack.Select(p => $"{runner.Content.Items[p.Key].Name} ×{p.Value}")));

        var queue = runner.Campaign.QueueOf(id);
        _queue.Text = queue.Count == 0
            ? "Next: nothing queued"
            : "Next:\n" + string.Join("\n", queue.Select((task, i) =>
            {
                var name = runner.Content.Tasks[task.DefinitionId].Name;
                var urgent = task.Priority == TaskPriority.Urgent ? " (urgent)" : task.Origin == TaskOrigin.Duty ? " (duty)" : "";
                var paused = task.Progress > 0 ? $" (paused at {task.Progress / runner.Content.Tasks[task.DefinitionId].DurationMinutes:P0})" : "";
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

        // Orders stay clickable so the player can see why one is refused; unavailable ones are dimmed.
        foreach (var (button, taskId) in _taskButtons)
        {
            var eligibility = runner.CheckOrder(id, taskId, CurrentPriority);
            var name = runner.Content.Tasks[taskId].Name;
            button.Text = eligibility.Succeeded ? name : $"{name}: {eligibility.Reason}";
            button.Modulate = eligibility.Succeeded ? Colors.White : new Color(1, 1, 1, 0.5f);
        }
    }

    /// <summary>How the character regards others, each with the most recent reason.</summary>
    private static string Views(SimulationRunner runner, CharacterId id)
    {
        var views = runner.Campaign.Relationships.Values
            .Where(r => r.From == id)
            .OrderBy(r => r.To)
            .Select(r =>
            {
                var values = new[] { ("trust", r.Trust), ("respect", r.Respect), ("affection", r.Affection) }
                    .Where(v => v.Item2 != 0)
                    .Select(v => $"{v.Item1} {v.Item2:+0;-0}");
                return $"  {runner.Campaign.Characters[r.To].Name}: {string.Join(", ", values)} — {r.History[^1].Cause}";
            })
            .ToList();
        return views.Count == 0 ? "" : "Views of others:\n" + string.Join("\n", views) + "\n";
    }

    private void BuildTopBar(Control root)
    {
        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        root.AddChild(panel);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        panel.AddChild(row);

        _time = new Label { CustomMinimumSize = new Vector2(260, 0) };
        row.AddChild(_time);
        for (var i = 0; i < SpeedNames.Length; i++)
        {
            var speed = i;
            var button = new Button { Text = SpeedNames[i], ToggleMode = true, TooltipText = $"Key {i + 1}", FocusMode = Control.FocusModeEnum.None };
            button.Pressed += () => SpeedChosen?.Invoke(speed);
            row.AddChild(button);
            _speedButtons.Add((button, speed));
        }

        var save = new Button { Text = "Save", TooltipText = "Cmd+S or F5", FocusMode = Control.FocusModeEnum.None };
        save.Pressed += () => SaveRequested?.Invoke();
        row.AddChild(save);
        var load = new Button { Text = "Load", TooltipText = "Cmd+L or F9", FocusMode = Control.FocusModeEnum.None };
        load.Pressed += () => LoadRequested?.Invoke();
        row.AddChild(load);

        _status = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true };
        row.AddChild(_status);
    }

    private void BuildLeftPanel(Control root)
    {
        var panel = new PanelContainer { Position = new Vector2(8, 40), CustomMinimumSize = new Vector2(230, 0) };
        root.AddChild(panel);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 2);
        panel.AddChild(column);
        column.AddChild(new Label { Text = "Crew" });
        _crewList = new VBoxContainer();
        _crewList.AddThemeConstantOverride("separation", 2);
        column.AddChild(_crewList);
        column.AddChild(new HSeparator());
        _hold = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(214, 0) };
        _hold.AddThemeFontSizeOverride("font_size", 11);
        column.AddChild(_hold);

        // Controls reminder, folded away until asked for.
        var help = new Label
        {
            Text = "Left-click: select · Right-click deck: walk there\nSpace: pause · 1–4: speed · Tab: next crew member\n" +
                   "WASD: pan · wheel: zoom (zoom out to see the sails) · Q/E: turn\nCmd+S / Cmd+L: save / load",
            Modulate = new Color(1, 1, 1, 0.75f),
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(214, 0),
        };
        help.AddThemeFontSizeOverride("font_size", 11);
        var toggle = new Button { Text = "Controls", FocusMode = Control.FocusModeEnum.None, Alignment = HorizontalAlignment.Left };
        toggle.Pressed += () => help.Visible = !help.Visible;
        column.AddChild(toggle);
        column.AddChild(help);
    }

    // Header (who, what they are doing, needs) always visible; below it three tabs, each scrolling, so
    // nothing falls off the bottom of the window. Orders come first because they are used most.
    private void BuildRightPanel(Control root)
    {
        var panel = new PanelContainer
        {
            AnchorLeft = 1, AnchorRight = 1, AnchorBottom = 1,
            OffsetLeft = -318, OffsetRight = -8, OffsetTop = 40, OffsetBottom = -8,
        };
        root.AddChild(panel);
        var column = new VBoxContainer();
        panel.AddChild(column);

        _name = new Label();
        _name.AddThemeFontSizeOverride("font_size", 17);
        column.AddChild(_name);
        _doing = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(290, 0) };
        column.AddChild(_doing);

        foreach (var need in new[] { "Hunger", "Thirst", "Fatigue", "Stress" })
        {
            var line = new HBoxContainer();
            line.AddChild(new Label { Text = need, CustomMinimumSize = new Vector2(70, 0) });
            var bar = new ProgressBar
            {
                MinValue = 0, MaxValue = 100, ShowPercentage = false,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 14),
            };
            line.AddChild(bar);
            column.AddChild(line);
            _needBars[need] = bar;
        }

        var tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        column.AddChild(tabs);

        var orders = AddTab(tabs, "Orders");
        _queue = Wrapped();
        orders.AddChild(_queue);
        orders.AddChild(new HSeparator());
        orders.AddChild(new Label
        {
            Text = "Click a task to order it. It starts when current work is done.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(286, 0),
        });
        _urgent = new CheckBox { Text = "Urgent: pause current work and do this first", FocusMode = Control.FocusModeEnum.None };
        orders.AddChild(_urgent);
        _taskList = new VBoxContainer();
        orders.AddChild(_taskList);
        orders.AddChild(new HSeparator());
        var stop = new Button { Text = "Stop current task", Alignment = HorizontalAlignment.Left, FocusMode = Control.FocusModeEnum.None };
        stop.Pressed += () => StopOrdered?.Invoke();
        orders.AddChild(stop);
        var clear = new Button { Text = "Clear queued orders", Alignment = HorizontalAlignment.Left, FocusMode = Control.FocusModeEnum.None };
        clear.Pressed += () => QueueCleared?.Invoke();
        orders.AddChild(clear);

        var duties = AddTab(tabs, "Duties");
        duties.AddChild(new Label
        {
            Text = "When idle with no orders, this crew member takes up these duties in turn. Everyone rests on their own when tired, and your orders always come first.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(286, 0),
        });
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

    private static VBoxContainer AddTab(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer { Name = title, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        tabs.AddChild(scroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(content);
        return content;
    }

    private static Label Wrapped() =>
        new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(286, 0) };

    private void BuildChronicle(Control root)
    {
        var panel = new PanelContainer
        {
            AnchorTop = 1, AnchorBottom = 1, AnchorRight = 1,
            OffsetLeft = 246, OffsetRight = -326, OffsetTop = -112, OffsetBottom = -8,
        };
        root.AddChild(panel);
        var column = new VBoxContainer();
        panel.AddChild(column);
        var header = new HBoxContainer();
        column.AddChild(header);
        header.AddChild(new Label { Text = "Chronicle", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        _chronicle = new RichTextLabel
        {
            ScrollFollowing = true,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SelectionEnabled = true,
        };
        _chronicle.AddThemeFontSizeOverride("normal_font_size", 12);
        column.AddChild(_chronicle);

        // Fold the log down to its title bar to see more of the deck.
        var fold = new Button { Text = "Hide", FocusMode = Control.FocusModeEnum.None };
        fold.Pressed += () =>
        {
            _chronicle.Visible = !_chronicle.Visible;
            panel.OffsetTop = _chronicle.Visible ? -112 : -36;
            fold.Text = _chronicle.Visible ? "Hide" : "Show";
        };
        header.AddChild(fold);
    }
}

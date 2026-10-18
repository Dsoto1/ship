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
    private readonly Dictionary<string, ProgressBar> _needBars = [];

    private Label _time = null!;
    private Label _status = null!;
    private VBoxContainer _crewList = null!;
    private Label _hold = null!;
    private Label _name = null!;
    private Label _details = null!;
    private Label _pack = null!;
    private VBoxContainer _taskList = null!;
    private Label _why = null!;
    private Label _queue = null!;
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

    /// <summary>The player cleared the selected character's waiting orders.</summary>
    public event Action? QueueCleared;

    /// <summary>The player ordered the selected character to stop.</summary>
    public event Action? StopOrdered;

    public override void _Ready()
    {
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        BuildTopBar(root);
        BuildLeftPanel(root);
        BuildRightPanel(root);
        BuildChronicle(root);
    }

    /// <summary>Rebuilds the crew list and task buttons for a newly started or loaded campaign.</summary>
    public void Bind(SimulationRunner runner)
    {
        foreach (var (button, _) in _crewButtons) button.QueueFree();
        foreach (var (button, _) in _taskButtons) button.QueueFree();
        _crewButtons.Clear();
        _taskButtons.Clear();
        _chronicleCount = -1;
        _formatter = new ChronicleFormatter(runner.Campaign, runner.Content);

        foreach (var character in runner.Campaign.Characters.Values.OrderBy(c => c.Id))
        {
            var id = character.Id;
            var button = new Button { Alignment = HorizontalAlignment.Left, ClipText = true };
            button.Pressed += () => CharacterChosen?.Invoke(id);
            _crewList.AddChild(button);
            _crewButtons.Add((button, id));
        }

        foreach (var task in runner.Content.Tasks.Values.OrderBy(t => t.Name))
        {
            var taskId = task.Id;
            var button = new Button { Text = task.Name, Alignment = HorizontalAlignment.Left };
            button.Pressed += () => TaskOrdered?.Invoke(taskId, CurrentPriority);
            _taskList.AddChild(button);
            _taskButtons.Add((button, taskId));
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

        _hold.Text = "Ship's hold\n" + string.Join("\n", campaign.SharedStore.Quantities
            .Select(pair => $"  {runner.Content.Items[pair.Key].Name}: {pair.Value}"));

        RefreshSelected(runner, selected);

        if (campaign.Chronicle.Entries.Count != _chronicleCount)
        {
            _chronicleCount = campaign.Chronicle.Entries.Count;
            _chronicle.Text = string.Join("\n", campaign.Chronicle.Entries.TakeLast(40)
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
            return walking ? $"going to {name.ToLowerInvariant()}" : name;
        }

        return walking ? "walking" : "idle";
    }

    private void RefreshSelected(SimulationRunner runner, CharacterId? selected)
    {
        if (selected is not { } id || !runner.Campaign.Characters.TryGetValue(id, out var character))
        {
            _name.Text = "No one selected";
            _details.Text = "Click a crew member on deck or in the list.";
            _why.Text = "";
            _pack.Text = "";
            return;
        }

        _name.Text = character.Name;
        var skills = character.Skills.Levels.Count == 0
            ? "none"
            : string.Join(", ", character.Skills.Levels.OrderByDescending(p => p.Value)
                .Select(p => $"{runner.Content.Skills[p.Key].Name} {p.Value}"));
        var progress = runner.Campaign.ActiveTasks.TryGetValue(id, out var task)
            ? $" ({task.Progress / runner.Content.Tasks[task.DefinitionId].DurationMinutes:P0})"
            : "";
        var attributes = runner.Content.Attributes.Values
            .Select(a => $"{a.Name} {character.Attributes.Get(a.Id)}");
        _details.Text =
            $"{character.Age}, from {character.Origin}\n" +
            $"{character.SurvivalState} · health {character.Health.Value:0}\n" +
            $"Doing: {Activity(runner, character)}{progress}\n" +
            (character.Appearance is { } looks ? $"Looks: {AppearanceText.Describe(looks, runner.Content.Appearance)}\n" : "") +
            $"Skills: {skills}\n" +
            $"Physique: {string.Join(", ", attributes)}\n" +
            $"Traits: {(character.Traits.Count == 0 ? "none" : string.Join(", ", character.Traits.Select(t => runner.Content.Traits.TryGetValue(t, out var trait) ? trait.Name : t)))}\n" +
            (character.Health.Conditions.Count > 0
                ? $"Conditions: {string.Join(", ", character.Health.Conditions.Select(c => runner.Content.Conditions.TryGetValue(c, out var condition) ? condition.Name : c))}\n"
                : "") +
            Views(runner, id);

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
                var urgent = task.Priority == TaskPriority.Urgent ? " (urgent)" : "";
                var paused = task.Progress > 0 ? $" (paused at {task.Progress / runner.Content.Tasks[task.DefinitionId].DurationMinutes:P0})" : "";
                return $"  {i + 1}. {name}{urgent}{paused}";
            }));

        // Orders stay clickable so the player can see why one is refused; unavailable ones are dimmed.
        foreach (var (button, taskId) in _taskButtons)
        {
            var eligibility = runner.CheckOrder(id, taskId, CurrentPriority);
            button.Modulate = eligibility.Succeeded ? Colors.White : new Color(1, 1, 1, 0.45f);
            button.TooltipText = eligibility.Succeeded ? "" : eligibility.Reason;
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
            var button = new Button { Text = SpeedNames[i], ToggleMode = true, TooltipText = $"Key {i + 1}" };
            button.Pressed += () => SpeedChosen?.Invoke(speed);
            row.AddChild(button);
            _speedButtons.Add((button, speed));
        }

        var save = new Button { Text = "Save (F5)" };
        save.Pressed += () => SaveRequested?.Invoke();
        row.AddChild(save);
        var load = new Button { Text = "Load (F9)" };
        load.Pressed += () => LoadRequested?.Invoke();
        row.AddChild(load);

        _status = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true };
        row.AddChild(_status);
    }

    private void BuildLeftPanel(Control root)
    {
        var panel = new PanelContainer { Position = new Vector2(10, 50), CustomMinimumSize = new Vector2(260, 0) };
        root.AddChild(panel);
        var column = new VBoxContainer();
        panel.AddChild(column);
        column.AddChild(new Label { Text = "Crew" });
        _crewList = new VBoxContainer();
        column.AddChild(_crewList);
        column.AddChild(new HSeparator());
        _hold = new Label();
        column.AddChild(_hold);
    }

    private void BuildRightPanel(Control root)
    {
        var panel = new PanelContainer
        {
            AnchorLeft = 1, AnchorRight = 1, OffsetLeft = -350, OffsetRight = -10, OffsetTop = 50,
        };
        root.AddChild(panel);
        var column = new VBoxContainer();
        panel.AddChild(column);

        _name = new Label();
        _name.AddThemeFontSizeOverride("font_size", 20);
        column.AddChild(_name);
        _details = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(320, 0) };
        column.AddChild(_details);

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

        _why = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(320, 0) };
        _why.AddThemeFontSizeOverride("font_size", 13);
        column.AddChild(_why);
        _pack = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(320, 0) };
        column.AddChild(_pack);
        column.AddChild(new HSeparator());
        _queue = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(320, 0) };
        column.AddChild(_queue);
        column.AddChild(new HSeparator());
        column.AddChild(new Label { Text = "Orders (queued after current work; right-click deck to walk)" });
        _urgent = new CheckBox { Text = "Urgent: pause current work and do this first" };
        column.AddChild(_urgent);
        _taskList = new VBoxContainer();
        column.AddChild(_taskList);
        var stop = new Button { Text = "Stop current task", Alignment = HorizontalAlignment.Left };
        stop.Pressed += () => StopOrdered?.Invoke();
        column.AddChild(stop);
        var clear = new Button { Text = "Clear queued orders", Alignment = HorizontalAlignment.Left };
        clear.Pressed += () => QueueCleared?.Invoke();
        column.AddChild(clear);
    }

    private void BuildChronicle(Control root)
    {
        var panel = new PanelContainer
        {
            AnchorTop = 1, AnchorBottom = 1, AnchorRight = 1,
            OffsetLeft = 280, OffsetRight = -360, OffsetTop = -200, OffsetBottom = -10,
        };
        root.AddChild(panel);
        var column = new VBoxContainer();
        panel.AddChild(column);
        column.AddChild(new Label { Text = "Chronicle" });
        _chronicle = new RichTextLabel
        {
            ScrollFollowing = true,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SelectionEnabled = true,
        };
        column.AddChild(_chronicle);
    }
}

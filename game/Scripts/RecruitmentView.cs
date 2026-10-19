using Expedition.Simulation.History;
using Expedition.Simulation.Recruitment;
using Expedition.Simulation.World;
using Godot;

namespace Expedition.Game;

/// <summary>
/// Hiring at La Bocana (V4, D-019): the people looking for a berth, what the captain knows about each,
/// the inquiries and offers that can be made, the chandler, and the order to sail. It shows only what the
/// captain has learned, never a candidate's hidden truth, and raises events for <see cref="Main"/> to turn
/// into commands.
/// </summary>
public partial class RecruitmentView : CanvasLayer
{
    private Label _header = null!;
    private Label _forecast = null!;
    private VBoxContainer _list = null!;
    private Label _name = null!;
    private Label _who = null!;
    private Label _story = null!;
    private Label _known = null!;
    private HFlowContainer _actions = null!;
    private VBoxContainer _goods = null!;
    private Label _hold = null!;
    private Label _status = null!;
    private Button _sail = null!;
    private string? _selected;

    /// <summary>The player chose a candidate to look at.</summary>
    public event Action<string>? Selected;

    /// <summary>Talk, ask, test, haggle or hire: (action, candidate ID, topic for tests).</summary>
    public event Action<string, string, string?>? ActionRequested;

    /// <summary>The player bought a chandler's lot (index).</summary>
    public event Action<int>? Bought;

    /// <summary>The player gave the order to sail.</summary>
    public event Action? SailRequested;

    public override void _Ready()
    {
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        var panel = new PanelContainer { AnchorRight = 1, AnchorBottom = 1, OffsetLeft = 14, OffsetTop = 14, OffsetRight = -14, OffsetBottom = -14 };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.08f, 0.06f, 0.93f),
            BorderColor = new Color(0.6f, 0.48f, 0.3f), BorderWidthTop = 1, BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 10,
        });
        root.AddChild(panel);
        var page = new VBoxContainer();
        page.AddThemeConstantOverride("separation", 8);
        panel.AddChild(page);

        var title = new Label { Text = "La Bocana — taking on crew for the Garza" };
        title.AddThemeFontSizeOverride("font_size", 22);
        page.AddChild(title);
        _header = new Label();
        page.AddChild(_header);
        _forecast = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        page.AddChild(_forecast);
        page.AddChild(new HSeparator());

        var columns = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 14);
        page.AddChild(columns);

        // Left: who is on the waterfront.
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(300, 0) };
        columns.AddChild(left);
        left.AddChild(new Label { Text = "Looking for a berth" });
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        left.AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(_list);

        // Middle: the selected candidate.
        var middle = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        middle.AddThemeConstantOverride("separation", 6);
        columns.AddChild(middle);
        _name = new Label();
        _name.AddThemeFontSizeOverride("font_size", 19);
        middle.AddChild(_name);
        _who = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1, 1, 1, 0.8f) };
        middle.AddChild(_who);
        _story = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        middle.AddChild(_story);
        _known = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        middle.AddChild(_known);
        _actions = new HFlowContainer();
        _actions.AddThemeConstantOverride("h_separation", 6);
        _actions.AddThemeConstantOverride("v_separation", 6);
        middle.AddChild(_actions);

        // Right: the chandler and the order to sail.
        var right = new VBoxContainer { CustomMinimumSize = new Vector2(280, 0) };
        right.AddThemeConstantOverride("separation", 4);
        columns.AddChild(right);
        right.AddChild(new Label { Text = "The chandler" });
        _goods = new VBoxContainer();
        right.AddChild(_goods);
        _hold = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1, 1, 1, 0.8f) };
        _hold.AddThemeFontSizeOverride("font_size", 12);
        right.AddChild(_hold);
        right.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        _sail = new Button { Text = "Set sail with the convoy", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 44) };
        _sail.AddThemeFontSizeOverride("font_size", 17);
        _sail.Pressed += () => SailRequested?.Invoke();
        right.AddChild(_sail);

        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1f, 0.85f, 0.55f) };
        page.AddChild(_status);
    }

    /// <summary>Shows a message, such as why an offer was refused.</summary>
    public void ShowStatus(string text) => _status.Text = text;

    /// <summary>Redraws from the simulation.</summary>
    public void Refresh(SimulationRunner runner, string? selected)
    {
        if (runner.Campaign.Recruitment is not { } state || runner.Content.Recruitment is not { } rules)
        {
            return;
        }

        _selected = selected;
        var (food, water) = runner.SupplyForecast();
        _header.Text = $"Funds: {state.Funds} sueldos   ·   Inquiries before the convoy sails: {state.InquiriesLeft}   ·   " +
                       $"Signed on: {state.Hired.Count} of {rules.Berths} berths (at least {rules.MinimumCrew} to sail)";
        _forecast.Text = $"With {runner.Campaign.Characters.Count} aboard, the hold has water for about {water:0} days and food for about {food:0}. " +
                         "The crossing to the Ember Coast takes about three weeks, longer in bad weather or with few hands.";
        _forecast.Modulate = Math.Min(food, water) < 26 ? new Color(1f, 0.7f, 0.45f) : new Color(0.85f, 0.95f, 0.8f);

        foreach (var child in _list.GetChildren()) child.QueueFree();
        foreach (var candidate in rules.Candidates)
        {
            var hired = state.Hired.ContainsKey(candidate.Id);
            var gone = state.Gone.Contains(candidate.Id);
            var price = runner.AskingPrice(candidate);
            var cost = price < 0 ? $"pays {-price}" : $"{price} s";
            var button = new Button
            {
                Text = $"{(hired ? "✓ " : "")}{candidate.Name} — {StatusName(runner, candidate.Status)}, {(gone ? "gone" : cost)}",
                Alignment = HorizontalAlignment.Left,
                FocusMode = Control.FocusModeEnum.None,
                ClipText = true,
                Disabled = gone,
                Modulate = candidate.Id == selected ? new Color(1f, 0.92f, 0.65f) : hired ? new Color(0.75f, 1f, 0.75f) : Colors.White,
            };
            var id = candidate.Id;
            button.Pressed += () => Selected?.Invoke(id);
            _list.AddChild(button);
        }

        ShowCandidate(runner, state, rules.Candidates.FirstOrDefault(c => c.Id == selected));

        foreach (var child in _goods.GetChildren()) child.QueueFree();
        for (var i = 0; i < rules.Goods.Count; i++)
        {
            var lot = rules.Goods[i];
            var index = i;
            var button = new Button
            {
                Text = $"{lot.Quantity} × {runner.Content.Items[lot.Item].Name} — {lot.Price} s",
                Alignment = HorizontalAlignment.Left,
                FocusMode = Control.FocusModeEnum.None,
                Disabled = state.Funds < lot.Price,
            };
            button.Pressed += () => Bought?.Invoke(index);
            _goods.AddChild(button);
        }

        _hold.Text = "In the hold: " + string.Join(" · ", runner.Campaign.SharedStore.Quantities.Select(q => $"{runner.Content.Items[q.Key].Name} {q.Value}"));
        _sail.Disabled = state.Hired.Count < rules.MinimumCrew;
        _sail.Text = _sail.Disabled ? $"Set sail (need {rules.MinimumCrew - state.Hired.Count} more hands)" : "Set sail with the convoy";
    }

    private void ShowCandidate(SimulationRunner runner, RecruitmentState state, CandidateDefinition? candidate)
    {
        foreach (var child in _actions.GetChildren()) child.QueueFree();
        if (candidate is null)
        {
            _name.Text = "Choose someone on the left";
            _who.Text = "Talk to them to hear what they say for themselves. Asking around and putting someone to the test take time, " +
                        "and there are only a few days before the convoy sails. What people claim is not always true.";
            _story.Text = _known.Text = "";
            return;
        }

        var sex = candidate.Sex.ToString().ToLowerInvariant();
        _name.Text = candidate.Name;
        _who.Text = $"{sex}, {candidate.Age}, {StatusName(runner, candidate.Status)}, from {candidate.Origin}. {candidate.Look}";
        var parts = new List<string>();
        if (state.TalkedTo.Contains(candidate.Id)) parts.Add(candidate.Pitch);
        if (state.AskedAbout.Contains(candidate.Id)) parts.Add($"{Capital(candidate.Reference.Who)}: \"{candidate.Reference.Text}\"");
        if (state.RefusedTest.Contains(candidate.Id)) parts.Add($"{candidate.Name} refused to be tested.");
        _story.Text = string.Join("\n\n", parts);
        var known = CandidateText.Describe(state, candidate.Id, runner.Content);
        _known.Text = known.Count == 0 ? "You know nothing of their skills yet." : "What you know: " + string.Join(", ", known);

        var hired = state.Hired.ContainsKey(candidate.Id);
        if (hired)
        {
            _actions.AddChild(new Label { Text = "Signed on. They are aboard." });
            return;
        }

        void Add(string text, string action, string? topic = null, bool enabled = true)
        {
            var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, Disabled = !enabled };
            button.Pressed += () => ActionRequested?.Invoke(action, candidate.Id, topic);
            _actions.AddChild(button);
        }

        Add("Talk", "talk", enabled: !state.TalkedTo.Contains(candidate.Id));
        Add("Ask around (1 inquiry)", "ask", enabled: !state.AskedAbout.Contains(candidate.Id) && state.InquiriesLeft > 0);
        var topics = state.Facts.Where(f => f.Candidate == candidate.Id).Select(f => f.Topic)
            .Where(t => runner.Content.Skills.ContainsKey(t) || runner.Content.Languages.ContainsKey(t)).Distinct().Take(4);
        foreach (var topic in topics)
        {
            var name = runner.Content.Skills.TryGetValue(topic, out var skill) ? skill.Name : runner.Content.Languages[topic].Name;
            var tested = state.Facts.Any(f => f.Candidate == candidate.Id && f.Topic == topic && f.Source == Expedition.Simulation.Knowledge.KnowledgeSource.Test);
            Add($"Test {name} (1 inquiry)", "test", topic, !tested && state.InquiriesLeft > 0 && !state.RefusedTest.Contains(candidate.Id));
        }

        var price = runner.AskingPrice(candidate);
        Add("Haggle over the advance", "haggle", enabled: !state.Haggled.Contains(candidate.Id) && price > 0);
        Add(price < 0 ? $"Take aboard (pays {-price} s)" : $"Hire for {price} sueldos", "hire", enabled: price <= state.Funds);
    }

    private static string StatusName(SimulationRunner runner, string status) =>
        runner.Content.Statuses.TryGetValue(status, out var definition) ? definition.Name.ToLowerInvariant() : status;

    private static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}

using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.Knowledge;
using Expedition.Simulation.World;
using Godot;

namespace Expedition.Game;

/// <summary>
/// The port's working papers (R2a): the departure bar (deadline, purse, berths, stores), the card for
/// whoever the captain is looking at (what is known of them, from whom, how sure), what was said, and the
/// chandler's list. It shows only what the captain knows (PORT_EXPERIENCE §10) and turns clicks into
/// requests that <see cref="Main"/> carries out, walking the captain over first.
/// </summary>
public partial class PortHud : CanvasLayer
{
    private Control _root = null!;
    private Label _deadline = null!;
    private Label _purse = null!;
    private Label _berths = null!;
    private Label _stores = null!;
    private PanelContainer _card = null!;
    private Label _cardTitle = null!;
    private Label _cardSubtitle = null!;
    private Label _cardLook = null!;
    private Label _cardDoing = null!;
    private Label _cardSeeking = null!;
    private VBoxContainer _cardKnown = null!;
    private OptionButton _askAbout = null!;
    private OptionButton _demonstrate = null!;
    private SpinBox _offer = null!;
    private Button _offerButton = null!;
    private Button _demonstrateButton = null!;
    private Label _pending = null!;
    private PanelContainer _dialogue = null!;
    private Label _dialogueText = null!;
    private PanelContainer _chandler = null!;
    private PanelContainer _journal = null!;
    private VBoxContainer _journalList = null!;
    private int _journalCount = -1;
    private Label _cardLeads = null!;
    private VBoxContainer _lots = null!;
    private Label _hold = null!;
    private readonly List<CharacterId> _askChoices = [];
    private readonly List<string> _demoChoices = [];
    private CharacterId? _shown;
    private string _knownSignature = "";

    /// <summary>Talk with someone.</summary>
    public event Action<CharacterId>? TalkRequested;

    /// <summary>Ask someone about another person.</summary>
    public event Action<CharacterId, CharacterId>? AskRequested;

    /// <summary>Ask someone to show a skill.</summary>
    public event Action<CharacterId, string>? DemonstrationRequested;

    /// <summary>Offer someone a berth for an advance.</summary>
    public event Action<CharacterId, int>? OfferRequested;

    /// <summary>Buy a lot from the chandler.</summary>
    public event Action<int>? BuyRequested;

    /// <summary>Walk to the Chamber's board and read it.</summary>
    public event Action? NoticesRequested;

    /// <summary>Sail now.</summary>
    public event Action? SetSailRequested;

    /// <summary>The card was closed.</summary>
    public event Action? CardClosed;

    public override void _Ready()
    {
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = Hud.BuildTheme() };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);
        BuildBar();
        BuildCard();
        BuildDialogue();
        BuildChandler();
        BuildJournal();
    }

    /// <summary>The name the captain knows someone by, or how they look to a stranger ("a docker").</summary>
    public static string KnownName(SimulationRunner runner, CharacterId id)
    {
        var campaign = runner.Campaign;
        var character = campaign.Characters[id];
        if (character.Membership == Membership.Crew || campaign.Port is not { } port || port.TalkedTo.Contains(id)
            || campaign.Knowledge.About(id).Any())
        {
            return character.Name;
        }

        var occupation = runner.PersonOf(id)?.Occupation ?? "stranger";
        var article = "aeiou".Contains(char.ToLowerInvariant(occupation[0])) ? "An" : "A";
        return $"{article} {occupation.Split(',')[0]}";
    }

    /// <summary>Opens the journal of leads.</summary>
    public void ShowJournal() => _journal.Visible = true;

    /// <summary>Shows what someone said, or why something could not be done.</summary>
    public void Say(string text)
    {
        _dialogueText.Text = text;
        _dialogue.Visible = !string.IsNullOrEmpty(text);
    }

    /// <summary>Shows what the captain is on the way to do, or nothing.</summary>
    public void ShowPending(string? text)
    {
        _pending.Text = text ?? "";
        _pending.Visible = text is not null;
    }

    /// <summary>Refreshes from the simulation each frame; <paramref name="shown"/> is the person on the card.</summary>
    public void Refresh(SimulationRunner runner, CharacterId? shown)
    {
        var campaign = runner.Campaign;
        Visible = campaign.Port is not null;
        if (campaign.Port is not { } port || runner.Content.Port is not { } rules)
        {
            return;
        }

        var hoursLeft = (rules.Departure.Tick - campaign.Clock.Tick) / (double)SimulationClock.TicksPerHour;
        _deadline.Text = hoursLeft > 0
            ? $"The convoy sails at dawn, day {rules.Departure.Day} — in {Math.Floor(hoursLeft):0} h {(hoursLeft - Math.Floor(hoursLeft)) * 60:00} m"
            : $"The convoy has sailed. Every half day in port costs {rules.LateFeePerHalfDay} sueldos.";
        _deadline.Modulate = hoursLeft > 12 ? Colors.White : hoursLeft > 0 ? new Color(1f, 0.8f, 0.4f) : new Color(1f, 0.5f, 0.4f);
        _purse.Text = $"Purse: {port.Funds} sueldos";
        _berths.Text = $"Signed on: {port.Hired.Count} of {rules.Berths} berths (need {rules.MinimumCrew} to sail)";
        _berths.Modulate = port.Hired.Count >= rules.MinimumCrew ? new Color(0.8f, 1f, 0.8f) : Colors.White;
        var (food, water) = runner.SupplyForecast();
        _stores.Text = $"Stores for {port.Hired.Count + 1}: water ~{water:0} days, food ~{food:0} days (the crossing takes about 25)";
        _stores.Modulate = Math.Min(food, water) < 25 ? new Color(1f, 0.8f, 0.4f) : new Color(0.8f, 1f, 0.8f);

        RefreshCard(runner, shown);
        if (_chandler.Visible)
        {
            RefreshChandler(runner);
        }

        if (_journal.Visible)
        {
            RefreshJournal(runner);
        }
    }

    private void RefreshCard(SimulationRunner runner, CharacterId? shown)
    {
        var campaign = runner.Campaign;
        if (shown is not { } id || !campaign.Characters.TryGetValue(id, out var character) || character.Membership != Membership.Local)
        {
            _card.Visible = false;
            _shown = null;
            return;
        }

        _card.Visible = true;
        var person = runner.PersonOf(id);
        var name = KnownName(runner, id);
        _cardTitle.Text = name;
        _cardSubtitle.Text = name == character.Name && person is not null ? Capital(person.Occupation) : "You have not spoken.";
        _cardLook.Text = person?.Look ?? "";
        var indoors = runner.IsIndoors(id);
        _cardDoing.Text = indoors ? "Indoors, out of sight." : $"Now: {Hud.Activity(runner, character)}.";

        var port = campaign.Port!;
        var talked = port.TalkedTo.Contains(id);
        _cardSeeking.Text = port.Gone.Contains(id) ? "Gone from the port."
            : person?.Seeking is { } seeking && talked
                ? seeking.Asks < 0 ? $"Wants passage, and offers to pay {-seeking.Asks} sueldos." : $"Looking for a berth. Asks an advance of {seeking.Asks} sueldos."
            : talked ? "Not looking for a berth."
            : "Talk to them to learn whether they want work.";
        _cardSeeking.Modulate = person?.Seeking is not null && talked ? new Color(1f, 0.9f, 0.55f) : new Color(1, 1, 1, 0.75f);

        // What you have heard that may point to them: the face is new, but the description fits.
        var leads = runner.LeadsAbout(id).ToList();
        _cardLeads.Visible = leads.Count > 0;
        _cardLeads.Text = string.Join("\n", leads.Select(l =>
            $"{(talked ? "You heard" : "Could this be the one you heard of?")} \"{l.Text}\" — {(runner.LeadGiver(l.Id) is { } giver ? KnownName(runner, giver) : "the Chamber's board")}"));

        // What the captain believes, rebuilt only when it changes.
        var lines = KnowledgeText.Lines(campaign.Knowledge, id, runner.Content, campaign.Characters);
        var signature = id + string.Join("|", lines.Select(l => $"{l.Topic}{l.Value}{l.Source}{l.Confidence}"));
        if (signature != _knownSignature || _shown != id)
        {
            _knownSignature = signature;
            foreach (var child in _cardKnown.GetChildren())
            {
                child.QueueFree();
            }

            if (lines.Count == 0)
            {
                _cardKnown.AddChild(Small("Nothing yet. Watch them work, talk with them, or ask people who know them.", 0.75f));
            }

            foreach (var line in lines)
            {
                var text = line.Name == "Seen"
                    ? $"Seen: {person?.Secrets.FirstOrDefault(s => s.Id == line.Value)?.Text ?? line.Value}"
                    : $"{line.Name}: {line.Value}";
                var row = new VBoxContainer();
                row.AddThemeConstantOverride("separation", 0);
                row.AddChild(new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(300, 0) });
                var source = Small($"   {line.Source} — {line.Confidence}", 0.7f);
                source.Modulate = ConfidenceColour(line.Confidence);
                row.AddChild(source);
                _cardKnown.AddChild(row);
            }
        }

        if (_shown != id)
        {
            _shown = id;
            FillChoices(runner, id);
        }

        var seekingNow = person?.Seeking is not null && talked && !port.Hired.Contains(id) && !port.Gone.Contains(id);
        _offer.Editable = seekingNow;
        _offerButton.Disabled = !seekingNow;
        _demonstrateButton.Disabled = !seekingNow || _demoChoices.Count == 0;
        if (seekingNow && _offer.Value == 0 && person!.Seeking!.Asks > 0)
        {
            _offer.Value = person.Seeking.Asks;
        }
    }

    private static Color ConfidenceColour(string confidence) => confidence switch
    {
        "Certain" or "Sure" => new Color(0.75f, 1f, 0.75f),
        "Fairly sure" => new Color(0.9f, 0.95f, 0.75f),
        "Disputed" => new Color(1f, 0.6f, 0.5f),
        _ => new Color(1f, 0.85f, 0.6f),
    };

    // Whom to ask about (people the captain knows by name), and what to ask them to show (what they claim).
    private void FillChoices(SimulationRunner runner, CharacterId id)
    {
        _askAbout.Clear();
        _askChoices.Clear();
        foreach (var other in runner.Campaign.Characters.Values.Where(c => c.Id != id && c.Id != runner.Campaign.CaptainId && c.Membership == Membership.Local)
                     .Where(c => KnownName(runner, c.Id) == c.Name).OrderBy(c => c.Name))
        {
            _askAbout.AddItem(other.Name);
            _askChoices.Add(other.Id);
        }

        if (_askChoices.Count == 0)
        {
            _askAbout.AddItem("(no one you know by name yet)");
            _askAbout.Disabled = true;
        }
        else
        {
            _askAbout.Disabled = false;
        }

        _demonstrate.Clear();
        _demoChoices.Clear();
        foreach (var skill in runner.PersonOf(id)?.Seeking?.Claims.Keys.Where(runner.Content.Skills.ContainsKey).Order() ?? Enumerable.Empty<string>())
        {
            _demonstrate.AddItem(runner.Content.Skills[skill].Name);
            _demoChoices.Add(skill);
        }

        _offer.Value = 0;
    }

    private void RefreshChandler(SimulationRunner runner)
    {
        var rules = runner.Content.Port!;
        var port = runner.Campaign.Port!;
        var buttons = _lots.GetChildren().OfType<Button>().ToList();
        for (var lot = 0; lot < rules.Goods.Count && lot < buttons.Count; lot++)
        {
            var goods = rules.Goods[lot];
            var item = runner.Content.Items[goods.Item];
            var left = port.Stock.GetValueOrDefault(lot);
            buttons[lot].Text = $"{goods.Quantity} {item.Name.ToLowerInvariant()} — {goods.Price} sueldos   ({left} left, ~{goods.SoldPerDay} sold to others a day)";
            buttons[lot].Disabled = left <= 0 || port.Funds < goods.Price;
        }

        _hold.Text = "In the Garza's hold: " + string.Join(", ", runner.Campaign.SharedStore.Quantities.Select(p => $"{runner.Content.Items[p.Key].Name} {p.Value}"));
    }

    // ---- building

    private void BuildBar()
    {
        var panel = new PanelContainer { AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -330, OffsetRight = 330, OffsetTop = 40 };
        _root.AddChild(panel);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 2);
        panel.AddChild(column);
        _deadline = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _deadline.AddThemeFontSizeOverride("font_size", 15);
        column.AddChild(_deadline);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 16);
        column.AddChild(row);
        _purse = new Label();
        row.AddChild(_purse);
        _berths = new Label();
        row.AddChild(_berths);
        _stores = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        column.AddChild(_stores);
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 10);
        column.AddChild(buttons);
        var notices = new Button { Text = "Read the notices", FocusMode = Control.FocusModeEnum.None, TooltipText = "Walk to the Chamber's board on the terrace: berths wanted, passages, pardons" };
        notices.Pressed += () => NoticesRequested?.Invoke();
        buttons.AddChild(notices);
        var journal = new Button { Text = "Journal", FocusMode = Control.FocusModeEnum.None, TooltipText = "People worth finding: what you heard, from whom, and where they are said to be" };
        journal.Pressed += () => _journal.Visible = !_journal.Visible;
        buttons.AddChild(journal);
        var chandler = new Button { Text = "Chandler's list", FocusMode = Control.FocusModeEnum.None, TooltipText = "Maese Corbalán's stores, delivered to the Garza" };
        chandler.Pressed += () => _chandler.Visible = !_chandler.Visible;
        buttons.AddChild(chandler);
        var sail = new Button { Text = "Set sail", FocusMode = Control.FocusModeEnum.None, TooltipText = "Go aboard with the crew you have signed and sail" };
        sail.Pressed += () => SetSailRequested?.Invoke();
        buttons.AddChild(sail);
    }

    private void BuildCard()
    {
        _card = new PanelContainer
        {
            AnchorLeft = 1, AnchorRight = 1, AnchorBottom = 1,
            OffsetLeft = -348, OffsetRight = -8, OffsetTop = 40, OffsetBottom = -120,
            Visible = false,
        };
        _card.AddThemeStyleboxOverride("panel", Paper());
        _root.AddChild(_card);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _card.AddChild(scroll);
        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(column);

        var header = new HBoxContainer();
        column.AddChild(header);
        _cardTitle = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _cardTitle.AddThemeFontSizeOverride("font_size", 19);
        header.AddChild(_cardTitle);
        var close = new Button { Text = "×", FocusMode = Control.FocusModeEnum.None, Flat = true };
        close.Pressed += () => CardClosed?.Invoke();
        header.AddChild(close);
        _cardSubtitle = Small("", 0.85f);
        column.AddChild(_cardSubtitle);
        _cardLook = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(310, 0) };
        _cardLook.AddThemeFontSizeOverride("font_size", 12);
        column.AddChild(_cardLook);
        _cardDoing = Small("", 0.85f);
        column.AddChild(_cardDoing);
        _cardSeeking = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(310, 0) };
        column.AddChild(_cardSeeking);
        _cardLeads = Small("", 0.9f);
        _cardLeads.Modulate = new Color(0.85f, 0.9f, 1f);
        column.AddChild(_cardLeads);
        column.AddChild(new HSeparator());
        column.AddChild(new Label { Text = "What you know" });
        _cardKnown = new VBoxContainer();
        _cardKnown.AddThemeConstantOverride("separation", 3);
        column.AddChild(_cardKnown);
        column.AddChild(new HSeparator());

        _pending = Small("", 0.9f);
        _pending.Modulate = new Color(1f, 0.9f, 0.6f);
        column.AddChild(_pending);
        var talk = new Button { Text = "Talk", FocusMode = Control.FocusModeEnum.None, TooltipText = "Walk over and talk: they will say what they do, and whether they want work" };
        talk.Pressed += () => { if (_shown is { } id) TalkRequested?.Invoke(id); };
        column.AddChild(talk);

        var ask = new HBoxContainer();
        column.AddChild(ask);
        var askButton = new Button { Text = "Ask about", FocusMode = Control.FocusModeEnum.None, TooltipText = "What does this person know of someone else? Those who work with someone know them best" };
        askButton.Pressed += () =>
        {
            if (_shown is { } id && _askAbout.Selected >= 0 && _askAbout.Selected < _askChoices.Count)
            {
                AskRequested?.Invoke(id, _askChoices[_askAbout.Selected]);
            }
        };
        ask.AddChild(askButton);
        _askAbout = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, FocusMode = Control.FocusModeEnum.None, ClipText = true };
        ask.AddChild(_askAbout);

        var demo = new HBoxContainer();
        column.AddChild(demo);
        _demonstrateButton = new Button { Text = "Ask to show", FocusMode = Control.FocusModeEnum.None, TooltipText = "Ask them to show what they claim. Watch them finish: it counts as a test. The proud take offence" };
        _demonstrateButton.Pressed += () =>
        {
            if (_shown is { } id && _demonstrate.Selected >= 0 && _demonstrate.Selected < _demoChoices.Count)
            {
                DemonstrationRequested?.Invoke(id, _demoChoices[_demonstrate.Selected]);
            }
        };
        demo.AddChild(_demonstrateButton);
        _demonstrate = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, FocusMode = Control.FocusModeEnum.None };
        demo.AddChild(_demonstrate);

        var offer = new HBoxContainer();
        column.AddChild(offer);
        _offerButton = new Button { Text = "Offer a berth", FocusMode = Control.FocusModeEnum.None, TooltipText = "Offer an advance on wages. Too little insults; some will not sail with certain others, or under a captain they distrust" };
        _offerButton.Pressed += () => { if (_shown is { } id) OfferRequested?.Invoke(id, (int)_offer.Value); };
        offer.AddChild(_offerButton);
        _offer = new SpinBox { MinValue = -2000, MaxValue = 5000, Step = 50, Suffix = "sueldos", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        offer.AddChild(_offer);
    }

    private void BuildDialogue()
    {
        _dialogue = new PanelContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 1, AnchorBottom = 1,
            OffsetLeft = -330, OffsetRight = 330, OffsetTop = -230, OffsetBottom = -124,
            Visible = false,
        };
        _dialogue.AddThemeStyleboxOverride("panel", Paper());
        _root.AddChild(_dialogue);
        var row = new HBoxContainer();
        _dialogue.AddChild(row);
        _dialogueText = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
        _dialogueText.AddThemeFontSizeOverride("font_size", 15);
        row.AddChild(_dialogueText);
        var close = new Button { Text = "×", Flat = true, FocusMode = Control.FocusModeEnum.None };
        close.Pressed += () => _dialogue.Visible = false;
        row.AddChild(close);
    }

    private void BuildChandler()
    {
        _chandler = new PanelContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -330, OffsetRight = 330, OffsetTop = 150,
            Visible = false,
        };
        _chandler.AddThemeStyleboxOverride("panel", Paper());
        _root.AddChild(_chandler);
        var column = new VBoxContainer();
        _chandler.AddChild(column);
        var header = new HBoxContainer();
        column.AddChild(header);
        header.AddChild(new Label { Text = "Maese Corbalán, chandler — delivered aboard", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var close = new Button { Text = "×", Flat = true, FocusMode = Control.FocusModeEnum.None };
        close.Pressed += () => _chandler.Visible = false;
        header.AddChild(close);
        _lots = new VBoxContainer();
        column.AddChild(_lots);
        for (var lot = 0; lot < 16; lot++)
        {
            var index = lot;
            var button = new Button { Alignment = HorizontalAlignment.Left, FocusMode = Control.FocusModeEnum.None };
            button.Pressed += () => BuyRequested?.Invoke(index);
            _lots.AddChild(button);
        }

        _hold = Small("", 0.85f);
        _hold.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _hold.CustomMinimumSize = new Vector2(640, 0);
        column.AddChild(_hold);
    }

    private void BuildJournal()
    {
        _journal = new PanelContainer
        {
            AnchorTop = 1, AnchorBottom = 1, OffsetLeft = 8, OffsetRight = 380, OffsetTop = -560, OffsetBottom = -250,
            Visible = false,
        };
        _journal.AddThemeStyleboxOverride("panel", Paper());
        _root.AddChild(_journal);
        var column = new VBoxContainer();
        _journal.AddChild(column);
        var header = new HBoxContainer();
        column.AddChild(header);
        header.AddChild(new Label { Text = "Journal — people worth finding", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var close = new Button { Text = "×", Flat = true, FocusMode = Control.FocusModeEnum.None };
        close.Pressed += () => _journal.Visible = false;
        header.AddChild(close);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        column.AddChild(scroll);
        _journalList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _journalList.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_journalList);
    }

    private void RefreshJournal(SimulationRunner runner)
    {
        var port = runner.Campaign.Port!;
        var signature = port.Leads.Count * 1000 + port.TalkedTo.Count * 31 + port.Hired.Count * 7 + port.Gone.Count;
        if (signature == _journalCount)
        {
            return;
        }

        _journalCount = signature;
        foreach (var child in _journalList.GetChildren())
        {
            child.QueueFree();
        }

        if (port.Leads.Count == 0)
        {
            _journalList.AddChild(Small("Nothing yet. Talk to people who see a lot of the waterfront (a landlady, a chandler, a priest, a guard), or read the Chamber's notices.", 0.8f));
            return;
        }

        var leads = runner.Content.Port!.AllLeads().ToDictionary(l => l.Lead.Id, l => l.Lead);
        foreach (var heard in port.Leads.AsEnumerable().Reverse())
        {
            var lead = leads[heard.LeadId];
            var about = runner.CharacterFor(lead.About);
            var status = about is null ? "" : port.Hired.Contains(about.Id) ? "Signed on."
                : port.Gone.Contains(about.Id) ? "Gone from the port."
                : port.TalkedTo.Contains(about.Id) ? $"Found: you have spoken with {about.Name}."
                : "Not yet found.";
            var giver = runner.LeadGiver(lead.Id) is { } from ? KnownName(runner, from) : "the Chamber's board";
            var entry = new VBoxContainer();
            entry.AddThemeConstantOverride("separation", 1);
            entry.AddChild(new Label { Text = $"\"{lead.Text}\"", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(340, 0) });
            entry.AddChild(Small($"   Said to be: {lead.Where}.", 0.85f));
            var line = Small($"   From {giver}, {new SimulationClock(heard.Tick)}. {status}", 0.7f);
            line.Modulate = status.StartsWith("Not yet") ? new Color(1f, 0.9f, 0.6f) : new Color(0.75f, 1f, 0.75f);
            entry.AddChild(line);
            _journalList.AddChild(entry);
        }
    }

    /// <summary>Hides chandler buttons beyond the number of lots on sale.</summary>
    public void Bind(SimulationRunner runner)
    {
        var count = runner.Content.Port?.Goods.Count ?? 0;
        var buttons = _lots.GetChildren().OfType<Button>().ToList();
        for (var i = 0; i < buttons.Count; i++)
        {
            buttons[i].Visible = i < count;
        }

        _shown = null;
        _chandler.Visible = false;
        _journal.Visible = false;
        _journalCount = -1;
        Say("");
    }

    // Warm paper for the captain's papers, distinct from the dark working panels (ART_BIBLE §16).
    private static StyleBoxFlat Paper() => new()
    {
        BgColor = new Color(0.16f, 0.13f, 0.09f, 0.9f),
        BorderColor = new Color(0.62f, 0.5f, 0.3f, 0.9f),
        BorderWidthTop = 1, BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
        CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
        ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 10, ContentMarginBottom = 10,
    };

    private static Label Small(string text, float alpha)
    {
        var label = new Label { Text = text, Modulate = new Color(1, 1, 1, alpha), AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(300, 0) };
        label.AddThemeFontSizeOverride("font_size", 12);
        return label;
    }

    private static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}

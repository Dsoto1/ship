using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.World;
using Godot;

namespace Expedition.Game;

/// <summary>
/// The port's contextual and management interface, inside the shared <see cref="Hud"/> shell
/// (UI_UX_DESIGN_SYSTEM.md §13, §19–25, §40):
/// <list type="bullet">
/// <item>the card for whoever the captain is looking at: who they are, whether they want work, and what
/// is known of them, with its source and confidence;</item>
/// <item>a small conversation strip for what was said;</item>
/// <item>in Expedition: objectives, the departure, the journal of leads, the notices and Set sail;</item>
/// <item>in Cargo: the chandler's list.</item>
/// </list>
/// It shows only what the captain knows (PORT_EXPERIENCE §10) and turns clicks into requests that
/// <see cref="Main"/> carries out, walking the captain over first.
/// </summary>
public partial class PortHud : CanvasLayer
{
    private Control _root = null!;
    private PanelContainer _card = null!;
    private Label _cardTitle = null!;
    private Label _cardSubtitle = null!;
    private Label _cardLook = null!;
    private Label _cardDoing = null!;
    private Label _cardSeeking = null!;
    private Label _cardLeads = null!;
    private VBoxContainer _cardKnown = null!;
    private OptionButton _askAbout = null!;
    private OptionButton _demonstrate = null!;
    private SpinBox _offer = null!;
    private Button _offerButton = null!;
    private HBoxContainer _claimRow = null!;
    private Label _claimText = null!;
    private Button _demonstrateButton = null!;
    private Label _pending = null!;
    private PanelContainer _dialogue = null!;
    private Label _dialogueText = null!;
    private VBoxContainer _lots = null!;
    private VBoxContainer _expedition = null!;
    private Label _objectives = null!;
    private VBoxContainer _journalList = null!;
    private ConfirmationDialog _confirmSail = null!;
    private readonly List<CharacterId> _askChoices = [];
    private readonly List<string> _demoChoices = [];
    private CharacterId? _shown;
    private string _knownSignature = "";
    private int _journalSignature = -1;
    private Hud? _hud;
    private SimulationRunner? _runner;

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

    /// <summary>Sail now (after any confirmation).</summary>
    public event Action? SetSailRequested;

    /// <summary>The card was closed.</summary>
    public event Action? CardClosed;

    /// <summary>The captain answers a creditor's claim: the creditor, and true to pay.</summary>
    public event Action<CharacterId, bool>? ClaimAnswered;

    public override void _Ready()
    {
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = Ui.BuildTheme() };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);
        BuildCard();
        BuildDialogue();
        _confirmSail = new ConfirmationDialog { Title = "Set sail?", OkButtonText = "Sail", CancelButtonText = "Not yet" };
        _confirmSail.Confirmed += () => SetSailRequested?.Invoke();
        AddChild(_confirmSail);
    }

    /// <summary>Puts the port's sections into the shell's Expedition and Cargo screens.</summary>
    public void Attach(Hud hud)
    {
        _hud = hud;
        BuildExpedition(hud.PanelBody(PanelId.Expedition));
        BuildChandler(hud.PanelBody(PanelId.Cargo));
    }

    /// <summary>The name the captain knows someone by, or how they look to a stranger ("A docker").</summary>
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

    /// <summary>Shows what someone said, or why something could not be done.</summary>
    public void Say(string text)
    {
        _dialogueText.Text = text;
        if (string.IsNullOrEmpty(text))
        {
            _dialogue.Visible = false;
        }
        else
        {
            Ui.FadeIn(_dialogue);
        }
    }

    /// <summary>Shows what the captain is on the way to do, or nothing.</summary>
    public void ShowPending(string? text)
    {
        _pending.Text = text ?? "";
        _pending.Visible = text is not null;
    }

    /// <summary>Opens the journal (Expedition).</summary>
    public void ShowJournal() => _hud?.OpenPanel(PanelId.Expedition);

    /// <summary>Refreshes from the simulation each frame; <paramref name="shown"/> is the person on the card.</summary>
    public void Refresh(SimulationRunner runner, CharacterId? shown)
    {
        _runner = runner;
        var inPort = runner.Campaign.Port is not null && runner.Content.Port is not null;
        Visible = inPort;
        foreach (var section in new Control[] { _expedition, _lots.GetParent<Control>() })
        {
            section.Visible = inPort;
        }

        if (!inPort)
        {
            return;
        }

        RefreshCard(runner, shown);
        if (_hud?.PanelBody(PanelId.Expedition).Visible == true && _hud.PanelOpen)
        {
            RefreshExpedition(runner);
        }

        if (_hud?.PanelBody(PanelId.Cargo).Visible == true && _hud.PanelOpen)
        {
            RefreshChandler(runner);
        }
    }

    // ================================================================ the person card (contextual)

    private void RefreshCard(SimulationRunner runner, CharacterId? shown)
    {
        var campaign = runner.Campaign;
        if (shown is not { } id || !campaign.Characters.TryGetValue(id, out var character) || character.Membership != Membership.Local)
        {
            _card.Visible = false;
            _shown = null;
            return;
        }

        if (!_card.Visible)
        {
            Ui.FadeIn(_card);
        }

        var person = runner.PersonOf(id);
        var name = KnownName(runner, id);
        _cardTitle.Text = name;
        _cardSubtitle.Text = name == character.Name && person is not null ? Capital(person.Occupation) : "You have not spoken.";
        _cardLook.Text = person?.Look ?? "";
        _cardDoing.Text = runner.IsIndoors(id) ? "Indoors, out of sight." : $"Now: {Hud.Activity(runner, character)}.";

        var port = campaign.Port!;
        var talked = port.TalkedTo.Contains(id);
        _cardSeeking.Text = port.Gone.Contains(id) ? "Gone from the port."
            : port.Hired.Contains(id) ? "Signed on."
            : person?.Seeking is { } seeking && talked
                ? seeking.Asks < 0 ? $"Wants passage. Offers to pay {-seeking.Asks} sueldos." : $"Wants a berth. Asks an advance of {seeking.Asks} sueldos."
            : talked ? "Not looking for a berth."
            : "Talk to them to learn whether they want work.";
        _cardSeeking.AddThemeColorOverride("font_color", person?.Seeking is not null && talked ? Ui.Brass : Ui.InkSoft);

        // What you have heard that may point to them: the face is new, but the description fits.
        var leads = runner.LeadsAbout(id).ToList();
        _cardLeads.Visible = leads.Count > 0;
        _cardLeads.Text = string.Join("\n", leads.Select(l =>
            $"{Ui.IconLead} {(talked ? "You heard:" : "Could this be the one you heard of?")} \"{l.Text}\" — {(runner.LeadGiver(l.Id) is { } giver ? KnownName(runner, giver) : "the Chamber's board")}"));

        // What the captain believes, in the shared vocabulary (UI_UX §21–23); rebuilt only when it changes.
        var lines = KnowledgeText.Lines(campaign.Knowledge, id, runner.Content, campaign.Characters);
        var signature = id + string.Join("|", lines.Select(l => $"{l.Topic}{l.Value}{l.Status}{l.Confidence}"));
        if (signature != _knownSignature || _shown != id)
        {
            _knownSignature = signature;
            foreach (var child in _cardKnown.GetChildren())
            {
                child.QueueFree();
            }

            if (lines.Count == 0)
            {
                _cardKnown.AddChild(Ui.Text("Nothing yet. Watch them work, talk with them, or ask people who know them.", Ui.TextMeta, Ui.InkSoft, 290));
            }

            foreach (var line in lines)
            {
                var row = new VBoxContainer();
                row.AddThemeConstantOverride("separation", 0);
                var fact = line.Name == "Seen";
                var headline = fact
                    ? $"Seen: {person?.Secrets.FirstOrDefault(s => s.Id == line.Value)?.Text ?? line.Value}"
                    : line.Status is "Observed" or "Confirmed" ? $"{line.Name} — {Capital(line.Value)}" : $"{line.Name} — {line.Status} {line.Value}";
                var certain = line.Confidence is "High" or "Very High";
                row.AddChild(Ui.Text(headline, Ui.TextBody, certain ? Ui.Ink : Ui.Uncertain, 290));
                var meta = Ui.Text($"Confidence: {line.Confidence}{(line.Conflicting ? " (sources disagree)" : "")} · Source: {line.Source} · {new SimulationClock(line.Tick)}",
                    Ui.TextMeta, certain ? Ui.Of(Severity.Positive) : Ui.Of(Severity.Attention), 290);
                row.AddChild(meta);
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
        var claim = runner.OpenClaims().FirstOrDefault(c => c.Creditor == id);
        _claimRow.Visible = claim.Claim is not null && runner.Campaign.Port?.Approached.Contains(id) == true;
        _claimText.Visible = _claimRow.Visible;
        _claimText.Text = claim.Claim is { } open && runner.CharacterFor(open.Debtor) is { } debtor
            ? $"Claims {open.Amount} sueldos that {debtor.Name} owes him."
            : "";
        _offerButton.Disabled = !seekingNow;
        _offerButton.TooltipText = seekingNow
            ? "Offer an advance on wages. Too little insults; some will not sail with certain others, or under a captain they distrust."
            : talked ? "They are not looking for a berth." : "Talk to them first: you do not yet know whether they want work.";
        _demonstrateButton.Disabled = !seekingNow || _demoChoices.Count == 0;
        _demonstrateButton.TooltipText = _demonstrateButton.Disabled
            ? "Only someone looking for a berth will show what they claim; talk to them first."
            : "Ask them to show what they claim. Watch them finish: it counts as confirmed. The proud take offence.";
        if (seekingNow && _offer.Value == 0 && person!.Seeking!.Asks > 0)
        {
            _offer.Value = person.Seeking.Asks;
        }
    }

    // Whom to ask about (people the captain knows by name), and what to ask them to show (what they claim).
    private void FillChoices(SimulationRunner runner, CharacterId id)
    {
        _askAbout.Clear();
        _askChoices.Clear();
        foreach (var other in runner.Campaign.Characters.Values.Where(c => c.Id != id && c.Id != runner.Campaign.CaptainId && c.Membership == Membership.Local)
                     .Where(c => KnownName(runner, c.Id) == c.Name || runner.LeadsAbout(c.Id).Any()).OrderBy(c => c.Name))
        {
            _askAbout.AddItem(other.Name);
            _askChoices.Add(other.Id);
        }

        _askAbout.Disabled = _askChoices.Count == 0;
        if (_askChoices.Count == 0)
        {
            _askAbout.AddItem("(no one you know by name yet)");
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

    private void BuildCard()
    {
        _card = Ui.PanelOf(Ui.Paper, Ui.Rule, Ui.SpaceL, Ui.SpaceL);
        _card.AnchorLeft = 1;
        _card.AnchorRight = 1;
        _card.AnchorBottom = 1;
        _card.OffsetLeft = -330;
        _card.OffsetRight = -Ui.SpaceM;
        _card.OffsetTop = 52;
        _card.OffsetBottom = -56;
        _card.Visible = false;
        _root.AddChild(_card);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _card.AddChild(scroll);
        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", Ui.SpaceS);
        scroll.AddChild(column);

        var header = new HBoxContainer();
        column.AddChild(header);
        _cardTitle = Ui.Text("", Ui.TextDisplay - 2, Ui.Brass, 0);
        _cardTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _cardTitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        header.AddChild(_cardTitle);
        header.AddChild(Ui.Button("×", "Close  (Esc)", () => CardClosed?.Invoke()));
        _cardSubtitle = Ui.Text("", Ui.TextMeta, Ui.InkSoft, 290);
        column.AddChild(_cardSubtitle);
        _cardLook = Ui.Text("", Ui.TextMeta + 1, Ui.Ink, 290);
        column.AddChild(_cardLook);
        _cardDoing = Ui.Text("", Ui.TextMeta, Ui.InkSoft, 290);
        column.AddChild(_cardDoing);
        _cardSeeking = Ui.Text("", Ui.TextBody, Ui.Brass, 290);
        column.AddChild(_cardSeeking);
        _cardLeads = Ui.Text("", Ui.TextMeta + 1, Ui.Uncertain, 290);
        column.AddChild(_cardLeads);
        column.AddChild(new HSeparator());
        column.AddChild(Ui.Heading("What you know"));
        _cardKnown = new VBoxContainer();
        _cardKnown.AddThemeConstantOverride("separation", Ui.SpaceS);
        column.AddChild(_cardKnown);
        column.AddChild(new HSeparator());

        _pending = Ui.Text("", Ui.TextMeta, Ui.Brass, 290);
        column.AddChild(_pending);
        column.AddChild(Ui.Button("Talk", "Walk over and talk: they say what they do, whether they want work, and whom they know",
            () => { if (_shown is { } id) TalkRequested?.Invoke(id); }));

        var ask = new HBoxContainer();
        column.AddChild(ask);
        ask.AddChild(Ui.Button("Ask about", "What does this person know of someone else? Those who work with someone know them best", () =>
        {
            if (_shown is { } id && _askAbout.Selected >= 0 && _askAbout.Selected < _askChoices.Count)
            {
                AskRequested?.Invoke(id, _askChoices[_askAbout.Selected]);
            }
        }));
        _askAbout = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, FocusMode = Control.FocusModeEnum.None, ClipText = true };
        ask.AddChild(_askAbout);

        var demo = new HBoxContainer();
        column.AddChild(demo);
        _demonstrateButton = Ui.Button("Ask to show", "", () =>
        {
            if (_shown is { } id && _demonstrate.Selected >= 0 && _demonstrate.Selected < _demoChoices.Count)
            {
                DemonstrationRequested?.Invoke(id, _demoChoices[_demonstrate.Selected]);
            }
        });
        demo.AddChild(_demonstrateButton);
        _demonstrate = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, FocusMode = Control.FocusModeEnum.None };
        demo.AddChild(_demonstrate);

        var offer = new HBoxContainer();
        column.AddChild(offer);
        _offerButton = Ui.Button("Offer a berth", "", () => { if (_shown is { } id) OfferRequested?.Invoke(id, (int)_offer.Value); });
        offer.AddChild(_offerButton);
        _offer = new SpinBox { MinValue = -2000, MaxValue = 5000, Step = 50, Suffix = "sueldos", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        offer.AddChild(_offer);

        // A creditor's claim against someone signed on (R2e): pay it from the purse, or refuse.
        _claimText = Ui.Text("", Ui.TextMeta + 1, Ui.Brass, 290);
        column.AddChild(_claimText);
        _claimRow = new HBoxContainer();
        column.AddChild(_claimRow);
        _claimRow.AddChild(Ui.Button("Pay the debt", "Pay it from the purse: the debtor sails with his name clear", () => { if (_shown is { } id) ClaimAnswered?.Invoke(id, true); }));
        _claimRow.AddChild(Ui.Button("Refuse", "He sails anyway; the creditor will not forget it", () => { if (_shown is { } id) ClaimAnswered?.Invoke(id, false); }));
    }

    private void BuildDialogue()
    {
        _dialogue = Ui.PanelOf(Ui.Paper, Ui.Rule, Ui.SpaceL, Ui.SpaceM);
        _dialogue.AnchorLeft = 0.5f;
        _dialogue.AnchorRight = 0.5f;
        _dialogue.AnchorTop = 1;
        _dialogue.AnchorBottom = 1;
        _dialogue.OffsetLeft = -280;
        _dialogue.OffsetRight = 280;
        _dialogue.OffsetBottom = -96;
        _dialogue.GrowVertical = Control.GrowDirection.Begin;
        _dialogue.Visible = false;
        _root.AddChild(_dialogue);
        var row = new HBoxContainer();
        _dialogue.AddChild(row);
        _dialogueText = Ui.Text("", Ui.TextBody + 1, Ui.Ink, 0);
        _dialogueText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _dialogueText.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _dialogueText.CustomMinimumSize = new Vector2(500, 0);
        row.AddChild(_dialogueText);
        row.AddChild(Ui.Button("×", "Close", () => _dialogue.Visible = false));
    }

    // ================================================================ Expedition screen: objectives, papers, journal

    private void BuildExpedition(VBoxContainer body)
    {
        _expedition = new VBoxContainer();
        _expedition.AddThemeConstantOverride("separation", Ui.SpaceM);
        body.AddChild(_expedition);
        _expedition.AddChild(Ui.Heading("Objectives"));
        _objectives = Ui.Text("", Ui.TextBody, Ui.Ink, 370);
        _expedition.AddChild(_objectives);
        var buttons = new HBoxContainer();
        _expedition.AddChild(buttons);
        buttons.AddChild(Ui.Button("Read the notices", "Walk to the Chamber's board on the terrace: berths wanted, passages, pardons", () => NoticesRequested?.Invoke()));
        buttons.AddChild(Ui.Button("Set sail", "Go aboard with the crew you have signed and sail", ConfirmSail));
        _expedition.AddChild(new HSeparator());
        _expedition.AddChild(Ui.Heading("Journal — people worth finding"));
        _journalList = new VBoxContainer();
        _journalList.AddThemeConstantOverride("separation", Ui.SpaceM);
        _expedition.AddChild(_journalList);
    }

    // Sailing short of stores is allowed, but it is a high-cost decision (§43): say so first.
    private void ConfirmSail()
    {
        if (_runner is not { } runner || runner.Campaign.Port is not { } port || runner.Content.Port is not { } rules)
        {
            return;
        }

        var (food, water) = runner.SupplyForecast();
        var worries = new List<string>();
        if (port.Hired.Count < rules.MinimumCrew) worries.Add($"Only {port.Hired.Count} of the {rules.MinimumCrew} hands she needs are signed on.");
        if (Math.Min(food, water) < 25) worries.Add($"The stores last about {Math.Floor(Math.Min(food, water)):0} days for {port.Hired.Count + 1}; the crossing takes about 25.");
        if (worries.Count == 0)
        {
            SetSailRequested?.Invoke();
            return;
        }

        _confirmSail.DialogText = string.Join("\n", worries) + "\n\nSail anyway?";
        _confirmSail.PopupCentered();
    }

    private void RefreshExpedition(SimulationRunner runner)
    {
        var port = runner.Campaign.Port!;
        var rules = runner.Content.Port!;
        var hours = (rules.Departure.Tick - runner.Campaign.Clock.Tick) / (double)SimulationClock.TicksPerHour;
        var (food, water) = runner.SupplyForecast();
        _objectives.Text =
            $"Now: {(port.Hired.Count < rules.MinimumCrew ? $"sign on {rules.MinimumCrew - port.Hired.Count} more hands ({port.Hired.Count} of {rules.Berths} berths taken)" : "stores and any last hands, then sail")}\n" +
            $"Expedition: sail with the convoy at dawn on day {rules.Departure.Day} — {(hours > 0 ? $"in {Math.Floor(hours):0} hours" : $"it has sailed; {rules.LateFeePerHalfDay} sueldos for every half day late")}\n" +
            "Charter: reach Punta Ceniza, learn what became of the factory, re-establish it and hold it\n\n" +
            $"Purse: {port.Funds} sueldos\nStores for {port.Hired.Count + 1}: water ~{Math.Floor(water * 0.9):0}–{Math.Ceiling(water * 1.1):0} days, food ~{Math.Floor(food * 0.9):0}–{Math.Ceiling(food * 1.1):0} days";
        if (Expedition.Simulation.Port.ShipPreparation.Of(runner.Campaign, runner.Content) is { } preparation)
        {
            _objectives.Text += $"\n\nThe Garza — {Expedition.Simulation.Port.ShipPreparation.Describe(preparation.Stage)}\n{runner.DescribeStanding()}";
            if (preparation.OnQuay.Count > 0)
            {
                _objectives.Text += "\nOn the quay, going aboard: " + string.Join(", ", preparation.OnQuay.Select(s =>
                    $"{s.Count} {(s.Count == 1 ? s.Package : s.Package + "s")} of {runner.Content.Items[s.Item].Name.Split(" (")[0].ToLowerInvariant()}"));
            }
        }

        var signature = port.Leads.Count * 1000 + port.TalkedTo.Count * 31 + port.Hired.Count * 7 + port.Gone.Count;
        if (signature == _journalSignature)
        {
            return;
        }

        _journalSignature = signature;
        foreach (var child in _journalList.GetChildren())
        {
            child.QueueFree();
        }

        if (port.Leads.Count == 0)
        {
            _journalList.AddChild(Ui.Text("Nothing yet. Talk to people who see a lot of the waterfront (a landlady, a chandler, a priest, a guard), or read the Chamber's notices.", Ui.TextMeta, Ui.InkSoft, 370));
            return;
        }

        var leads = rules.AllLeads().ToDictionary(l => l.Lead.Id, l => l.Lead);
        foreach (var heard in port.Leads.AsEnumerable().Reverse())
        {
            var lead = leads[heard.LeadId];
            var about = runner.CharacterFor(lead.About);
            var (status, severity) = about is null ? ("", Severity.Information) : port.Hired.Contains(about.Id) ? ("Signed on", Severity.Positive)
                : port.Gone.Contains(about.Id) ? ("Gone from the port", Severity.Urgent)
                : port.TalkedTo.Contains(about.Id) ? ($"Found: you have spoken with {about.Name}", Severity.Positive)
                : ("Not yet found", Severity.Attention);
            var giver = runner.LeadGiver(lead.Id) is { } from ? KnownName(runner, from) : "the Chamber's board";
            var entry = new VBoxContainer();
            entry.AddThemeConstantOverride("separation", 1);
            entry.AddChild(Ui.Text($"\"{lead.Text}\"", Ui.TextBody, Ui.Ink, 370));
            entry.AddChild(Ui.Text($"Said to be: {lead.Where}.", Ui.TextMeta, Ui.InkSoft, 370));
            entry.AddChild(Ui.Text($"{Ui.IconOf(severity)} {status} · Rumored · from {giver}, {new SimulationClock(heard.Tick)}", Ui.TextMeta, Ui.Of(severity), 370));
            _journalList.AddChild(entry);
        }
    }

    // ================================================================ Cargo screen: the chandler

    private void BuildChandler(VBoxContainer body)
    {
        var section = new VBoxContainer();
        body.AddChild(section);
        body.MoveChild(section, 0);
        section.AddChild(Ui.Heading("Maese Corbalán, chandler — delivered to the gangway"));
        _lots = new VBoxContainer();
        _lots.AddThemeConstantOverride("separation", 2);
        section.AddChild(_lots);
        for (var lot = 0; lot < 16; lot++)
        {
            var index = lot;
            var button = Ui.Button("", "", () => BuyRequested?.Invoke(index));
            button.Alignment = HorizontalAlignment.Left;
            button.Visible = false;
            _lots.AddChild(button);
        }

        section.AddChild(new HSeparator());
    }

    private void RefreshChandler(SimulationRunner runner)
    {
        var rules = runner.Content.Port!;
        var port = runner.Campaign.Port!;
        var buttons = _lots.GetChildren().OfType<Button>().ToList();
        for (var lot = 0; lot < buttons.Count; lot++)
        {
            buttons[lot].Visible = lot < rules.Goods.Count;
            if (lot >= rules.Goods.Count)
            {
                continue;
            }

            var goods = rules.Goods[lot];
            var item = runner.Content.Items[goods.Item];
            var left = port.Stock.GetValueOrDefault(lot);
            buttons[lot].Text = $"{goods.Quantity} {item.Name.ToLowerInvariant()} — {goods.Price} sueldos   ({left} left)";
            buttons[lot].Disabled = left <= 0 || port.Funds < goods.Price;
            buttons[lot].TooltipText = left <= 0 ? "Sold out: others bought the last of it."
                : port.Funds < goods.Price ? $"Only {port.Funds} sueldos left in the purse."
                : $"Left on the quay by the gangway and carried aboard over the next hours. About {goods.SoldPerDay} of these sell to other ships each day.";
        }
    }

    /// <summary>Resets for a newly started or loaded campaign.</summary>
    public void Bind(SimulationRunner runner)
    {
        _runner = runner;
        _shown = null;
        _journalSignature = -1;
        Say("");
    }

    private static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}

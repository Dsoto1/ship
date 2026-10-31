using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.World;
using Godot;

namespace Expedition.Game;

/// <summary>
/// Talking with someone in port: their portrait and how they take the captain, what has been said, and
/// what he can ask them now (SimulationRunner.Topics). Questions already answered stay, dimmed, to ask
/// again. Asking about someone else, and the offer of a berth once they have said what they want, are
/// here too. The game stands still while it is open. It shows and asks; the simulation decides.
/// </summary>
public partial class ConversationView : Control
{
    private PanelContainer _panel = null!;
    private TextureRect _portrait = null!;
    private Label _name = null!;
    private Label _who = null!;
    private Label _manner = null!;
    private RichTextLabel _said = null!;
    private VBoxContainer _topics = null!;
    private OptionButton _askAbout = null!;
    private PanelContainer _offerRow = null!;
    private SpinBox _offer = null!;
    private readonly List<CharacterId> _askChoices = [];
    private CharacterId? _with;
    private bool _ended;

    /// <summary>The captain asks about a topic: the person, the topic ID, and what he asks.</summary>
    public event Action<CharacterId, string, string>? TopicChosen;

    /// <summary>The captain asks them about someone else.</summary>
    public event Action<CharacterId, CharacterId>? AskAboutChosen;

    /// <summary>The captain offers them a berth for an advance.</summary>
    public event Action<CharacterId, int>? OfferChosen;

    /// <summary>The conversation is over.</summary>
    public event Action? Closed;

    /// <summary>Who the captain is talking with, or null.</summary>
    public CharacterId? With => Visible ? _with : null;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Theme = Ui.BuildTheme();
        Visible = false;
        _panel = Ui.Frame(Ui.SpaceL, Ui.SpaceL);
        _panel.AnchorLeft = 0.5f;
        _panel.AnchorRight = 0.5f;
        _panel.AnchorTop = 1;
        _panel.AnchorBottom = 1;
        // Left of centre, clear of the person card on the right, whose "What you know" fills as they talk.
        _panel.OffsetLeft = -620;
        _panel.OffsetRight = 220;
        _panel.OffsetBottom = -Ui.AboveTabBar;
        _panel.OffsetTop = -Ui.AboveTabBar - 560;
        AddChild(_panel);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", Ui.SpaceL);
        _panel.AddChild(row);

        var left = new VBoxContainer { CustomMinimumSize = new Vector2(190, 0) };
        left.AddThemeConstantOverride("separation", Ui.SpaceS);
        row.AddChild(left);
        _portrait = new TextureRect
        {
            CustomMinimumSize = new Vector2(190, 238),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
        };
        left.AddChild(_portrait);
        _name = Ui.Text("", Ui.TextHeading + 2, Ui.Brass, 190);
        left.AddChild(_name);
        _who = Ui.Text("", Ui.TextMeta, Ui.InkSoft, 190);
        left.AddChild(_who);
        _manner = Ui.Text("", Ui.TextMeta + 1, Ui.Uncertain, 190);
        left.AddChild(_manner);

        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", Ui.SpaceM);
        row.AddChild(right);

        // What is said: on parchment, in ink, so that words spoken never look like something to press.
        var speech = Ui.ParchmentPanel(Ui.SpaceL, Ui.SpaceM);
        speech.SizeFlagsVertical = SizeFlags.ExpandFill;
        right.AddChild(speech);
        _said = new RichTextLabel
        {
            BbcodeEnabled = true,
            ScrollFollowing = true,
            FitContent = false,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(560, 150),
        };
        _said.AddThemeFontOverride("normal_font", Ui.SerifFont);
        _said.AddThemeFontOverride("italics_font", Ui.TitleItalic);
        _said.AddThemeFontSizeOverride("normal_font_size", Ui.TextBody + 2);
        _said.AddThemeFontSizeOverride("italics_font_size", Ui.TextBody + 2);
        _said.AddThemeColorOverride("default_color", Ui.InkDark);
        Ui.ContentOf(speech).AddChild(_said);

        // What the captain can say: every line a button, marked as his to choose.
        right.AddChild(Ui.SectionHeader("Ask them", "Choose a question"));
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 112), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        right.AddChild(scroll);
        _topics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _topics.AddThemeConstantOverride("separation", Ui.SpaceS);
        scroll.AddChild(_topics);

        var ask = new HBoxContainer();
        ask.AddThemeConstantOverride("separation", Ui.SpaceM);
        right.AddChild(ask);
        var askLabel = Ui.Text("What do they know of", Ui.TextBody, Ui.InkSoft);
        askLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        ask.AddChild(askLabel);
        _askAbout = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.None, ClipText = true };
        ask.AddChild(_askAbout);
        _askButton = Ui.Button("Ask", "What do they know of someone else? Those who work with someone know them best", () =>
        {
            if (_with is { } id && _askAbout.Selected >= 0 && _askAbout.Selected < _askChoices.Count)
            {
                AskAboutChosen?.Invoke(id, _askChoices[_askAbout.Selected]);
            }
        });
        ask.AddChild(_askButton);

        // The terms, once heard: what they ask, what to offer, and the one thing to do, in gold.
        _offerRow = new PanelContainer();
        _offerRow.AddThemeStyleboxOverride("panel", Ui.InsetStyle(Ui.SpaceL, Ui.SpaceM));
        right.AddChild(_offerRow);
        var terms = new VBoxContainer();
        terms.AddThemeConstantOverride("separation", Ui.SpaceS);
        _offerRow.AddChild(terms);
        terms.AddChild(Ui.SectionHeader("A berth aboard the Garza"));
        _asks = Ui.Text("", Ui.TextBody, Ui.Ink, 560);
        terms.AddChild(_asks);
        var deal = new HBoxContainer();
        deal.AddThemeConstantOverride("separation", Ui.SpaceM);
        terms.AddChild(deal);
        var advance = Ui.Text("Advance", Ui.TextBody, Ui.InkSoft);
        advance.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        deal.AddChild(advance);
        _offer = new SpinBox { MinValue = -2000, MaxValue = 5000, Step = 50, Suffix = "sueldos", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _offer.ValueChanged += _ => _offerButton.Text = OfferText();
        deal.AddChild(_offer);
        _offerButton = Ui.Button(Ui.ButtonKind.Primary, "Offer a berth", "Offer them a berth for this advance (a passenger's is what they pay). Too little insults", () =>
        {
            if (_with is { } id)
            {
                OfferChosen?.Invoke(id, (int)_offer.Value);
            }
        });
        _offerButton.CustomMinimumSize = new Vector2(190, Ui.MinHit + 6);
        deal.AddChild(_offerButton);

        var bottom = new HBoxContainer();
        right.AddChild(bottom);
        bottom.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        bottom.AddChild(Ui.Button(Ui.ButtonKind.Tertiary, "Goodbye  (Esc)", "End the conversation", Close));
    }

    private Label _asks = null!;
    private Button _offerButton = null!, _askButton = null!;

    private string OfferText() => _offer.Value < 0 ? $"Take them for {-(int)_offer.Value} sueldos" : $"Offer a berth · {(int)_offer.Value} sueldos";

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && @event.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Starts talking with someone: how they met him, then what he can ask.</summary>
    public void Open(SimulationRunner runner, CharacterId id, string greeting)
    {
        _with = id;
        _ended = false;
        var character = runner.Campaign.Characters[id];
        var person = runner.PersonOf(id);
        var portrait = person is null ? null : $"res://Assets/Characters/{person.Id}/portraits/neutral.png";
        _portrait.Texture = portrait is not null && ResourceLoader.Exists(portrait) ? GD.Load<Texture2D>(portrait) : null;
        _portrait.Visible = _portrait.Texture is not null;
        _name.Text = character.Name;
        _who.Text = person is null ? "" : char.ToUpperInvariant(person.Occupation[0]) + person.Occupation[1..];
        _said.Clear();
        _said.AppendText(Escape(greeting));
        _offer.Value = person?.Seeking?.Asks ?? 0;
        FillAskAbout(runner, id);
        Refresh(runner);
        Visible = true;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Ui.FadeIn(_panel);
    }

    /// <summary>What the captain said, and what came back.</summary>
    public void Add(SimulationRunner runner, string asked, string said, bool ends = false)
    {
        _said.AppendText($"\n\n[color=#{CaptainInk.ToHtml(false)}][i]You: {Escape(asked)}[/i][/color]\n{Escape(said)}");
        _ended |= ends;
        Refresh(runner);
    }

    /// <summary>Redraws the questions and the offer from the simulation.</summary>
    public void Refresh(SimulationRunner runner)
    {
        if (_with is not { } id)
        {
            return;
        }

        _manner.Text = runner.Manner(id);
        foreach (var child in _topics.GetChildren())
        {
            child.QueueFree();
        }

        if (_ended)
        {
            _topics.AddChild(Ui.Text("They have said all they mean to, for now.", Ui.TextMeta + 1, Ui.InkSoft, 520));
        }
        else
        {
            // New questions first, as buttons; those already asked after them, quieter, to ask again.
            foreach (var topic in runner.Topics(id).OrderBy(t => t.Discussed))
            {
                var button = Ui.Button(topic.Discussed ? Ui.ButtonKind.Tertiary : Ui.ButtonKind.Secondary,
                    topic.Discussed ? $"{Ui.IconLead}  {topic.Ask}   (asked)" : $"{Ui.IconLead}  {topic.Ask}", "", () => TopicChosen?.Invoke(id, topic.Id, topic.Ask));
                button.Alignment = HorizontalAlignment.Left;
                button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                button.ClipText = true;
                _topics.AddChild(button);
            }
        }

        var port = runner.Campaign.Port;
        var seeking = runner.PersonOf(id)?.Seeking is not null && port is not null && !port.Hired.Contains(id) && !port.Gone.Contains(id);
        _offerRow.Visible = seeking && runner.TermsHeard(id);
        if (_offerRow.Visible && runner.PersonOf(id)?.Seeking is { } wants)
        {
            _asks.Text = wants.Asks < 0 ? $"They want passage, and offer to pay {-wants.Asks} sueldos." : $"They ask an advance of {wants.Asks} sueldos on their wages.";
        }

        _offerButton.Text = OfferText();
        _askButton.Disabled = _askChoices.Count == 0;
    }

    // The captain's own words in the record: a dark red-brown, set apart from theirs.
    private static readonly Color CaptainInk = new("#7A2E1E");

    private void FillAskAbout(SimulationRunner runner, CharacterId id)
    {
        _askAbout.Clear();
        _askChoices.Clear();
        foreach (var other in runner.Campaign.Characters.Values.Where(c => c.Id != id && c.Id != runner.Campaign.CaptainId && c.Membership == Membership.Local)
                     .Where(c => PortHud.KnownName(runner, c.Id) == c.Name || runner.LeadsAbout(c.Id).Any()).OrderBy(c => c.Name))
        {
            _askAbout.AddItem(other.Name);
            _askChoices.Add(other.Id);
        }

        _askAbout.Disabled = _askChoices.Count == 0;
        if (_askChoices.Count == 0)
        {
            _askAbout.AddItem("(no one you know by name yet)");
        }
    }

    private void Close()
    {
        if (!Visible)
        {
            return;
        }

        Visible = false;
        _with = null;
        Closed?.Invoke();
    }

    private static string Escape(string text) => text.Replace("[", "[lb]");
}

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
    private HBoxContainer _offerRow = null!;
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
        _panel.OffsetTop = -Ui.AboveTabBar - 470;
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
        right.AddThemeConstantOverride("separation", Ui.SpaceS);
        row.AddChild(right);
        _said = new RichTextLabel
        {
            BbcodeEnabled = true,
            ScrollFollowing = true,
            FitContent = false,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(560, 210),
        };
        _said.AddThemeFontSizeOverride("normal_font_size", Ui.TextBody + 1);
        _said.AddThemeColorOverride("default_color", Ui.Ink);
        right.AddChild(_said);
        right.AddChild(new HSeparator());

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 130), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        right.AddChild(scroll);
        _topics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _topics.AddThemeConstantOverride("separation", 2);
        scroll.AddChild(_topics);

        var ask = new HBoxContainer();
        right.AddChild(ask);
        ask.AddChild(Ui.Button("Ask about", "What do they know of someone else? Those who work with someone know them best", () =>
        {
            if (_with is { } id && _askAbout.Selected >= 0 && _askAbout.Selected < _askChoices.Count)
            {
                AskAboutChosen?.Invoke(id, _askChoices[_askAbout.Selected]);
            }
        }));
        _askAbout = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.None, ClipText = true };
        ask.AddChild(_askAbout);

        var bottom = new HBoxContainer();
        right.AddChild(bottom);
        _offerRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        bottom.AddChild(_offerRow);
        _offerRow.AddChild(Ui.Button("Offer a berth", "Offer them a berth for this advance (a passenger's is what they pay)", () =>
        {
            if (_with is { } id)
            {
                OfferChosen?.Invoke(id, (int)_offer.Value);
            }
        }));
        _offer = new SpinBox { MinValue = -2000, MaxValue = 5000, Step = 50, Suffix = "sueldos", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _offerRow.AddChild(_offer);
        bottom.AddChild(Ui.Button("Goodbye", "End the conversation  (Esc)", Close));
    }

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
        _said.AppendText($"\n\n[color=#{Ui.Brass.ToHtml(false)}]You: {Escape(asked)}[/color]\n{Escape(said)}");
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
            foreach (var topic in runner.Topics(id))
            {
                var button = Ui.Button(topic.Discussed ? $"{topic.Ask}  (again)" : topic.Ask, "", () => TopicChosen?.Invoke(id, topic.Id, topic.Ask));
                button.Alignment = HorizontalAlignment.Left;
                button.Flat = true;
                button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                if (topic.Discussed)
                {
                    button.AddThemeColorOverride("font_color", Ui.InkSoft);
                }

                _topics.AddChild(button);
            }
        }

        var port = runner.Campaign.Port;
        var seeking = runner.PersonOf(id)?.Seeking is not null && port is not null && !port.Hired.Contains(id) && !port.Gone.Contains(id);
        _offerRow.Visible = seeking && runner.TermsHeard(id);
    }

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

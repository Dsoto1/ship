using Expedition.Simulation.Characters;
using Expedition.Simulation.Core;
using Expedition.Simulation.History;
using Expedition.Simulation.World;
using Godot;

namespace Expedition.Game;

/// <summary>
/// The moment someone signs on: a card over the port with their portrait, "Signed on" struck across it like
/// a stamp in the ship's book, what they bring as far as the captain knows, what was agreed, and how the
/// crew stands now. The ship's bell rings and coins change hands. It waits for the player (Enter, Esc or
/// the button), with the game standing still.
/// </summary>
public partial class SignedOnView : Control
{
    private PanelContainer _card = null!;
    private TextureRect _portrait = null!;
    private Label _name = null!, _who = null!, _agreed = null!, _crew = null!, _next = null!, _stamp = null!;
    private VBoxContainer _brings = null!;
    private AudioStreamPlayer _bell = null!, _coins = null!;
    private ColorRect _backdrop = null!;
    private CenterContainer _centre = null!;

    /// <summary>The card has been put away.</summary>
    public event Action? Closed;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = Ui.BuildTheme();
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        _backdrop = new ColorRect { Color = new Color(0.02f, 0.05f, 0.08f, 0.62f), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_backdrop);
        _centre = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_centre);
        var centre = _centre;
        _card = Ui.ParchmentPanel(Ui.SpaceXl, Ui.SpaceL);
        _card.CustomMinimumSize = new Vector2(560, 0);
        centre.AddChild(_card);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", Ui.SpaceM);
        Ui.ContentOf(_card).AddChild(column);

        var banner = Ui.Title("Signed on for the Garza", Ui.TextDisplay + 4, Ui.InkDark);
        banner.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(banner);
        column.AddChild(new ColorRect { Color = new Color(Ui.InkDark, 0.5f), CustomMinimumSize = new Vector2(0, 2), MouseFilter = MouseFilterEnum.Ignore });

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", Ui.SpaceL);
        column.AddChild(row);
        var frame = Ui.PanelOf(Ui.InkDark, Ui.Gold, 3, 3);
        row.AddChild(frame);
        _portrait = new TextureRect
        {
            CustomMinimumSize = new Vector2(150, 188),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
        };
        frame.AddChild(_portrait);

        var about = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        about.AddThemeConstantOverride("separation", Ui.SpaceS);
        row.AddChild(about);
        _name = Ui.Title("", Ui.TextDisplay, Ui.InkDark);
        about.AddChild(_name);
        _who = Ui.Text("", Ui.TextBody, Ui.InkDarkSoft, 340);
        about.AddChild(_who);
        _agreed = Ui.Text("", Ui.TextBody, Ui.InkDark, 340);
        about.AddChild(_agreed);
        about.AddChild(Ui.Text("What you know they bring", Ui.TextMeta + 1, Ui.InkDarkSoft));
        _brings = new VBoxContainer();
        _brings.AddThemeConstantOverride("separation", 0);
        about.AddChild(_brings);

        // The stamp across the foot of the portrait, struck a moment after the card arrives.
        _stamp = Ui.Title("SIGNED", Ui.TextDisplay + 8, new Color("#9A2B1A"));
        _stamp.AddThemeColorOverride("font_outline_color", new Color("#F3E3C0"));
        _stamp.AddThemeConstantOverride("outline_size", 6);
        _stamp.Rotation = Mathf.DegToRad(-14);
        _stamp.Position = new Vector2(14, 128);
        _stamp.PivotOffset = new Vector2(60, 16);
        _stamp.MouseFilter = MouseFilterEnum.Ignore;
        _portrait.AddChild(_stamp);

        column.AddChild(new ColorRect { Color = new Color(Ui.InkDark, 0.3f), CustomMinimumSize = new Vector2(0, 1), MouseFilter = MouseFilterEnum.Ignore });
        _crew = Ui.Text("", Ui.TextBody + 1, Ui.InkDark, 520);
        column.AddChild(_crew);
        _next = Ui.Text("", Ui.TextBody, Ui.InkDarkSoft, 520);
        column.AddChild(_next);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        column.AddChild(buttons);
        var welcome = Ui.Button(Ui.ButtonKind.Primary, "Welcome aboard", "Back to the port  (Enter)", Close);
        welcome.CustomMinimumSize = new Vector2(220, Ui.MinHit + 10);
        buttons.AddChild(welcome);

        _bell = new AudioStreamPlayer { Stream = GD.Load<AudioStream>("res://Assets/Audio/impact/impactBell_heavy_001.ogg"), VolumeDb = -6 };
        AddChild(_bell);
        _coins = new AudioStreamPlayer { Stream = GD.Load<AudioStream>("res://Assets/Audio/rpg/handleCoins.ogg"), VolumeDb = -4 };
        AddChild(_coins);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && (@event.IsActionPressed("ui_cancel") || @event.IsActionPressed("ui_accept")))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Shows the card for someone just signed on for <paramref name="advance"/> (negative: passage they pay).</summary>
    public void Open(SimulationRunner runner, CharacterId id, int advance)
    {
        var character = runner.Campaign.Characters[id];
        var person = runner.PersonOf(id);
        var portrait = person is null ? null : $"res://Assets/Characters/{person.Id}/portraits/neutral.png";
        _portrait.Texture = portrait is not null && ResourceLoader.Exists(portrait) ? GD.Load<Texture2D>(portrait) : null;
        _portrait.GetParent<Control>().Visible = _portrait.Texture is not null;
        _name.Text = character.Name;
        _who.Text = person is null ? "" : char.ToUpperInvariant(person.Occupation[0]) + person.Occupation[1..];
        _agreed.Text = advance < 0 ? $"Sails as a passenger, paying {-advance} sueldos." : $"An advance of {advance} sueldos, paid from the purse.";

        foreach (var child in _brings.GetChildren())
        {
            child.QueueFree();
        }

        var lines = KnowledgeText.Lines(runner.Campaign.Knowledge, id, runner.Content, runner.Campaign.Characters)
            .Where(l => l.Name is not ("Seen" or "Heard")).Take(3).ToList();
        foreach (var line in lines)
        {
            var sure = line.Confidence is "High" or "Very High";
            _brings.AddChild(Ui.Text($"{Ui.IconLead} {line.Name}: {(line.Status is "Observed" or "Confirmed" ? line.Value : $"{line.Status.ToLowerInvariant()} {line.Value}")}{(sure ? "" : " (their word)")}",
                Ui.TextBody, Ui.InkDark, 340));
        }

        if (lines.Count == 0)
        {
            _brings.AddChild(Ui.Text("Little yet: you will learn what they can do at sea.", Ui.TextBody, Ui.InkDarkSoft, 340));
        }

        var port = runner.Campaign.Port!;
        var rules = runner.Content.Port!;
        var hands = port.Hired.Count;
        _crew.Text = hands >= rules.MinimumCrew
            ? $"Hands signed on: {hands} of {rules.Berths} berths. Enough to sail."
            : $"Hands signed on: {hands}. The Garza wants {rules.MinimumCrew - hands} more before she sails.";
        _crew.AddThemeColorOverride("font_color", hands >= rules.MinimumCrew ? new Color("#2E6B3A") : Ui.InkDark);
        _next.Text = hands >= rules.MinimumCrew ? "Stores next, then the convoy." : "The journal (X) says who else to find.";

        Visible = true;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _centre.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _card.PivotOffset = _card.Size / 2;
        _card.Scale = new Vector2(0.85f, 0.85f);
        _card.Modulate = new Color(1, 1, 1, 0);
        var tween = CreateTween().SetParallel();
        tween.TweenProperty(_card, "scale", Vector2.One, 0.35f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_card, "modulate:a", 1f, 0.2f);
        _stamp.Modulate = new Color(1, 1, 1, 0);
        _stamp.Scale = new Vector2(1.8f, 1.8f);
        var stamp = CreateTween();
        stamp.TweenInterval(0.35f);
        stamp.TweenCallback(Callable.From(() => _bell.Play()));
        stamp.Parallel().TweenProperty(_stamp, "modulate:a", 0.85f, 0.08f);
        stamp.Parallel().TweenProperty(_stamp, "scale", Vector2.One, 0.12f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        stamp.TweenInterval(0.25f);
        stamp.TweenCallback(Callable.From(() => _coins.Play()));
    }

    private void Close()
    {
        if (!Visible)
        {
            return;
        }

        Visible = false;
        Closed?.Invoke();
    }
}

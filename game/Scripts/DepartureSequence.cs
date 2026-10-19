using Expedition.Simulation.Core;
using Expedition.Simulation.World;
using Godot;

namespace Expedition.Game;

/// <summary>
/// The departure (PORT_EXPERIENCE §17, R2e): the payoff of the port. It plays the beats of
/// <see cref="SimulationRunner.DeparturePlan"/>, written from the expedition as it actually is: the
/// hour against the convoy, the real crew and who has come to see each of them off, any creditor at the
/// gangway (who must be answered), the last stores and the hold, and the Company's clearance. Then the
/// gangway comes in, the lines are cast off and the Garza moves out while La Bocana falls astern. The
/// management interface steps aside meanwhile and time stands still, so nothing changes under it. At
/// the end it hands over to <see cref="Finished"/>, which sails for real. Escape or "Skip" cuts to that.
/// </summary>
public partial class DepartureSequence : CanvasLayer
{
    private const float BeatSeconds = 4.2f;
    private const float SailSeconds = 13f;

    private readonly List<DepartureBeat> _beats = [];
    private SimulationRunner _runner = null!;
    private PanelContainer _panel = null!;
    private Label _text = null!;
    private HBoxContainer _answer = null!;
    private Button _pay = null!;
    private ColorRect _fade = null!;
    private int _index = -1;
    private float _clock;
    private float _sailing = -1;
    private float _closing = -1;
    private bool _waiting;
    private Vector3 _berth;

    /// <summary>True while the departure is playing.</summary>
    public bool Playing { get; private set; }

    /// <summary>True once the captain has gone up the gangway: he is no longer on the quay.</summary>
    public bool Boarded { get; private set; }

    /// <summary>How far the Garza has moved from her berth, in metres, and turned, in degrees.</summary>
    public (Vector3 Offset, float Turn) Ship { get; private set; }

    /// <summary>Where the camera should look and how close.</summary>
    public (Vector3 Target, float Size)? Camera { get; private set; }

    /// <summary>A beat began: its kind, so the scene can take in the gangway, cast off, and so on.</summary>
    public event Action<string>? Beat;

    /// <summary>The departure is over: sail now.</summary>
    public event Action? Finished;

    public override void _Ready()
    {
        Layer = 20;
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = Ui.BuildTheme() };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        _fade = new ColorRect { Color = new Color(0, 0, 0, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        _fade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(_fade);

        // A caption low on the screen, like the dialogue strip, and nothing else.
        _panel = Ui.PanelOf(Ui.Paper, Ui.Rule, Ui.SpaceL, Ui.SpaceM);
        _panel.AnchorLeft = 0.5f;
        _panel.AnchorRight = 0.5f;
        _panel.AnchorTop = 1;
        _panel.AnchorBottom = 1;
        _panel.OffsetLeft = -330;
        _panel.OffsetRight = 330;
        _panel.OffsetBottom = -40;
        _panel.GrowVertical = Control.GrowDirection.Begin;
        root.AddChild(_panel);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", Ui.SpaceM);
        _panel.AddChild(column);
        _text = Ui.Text("", Ui.TextBody + 2, Ui.Ink, 600);
        column.AddChild(_text);
        _answer = new HBoxContainer();
        column.AddChild(_answer);
        _pay = Ui.Button("Pay him", "Pay the debt from the purse", () => Answer(true));
        _answer.AddChild(_pay);
        _answer.AddChild(Ui.Button("Refuse him", "Sail anyway; he will not forget it", () => Answer(false)));
        var skip = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        column.AddChild(skip);
        skip.AddChild(Ui.Button("Skip ▸", "Cast off at once  (Esc)", Skip));
        Visible = false;
    }

    /// <summary>Plays the departure for the expedition as it stands.</summary>
    public void Play(SimulationRunner runner, Vector3 gangway, Vector3 berth)
    {
        _runner = runner;
        _berth = berth;
        _beats.Clear();
        _beats.AddRange(runner.DeparturePlan());
        _index = -1;
        _clock = BeatSeconds;
        _sailing = -1;
        _closing = -1;
        _waiting = false;
        Boarded = false;
        Ship = (Vector3.Zero, 0);
        Camera = (gangway, 15f);
        _fade.Color = new Color(0, 0, 0, 0);
        Visible = true;
        Playing = true;
    }

    private void Answer(bool pay)
    {
        if (_index < 0 || _index >= _beats.Count || _beats[_index].Who is not { } creditor)
        {
            return;
        }

        var result = pay ? _runner.PayClaim(creditor) : _runner.RefuseClaim(creditor);
        if (!result.Succeeded)
        {
            _text.Text += "\n" + result.Reason;
            return;
        }

        _text.Text = pay
            ? $"{_runner.Campaign.Characters[creditor].Name} counts the coins twice and steps aside."
            : $"{_runner.Campaign.Characters[creditor].Name} shouts after you up the gangway that the Chamber will hear of this.";
        _waiting = false;
        _answer.Visible = false;
        _clock = BeatSeconds * 0.5f;
    }

    private void Skip()
    {
        // Any claim still open is refused: the Garza sails.
        foreach (var (creditor, _) in _runner.OpenClaims().ToList())
        {
            _runner.RefuseClaim(creditor);
        }

        _closing = 0;
        _sailing = SailSeconds;
        Boarded = true;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Playing && @event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Skip();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (!Playing)
        {
            return;
        }

        var dt = (float)delta;
        if (_closing >= 0)
        {
            // Fade out, then sail for real and fade back in at sea.
            _closing += dt;
            _fade.Color = new Color(0, 0, 0, Mathf.Clamp(_closing / 1.2f, 0, 1));
            if (_closing >= 1.2f && Playing)
            {
                Playing = false;
                Camera = null;
                Finished?.Invoke();
                var tween = CreateTween();
                tween.TweenProperty(_fade, "color", new Color(0, 0, 0, 0), 1.2f);
                tween.TweenCallback(Callable.From(() => Visible = false));
            }

            return;
        }

        if (_sailing >= 0)
        {
            // She is towed off and gathers way, turning her head to the bar; the camera follows, rising.
            _sailing += dt;
            var t = Mathf.Clamp(_sailing / SailSeconds, 0, 1);
            var ease = t * t * (3 - 2 * t);
            Ship = (new Vector3(-8f * ease, 0, 34f * ease * ease + 4f * ease), -20f * ease);
            if (Camera is { } view)
            {
                Camera = (view.Target.Lerp(_berth + Ship.Offset, dt * 0.6f), Mathf.Lerp(15f, 34f, ease));
            }

            if (t >= 1)
            {
                _closing = 0;
            }

            return;
        }

        if (_waiting)
        {
            return;
        }

        _clock += dt;
        if (_clock < BeatSeconds)
        {
            return;
        }

        _clock = 0;
        _index++;
        if (_index >= _beats.Count)
        {
            _sailing = 0;
            return;
        }

        var beat = _beats[_index];
        _text.Text = beat.Text;
        Ui.FadeIn(_panel);
        _answer.Visible = beat.Kind == "claim";
        if (beat.Kind == "claim" && _runner.OpenClaims().FirstOrDefault(c => c.Creditor == beat.Who).Claim is { } claim)
        {
            _waiting = true;
            _pay.Text = $"Pay him {claim.Amount}";
            _pay.Disabled = _runner.Campaign.Port is { } port && port.Funds < claim.Amount;
        }

        Boarded |= beat.Kind == "board";
        Beat?.Invoke(beat.Kind);
        if (beat.Kind == "away")
        {
            _sailing = 0;
            _clock = 0;
        }
    }
}

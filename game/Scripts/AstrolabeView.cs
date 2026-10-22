using Godot;

namespace Expedition.Game;

/// <summary>
/// Taking the sun at noon with the mariner's astrolabe. The sun climbs slowly to its meridian height and
/// falls again; the ship's roll swings the instrument, more in a seaway. The captain marks the reading
/// (Space, or the button) when the sun stands highest and the ring hangs still: the nearer the true
/// height, the more care he took. It reports that care (0..1); the simulation turns it into a latitude.
/// </summary>
public partial class AstrolabeView : Control
{
    private const float Duration = 24f;
    private PanelContainer _panel = null!;
    private AstrolabeDial _dial = null!;
    private Label _hint = null!;
    private Button _mark = null!;
    private float _time;
    private float _peakAt;
    private float _seaState;
    private float _phase;
    private bool _marked;

    /// <summary>The sight is done: how much care it was taken with (0..1).</summary>
    public event Action<double>? Taken;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Theme = Ui.BuildTheme();
        Visible = false;
        _panel = Ui.Frame(Ui.SpaceL, Ui.SpaceL);
        _panel.SetAnchorsPreset(LayoutPreset.Center);
        _panel.OffsetLeft = -300;
        _panel.OffsetRight = 300;
        _panel.OffsetTop = -330;
        _panel.OffsetBottom = 310;
        AddChild(_panel);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", Ui.SpaceM);
        _panel.AddChild(column);
        column.AddChild(Ui.Text("The noon sight", Ui.TextHeading + 2, Ui.Brass));
        _hint = Ui.Text("", Ui.TextBody, Ui.Ink, 570);
        column.AddChild(_hint);
        _dial = new AstrolabeDial { CustomMinimumSize = new Vector2(570, 440) };
        column.AddChild(_dial);
        _mark = Ui.Button("Mark the reading  (Space)", "Read the height of the sun now", Mark);
        column.AddChild(_mark);
    }

    /// <summary>Starts a sight in a sea of <paramref name="seaState"/> (0 calm .. 3 storm).</summary>
    public void Open(float seaState)
    {
        _seaState = seaState;
        _time = 0;
        _marked = false;
        _peakAt = (float)GD.RandRange(9.0, 15.0);
        _phase = (float)GD.RandRange(0, Mathf.Tau);
        _hint.Text = "Hold the ring up and watch the sun climb through the sights. Mark the reading when it stands at its highest, " +
                     "and when the ship's roll leaves the ring still for a moment.";
        _mark.Disabled = false;
        Visible = true;
        Ui.FadeIn(_panel);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && !_marked && @event.IsActionPressed("ui_accept"))
        {
            Mark();
            GetViewport().SetInputAsHandled();
        }
    }

    // The sun's height above its noon height (0 at the peak, falling either side), in the dial's units.
    private float Sun(float t) => -0.006f * (t - _peakAt) * (t - _peakAt);

    // The swing of the instrument with the roll: a long roll and a shorter one, bigger in a seaway.
    private float Roll(float t)
    {
        var amplitude = 0.05f + 0.11f * _seaState;
        return amplitude * (Mathf.Sin(t * 1.25f + _phase) * 0.75f + Mathf.Sin(t * 2.9f + _phase * 1.7f) * 0.25f);
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        if (!_marked)
        {
            _time += (float)delta;
            if (_time >= Duration)
            {
                // The sun is past and falling: whatever the captain makes of it now is a poor sight.
                Finish(0.1, "The sun has gone past its height before you marked it. You take what you can, and it is not much.");
            }
        }

        _dial.Show(Sun(_time) + Roll(_time), Sun(_time), _time / Duration, _marked);
    }

    private void Mark()
    {
        if (_marked)
        {
            return;
        }

        // Care is how near the reading came to the true noon height.
        var error = Mathf.Abs(Sun(_time) + Roll(_time));
        var care = Mathf.Clamp(1 - error / 0.22f, 0, 1);
        var words = care switch
        {
            > 0.85f => "A clean sight: the sun at its height, the ring still. You would stake the ship on it.",
            > 0.6f => "A fair sight. The roll took a little of it, but not much.",
            > 0.35f => "A rough sight: the ring was swinging, or the sun not yet at its height.",
            _ => "A poor sight. You would not want to make a landfall on it.",
        };
        Finish(care, words);
    }

    private async void Finish(double care, string words)
    {
        _marked = true;
        _mark.Disabled = true;
        _hint.Text = words;
        await ToSignal(GetTree().CreateTimer(1.8, processAlways: true), SceneTreeTimer.SignalName.Timeout);
        Visible = false;
        Taken?.Invoke(care);
    }
}

/// <summary>The astrolabe's face: the graduated ring, the alidade's sights, and the sun as seen through them.</summary>
public partial class AstrolabeDial : Control
{
    private static readonly Color Brass = new(0.78f, 0.62f, 0.3f);
    private static readonly Color Dark = new(0.2f, 0.14f, 0.08f);
    private float _reading;
    private float _sun;
    private float _progress;
    private bool _done;

    /// <summary>What to draw: the reading with the roll, the sun's true height, how far through the sight.</summary>
    public void Show(float reading, float sun, float progress, bool done)
    {
        (_reading, _sun, _progress, _done) = (reading, sun, progress, done);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var centre = new Vector2(Size.X / 2, Size.Y * 0.56f);
        var radius = Mathf.Min(Size.X, Size.Y) * 0.42f;
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.55f, 0.68f, 0.8f, 0.35f));
        // The ring and its graduations.
        DrawArc(centre, radius, 0, Mathf.Tau, 72, Brass, 10);
        DrawArc(centre, radius * 0.86f, 0, Mathf.Tau, 72, Brass, 3);
        for (var i = 0; i < 36; i++)
        {
            var a = i * Mathf.Tau / 36;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            DrawLine(centre + dir * radius * 0.86f, centre + dir * radius * (i % 3 == 0 ? 0.76f : 0.8f), Dark, 2);
        }

        DrawLine(centre - new Vector2(radius * 0.86f, 0), centre + new Vector2(radius * 0.86f, 0), new Color(Brass, 0.7f), 4);
        DrawLine(centre - new Vector2(0, radius * 0.86f), centre + new Vector2(0, radius * 0.86f), new Color(Brass, 0.7f), 4);

        // The alidade's sight: the sun should stand on the line; the reading swings it up and down.
        var offset = _reading * radius * 2.2f;
        var sun = centre + new Vector2(0, -offset);
        DrawCircle(sun, 22, new Color(1f, 0.86f, 0.45f, 0.9f));
        DrawCircle(sun, 32, new Color(1f, 0.86f, 0.45f, 0.25f));
        DrawLine(centre - new Vector2(radius * 0.7f, 0), centre + new Vector2(radius * 0.7f, 0), new Color(0.6f, 0.15f, 0.1f), 2.5f);
        DrawString(ThemeDB.FallbackFont, centre + new Vector2(radius * 0.72f, 5), "noon line", HorizontalAlignment.Left, -1, 12, Dark);

        // How far the sun has come through the sight: a bar along the bottom.
        DrawRect(new Rect2(16, Size.Y - 18, Size.X - 32, 6), new Color(Dark, 0.3f));
        DrawRect(new Rect2(16, Size.Y - 18, (Size.X - 32) * Mathf.Clamp(_progress, 0, 1), 6), Brass);
        if (_done)
        {
            // Once marked, where the sun truly stood at its height, for the captain to see how near he came.
            DrawLine(centre - new Vector2(radius * 0.5f, _sun * radius * 2.2f), centre + new Vector2(radius * 0.5f, -_sun * radius * 2.2f), new Color(Dark, 0.5f), 1);
        }
    }
}

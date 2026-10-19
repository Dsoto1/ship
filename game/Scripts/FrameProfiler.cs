using Godot;

namespace Expedition.Game;

/// <summary>
/// A frame-time capture for R3 profiling (PORT_EXPERIENCE §18, §23 step 16). It records every frame for a
/// set time while the camera drifts slowly over the scene. It reports the frame times (mean, median,
/// 95th and 99th percentile, worst), the frames per second, and what was drawn (draw calls, primitives,
/// objects, nodes, video memory), with the machine's GPU and renderer. The report is shown on screen and
/// saved under the user data folder as <c>profiles/&lt;scene&gt;-&lt;time&gt;.txt</c>, so the owner
/// can send the numbers from the Mac. F10 starts one on whatever is in view.
/// </summary>
public partial class FrameProfiler : CanvasLayer
{
    private readonly List<double> _frames = [];
    private readonly List<(double DrawCalls, double Primitives, double Objects)> _drawn = [];
    private Label _label = null!;
    private PanelContainer _panel = null!;
    private string _scene = "view";
    private double _length;
    private double _warmup;
    private double _elapsed;

    /// <summary>True while a capture is running.</summary>
    public bool Capturing { get; private set; }

    /// <summary>How far through the capture, 0..1, for the camera's drift.</summary>
    public float Progress => _length <= 0 ? 0 : (float)Mathf.Clamp(_elapsed / _length, 0, 1);

    /// <summary>The capture has finished: the report, and where it was saved.</summary>
    public event Action<string, string>? Finished;

    /// <summary>The crowd's size, for the report.</summary>
    public Func<int>? Crowd { get; set; }

    public override void _Ready()
    {
        Layer = 30;
        _panel = Ui.PanelOf(Ui.Paper, Ui.Rule, Ui.SpaceL, Ui.SpaceM);
        _panel.Theme = Ui.BuildTheme();
        _panel.Position = new Vector2(Ui.SpaceL, 90);
        _panel.Visible = false;
        AddChild(_panel);
        _label = Ui.Text("", Ui.TextMeta + 1, Ui.Ink, 420);
        _panel.AddChild(_label);
    }

    /// <summary>Starts a capture of <paramref name="seconds"/> after a short settling time.</summary>
    public void Start(string scene, double seconds = 20)
    {
        _scene = scene;
        _length = seconds;
        _warmup = 2;
        _elapsed = 0;
        _frames.Clear();
        _drawn.Clear();
        Capturing = true;
        _panel.Visible = true;
        _label.Text = $"Profiling {scene}: hold still for {seconds:0} s…";
    }

    public override void _Process(double delta)
    {
        if (!Capturing)
        {
            return;
        }

        if (_warmup > 0)
        {
            _warmup -= delta;
            return;
        }

        _elapsed += delta;
        _frames.Add(delta * 1000.0);
        _drawn.Add((Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),
            Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame),
            Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame)));
        _label.Text = $"Profiling {_scene}: {Math.Max(0, _length - _elapsed):0} s left";
        if (_elapsed >= _length)
        {
            Finish();
        }
    }

    private void Finish()
    {
        Capturing = false;
        var sorted = _frames.Order().ToList();
        double At(double share) => sorted.Count == 0 ? 0 : sorted[Math.Clamp((int)Math.Ceiling(share * sorted.Count) - 1, 0, sorted.Count - 1)];
        var mean = sorted.Count == 0 ? 0 : sorted.Average();
        var report = string.Join("\n",
            $"The Expedition: frame profile of '{_scene}'",
            $"When: {DateTime.Now:yyyy-MM-dd HH:mm}",
            $"Machine: {OS.GetName()} {OS.GetVersion()}; {RenderingServer.GetVideoAdapterVendor()} {RenderingServer.GetVideoAdapterName()}",
            $"Renderer: {ProjectSettings.GetSetting("rendering/renderer/rendering_method")}, {RenderingServer.GetVideoAdapterApiVersion()}",
            $"Window: {DisplayServer.WindowGetSize()}; frames captured: {sorted.Count} over {_elapsed:0.0} s",
            "",
            $"Frame time (ms): mean {mean:0.0}, median {At(0.5):0.0}, 95th {At(0.95):0.0}, 99th {At(0.99):0.0}, worst {At(1):0.0}",
            $"Frames per second: {(mean > 0 ? 1000 / mean : 0):0} on average, {(At(0.95) > 0 ? 1000 / At(0.95) : 0):0} at the 95th percentile",
            $"Draw calls: mean {_drawn.Select(d => d.DrawCalls).DefaultIfEmpty().Average():0}, most {_drawn.Select(d => d.DrawCalls).DefaultIfEmpty().Max():0}",
            $"Primitives: mean {_drawn.Select(d => d.Primitives).DefaultIfEmpty().Average():0}; objects: mean {_drawn.Select(d => d.Objects).DefaultIfEmpty().Average():0}",
            $"Nodes: {Performance.GetMonitor(Performance.Monitor.ObjectNodeCount):0}; video memory: {Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed) / 1048576:0} MB; crowd: {Crowd?.Invoke() ?? 0}");

        var folder = "user://profiles";
        DirAccess.MakeDirRecursiveAbsolute(folder);
        var path = $"{folder}/{_scene}-{DateTime.Now:yyyyMMdd-HHmmss}.txt";
        using (var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write))
        {
            file?.StoreString(report + "\n");
        }

        var saved = ProjectSettings.GlobalizePath(path);
        GD.Print(report);
        GD.Print($"PROFILE saved to {saved}");
        _label.Text = report + $"\n\nSaved to {saved}\n(F10 to run again; Esc closes)";
        Finished?.Invoke(report, saved);
    }

    /// <summary>Hides the report.</summary>
    public void Close() => _panel.Visible = Capturing;
}

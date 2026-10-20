using Godot;

namespace Expedition.Game;

/// <summary>
/// The character validation lineup (CHARACTER_ART §50–51, C-1): each persistent person who has a
/// model, standing in a row on the quay stones under the port's daylight, in each state the slice must
/// show: idle, walking, at their work, wet, tired, and injured. Run it with
/// <c>Godot --path game res://Lineup.tscn</c>; add <c>-- --people=person.bartolo,person.velho</c> to
/// choose who, <c>--view=game|close|portrait</c> for the camera, and <c>--screenshot=PATH</c> to save
/// the view and quit. Q/E turn the camera, the wheel zooms.
/// </summary>
public partial class CharacterLineup : Node3D
{
    private static readonly (string Label, Pose Pose, bool Moving, float Wet, float Dirt, float Fatigue, int Injured)[] States =
    [
        ("Idle", Pose.Standing, false, 0, 0, 0, 0),
        ("Walking", Pose.Walking, true, 0, 0, 0, 0),
        ("At work", Pose.Hammering, false, 0, 0.3f, 0, 0),
        ("Wet", Pose.Standing, false, 1, 0.2f, 0, 0),
        ("Tired", Pose.Walking, true, 0, 0.5f, 1, 0),
        ("Injured arm", Pose.Standing, false, 0, 0.2f, 0.4f, -1),
    ];

    private readonly List<(PersonModel Model, int State)> _models = [];
    private string? _screenshot;
    private string _view = "game";
    private double _delay = 3;
    private double _elapsed;
    private Camera3D _camera = null!;
    private float _yaw = 45;
    private float _size = 7;
    private bool _paint = true;
    private int _portraitState;
    private bool _far;
    private bool _bake;
    private List<string> _people = [];

    public override void _Ready()
    {
        var people = new List<string>();
        foreach (var argument in OS.GetCmdlineUserArgs())
        {
            var (key, value) = argument.TrimStart('-').Split('=', 2) is [var k, var v] ? (k, v) : (argument.TrimStart('-'), "");
            switch (key)
            {
                case "people": people = [.. value.Split(',')]; break;
                case "view": _view = value; break;
                case "screenshot": _screenshot = value; break;
                case "paint": _paint = value != "0"; break;
                case "state": _portraitState = int.Parse(value); break;
                case "lod": _far = value == "1"; break;
                case "bake": _bake = true; break;
                case "shotdelay": _delay = double.Parse(value, System.Globalization.CultureInfo.InvariantCulture); break;
            }
        }

        if (people.Count == 0)
        {
            // Everyone with a model.
            using var folder = DirAccess.Open("res://Assets/Characters");
            people = folder?.GetDirectories().Where(d => d.StartsWith("person.")).Order().ToList() ?? [];
        }

        _people = people;
        if (_bake)
        {
            _view = "portrait";
            people = [people[0]];
        }

        var portrait = _view == "portrait";
        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.62f, 0.7f, 0.76f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.62f, 0.66f, 0.72f),
            AmbientLightEnergy = 0.55f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            TonemapExposure = 0.9f,
            TonemapWhite = 6f,
            SsaoEnabled = true,
            SsaoRadius = 0.6f,
            SsaoIntensity = 1.2f,
        };
        AddChild(new WorldEnvironment { Environment = environment });
        if (portrait)
        {
            // The portrait standard (§10–12): a plain warm ground, a soft key from high front-left,
            // a cool fill, a faint rim to lift the head off the ground.
            environment.BackgroundColor = new Color(0.36f, 0.31f, 0.25f);
            environment.AmbientLightColor = new Color(0.55f, 0.52f, 0.5f);
            environment.AmbientLightEnergy = 0.45f;
            environment.SsaoEnabled = false;
            AddChild(new DirectionalLight3D { LightColor = new Color(1f, 0.9f, 0.78f), LightEnergy = 1.5f, ShadowEnabled = true, RotationDegrees = new Vector3(-30, -35, 0) });
            AddChild(new DirectionalLight3D { LightColor = new Color(0.7f, 0.78f, 0.9f), LightEnergy = 0.35f, RotationDegrees = new Vector3(-10, 60, 0) });
            AddChild(new DirectionalLight3D { LightColor = new Color(1f, 0.92f, 0.8f), LightEnergy = 0.8f, RotationDegrees = new Vector3(-20, 160, 0) });
        }
        else
        {
            AddChild(new DirectionalLight3D
            {
                LightColor = new Color(1f, 0.93f, 0.82f),
                LightEnergy = 1.25f,
                ShadowEnabled = true,
                RotationDegrees = new Vector3(-48, 35, 0),
                DirectionalShadowMaxDistance = 40,
            });
        }

        var stones = GD.Load<Texture2D>("res://Assets/Textures/cobblestone_floor_08/cobblestone_floor_08_diff_1k.jpg");
        AddChild(new MeshInstance3D
        {
            Visible = !portrait,
            Mesh = new PlaneMesh { Size = new Vector2(40, 40) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = stones,
                AlbedoColor = new Color(0.86f, 0.8f, 0.72f),
                Uv1Scale = new Vector3(20, 20, 1),
                Roughness = 0.95f,
            },
        });

        for (var p = 0; p < people.Count; p++)
        {
            for (var s = 0; s < (portrait ? 1 : States.Length); s++)
            {
                var model = PersonModel.TryLoad(people[p], _view == "portrait");
                if (model is null)
                {
                    GD.PrintErr($"No model for {people[p]}");
                    continue;
                }

                model.Position = new Vector3((s - (States.Length - 1) / 2f) * 1.3f, 0, p * -2.2f);
                // Three-quarters to the camera, as the game's isometric view mostly sees people.
                model.RotationDegrees = new Vector3(0, 20, 0);
                AddChild(model);
                if (portrait)
                {
                    // Sitting for the portrait: the habitual stoop eased, the head up.
                    model.Posture.Stoop *= 0.35f;
                    model.Posture.HeadForward *= 0.5f;
                    model.Position = new Vector3(0, 0, p * -3f);
                }

                _models.Add((model, portrait ? _portraitState : s));
                var label = new Label3D
                {
                    Text = p == 0 ? States[s].Label : "",
                    FontSize = 28,
                    PixelSize = 0.006f,
                    OutlineSize = 8,
                    Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                    Position = model.Position + new Vector3(0, 2.15f, 0),
                };
                AddChild(label);
            }
        }

        _camera = new Camera3D { Current = true, Far = 200 };
        AddChild(_camera);
        PlaceCamera();
    }

    private void PlaceCamera()
    {
        if (_view == "portrait")
        {
            // A three-quarter head and shoulders of the first model in the idle row.
            _camera.Projection = Camera3D.ProjectionType.Perspective;
            _camera.Fov = 18;
            _camera.Fov = 13;
            var model = _models.FirstOrDefault().Model;
            var head = (model?.GlobalPosition ?? Vector3.Zero) + new Vector3(0, (model?.Height ?? 1.7f) * 0.885f, 0);
            _camera.Position = head + new Vector3(0, 0.02f, 2.6f);
            _camera.LookAt(head + new Vector3(0, -0.07f, 0));
            if (_paint)
            {
                _camera.AddChild(new MeshInstance3D
                {
                    Mesh = new QuadMesh { Size = Vector2.One },
                    MaterialOverride = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/portrait_paint.gdshader") },
                    ExtraCullMargin = 16384,
                    Position = new Vector3(0, 0, -1),
                });
            }

            return;
        }

        _camera.Projection = Camera3D.ProjectionType.Orthogonal;
        _camera.Size = _view == "close" ? 4.5f : _size;
        var forward = new Vector3(Mathf.Sin(Mathf.DegToRad(_yaw)), 0, Mathf.Cos(Mathf.DegToRad(_yaw)));
        var pitch = Mathf.DegToRad(35.264f);
        var target = new Vector3(0, 0.9f, -1.1f * Math.Max(0, _models.Count / States.Length - 1));
        _camera.Position = target + (forward * Mathf.Cos(pitch) + Vector3.Up * Mathf.Sin(pitch)) * 60;
        _camera.LookAt(target);
    }

    // --bake: saves the portrait (§10) and the thumbnail cut from it (§35) into the person's folder,
    // from the window as rendered: Godot --path game res://Lineup.tscn --resolution 1600x900 -- --bake --people=ID
    private void Bake()
    {
        _bake = false;
        var image = GetViewport().GetTexture().GetImage();
        var (w, h) = (image.GetWidth(), image.GetHeight());
        // A 4:5 head-and-shoulders frame, centred on the head.
        var frameH = (int)(h * 0.96f);
        var frameW = frameH * 4 / 5;
        var portrait = image.GetRegion(new Rect2I((w - frameW) / 2, (int)(h * 0.02f), frameW, frameH));
        portrait.Resize(512, 640, Image.Interpolation.Lanczos);
        var folder = ProjectSettings.GlobalizePath($"res://Assets/Characters/{_people[0]}");
        DirAccess.MakeDirRecursiveAbsolute($"{folder}/portraits");
        DirAccess.MakeDirRecursiveAbsolute($"{folder}/thumbnails");
        portrait.SavePng($"{folder}/portraits/neutral.png");
        // The thumbnail is the face, square: recognisable at 64 px.
        var face = portrait.GetRegion(new Rect2I(96, 70, 320, 320));
        face.Resize(128, 128, Image.Interpolation.Lanczos);
        face.SavePng($"{folder}/thumbnails/face.png");
        GD.Print($"Baked {folder}/portraits/neutral.png and thumbnails/face.png");
        GetTree().Quit();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true } key)
        {
            if (key.Keycode == Key.Q) _yaw -= 90;
            if (key.Keycode == Key.E) _yaw += 90;
            PlaceCamera();
        }
        else if (@event is InputEventMouseButton { Pressed: true } wheel)
        {
            if (wheel.ButtonIndex == MouseButton.WheelUp) _size = Mathf.Max(2, _size * 0.9f);
            if (wheel.ButtonIndex == MouseButton.WheelDown) _size = Mathf.Min(30, _size * 1.1f);
            PlaceCamera();
        }
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        foreach (var (model, s) in _models)
        {
            var state = States[s];
            model.Posture.InjuredArm = state.Injured;
            model.Condition(state.Wet, state.Dirt, state.Fatigue);
            model.Show(state.Pose, state.Moving, 1f);
            model.Detail(!_far);
        }

        if (_bake && _elapsed > _delay)
        {
            Bake();
            return;
        }

        if (_screenshot is not null && _elapsed > _delay)
        {
            var image = GetViewport().GetTexture().GetImage();
            image.SavePng(_screenshot);
            GD.Print($"Saved {_screenshot}");
            GetTree().Quit();
            _screenshot = null;
        }
    }
}

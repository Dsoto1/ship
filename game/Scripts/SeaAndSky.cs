using Godot;

namespace Expedition.Game;

/// <summary>
/// The world around the ship: the ocean surface, the sun and moon, ambient light and haze, all set from
/// the simulation's time of day (ART_BIBLE.md 9) and a sea state. The ocean follows the camera so it
/// never ends. Purely visual: it holds no game state.
/// </summary>
public partial class SeaAndSky : Node3D
{
    private const float OceanSize = 260f;
    private const int OceanSubdivisions = 220;

    private ShaderMaterial _ocean = null!;
    private MeshInstance3D _oceanMesh = null!;
    private DirectionalLight3D _sun = null!;
    private DirectionalLight3D _moon = null!;
    private Godot.Environment _environment = null!;
    private Vector2 _flow;

    /// <summary>0 in full daylight, 1 at night. Lanterns use it.</summary>
    public float Darkness { get; private set; }

    /// <summary>Height of the still-water surface relative to the deck.</summary>
    public const float WaterLevel = -1.05f;

    public override void _Ready()
    {
        _environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            TonemapExposure = 0.9f,
            TonemapWhite = 6f,
            GlowEnabled = true,
            GlowIntensity = 0.4f,
            GlowHdrThreshold = 1.4f,
        };
        AddChild(new WorldEnvironment { Environment = _environment });

        _sun = new DirectionalLight3D { ShadowEnabled = true, DirectionalShadowMaxDistance = 80f };
        AddChild(_sun);
        _moon = new DirectionalLight3D { LightColor = new Color(0.55f, 0.65f, 0.95f), RotationDegrees = new Vector3(-60, 140, 0) };
        AddChild(_moon);

        _ocean = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/ocean.gdshader") };
        _oceanMesh = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(OceanSize, OceanSize), SubdivideWidth = OceanSubdivisions, SubdivideDepth = OceanSubdivisions },
            MaterialOverride = _ocean,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_oceanMesh);
    }

    /// <summary>Updates light, water and haze.</summary>
    /// <param name="hourOfDay">0..24 from the simulation clock, with fractions.</param>
    /// <param name="seaState">0 calm, 1 moderate, 2 heavy.</param>
    /// <param name="shipSpeed">Metres per real second the water should stream past the hull.</param>
    /// <param name="delta">Seconds since the last frame.</param>
    /// <param name="focus">Point the camera looks at.</param>
    /// <param name="hull">Hull centre x, centre z, half beam, half length, for the wake.</param>
    public void UpdateWorld(float hourOfDay, float seaState, float shipSpeed, float delta, Vector3 focus, Vector4 hull)
    {
        // Sun height: -1 at midnight, 0 at 6:00 and 18:00, 1 at noon.
        var elevation = Mathf.Sin((hourOfDay - 6f) / 12f * Mathf.Pi);
        var day = Mathf.SmoothStep(-0.3f, 0.22f, elevation);
        var dusk = Mathf.Clamp(1f - Mathf.Abs(elevation + 0.05f) / 0.4f, 0, 1);
        Darkness = 1f - day;

        _sun.RotationDegrees = new Vector3(-Mathf.Max(elevation, 0.05f) * 70f - 8f, -90f + (hourOfDay - 6f) / 12f * 180f, 0);
        _sun.LightColor = new Color(1f, 0.95f, 0.86f).Lerp(new Color(1f, 0.58f, 0.32f), dusk);
        _sun.LightEnergy = 1.35f * Mathf.SmoothStep(-0.12f, 0.3f, elevation);
        _sun.Visible = _sun.LightEnergy > 0.01f;
        _moon.LightEnergy = 0.55f * Darkness;
        _moon.Visible = Darkness > 0.05f;

        var haze = Blend(day, dusk, new Color(0.58f, 0.68f, 0.75f), new Color(0.8f, 0.6f, 0.5f), new Color(0.06f, 0.09f, 0.15f));
        _environment.BackgroundColor = haze;
        _environment.AmbientLightColor = Blend(day, dusk, new Color(0.66f, 0.74f, 0.84f), new Color(0.75f, 0.6f, 0.58f), new Color(0.3f, 0.36f, 0.58f));
        _environment.AmbientLightEnergy = Mathf.Lerp(0.5f, 0.62f, day);

        _ocean.SetShaderParameter("deep_colour", Blend(day, dusk, new Color(0.025f, 0.11f, 0.17f), new Color(0.1f, 0.12f, 0.2f), new Color(0.008f, 0.025f, 0.05f)));
        _ocean.SetShaderParameter("crest_colour", Blend(day, dusk, new Color(0.08f, 0.25f, 0.31f), new Color(0.5f, 0.36f, 0.32f), new Color(0.04f, 0.09f, 0.16f)));
        _ocean.SetShaderParameter("sky_colour", Blend(day, dusk, new Color(0.66f, 0.77f, 0.86f), new Color(0.95f, 0.62f, 0.42f), new Color(0.2f, 0.26f, 0.42f)));
        _ocean.SetShaderParameter("haze_colour", haze);
        _ocean.SetShaderParameter("foam_colour", new Color(0.93f, 0.95f, 0.94f).Lerp(new Color(0.35f, 0.42f, 0.52f), Darkness));
        _ocean.SetShaderParameter("cloud_shadow", 0.3f * day);
        _ocean.SetShaderParameter("sea_state", seaState);

        // The ship sails towards -z, so the water streams towards +z past it.
        _flow += new Vector2(0, -shipSpeed * delta);
        _ocean.SetShaderParameter("flow", _flow);
        _ocean.SetShaderParameter("focus", focus);
        _ocean.SetShaderParameter("hull", hull);

        // Keep the ocean centred on the view, snapped to its vertex spacing so the waves do not shimmer.
        var spacing = OceanSize / (OceanSubdivisions + 1);
        _oceanMesh.Position = new Vector3(Mathf.Snapped(focus.X, spacing), WaterLevel, Mathf.Snapped(focus.Z, spacing));
    }

    // Day colour, warmed towards the dusk colour near sunrise and sunset, darkened towards night.
    private static Color Blend(float day, float dusk, Color noon, Color sunset, Color night) =>
        night.Lerp(noon, day).Lerp(sunset, dusk * 0.5f);
}

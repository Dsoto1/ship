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
    private CpuParticles3D _rain = null!;
    private float _flash;

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
            // A warm southern port: colours a little richer and the light a little harder than the screen gives raw.
            AdjustmentEnabled = true,
            AdjustmentSaturation = 1.16f,
            AdjustmentContrast = 1.06f,
            GlowEnabled = true,
            GlowIntensity = 0.55f,
            GlowHdrThreshold = 1.2f,

            // Contact darkening where things meet (walls and ground, casks, feet), kept light (PORT_VISUAL_PASS §3.8).
            SsaoEnabled = true,
            SsaoRadius = 1.1f,
            SsaoIntensity = 1.6f,
            SsaoPower = 1.6f,
            SsaoDetail = 0.4f,
            SsaoLightAffect = 0.15f,

            // Aerial haze: the far town and the harbour soften with distance (§3.9, §3.12).
            FogEnabled = true,
            FogDensity = 0.004f,
            FogAerialPerspective = 0.35f,
            FogSkyAffect = 0f,
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

        // Rain: thin streaks falling around the view, on in heavy weather.
        _rain = new CpuParticles3D
        {
            Amount = 1800,
            Lifetime = 0.7f,
            Emitting = false,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(30, 1, 30),
            Direction = new Vector3(0.15f, -1, 0.1f),
            Spread = 4f,
            Gravity = new Vector3(0, -30, 0),
            InitialVelocityMin = 18f,
            InitialVelocityMax = 24f,
            Mesh = new BoxMesh { Size = new Vector3(0.02f, 0.7f, 0.02f) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.75f, 0.8f, 0.88f, 0.35f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
        };
        AddChild(_rain);
    }

    /// <summary>A lightning flash, fading over a fraction of a second.</summary>
    public void Flash() => _flash = 1f;

    /// <summary>Updates light, water and haze.</summary>
    /// <param name="hourOfDay">0..24 from the simulation clock, with fractions.</param>
    /// <param name="seaState">0 calm, 1 moderate, 2 heavy.</param>
    /// <param name="shipSpeed">Metres per real second the water should stream past the hull.</param>
    /// <param name="delta">Seconds since the last frame.</param>
    /// <param name="focus">Point the camera looks at.</param>
    /// <param name="hull">Hull centre x, centre z, half beam, half length, for the wake.</param>
    /// <param name="yaw">The ship's swing about the vertical, radians (Godot's sense), for the wake and the flow.</param>
    /// <param name="gloom">0 clear, 1 storm-dark: overcast, grey water, rain, close haze (ART_BIBLE.md 9 "Storm").</param>
    /// <param name="fog">0 clear, 1 thick fog in port: pale haze closing in, the far water gone.</param>
    public void UpdateWorld(float hourOfDay, float seaState, float shipSpeed, float delta, Vector3 focus, Vector4 hull, float gloom = 0f, float fog = 0f, float yaw = 0f)
    {
        // Sun height: 0 at about 05:30 and 19:30 (a summer sailing season in the south), 1 at half past noon.
        var elevation = Mathf.Sin((hourOfDay - 5.5f) / 14f * Mathf.Pi);
        var day = Mathf.SmoothStep(-0.3f, 0.22f, elevation);
        var dusk = Mathf.Clamp(1f - Mathf.Abs(elevation + 0.05f) / 0.4f, 0, 1);
        Darkness = 1f - day;

        // The light of the hour from the table of presets, then overcast for the weather (ART_BIBLE §9).
        var light = Lighting.At(hourOfDay);
        _sun.RotationDegrees = new Vector3(-Mathf.Max(elevation, 0.06f) * 62f - 10f, -90f + (hourOfDay - 5.5f) / 14f * 180f, 0);
        _sun.LightColor = light.Sun.Lerp(new Color(0.85f, 0.88f, 0.92f), gloom * 0.7f);
        _sun.LightEnergy = light.SunEnergy * Mathf.SmoothStep(-0.1f, 0.1f, elevation) * (1f - 0.75f * gloom);
        _sun.Visible = _sun.LightEnergy > 0.01f;
        _sun.ShadowBlur = Mathf.Lerp(1f, 3f, gloom);
        _moon.LightEnergy = 0.45f * Darkness * (1f - 0.6f * gloom);
        _moon.Visible = Darkness > 0.05f;

        _flash = Mathf.Max(0, _flash - delta * 5f);
        var overcast = new Color(0.42f, 0.46f, 0.5f) * Mathf.Lerp(0.3f, 1f, day);
        var haze = light.Haze.Lerp(overcast, gloom).Lerp(new Color(0.74f, 0.76f, 0.76f) * Mathf.Lerp(0.35f, 1f, day), fog * 0.85f);
        _environment.BackgroundColor = haze;
        _environment.AmbientLightColor = light.Ambient.Lerp(new Color(0.58f, 0.62f, 0.66f), gloom);
        _environment.AmbientLightEnergy = light.AmbientEnergy * Mathf.Lerp(1f, 1.15f, gloom) + _flash * 3f;
        _environment.TonemapExposure = light.Exposure * Mathf.Lerp(1f, 0.92f, gloom);
        _environment.FogLightColor = haze;
        // Light mist lies low on the water (height fog); only thick fog closes in on everything.
        _environment.FogDensity = light.FogDensity + gloom * 0.005f + fog * fog * 0.0085f;
        _environment.FogHeight = WaterLevel + 1.2f;
        _environment.FogHeightDensity = fog * 0.6f;
        _rain.Emitting = gloom > 0.55f;
        _rain.Position = focus + new Vector3(0, 14, 0);

        var greyWater = Mathf.Lerp(0.3f, 1f, day);
        _ocean.SetShaderParameter("deep_colour", Blend(day, dusk, new Color(0.03f, 0.15f, 0.2f), new Color(0.1f, 0.12f, 0.2f), new Color(0.008f, 0.025f, 0.05f))
            .Lerp(new Color(0.05f, 0.08f, 0.09f) * greyWater, gloom * 0.8f));
        _ocean.SetShaderParameter("crest_colour", Blend(day, dusk, new Color(0.1f, 0.34f, 0.37f), new Color(0.5f, 0.36f, 0.32f), new Color(0.04f, 0.09f, 0.16f))
            .Lerp(new Color(0.2f, 0.25f, 0.26f) * greyWater, gloom * 0.8f));
        _ocean.SetShaderParameter("haze_start", Mathf.Lerp(Mathf.Lerp(28f, 10f, gloom), 3f, fog));
        _ocean.SetShaderParameter("haze_end", Mathf.Lerp(Mathf.Lerp(70f, 38f, gloom), 16f, fog));
        _ocean.SetShaderParameter("sky_colour", Blend(day, dusk, new Color(0.66f, 0.77f, 0.86f), new Color(0.95f, 0.62f, 0.42f), new Color(0.2f, 0.26f, 0.42f)));
        _ocean.SetShaderParameter("haze_colour", haze);
        _ocean.SetShaderParameter("foam_colour", new Color(0.93f, 0.95f, 0.94f).Lerp(new Color(0.35f, 0.42f, 0.52f), Darkness));
        _ocean.SetShaderParameter("cloud_shadow", 0.3f * day * (1f - gloom));
        _ocean.SetShaderParameter("sea_state", seaState);

        // The ship sails towards her own -z, swung by her yaw, so the water streams astern past her.
        _flow -= new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw)) * shipSpeed * delta;
        _ocean.SetShaderParameter("hull_yaw", yaw);
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

/// <summary>
/// The light at each part of the day (PORT_VISUAL_PASS §3.9, ART_BIBLE §9): naturalistic sources,
/// composed for readability. Dawn has cool haze and a warm low sun; morning, clean light and mild haze;
/// midday, bright stone and short shadows; evening, long warm light; night, cool ambient against warm
/// lanterns. Values blend smoothly between the keys.
/// </summary>
public static class Lighting
{
    /// <summary>One key of the day.</summary>
    public readonly record struct Preset(float Hour, Color Sun, float SunEnergy, Color Ambient, float AmbientEnergy, Color Haze, float FogDensity, float Exposure);

    private static readonly Preset[] Keys =
    [
        new(0f, new Color(0.6f, 0.68f, 0.95f), 0f, new Color(0.24f, 0.3f, 0.5f), 0.42f, new Color(0.05f, 0.07f, 0.13f), 0.0018f, 1.05f),
        // Sunrise at six, the hour the convoy sails (R3 scene D: the departure is a dawn scene).
        new(4.6f, new Color(0.7f, 0.6f, 0.75f), 0f, new Color(0.3f, 0.34f, 0.52f), 0.42f, new Color(0.12f, 0.14f, 0.22f), 0.0035f, 1.0f),
        new(6f, new Color(1f, 0.66f, 0.4f), 1.5f, new Color(0.5f, 0.56f, 0.72f), 0.48f, new Color(0.64f, 0.68f, 0.76f), 0.003f, 0.95f),
        new(9f, new Color(1f, 0.87f, 0.68f), 1.6f, new Color(0.68f, 0.66f, 0.66f), 0.5f, new Color(0.78f, 0.74f, 0.68f), 0.0016f, 0.94f),
        new(13f, new Color(1f, 0.93f, 0.8f), 1.85f, new Color(0.7f, 0.69f, 0.7f), 0.52f, new Color(0.8f, 0.78f, 0.74f), 0.001f, 0.9f),
        new(16.5f, new Color(1f, 0.8f, 0.56f), 1.7f, new Color(0.7f, 0.63f, 0.62f), 0.5f, new Color(0.82f, 0.72f, 0.62f), 0.0014f, 0.93f),
        new(18.6f, new Color(1f, 0.6f, 0.34f), 1.55f, new Color(0.62f, 0.52f, 0.55f), 0.46f, new Color(0.8f, 0.62f, 0.52f), 0.002f, 0.95f),
        new(19.8f, new Color(0.9f, 0.5f, 0.4f), 0.2f, new Color(0.38f, 0.38f, 0.55f), 0.44f, new Color(0.22f, 0.2f, 0.3f), 0.003f, 1.0f),
        new(21f, new Color(0.6f, 0.68f, 0.95f), 0f, new Color(0.25f, 0.31f, 0.52f), 0.42f, new Color(0.06f, 0.08f, 0.14f), 0.0018f, 1.05f),
        new(24f, new Color(0.6f, 0.68f, 0.95f), 0f, new Color(0.24f, 0.3f, 0.5f), 0.42f, new Color(0.05f, 0.07f, 0.13f), 0.0018f, 1.05f),
    ];

    /// <summary>The light at an hour of the day (0..24).</summary>
    public static Preset At(float hour)
    {
        hour = Mathf.PosMod(hour, 24f);
        for (var i = 0; i < Keys.Length - 1; i++)
        {
            var (a, b) = (Keys[i], Keys[i + 1]);
            if (hour > b.Hour)
            {
                continue;
            }

            var t = Mathf.SmoothStep(0f, 1f, (hour - a.Hour) / (b.Hour - a.Hour));
            return new Preset(hour, a.Sun.Lerp(b.Sun, t), Mathf.Lerp(a.SunEnergy, b.SunEnergy, t), a.Ambient.Lerp(b.Ambient, t),
                Mathf.Lerp(a.AmbientEnergy, b.AmbientEnergy, t), a.Haze.Lerp(b.Haze, t), Mathf.Lerp(a.FogDensity, b.FogDensity, t), Mathf.Lerp(a.Exposure, b.Exposure, t));
        }

        return Keys[^1];
    }
}

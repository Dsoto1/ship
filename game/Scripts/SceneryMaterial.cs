using Godot;

namespace Expedition.Game;

/// <summary>
/// The one material for built scenery (D-021, ART_BIBLE §7–8): every box and beam keeps its palette
/// colour, and plaster, stone and timber also get real surface detail from CC0 photographs
/// (<c>scenery.gdshader</c>, <c>Assets/CREDITS.md</c>). Which surface a part is follows from its colour,
/// so the building code never names a texture: pale and warm reads as plaster, grey as stone, brown as
/// timber, anything else (cloth dyes, iron, paint) stays flat. The choice rides in the vertex colour's
/// alpha, so a chunk of scenery in every material is still one draw call.
/// </summary>
public static class SceneryMaterial
{
    /// <summary>Vertex alpha for each surface, as the shader reads it.</summary>
    public const float Flat = 1f, Plaster = 0.75f, Stone = 0.5f, Timber = 0.25f;

    private static ShaderMaterial? _shared;

    /// <summary>The shared material. Its wetness follows the port's rain.</summary>
    public static ShaderMaterial Shared => _shared ??= Create();

    /// <summary>Wets every built surface at once: darker and glossier in rain.</summary>
    public static float Wetness
    {
        set => Shared.SetShaderParameter("wetness", value);
    }

    /// <summary>Marks a colour to stay flat whatever it looks like: cloth, canvas, sails.</summary>
    public static Color Cloth(Color colour) => colour with { A = 0.99f };

    /// <summary>
    /// The surface a palette colour stands for (see the class summary). A colour already marked with
    /// <see cref="Cloth"/> stays flat.
    /// </summary>
    public static float SurfaceOf(Color colour)
    {
        if (colour.A < 0.999f)
        {
            return Flat;
        }

        var (h, s, v) = (colour.H, colour.S, colour.V);
        var warm = h is > 0.04f and < 0.15f;
        if (v >= 0.66f && (s <= 0.32f || (warm && s <= 0.55f)))
        {
            return Plaster;
        }

        if (s <= 0.2f && v is >= 0.25f and < 0.66f)
        {
            return Stone;
        }

        return warm && s >= 0.3f && v is >= 0.12f and <= 0.62f ? Timber : Flat;
    }

    private static ShaderMaterial Create()
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/scenery.gdshader") };
        material.SetShaderParameter("albedo_tex", GD.Load<TextureLayered>("res://Assets/Textures/scenery_array/scenery_diff.jpg"));
        material.SetShaderParameter("normal_tex", GD.Load<TextureLayered>("res://Assets/Textures/scenery_array/scenery_nor_gl.jpg"));
        material.SetShaderParameter("rough_tex", GD.Load<TextureLayered>("res://Assets/Textures/scenery_array/scenery_rough.jpg"));
        return material;
    }
}

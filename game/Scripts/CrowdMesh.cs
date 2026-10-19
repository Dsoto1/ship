using Godot;

namespace Expedition.Game;

/// <summary>
/// The figure for Tier C people (PORT_EXPERIENCE §6): passers-by, hands on other ships, a crowd seeing a
/// ship off. One low mesh drawn many times through a <see cref="MultiMesh"/> and animated by
/// <c>crowd.gdshader</c>, so a crowd of any size is one draw call. They are never characters: they cannot
/// be clicked, have no names and keep no state in the simulation.
/// </summary>
public static class CrowdMesh
{
    /// <summary>Poses the shader knows.</summary>
    public const int Walking = 0, Carrying = 1, Waving = 2, Hauling = 3;

    /// <summary>
    /// Clothing seen in a working port: undyed linen and wool, browns, faded madder and woad, black for
    /// the better off (ART_BIBLE §4.6, kept muted so named people stand out).
    /// </summary>
    public static readonly Color[] Clothing =
    [
        new(0.78f, 0.74f, 0.64f), new(0.62f, 0.56f, 0.46f), new(0.45f, 0.36f, 0.27f), new(0.36f, 0.3f, 0.24f),
        new(0.52f, 0.33f, 0.25f), new(0.6f, 0.45f, 0.3f), new(0.34f, 0.38f, 0.42f), new(0.4f, 0.44f, 0.36f),
        new(0.22f, 0.2f, 0.19f), new(0.68f, 0.62f, 0.5f), new(0.47f, 0.3f, 0.3f), new(0.3f, 0.33f, 0.38f),
    ];

    private static ArrayMesh? _mesh;
    private static ShaderMaterial? _material;

    /// <summary>The shared material; its wetness follows the rain.</summary>
    public static ShaderMaterial Material => _material ??= CreateMaterial();

    /// <summary>A MultiMesh of crowd figures with room for <paramref name="count"/>, none shown yet.</summary>
    public static MultiMeshInstance3D Create(int count) => new()
    {
        Multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            Mesh = _mesh ??= Build(),
            InstanceCount = count,
            VisibleInstanceCount = 0,
        },
        MaterialOverride = Material,
    };

    /// <summary>Soft contact shadows to go under <see cref="Create"/>'s figures: one more draw call for the lot.</summary>
    public static MultiMeshInstance3D CreateShadows(int count) => new()
    {
        Multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = new PlaneMesh { Size = new Vector2(0.9f, 0.9f) },
            InstanceCount = count,
            VisibleInstanceCount = 0,
        },
        MaterialOverride = CrewFigure.ContactShadow,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
    };

    /// <summary>Places figure <paramref name="index"/>: where, facing, how tall, its stride and pose, and its looks.</summary>
    /// <param name="phase">Walk cycle in radians; advance it by distance walked.</param>
    /// <param name="stride">0 standing still .. 1 walking.</param>
    /// <param name="look">Any whole number: picks the clothing and skin.</param>
    public static void Set(MultiMesh multimesh, int index, Vector3 at, float yaw, float scale, float phase, float stride, int pose, int look, MultiMesh? shadows = null)
    {
        shadows?.SetInstanceTransform(index, new Transform3D(Basis.Identity, at + new Vector3(0, 0.025f, 0)));
        multimesh.SetInstanceTransform(index, new Transform3D(Basis.FromEuler(new Vector3(0, yaw, 0)).Scaled(Vector3.One * scale), at));
        var skin = (look * 7 % 10) / 10f;
        multimesh.SetInstanceCustomData(index, new Color(Mathf.PosMod(phase, Mathf.Tau * 2f), stride, pose, Mathf.PosMod(look, Clothing.Length) + skin * 0.9f));
    }

    private static ShaderMaterial CreateMaterial()
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/crowd.gdshader") };
        material.SetShaderParameter("clothing", Clothing.Select(c => c.SrgbToLinear()).Select(c => new Vector3(c.R, c.G, c.B)).ToArray());
        return material;
    }

    // Proportions as CrewFigure's at 1.68 m, with fewer sides: these are seen small and in passing.
    private static ArrayMesh Build()
    {
        const float leg = 0.79f, torso = 0.5f, head = 0.11f;
        var breeches = new Color(0.24f, 0.2f, 0.17f, 0f);
        var shoes = new Color(0.12f, 0.09f, 0.07f, 0f);
        var belt = new Color(0.2f, 0.15f, 0.1f, 0f);
        var cap = new Color(0.38f, 0.3f, 0.24f, 0f);
        var cloth = new Color(1, 1, 1, 1f);
        var skin = new Color(1, 1, 1, 0.5f);
        var sacking = new Color(0.62f, 0.54f, 0.4f, 0f);
        var shoulder = leg + torso - 0.03f;

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var (side, part) in new[] { (-1f, 1), (1f, 2) })
        {
            Add(st, new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.05f, Height = leg - 0.06f, RadialSegments = 6, Rings = 1 }, new Vector3(side * 0.07f, leg - (leg - 0.06f) / 2f, 0), breeches, part, leg);
            Add(st, new BoxMesh { Size = new Vector3(0.09f, 0.06f, 0.2f) }, new Vector3(side * 0.07f, 0.03f, 0.04f), shoes, part, leg);
        }

        Add(st, new CylinderMesh { TopRadius = 0.17f, BottomRadius = 0.15f, Height = torso, RadialSegments = 6, Rings = 1 }, new Vector3(0, leg + torso / 2f, 0), cloth, 0, 0);
        Add(st, new CylinderMesh { TopRadius = 0.153f, BottomRadius = 0.156f, Height = 0.05f, RadialSegments = 6, Rings = 1 }, new Vector3(0, leg + 0.04f, 0), belt, 0, 0);
        foreach (var (side, part) in new[] { (-1f, 3), (1f, 4) })
        {
            Add(st, new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.04f, Height = 0.52f, RadialSegments = 5, Rings = 1 }, new Vector3(side * 0.2f, shoulder - 0.26f, 0), cloth, part, shoulder);
            Add(st, new SphereMesh { Radius = 0.04f, Height = 0.08f, RadialSegments = 5, Rings = 2 }, new Vector3(side * 0.2f, shoulder - 0.55f, 0), skin, part, shoulder);
        }

        var headCentre = leg + torso + 0.07f + head;
        Add(st, new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.06f, Height = 0.08f, RadialSegments = 5, Rings = 1 }, new Vector3(0, leg + torso + 0.03f, 0), skin, 0, 0);
        Add(st, new SphereMesh { Radius = head, Height = head * 2.1f, RadialSegments = 7, Rings = 4 }, new Vector3(0, headCentre, 0), skin, 0, 0);
        Add(st, new SphereMesh { Radius = head * 1.08f, Height = head * 1.3f, RadialSegments = 7, Rings = 3 }, new Vector3(0, headCentre + head * 0.5f, -0.01f), cap, 0, 0);

        // A sack over the right shoulder, shown only when carrying.
        Add(st, new BoxMesh { Size = new Vector3(0.3f, 0.24f, 0.46f) }, new Vector3(0.16f, shoulder + 0.13f, -0.02f), sacking, 5, shoulder);
        return st.Commit();
    }

    private static void Add(SurfaceTool st, PrimitiveMesh shape, Vector3 offset, Color colour, int part, float pivot)
    {
        var arrays = shape.GetMeshArrays();
        var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
        var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
        foreach (var i in indices)
        {
            st.SetColor(colour);
            st.SetUV(new Vector2(part, pivot));
            st.SetNormal(normals[i]);
            st.AddVertex(vertices[i] + offset);
        }
    }
}

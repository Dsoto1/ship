using Godot;

namespace Expedition.Game;

/// <summary>
/// Collects static geometry and commits it as one mesh per material per chunk of the world, so a scene
/// of thousands of boxes, casks and roof slopes costs a few dozen draw calls instead of thousands
/// (PORT_VISUAL_PASS.md §1.6). Chunks keep frustum culling useful. Built once; for scenery that
/// never moves.
/// </summary>
public sealed class MeshBatcher
{
    private const float ChunkSize = 16f;

    private readonly Dictionary<(Material Material, int X, int Z), SurfaceTool> _indexed = [];
    private readonly Dictionary<(Material Material, int X, int Z), SurfaceTool> _loose = [];
    private readonly HashSet<SurfaceTool> _mapped = [];

    /// <summary>Adds a primitive mesh (a box, a cylinder) placed by <paramref name="transform"/>.</summary>
    public void Add(Mesh mesh, Transform3D transform, Material material)
    {
        var tool = Tool(_indexed, material, transform.Origin, indexed: true);
        tool.AppendFrom(mesh, 0, transform);
    }

    /// <summary>
    /// Adds a primitive mesh in a flat colour, sharing one vertex-coloured material with every other
    /// coloured part, so a chunk of scenery in many colours is still one draw call.
    /// </summary>
    public void AddColoured(Mesh mesh, Transform3D transform, Color colour)
    {
        var tool = Tool(_loose, Coloured, transform.Origin, indexed: false);
        if (Coloured == SceneryMaterial.Shared)
        {
            colour.A = SceneryMaterial.SurfaceOf(colour);
        }

        var arrays = mesh.SurfaceGetArrays(0);
        var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
        var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
        // A model's surface may come without an index: its vertices are then its triangles in order.
        if (indices.Length == 0)
        {
            indices = Enumerable.Range(0, vertices.Length).ToArray();
        }

        var basis = transform.Basis;
        foreach (var index in indices)
        {
            tool.SetColor(colour);
            tool.SetNormal((basis * normals[index]).Normalized());
            tool.AddVertex(transform * vertices[index]);
        }
    }

    /// <summary>
    /// The shared material for <see cref="AddColoured"/>: colour from the vertices, with plaster, stone
    /// and timber detail where the colour calls for it (<see cref="SceneryMaterial"/>).
    /// </summary>
    public Material Coloured { get; }

    /// <summary>
    /// A batcher for scenery; or, with <paramref name="ownMaterial"/>, one with a plain vertex-coloured
    /// material of its own, for pieces that fade or change on their own (foreground rigging).
    /// </summary>
    public MeshBatcher(bool ownMaterial = false)
    {
        Coloured = ownMaterial
            ? new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.9f }
            : SceneryMaterial.Shared;
    }

    /// <summary>
    /// Adds loose triangles (roof slopes, gables, ground) with UVs and an optional vertex colour.
    /// Normals are generated on commit, or, in the shared scenery material, made flat here.
    /// </summary>
    public void AddTriangles(Material material, IReadOnlyList<Vector3> vertices, IReadOnlyList<Vector2>? uvs = null, Color? colour = null)
    {
        var centre = Vector3.Zero;
        foreach (var v in vertices) centre += v;
        var tool = Tool(_loose, material, centre / vertices.Count, indexed: false);
        var coloured = material == SceneryMaterial.Shared;
        for (var i = 0; i < vertices.Count; i++)
        {
            if (colour is { } c) tool.SetColor(coloured ? c with { A = SceneryMaterial.SurfaceOf(c) } : c);
            if (uvs is not null)
            {
                tool.SetUV(uvs[i]);
                _mapped.Add(tool);
            }


            // The scenery material's other parts bring their own normals, so these get flat ones here.
            if (coloured && i % 3 == 0 && i + 2 < vertices.Count)
            {
                var normal = (vertices[i + 2] - vertices[i]).Cross(vertices[i + 1] - vertices[i]).Normalized();
                tool.SetNormal(normal);
            }

            tool.AddVertex(vertices[i]);
        }
    }

    /// <summary>Builds the meshes under <paramref name="parent"/>, clears the batcher, and returns what it made.</summary>
    public List<MeshInstance3D> Commit(Node3D parent)
    {
        var made = new List<MeshInstance3D>();
        foreach (var ((material, _, _), tool) in _indexed)
        {
            made.Add(new MeshInstance3D { Mesh = tool.Commit(), MaterialOverride = material });
        }

        foreach (var ((material, _, _), tool) in _loose)
        {
            if (material != Coloured && material != SceneryMaterial.Shared)
            {
                tool.GenerateNormals();

                // Surfaces with UVs (ground, roofs) get tangents for their normal maps.
                if (_mapped.Contains(tool))
                {
                    tool.GenerateTangents();
                }
            }

            made.Add(new MeshInstance3D { Mesh = tool.Commit(), MaterialOverride = material });
        }

        foreach (var instance in made)
        {
            parent.AddChild(instance);
        }

        _indexed.Clear();
        _loose.Clear();
        _mapped.Clear();
        return made;
    }

    private static SurfaceTool Tool(Dictionary<(Material, int, int), SurfaceTool> tools, Material material, Vector3 at, bool indexed)
    {
        var key = (material, Mathf.FloorToInt(at.X / ChunkSize), Mathf.FloorToInt(at.Z / ChunkSize));
        if (!tools.TryGetValue(key, out var tool))
        {
            tool = new SurfaceTool();
            tool.Begin(Mesh.PrimitiveType.Triangles);
            if (!indexed)
            {
                // Loose triangles need their colour channel declared before the first vertex.
                tool.SetColor(Colors.White);
            }

            tools[key] = tool;
        }

        return tool;
    }
}

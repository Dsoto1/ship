using System.Text.Json;
using Godot;

namespace Expedition.Game;

/// <summary>
/// A persistent person's own model (C-1, CHARACTER_ART §14–36, D-028), built offline from their
/// identity record by <c>tools/characters/build.py</c>: Assets/Characters/&lt;person id&gt;/model.glb
/// holds the full mesh, a light one for when the camera is far out, and their tools, all on the
/// shared Quaternius rig. The shared clips (Shared/animations.glb) drive it; on top of whatever clip
/// plays, <see cref="Posture"/> bends it for age and build (from the record), fatigue and injury
/// (from the simulation), in the order §29 gives: base action, body and age, condition. Wetness, dirt
/// and fatigue colour the one shared material per instance. The model reads state; it owns none.
/// </summary>
public partial class PersonModel : Node3D
{
    private const string Folder = "res://Assets/Characters";

    private static readonly HashSet<string> Looping =
    [
        "idle", "walk", "walk_formal", "walk_carry", "talk", "sit", "sit_talk", "kneel_work", "chop", "push",
        "fold_arms", "lantern", "rail", "harvest", "crouch", "torch", "no",
    ];

    private static ShaderMaterial? _material;
    private static AnimationLibrary? _clips;

    private AnimationPlayer _player = null!;
    private Skeleton3D _skeleton = null!;
    private MeshInstance3D _near = null!;
    private MeshInstance3D? _far;
    private readonly List<(MeshInstance3D Mesh, HashSet<string> When)> _equipment = [];
    private string _clip = "";
    private float _walkSpeed = 1f;
    private Identity _identity = new();

    /// <summary>The posture layer: age and build from the record, fatigue and injury set by the caller.</summary>
    public Posture Posture { get; private set; } = null!;

    /// <summary>The person's height in metres, for labels and the selection ring.</summary>
    public float Height { get; private set; } = 1.7f;

    /// <summary>The model for <paramref name="personId"/>, or null when there is none yet.</summary>
    /// <param name="portrait">True for the full-detail mesh portraits are rendered from.</param>
    public static PersonModel? TryLoad(string? personId, bool portrait = false)
    {
        var file = portrait ? "portrait.glb" : "model.glb";
        if (personId is null || !ResourceLoader.Exists($"{Folder}/{personId}/{file}"))
        {
            return null;
        }

        var model = new PersonModel();
        model.Build(personId, file);
        return model;
    }

    private static ShaderMaterial Material => _material ??= new ShaderMaterial
    {
        Shader = GD.Load<Shader>("res://Shaders/character.gdshader"),
    }.Also(m =>
    {
        m.SetShaderParameter("atlas", GD.Load<Texture2D>($"{Folder}/Shared/chr_atlas.png"));
        m.NextPass = Rim;
    });

    /// <summary>The second pass every figure carries for its recruiting rim (outline.gdshader).</summary>
    public static ShaderMaterial Rim => _rim ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/outline.gdshader") };

    private static ShaderMaterial? _rim;

    /// <summary>The rim round the silhouette; clear for none.</summary>
    public void Outline(Color colour)
    {
        foreach (var mesh in Meshes())
        {
            mesh.SetInstanceShaderParameter("rim", colour);
        }
    }

    private static AnimationLibrary Clips
    {
        get
        {
            if (_clips is not null)
            {
                return _clips;
            }

            var scene = GD.Load<PackedScene>($"{Folder}/Shared/animations.glb").Instantiate();
            var player = scene.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
            var library = player?.GetAnimationLibrary("") ?? new AnimationLibrary();
            _clips = (AnimationLibrary)library.Duplicate(true);
            foreach (var name in _clips.GetAnimationList())
            {
                var clip = _clips.GetAnimation(name);
                clip.LoopMode = Looping.Contains(name) ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
            }

            scene.Free();
            return _clips;
        }
    }

    private void Build(string personId, string file)
    {
        _identity = Identity.Load($"{Folder}/{personId}/identity.json");
        Height = _identity.HeightCm / 100f;
        var scene = GD.Load<PackedScene>($"{Folder}/{personId}/{file}").Instantiate<Node3D>();
        // Height by scale: every model is built on the 1.81 m base skeleton.
        scene.Scale = Vector3.One * (Height / 1.81f);
        AddChild(scene);
        _skeleton = (Skeleton3D)scene.FindChild("Skeleton3D", true, false);
        foreach (var mesh in _skeleton.GetChildren().OfType<MeshInstance3D>())
        {
            mesh.MaterialOverride = Material;
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            var item = _identity.Equipment.FirstOrDefault(e => e.Id == mesh.Name);
            if (mesh.Name == "LOD0" || mesh.Name == "Portrait")
            {
                _near = mesh;
            }
            else if (mesh.Name == "LOD1")
            {
                _far = mesh;
                mesh.Visible = false;
            }
            else if (item is not null)
            {
                _equipment.Add((mesh, item.When.ToHashSet()));
                mesh.Visible = false;
            }
        }

        _player = new AnimationPlayer { Name = "Clips" };
        scene.AddChild(_player);
        _player.RootNode = _player.GetPathTo(scene);
        _player.AddAnimationLibrary("", Clips);
        _walkSpeed = _identity.Animation.WalkSpeed;

        Posture = new Posture
        {
            Stoop = _identity.Looks.Posture.Stoop,
            HeadForward = _identity.Looks.Posture.HeadForward,
            Shoulders = _identity.Looks.Posture.Shoulders,
            Limp = _identity.Looks.Posture.Limp switch { "left" => -1, "right" => 1, _ => 0 },
        };
        _skeleton.AddChild(Posture);
        Play(_identity.Animation.Idle, 1f);
    }

    /// <summary>Plays the clip for <paramref name="pose"/>, and shows the tools that work uses.</summary>
    public void Show(Pose pose, bool moving, float rate)
    {
        var clip = pose switch
        {
            Pose.Walking => _identity.Animation.Walk,
            Pose.Carrying or Pose.Rolling when moving => "walk_carry",
            Pose.Carrying => "walk_carry",
            Pose.Hammering or Pose.Working => _identity.Animation.Work,
            Pose.Sawing or Pose.Rolling or Pose.Steadying => "push",
            Pose.Resting or Pose.Sitting => "sit",
            Pose.Lying => "lie",
            Pose.Praying => "kneel_work",
            Pose.Drinking => "drink",
            Pose.Talking => "talk",
            Pose.Roping or Pose.Leaning => "rail",
            Pose.Watching => "fold_arms",
            Pose.Lifting => "pick_up",
            Pose.Inspecting or Pose.Gutting => "interact",
            _ => _identity.Animation.Idle,
        };
        if (moving && pose is not (Pose.Carrying or Pose.Rolling))
        {
            clip = _identity.Animation.Walk;
        }

        // A disabled arm cannot do two-handed work (§24): the injured keep to light work.
        if (Posture.InjuredArm != 0 && clip is "chop" or "push" or "pick_up" or "walk_carry" or "rail")
        {
            clip = moving ? _identity.Animation.Walk : "idle";
        }

        var speed = rate * (clip.StartsWith("walk") ? _walkSpeed : 1f) * (1f - Posture.Fatigue * 0.3f);
        Play(clip, speed);
        foreach (var (mesh, when) in _equipment)
        {
            mesh.Visible = when.Contains(pose.ToString()) && Posture.InjuredArm == 0;
        }
    }

    private void Play(string clip, float speed)
    {
        if (!_player.HasAnimation(clip))
        {
            clip = "idle";
        }

        if (clip != _clip)
        {
            _player.Play(clip, 0.3);
            _clip = clip;
        }

        _player.SpeedScale = Mathf.Max(speed, 0);
    }

    /// <summary>Condition shown on the body: wet, dirty, tired (§25, §48). Each 0..1.</summary>
    public void Condition(float wet, float dirt, float fatigue, bool dead = false)
    {
        Posture.Fatigue = fatigue;
        foreach (var mesh in Meshes())
        {
            mesh.SetInstanceShaderParameter("wet", wet);
            mesh.SetInstanceShaderParameter("dirt", dirt);
            mesh.SetInstanceShaderParameter("fatigue", fatigue);
            mesh.SetInstanceShaderParameter("dead", dead ? 1f : 0f);
        }
    }

    /// <summary>The light model when the camera is far out (§36): identity cues stay, detail goes.</summary>
    public void Detail(bool near)
    {
        _near.Visible = near || _far is null;
        if (_far is not null)
        {
            _far.Visible = !near;
        }
    }

    /// <summary>Triangles in the full and light meshes, for the profile.</summary>
    public (int Near, int Far) Triangles => (Count(_near), _far is null ? 0 : Count(_far));

    private static int Count(MeshInstance3D mesh) =>
        Enumerable.Range(0, mesh.Mesh.GetSurfaceCount()).Sum(s => mesh.Mesh.SurfaceGetArrays(s)[(int)Mesh.ArrayType.Index].AsInt32Array().Length / 3);

    private IEnumerable<MeshInstance3D> Meshes()
    {
        yield return _near;
        if (_far is not null)
        {
            yield return _far;
        }

        foreach (var (mesh, _) in _equipment)
        {
            yield return mesh;
        }
    }

    /// <summary>The parts of the identity record the game reads (the build reads the rest).</summary>
    public sealed class Identity
    {
        public string CharacterId { get; set; } = "";
        public string Name { get; set; } = "";
        public int HeightCm { get; set; } = 170;
        public LooksRecord Looks { get; set; } = new();
        public List<EquipmentRecord> Equipment { get; set; } = [];
        public AnimationRecord Animation { get; set; } = new();

        public static Identity Load(string path)
        {
            using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
            return file is null
                ? new Identity()
                : JsonSerializer.Deserialize<Identity>(file.GetAsText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new Identity();
        }
    }

    public sealed class LooksRecord
    {
        public PostureRecord Posture { get; set; } = new();
    }

    public sealed class PostureRecord
    {
        public float Stoop { get; set; }
        public float HeadForward { get; set; }
        public float Shoulders { get; set; }
        public string? Limp { get; set; }
    }

    public sealed class EquipmentRecord
    {
        public string Id { get; set; } = "";
        public List<string> When { get; set; } = [];
    }

    public sealed class AnimationRecord
    {
        public string Walk { get; set; } = "walk";
        public float WalkSpeed { get; set; } = 1f;
        public string Work { get; set; } = "kneel_work";
        public string Idle { get; set; } = "idle";
    }
}

/// <summary>
/// The layer over every clip (CHARACTER_ART §29–30): a stoop and a forward head for age and trade,
/// rounded shoulders, the slump of fatigue, and an injured arm held in to the body. Each is a bend of
/// the spine, neck, head or arm in the body's own frame, added after the clip has posed the skeleton.
/// </summary>
public partial class Posture : SkeletonModifier3D
{
    /// <summary>0..1: how bent the back is (Bartolo's forty years over a plank).</summary>
    public float Stoop { get; set; }

    /// <summary>0..1: the head carried forward of the shoulders.</summary>
    public float HeadForward { get; set; }

    /// <summary>0..1: the shoulders rolled forward.</summary>
    public float Shoulders { get; set; }

    /// <summary>0..1: tiredness, from the simulation: the head drops, the back gives.</summary>
    public float Fatigue { get; set; }

    /// <summary>-1 the left arm, 1 the right, 0 neither: held bent against the body.</summary>
    public int InjuredArm { get; set; }

    /// <summary>-1 the left leg, 1 the right, 0 neither: a stiff knee that will not bend (an old injury
    /// in the record, like Andrés's, or a sprain): the walk shortens on that side and the hip hitches.</summary>
    public int Limp { get; set; }

    private int[]? _spine;
    private int _neck, _head, _clavicleL, _clavicleR, _upperL, _upperR, _lowerL, _lowerR, _thighL, _thighR, _calfL, _calfR, _pelvis;

    public override void _ProcessModificationWithDelta(double delta)
    {
        var skeleton = GetSkeleton();
        if (skeleton is null)
        {
            return;
        }

        if (_spine is null)
        {
            _spine = [skeleton.FindBone("spine_01"), skeleton.FindBone("spine_02"), skeleton.FindBone("spine_03")];
            _neck = skeleton.FindBone("neck_01");
            _head = skeleton.FindBone("Head");
            _clavicleL = skeleton.FindBone("clavicle_l");
            _clavicleR = skeleton.FindBone("clavicle_r");
            _upperL = skeleton.FindBone("upperarm_l");
            _upperR = skeleton.FindBone("upperarm_r");
            _lowerL = skeleton.FindBone("lowerarm_l");
            _lowerR = skeleton.FindBone("lowerarm_r");
            _thighL = skeleton.FindBone("thigh_l");
            _thighR = skeleton.FindBone("thigh_r");
            _calfL = skeleton.FindBone("calf_l");
            _calfR = skeleton.FindBone("calf_r");
            _pelvis = skeleton.FindBone("pelvis");
        }

        if (Limp != 0)
        {
            // The bad knee stays nearly straight and that thigh swings less; the hip hitches to clear the foot.
            var left = Limp < 0;
            var calf = left ? _calfL : _calfR;
            var thigh = left ? _thighL : _thighR;
            var bend = skeleton.GetBonePoseRotation(calf);
            var rest = skeleton.GetBoneRest(calf).Basis.GetRotationQuaternion();
            skeleton.SetBonePoseRotation(calf, rest.Slerp(bend, 0.25f));
            var swing = skeleton.GetBonePoseRotation(thigh);
            var thighRest = skeleton.GetBoneRest(thigh).Basis.GetRotationQuaternion();
            var lift = 1f - rest.Slerp(bend, 1f).AngleTo(rest) / Mathf.Pi;
            skeleton.SetBonePoseRotation(thigh, thighRest.Slerp(swing, 0.6f));
            Bend(skeleton, _pelvis, new Vector3(0, 0, 1), Mathf.DegToRad((left ? -1 : 1) * 4f * (1f - lift)));
        }

        var tired = Fatigue * Fatigue;
        var back = Mathf.DegToRad(7f * Stoop + 5f * tired);
        foreach (var bone in _spine)
        {
            Bend(skeleton, bone, new Vector3(1, 0, 0), back);
        }

        Bend(skeleton, _neck, new Vector3(1, 0, 0), Mathf.DegToRad(10f * HeadForward + 6f * tired - 4f * Stoop));
        Bend(skeleton, _head, new Vector3(1, 0, 0), Mathf.DegToRad(-8f * HeadForward + 10f * tired - 6f * Stoop));
        var roll = Mathf.DegToRad(8f * Shoulders + 6f * tired);
        // Rounded shoulders: each clavicle swings forward about the vertical (+x is the left side).
        Bend(skeleton, _clavicleL, new Vector3(0, 1, 0), -roll);
        Bend(skeleton, _clavicleR, new Vector3(0, 1, 0), roll);

        if (InjuredArm != 0)
        {
            // The upper arm hangs close to the side, the forearm across the belly.
            var left = InjuredArm < 0;
            Point(skeleton, left ? _upperL : _upperR, new Vector3(left ? 0.12f : -0.12f, -1f, 0.18f));
            Point(skeleton, left ? _lowerL : _lowerR, new Vector3(left ? -0.75f : 0.75f, 0.12f, 0.65f));
        }
    }

    // Turns a bone (and so everything below it) by angle about an axis in the skeleton's frame.
    private static void Bend(Skeleton3D skeleton, int bone, Vector3 axis, float angle)
    {
        if (bone < 0 || Mathf.IsZeroApprox(angle))
        {
            return;
        }

        var parent = skeleton.GetBoneParent(bone);
        var parentRotation = parent < 0 ? Quaternion.Identity : skeleton.GetBoneGlobalPose(parent).Basis.GetRotationQuaternion();
        var turn = new Quaternion(axis.Normalized(), angle);
        var local = skeleton.GetBonePoseRotation(bone);
        skeleton.SetBonePoseRotation(bone, (parentRotation.Inverse() * turn * parentRotation * local).Normalized());
    }

    // Turns a bone so it points along a direction in the skeleton's frame (bones run along their +Y).
    private static void Point(Skeleton3D skeleton, int bone, Vector3 direction)
    {
        if (bone < 0)
        {
            return;
        }

        var global = skeleton.GetBoneGlobalPose(bone).Basis.GetRotationQuaternion();
        var along = global * Vector3.Up;
        var turn = new Quaternion(along.Normalized(), direction.Normalized());
        var parent = skeleton.GetBoneParent(bone);
        var parentRotation = parent < 0 ? Quaternion.Identity : skeleton.GetBoneGlobalPose(parent).Basis.GetRotationQuaternion();
        skeleton.SetBonePoseRotation(bone, (parentRotation.Inverse() * turn * global).Normalized());
    }
}

internal static class Fluent
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}

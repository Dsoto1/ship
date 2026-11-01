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
        "fold_arms", "lantern", "rail", "call", "harvest", "crouch", "torch", "no",
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

    /// <summary>
    /// Colours for a crowd kit's dye slots (the main garment, the lower one, the headwear, the shirt); a clear colour
    /// keeps what the kit was built in. Named people have no slots, so this does nothing to them.
    /// </summary>
    public void Dye(Color main, Color lower, Color head, Color shirt)
    {
        foreach (var mesh in Meshes())
        {
            mesh.SetInstanceShaderParameter("dye_main", main);
            mesh.SetInstanceShaderParameter("dye_lower", lower);
            mesh.SetInstanceShaderParameter("dye_head", head);
            mesh.SetInstanceShaderParameter("dye_shirt", shirt);
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

            // Walks from Meshy's library, retargeted onto this rig (tools/characters/retarget_meshy.gd), with the
            // stride each covers at speed 1 (walks.json), so that every walk is paced to the ground.
            if (DirAccess.Open(WalksFolder) is { } walks)
            {
                foreach (var file in walks.GetFiles().Where(f => f.EndsWith(".res") || f.EndsWith(".res.remap")))
                {
                    var name = file.Replace(".remap", "")[..^4];
                    if (ResourceLoader.Load($"{WalksFolder}/{name}.res") is Animation walk)
                    {
                        _clips.AddAnimation(name, walk);
                    }
                }

                if (Godot.FileAccess.FileExists($"{WalksFolder}/walks.json")
                    && Json.ParseString(Godot.FileAccess.GetFileAsString($"{WalksFolder}/walks.json")).AsGodotDictionary() is { } meta)
                {
                    foreach (var (name, entry) in meta)
                    {
                        // Measured as the clips above were (0.977 for walk), then given the same tuning (to 1.01).
                        Strides[name.AsString()] = (float)entry.AsGodotDictionary()["stride"].AsDouble() * (StrideSpeed / 0.977f);
                    }
                }
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
        // Height by scale: every model is built on its base skeleton, 1.81 m for men and 1.77 m for women.
        _scale = Height / (_identity.Sex == "Female" ? 1.767f : 1.81f);
        scene.Scale = Vector3.One * _scale;
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
        _walk = ChooseWalk(personId);
        _footLock = new FootLock();
        _skeleton.AddChild(_footLock);
        Play(_identity.Animation.Idle, 1f);
    }

    /// <summary>Ground covered by the walk clips at speed 1, metres a second, for the base skeleton (1.81 m): a walk
    /// is played at the speed the figure is really moving over the ground divided by this, so feet do not slide.
    /// Measured from the clips (the planted ankle moves back at 0.98 m/s in walk and walk_formal, 0.65 in walk_carry), then
    /// tuned in the game, where 1.01 leaves the planted foot slipping least (about 5% of the pace; 1.25, as it was, 23%).</summary>
    public const float StrideSpeed = 1.01f;

    private const float CarryStride = 0.67f;
    private const string WalksFolder = "res://Assets/Characters/Shared/walks";

    // The ground each extra walk covers at speed 1, by clip.
    private static readonly Dictionary<string, float> Strides = [];

    /// <summary>For trying walks (--walkclip): every walking figure uses this clip, when set.</summary>
    public static string? ForcedWalk { get; set; }

    private string _walk = "walk";

    /// <summary>How they walk now (from the simulation): Usual uses their own walk.</summary>
    public Expedition.Simulation.World.Gait Gait { get; set; }

    private static string? GaitClip(Expedition.Simulation.World.Gait gait) => gait switch
    {
        Expedition.Simulation.World.Gait.Elderly => "walk_elderly",
        Expedition.Simulation.World.Gait.Old => "walk_thoughtful",
        Expedition.Simulation.World.Gait.Injured => "walk_injured",
        Expedition.Simulation.World.Gait.Ill => "walk_unsteady",
        _ => null,
    };

    // Each person's own walk, from their record: a limp if they have one; a woman's walk or an easy one for women;
    // a proud strut or the formal walk for those who walk formally; an easy walk or the plain one for the old; and
    // for the rest one of the quicker or plainer walks, the same one every time (picked by their id). The slower walks
    // (elderly, old, injured, ill) come from their state now, by Gait, at the pace the simulation walks them.
    private string ChooseWalk(string personId)
    {
        var own = _identity.Animation.Walk;
        var seed = personId.Aggregate(17u, (h, c) => h * 31u + c);
        string Pick(params string[] choices)
        {
            var have = choices.Where(c => _player.HasAnimation(c)).ToArray();
            return have.Length == 0 ? own : have[seed % (uint)have.Length];
        }

        if (_identity.Looks.Posture.Limp is "left" or "right" && _player.HasAnimation("walk_limp"))
        {
            // The clip limps; the posture's own stiff knee would limp twice over.
            Posture.Limp = 0;
            return "walk_limp";
        }

        return _identity.Sex == "Female" ? Pick("walk_woman", "walk_casual")
            : own == "walk_formal" ? Pick("walk_proud", "walk_formal")
            : _identity.Age >= 55 ? Pick("walk_casual", "walk")
            : _identity.Age is > 0 and < 30 ? Pick("walk_quick", "walk_meshy", "walk_plain", "walk")
            : Pick("walk", "walk_casual", "walk_plain", "walk_meshy");
    }

    /// <summary>How quick this person is on their feet, against the usual (from their identity).</summary>
    public float WalkSpeed => _walkSpeed;

    private float _scale = 1f;
    private FootLock _footLock = null!;

    /// <summary>Where the feet are in the world (for checking that a planted foot stays put).</summary>
    public (Vector3 Left, Vector3 Right) Feet => _footLock.Placed != default ? _footLock.Placed : (_skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(_skeleton.FindBone("foot_l")).Origin,
        _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(_skeleton.FindBone("foot_r")).Origin);

    /// <summary>The walk clip's speed for moving over the ground at <paramref name="metresPerSecond"/>.</summary>
    public static float WalkRate(float metresPerSecond) => metresPerSecond <= 0.01f ? 0f : Mathf.Clamp(metresPerSecond / StrideSpeed, 0.1f, 6f);

    /// <summary>Plays the clip for <paramref name="pose"/>, and shows the tools that work uses. With
    /// <paramref name="groundMatched"/>, a walk's <paramref name="rate"/> is already matched to the ground.</summary>
    public void Show(Pose pose, bool moving, float rate, bool groundMatched = false)
    {
        var clip = pose switch
        {
            Pose.Walking => _walk,
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
            Pose.Waving => "call",
            Pose.Lifting => "pick_up",
            Pose.Inspecting or Pose.Gutting => "interact",
            _ => _identity.Animation.Idle,
        };
        if (moving && pose is not (Pose.Carrying or Pose.Rolling))
        {
            clip = _walk;
        }

        // A disabled arm cannot do two-handed work (§24): the injured keep to light work.
        if (Posture.InjuredArm != 0 && clip is "chop" or "push" or "pick_up" or "walk_carry" or "rail")
        {
            clip = moving ? _walk : "idle";
        }

        var walking = clip.StartsWith("walk");
        // Ground-matched, the rate is for the base stride: a shorter person's stride is shorter, and carrying, shorter still.
        // How they walk now, from the simulation (age, injury, illness): the library's slower walks.
        if (walking && clip != "walk_carry" && GaitClip(Gait) is { } slow && _player.HasAnimation(slow))
        {
            clip = slow;
        }

        if (walking && ForcedWalk is { } forced && clip != "walk_carry" && _player.HasAnimation(forced))
        {
            clip = forced;
        }

        var stride = (clip == "walk_carry" ? StrideSpeed / CarryStride : Strides.TryGetValue(clip, out var own) && own > 0.1f ? StrideSpeed / own : 1f) / _scale;
        _footLock.Locking = walking && groundMatched && moving;
        var speed = walking && groundMatched && moving ? rate * stride : rate * (walking ? _walkSpeed : 1f) * (1f - Posture.Fatigue * 0.3f);
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
        public string Sex { get; set; } = "Male";
        public int HeightCm { get; set; } = 170;
        public int Age { get; set; }
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

/// <summary>
/// Keeps a planted foot where it was put down. Walking, the clip's stride never quite matches the ground
/// covered (it is played to the figure's pace, which rises, falls and turns), so a foot on the ground drifts.
/// When a foot comes down this pins it to that spot in the world, bending the leg (thigh and calf, a two-bone
/// reach) to keep it there until the clip lifts it, then lets it go over a few frames. It runs after the
/// clip and the posture, only while walking, and lets go of a foot that would have to stretch too far.
/// </summary>
public partial class FootLock : SkeletonModifier3D
{
    /// <summary>Whether to lock feet (while walking, at a pace matched to the ground).</summary>
    public bool Locking { get; set; }

    private sealed class Leg
    {
        public int Thigh, Calf, Foot;
        public bool Planted;
        public Vector3 At;          // where the planted foot is held, in the world
        public float Hold;          // 0..1: how firmly (eases in and out)
        public float Lowest = float.MaxValue;
    }

    private Leg[]? _legs;

    /// <summary>Where the feet were left this frame, in the world (after the lock), for checking.</summary>
    public (Vector3 Left, Vector3 Right) Placed { get; private set; }

    public override void _ProcessModificationWithDelta(double delta)
    {
        var skeleton = GetSkeleton();
        if (skeleton is null)
        {
            return;
        }

        _legs ??= [Find(skeleton, "l"), Find(skeleton, "r")];
        var toWorld = skeleton.GlobalTransform;
        var toSkeleton = toWorld.AffineInverse();
        var scale = toWorld.Basis.Scale.Y;
        foreach (var leg in _legs)
        {
            if (leg.Thigh < 0 || leg.Calf < 0 || leg.Foot < 0)
            {
                continue;
            }

            var foot = skeleton.GetBoneGlobalPose(leg.Foot);
            var world = toWorld * foot.Origin;
            if (!Locking)
            {
                leg.Planted = false;
                leg.Hold = 0;
                leg.Lowest = float.MaxValue;
                continue;
            }

            // Down when the clip has the ankle at the bottom of its swing.
            leg.Lowest = Mathf.Min(leg.Lowest, foot.Origin.Y);
            var down = foot.Origin.Y < leg.Lowest + 0.025f;
            if (down && !leg.Planted)
            {
                // Held at once (it is held where it is), let go of over a few frames.
                leg.Planted = true;
                leg.At = world;
                leg.Hold = 1f;
            }
            else if (!down)
            {
                leg.Planted = false;
            }

            // Too far from where the clip has it (a sharp turn, a jolt): let go and plant afresh.
            if (leg.Planted && (leg.At - world).Length() > 0.18f * scale)
            {
                leg.At = world;
            }

            leg.Hold = Mathf.MoveToward(leg.Hold, leg.Planted ? 1f : 0f, (float)delta * 8f);
            if (leg.Hold <= 0.001f)
            {
                continue;
            }

            var target = (toSkeleton * leg.At).Lerp(foot.Origin, 1f - leg.Hold);
            Reach(skeleton, leg, target, foot.Basis.GetRotationQuaternion());
        }

        Placed = (toWorld * skeleton.GetBoneGlobalPose(_legs[0].Foot).Origin, toWorld * skeleton.GetBoneGlobalPose(_legs[1].Foot).Origin);
    }

    private static Leg Find(Skeleton3D skeleton, string side) => new()
    {
        Thigh = skeleton.FindBone($"thigh_{side}"), Calf = skeleton.FindBone($"calf_{side}"), Foot = skeleton.FindBone($"foot_{side}"),
    };

    // Two-bone reach, in the skeleton's frame: bend the knee to the length wanted, swing the thigh to point
    // the leg at the target, and leave the foot as the clip turned it (so the sole stays flat).
    private static void Reach(Skeleton3D skeleton, Leg leg, Vector3 target, Quaternion footTurn)
    {
        var a = skeleton.GetBoneGlobalPose(leg.Thigh).Origin;
        var b = skeleton.GetBoneGlobalPose(leg.Calf).Origin;
        var c = skeleton.GetBoneGlobalPose(leg.Foot).Origin;
        var upper = (b - a).Length();
        var lower = (c - b).Length();
        var reach = Mathf.Clamp((target - a).Length(), Mathf.Abs(upper - lower) + 0.001f, upper + lower - 0.001f);
        var knee = (a - b).AngleTo(c - b);
        var wanted = Mathf.Acos(Mathf.Clamp((upper * upper + lower * lower - reach * reach) / (2f * upper * lower), -1f, 1f));
        var axis = (b - a).Cross(c - b);
        if (axis.LengthSquared() < 1e-8f)
        {
            axis = skeleton.GetBoneGlobalPose(leg.Thigh).Basis.X;
        }

        Turn(skeleton, leg.Calf, new Quaternion(axis.Normalized(), knee - wanted));
        c = skeleton.GetBoneGlobalPose(leg.Foot).Origin;
        if ((c - a).LengthSquared() > 1e-8f && (target - a).LengthSquared() > 1e-8f)
        {
            Turn(skeleton, leg.Thigh, new Quaternion((c - a).Normalized(), (target - a).Normalized()));
        }

        var parent = skeleton.GetBoneGlobalPose(leg.Calf).Basis.GetRotationQuaternion();
        skeleton.SetBonePoseRotation(leg.Foot, (parent.Inverse() * footTurn).Normalized());
    }

    // Turns a bone (and everything below it) by a rotation given in the skeleton's frame.
    private static void Turn(Skeleton3D skeleton, int bone, Quaternion turn)
    {
        var parent = skeleton.GetBoneParent(bone);
        var parentRotation = parent < 0 ? Quaternion.Identity : skeleton.GetBoneGlobalPose(parent).Basis.GetRotationQuaternion();
        var global = skeleton.GetBoneGlobalPose(bone).Basis.GetRotationQuaternion();
        skeleton.SetBonePoseRotation(bone, (parentRotation.Inverse() * turn.Normalized() * global).Normalized());
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

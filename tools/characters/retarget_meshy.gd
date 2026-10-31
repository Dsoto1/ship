extends SceneTree
# Retargets a Meshy animation (its 22-bone humanoid rig) onto the shared Quaternius rig our people use, and saves it
# as a looping clip beside Characters/Shared/animations.glb. Both rigs stand in a T-pose, Y up, facing +Z, and the
# Meshy rig was fitted to the Quaternius base figure itself, so each bone's turn away from its rest pose, in the
# world, carries straight over. The hips' rise and fall and sway carry over, scaled to our pelvis height. The
# stride (how fast the planted ankle moves back at speed 1) is measured and written to walks.json, for pacing.
#
#   godot --headless --path game --script ../tools/characters/retarget_meshy.gd -- IN.glb CLIP_NAME
#
# IN.glb is a Meshy animation export (an animate task's result.animation_glb_url, or a rig's basic walk).

const TARGET := "res://Assets/Characters/Shared/animations.glb"
const OUT := "res://Assets/Characters/Shared/walks/"
const FPS := 30.0
const MAP := {
	"Hips": "pelvis", "Spine": "spine_01", "Spine01": "spine_02", "Spine02": "spine_03", "neck": "neck_01", "Head": "Head",
	"LeftShoulder": "clavicle_l", "LeftArm": "upperarm_l", "LeftForeArm": "lowerarm_l", "LeftHand": "hand_l",
	"RightShoulder": "clavicle_r", "RightArm": "upperarm_r", "RightForeArm": "lowerarm_r", "RightHand": "hand_r",
	"LeftUpLeg": "thigh_l", "LeftLeg": "calf_l", "LeftFoot": "foot_l", "LeftToeBase": "ball_l",
	"RightUpLeg": "thigh_r", "RightLeg": "calf_r", "RightFoot": "foot_r", "RightToeBase": "ball_r",
}

func node_chain(n) -> Transform3D:
	var t := Transform3D()
	while n is Node3D:
		t = n.transform * t
		n = n.get_parent()
	return t

# A bone's pose at time t from an animation's tracks (or its rest where there is no track).
func local_pose(skel: Skeleton3D, anim: Animation, i: int, t: float) -> Transform3D:
	var nm := skel.get_bone_name(i)
	var rest := skel.get_bone_rest(i)
	var pos := rest.origin
	var rot := rest.basis.get_rotation_quaternion()
	var sc := rest.basis.get_scale()
	if anim != null:
		for k in anim.get_track_count():
			if not str(anim.track_get_path(k)).ends_with(":" + nm):
				continue
			match anim.track_get_type(k):
				Animation.TYPE_POSITION_3D: pos = anim.position_track_interpolate(k, t)
				Animation.TYPE_ROTATION_3D: rot = anim.rotation_track_interpolate(k, t)
				Animation.TYPE_SCALE_3D: sc = anim.scale_track_interpolate(k, t)
	return Transform3D(Basis(rot).scaled(sc), pos)

func global_pose(skel: Skeleton3D, anim: Animation, i: int, t: float) -> Transform3D:
	var tr := local_pose(skel, anim, i, t)
	var p := skel.get_bone_parent(i)
	while p >= 0:
		tr = local_pose(skel, anim, p, t) * tr
		p = skel.get_bone_parent(p)
	return node_chain(skel) * tr

func world_rest(skel: Skeleton3D, i: int) -> Transform3D:
	return node_chain(skel) * skel.get_bone_global_rest(i)

func _init():
	var args := OS.get_cmdline_user_args()
	if args.size() < 2:
		print("usage: -- IN.glb CLIP_NAME")
		quit(1)
		return
	var src_scene: Node = load(args[0]).instantiate()
	var ss: Skeleton3D = src_scene.find_children("*", "Skeleton3D", true, false)[0]
	var sp: AnimationPlayer = src_scene.find_child("AnimationPlayer", true, false)
	var sa: Animation = sp.get_animation(sp.get_animation_list()[0])
	var tgt_scene: Node = load(TARGET).instantiate()
	var ts: Skeleton3D = tgt_scene.find_children("*", "Skeleton3D", true, false)[0]
	var tp: AnimationPlayer = tgt_scene.find_child("AnimationPlayer", true, false)
	var prefix := str(tp.get_animation("walk").track_get_path(0)).split(":")[0]

	var src_of := {}
	for s in MAP:
		src_of[MAP[s]] = ss.find_bone(s)
	var hips := ss.find_bone("Hips")
	var pelvis := ts.find_bone("pelvis")
	var height := world_rest(ts, pelvis).origin.y / world_rest(ss, hips).origin.y

	var out := Animation.new()
	out.length = sa.length
	out.loop_mode = Animation.LOOP_LINEAR
	var tracks := {}
	for i in ts.get_bone_count():
		if src_of.has(ts.get_bone_name(i)):
			var k := out.add_track(Animation.TYPE_ROTATION_3D)
			out.track_set_path(k, NodePath(prefix + ":" + ts.get_bone_name(i)))
			tracks[i] = k
	var hips_track := out.add_track(Animation.TYPE_POSITION_3D)
	out.track_set_path(hips_track, NodePath(prefix + ":pelvis"))

	var frames := int(round(sa.length * FPS))
	var tnode := node_chain(ts)
	# A walk that travels (root motion in the hips) is put in place: the game moves the figure itself.
	var drift := global_pose(ss, sa, hips, sa.length).origin - global_pose(ss, sa, hips, 0.0).origin
	drift.y = 0
	for f in frames + 1:
		var t: float = min(sa.length, f / FPS)
		var world := {}  # target bone -> world rotation this frame
		for i in ts.get_bone_count():
			var parent := ts.get_bone_parent(i)
			var parent_rot: Quaternion = (tnode.basis.get_rotation_quaternion() if parent < 0 else world[parent])
			var name := ts.get_bone_name(i)
			var rot: Quaternion
			if src_of.has(name) and src_of[name] >= 0:
				var s: int = src_of[name]
				var turn: Quaternion = global_pose(ss, sa, s, t).basis.get_rotation_quaternion() * world_rest(ss, s).basis.get_rotation_quaternion().inverse()
				rot = (turn * world_rest(ts, i).basis.get_rotation_quaternion()).normalized()
				out.rotation_track_insert_key(tracks[i], t, (parent_rot.inverse() * rot).normalized())
			else:
				rot = (parent_rot * ts.get_bone_rest(i).basis.get_rotation_quaternion()).normalized()
			world[i] = rot
		# The hips: the source's offset from its rest, scaled to our height, in the pelvis's parent space.
		var offset := (global_pose(ss, sa, hips, t).origin - drift * (t / sa.length) - world_rest(ss, hips).origin) * height
		var parent_world := tnode * (ts.get_bone_global_rest(ts.get_bone_parent(pelvis)) if ts.get_bone_parent(pelvis) >= 0 else Transform3D())
		var pelvis_world := world_rest(ts, pelvis).origin + offset
		out.position_track_insert_key(hips_track, t, parent_world.affine_inverse() * pelvis_world)

	# Stride: how fast the planted ankle moves back, at speed 1.
	var foot := ts.find_bone("foot_l")
	var samples := []
	for f in 241:
		var t: float = out.length * f / 240.0
		samples.append([t, global_pose(ts, out, foot, t).origin])
	var lowest := 1e9
	for s in samples:
		lowest = min(lowest, s[1].y)
	var speeds := []
	for k in range(1, samples.size()):
		if samples[k][1].y < lowest + 0.015 and samples[k - 1][1].y < lowest + 0.015:
			speeds.append((samples[k][1] - samples[k - 1][1]).length() / (samples[k][0] - samples[k - 1][0]))
	speeds.sort()
	var stride: float = speeds[speeds.size() / 2] if speeds.size() > 0 else 0.0

	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUT))
	var path := OUT + args[1] + ".res"
	var err := ResourceSaver.save(out, path)
	var meta_path := OUT + "walks.json"
	var meta := {}
	if FileAccess.file_exists(meta_path):
		meta = JSON.parse_string(FileAccess.get_file_as_string(meta_path))
	meta[args[1]] = {"stride": snappedf(stride, 0.001), "length": snappedf(out.length, 0.001), "source": args[0].get_file()}
	var file := FileAccess.open(meta_path, FileAccess.WRITE)
	file.store_string(JSON.stringify(meta, "  ", true))
	file.close()
	print("retargeted %s -> %s (%s): %d frames, %.2f s, stride %.3f m/s, travel removed %.2f m" % [args[0], path, error_string(err), frames, out.length, stride, drift.length() / 100.0 if drift.length() > 10 else drift.length()])
	quit()

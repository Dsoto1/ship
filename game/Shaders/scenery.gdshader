// Built scenery with real surface detail (D-021 hybrid pipeline, ART_BIBLE §7–8). The colour is still
// the art-directed palette colour in the vertices; a photographed CC0 texture only adds variation,
// relief and roughness to it. Each texture is divided by its own average colour (its smallest mip), so
// plaster stays the palette's lime or ochre and timber the palette's brown, whatever the photograph's
// own tint. Mapped in world space from the three axes (triplanar), so boxes and beams of any size need
// no UVs and every surface keeps the texture's true scale in metres.
// The surface is chosen per vertex by the colour's alpha (SceneryMaterial.cs): 1 flat paint, 0.75
// plaster, 0.5 stone, 0.25 timber, so all the built scenery of a chunk is still one draw call.
shader_type spatial;
// Two-sided: roof gables and lofted hulls are single sheets.
render_mode cull_disabled;

uniform sampler2DArray albedo_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2DArray normal_tex : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2DArray rough_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
// Per layer (plaster, stone, timber): real size of one repeat in metres, and how much of the
// photograph's variation to keep (0 flat palette colour .. 1 all of it).
uniform vec3 tile_metres = vec3(1.0, 1.69, 1.8);
uniform vec3 strength = vec3(0.55, 0.85, 0.85);
uniform float wetness = 0.0;
// 0 still .. 1 a strong breeze: cloth (vertex alpha 0.99) ripples along its normal.
uniform float wind = 0.15;

varying vec3 world_position;
varying vec3 world_normal;
varying vec3 tint;
varying float surface;

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(vec3(0.04045), c));
}

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	if (abs(COLOR.a - 0.99) < 0.004) {
		float ripple = sin(TIME * (2.0 + wind * 4.0) + world_position.x * 1.7 + world_position.z * 1.1)
			* sin(TIME * 1.3 + world_position.x * 0.6 - world_position.y * 2.0);
		VERTEX += NORMAL * ripple * (0.012 + wind * 0.05);
		world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	}
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	tint = to_linear(COLOR.rgb);
	surface = COLOR.a;
}

vec3 sample3(sampler2DArray tex, vec2 a, vec2 b, vec2 c, vec3 w, float layer) {
	return texture(tex, vec3(a, layer)).rgb * w.x + texture(tex, vec3(b, layer)).rgb * w.y + texture(tex, vec3(c, layer)).rgb * w.z;
}

void fragment() {
	float layer = surface > 0.875 ? -1.0 : surface > 0.625 ? 0.0 : surface > 0.375 ? 1.0 : 2.0;
	if (layer < 0.0) {
		ALBEDO = tint * (1.0 - wetness * 0.2);
		ROUGHNESS = mix(0.9, 0.45, wetness);
	} else {
		int i = int(layer);
		vec3 n = normalize(world_normal) * (FRONT_FACING ? 1.0 : -1.0);
		vec3 weights = pow(abs(n), vec3(4.0));
		weights /= weights.x + weights.y + weights.z;
		float scale = tile_metres[i];
		vec2 uv_x = world_position.zy / scale;
		vec2 uv_y = world_position.xz / scale;
		vec2 uv_z = world_position.xy / scale;

		vec3 average = textureLod(albedo_tex, vec3(0.5, 0.5, layer), 12.0).rgb;
		vec3 photo = sample3(albedo_tex, uv_x, uv_y, uv_z, weights, layer);
		vec3 variation = mix(vec3(1.0), clamp(photo / max(average, vec3(0.02)), 0.0, 2.5), strength[i]);
		ALBEDO = tint * variation * (1.0 - wetness * 0.22);

		// Triplanar normals, "whiteout" blend: each projection's tangent normal turned onto its axis.
		vec3 tx = texture(normal_tex, vec3(uv_x, layer)).xyz * 2.0 - 1.0;
		vec3 ty = texture(normal_tex, vec3(uv_y, layer)).xyz * 2.0 - 1.0;
		vec3 tz = texture(normal_tex, vec3(uv_z, layer)).xyz * 2.0 - 1.0;
		vec3 nx = vec3(tx.xy + n.zy, abs(tx.z) * n.x);
		vec3 ny = vec3(ty.xy + n.xz, abs(ty.z) * n.y);
		vec3 nz = vec3(tz.xy + n.xy, abs(tz.z) * n.z);
		vec3 detailed = normalize(nx.zyx * weights.x + ny.xzy * weights.y + nz.xyz * weights.z);
		NORMAL = normalize((VIEW_MATRIX * vec4(detailed, 0.0)).xyz);

		float rough = texture(rough_tex, vec3(uv_x, layer)).g * weights.x + texture(rough_tex, vec3(uv_y, layer)).g * weights.y
			+ texture(rough_tex, vec3(uv_z, layer)).g * weights.z;
		ROUGHNESS = mix(clamp(rough * 1.05, 0.55, 1.0), 0.4, wetness * 0.7);
	}

	SPECULAR = mix(0.35, 0.55, wetness);
}

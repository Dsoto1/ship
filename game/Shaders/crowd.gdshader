// Tier C figures (PORT_EXPERIENCE §6): one mesh drawn many times in a single MultiMesh, animated here
// so a whole crowd costs one draw call. The mesh marks each part in UV: x is the part (0 body, 1 left
// leg, 2 right leg, 3 left arm, 4 right arm, 5 the load), y the height of its hip or shoulder.
// Vertex alpha says how to colour a vertex: 1 the instance's clothing, 0.5 its skin, 0 as painted.
// Per instance (INSTANCE_CUSTOM): x walk phase, y stride (0 standing .. 1 walking), z pose (0 walking,
// 1 carrying a sack on the shoulder, 2 waving, 3 hauling on a line), w clothing index + skin (0..0.9).
shader_type spatial;
render_mode cull_back;

uniform vec3 clothing[12]; // linear: converted when set
uniform vec3 skin_light : source_color = vec3(0.91, 0.77, 0.66);
uniform vec3 skin_dark : source_color = vec3(0.43, 0.29, 0.2);
uniform float wetness = 0.0;

varying vec3 tint;

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(vec3(0.04045), c));
}

vec3 rotate_x(vec3 v, float pivot, float angle) {
	float y = v.y - pivot;
	return vec3(v.x, pivot + y * cos(angle) - v.z * sin(angle), y * sin(angle) + v.z * cos(angle));
}

void vertex() {
	float part = UV.x;
	float pivot = UV.y;
	float phase = INSTANCE_CUSTOM.x;
	float stride = INSTANCE_CUSTOM.y;
	float pose = INSTANCE_CUSTOM.z;
	float swing = sin(phase) * 0.55 * stride;
	float angle = 0.0;
	if (part > 0.5 && part < 1.5) {
		angle = swing;
	} else if (part > 1.5 && part < 2.5) {
		angle = -swing;
	} else if (part > 2.5 && part < 3.5) {
		angle = -swing * 0.8;
		if (pose > 2.5) {
			angle = -1.3 + sin(phase * 0.5) * 0.35;
		}
	} else if (part > 3.5 && part < 4.5) {
		angle = swing * 0.8;
		if (pose > 0.5 && pose < 1.5) {
			angle = -2.7;
		} else if (pose > 1.5 && pose < 2.5) {
			angle = -2.9 + sin(TIME * 7.0 + phase) * 0.35;
		} else if (pose > 2.5) {
			angle = -1.3 + sin(phase * 0.5 + 0.6) * 0.35;
		}
	}

	vec3 v = VERTEX;
	if (part > 4.5 && (pose < 0.5 || pose > 1.5)) {
		v = vec3(0.0, pivot, 0.0);
	} else if (angle != 0.0) {
		v = rotate_x(v, pivot, angle);
	}

	v.y += abs(cos(phase)) * 0.035 * stride;
	VERTEX = v;

	float look = INSTANCE_CUSTOM.w;
	int index = clamp(int(floor(look)), 0, 11);
	vec3 skin = mix(skin_light, skin_dark, fract(look) / 0.9);
	if (COLOR.a > 0.75) {
		tint = clothing[index] * (part > 2.5 && part < 4.5 ? 0.88 : 1.0);
	} else if (COLOR.a > 0.25) {
		tint = skin;
	} else {
		tint = to_linear(COLOR.rgb);
	}
}

void fragment() {
	ALBEDO = tint * mix(1.0, 0.82, wetness);
	ROUGHNESS = mix(0.92, 0.6, wetness);
}

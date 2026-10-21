// Sailcloth (ART_BIBLE.md 8 "Cloth"): canvas panels with seams, a gentle flutter from the wind, and a
// fade so sails never hide the crew working beneath them (ART_BIBLE.md 3 "Occlusion").
shader_type spatial;
render_mode cull_disabled, depth_draw_always;

uniform vec3 canvas : source_color = vec3(0.86, 0.81, 0.69);
uniform float opacity : hint_range(0.0, 1.0) = 1.0;
uniform float wind_strength = 1.0;
uniform float panels = 9.0;
// How worn the canvas is, 0 new .. 1 in rags: it greys and stains, gets patched, then holes open and the
// foot frays (ShipView.ShowCondition, from the rigging's condition).
uniform float wear : hint_range(0.0, 1.0) = 0.0;
uniform float seed = 0.0;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y);
}

void vertex() {
	// UV.x across the sail, UV.y down from the yard; the yard edge stays put.
	float flutter = sin(TIME * 2.3 + UV.x * 6.0 + UV.y * 3.0) * 0.04 * wind_strength * UV.y;
	VERTEX.z += flutter;
}

void fragment() {
	float seam = smoothstep(0.88, 0.96, fract(UV.x * panels));
	vec3 colour = canvas * (0.93 + 0.07 * UV.y);
	colour = mix(colour, colour * 0.82, clamp(seam, 0.0, 1.0) * 0.5);
	vec2 p = UV * vec2(7.0, 6.0) + seed;
	float n = noise(p) * 0.65 + noise(p * 2.7) * 0.35;
	// Stains and weathering first, then square patches sewn on, then holes and a ragged foot.
	colour = mix(colour, colour * vec3(0.78, 0.76, 0.7), smoothstep(0.0, 0.6, wear) * smoothstep(0.35, 0.8, noise(p * 0.6 + 3.0)));
	vec2 cell = floor(UV * vec2(6.0, 5.0) + seed);
	vec2 local = fract(UV * vec2(6.0, 5.0) + seed);
	float patched = step(hash(cell), wear * 0.7 - 0.1) * step(0.15, local.x) * step(local.x, 0.8) * step(0.2, local.y) * step(local.y, 0.75);
	colour = mix(colour, vec3(0.93, 0.89, 0.78), patched * 0.8);
	float hole = n - (1.0 - smoothstep(0.3, 1.0, wear) * 0.5);
	float fray = UV.y - (1.0 - smoothstep(0.4, 1.0, wear) * (0.15 + 0.25 * noise(vec2(UV.x * 14.0, seed))));
	if (patched < 0.5 && (hole > 0.0 || fray > 0.0)) {
		discard;
	}
	colour *= 1.0 - 0.35 * smoothstep(-0.05, 0.0, max(hole, fray));
	ALBEDO = colour;
	ROUGHNESS = 0.95;
	ALPHA = opacity;
	BACKLIGHT = vec3(0.35);
}

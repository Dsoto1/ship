// Sailcloth (ART_BIBLE.md 8 "Cloth"): canvas panels with seams, a gentle flutter from the wind, and a
// fade so sails never hide the crew working beneath them (ART_BIBLE.md 3 "Occlusion").
shader_type spatial;
render_mode cull_disabled, depth_draw_always;

uniform vec3 canvas : source_color = vec3(0.86, 0.81, 0.69);
uniform float opacity : hint_range(0.0, 1.0) = 1.0;
uniform float wind_strength = 1.0;
uniform float panels = 9.0;

void vertex() {
	// UV.x across the sail, UV.y down from the yard; the yard edge stays put.
	float flutter = sin(TIME * 2.3 + UV.x * 6.0 + UV.y * 3.0) * 0.04 * wind_strength * UV.y;
	VERTEX.z += flutter;
}

void fragment() {
	float seam = smoothstep(0.88, 0.96, fract(UV.x * panels));
	vec3 colour = canvas * (0.93 + 0.07 * UV.y);
	colour = mix(colour, colour * 0.82, clamp(seam, 0.0, 1.0) * 0.5);
	ALBEDO = colour;
	ROUGHNESS = 0.95;
	ALPHA = opacity;
	BACKLIGHT = vec3(0.35);
}

shader_type spatial;
render_mode cull_disabled;

// The far coast, seen from the ship: vertex-coloured land that greys into the haze with distance, so it
// reads as far off when first raised and grows clearer as she closes.
uniform vec4 haze_colour : source_color = vec4(0.62, 0.7, 0.76, 1.0);
uniform float haze = 0.5;
uniform float darkness = 0.0;

void fragment() {
	// Vertex colours are authored in sRGB; the lighting works in linear.
	vec3 colour = mix(pow(COLOR.rgb, vec3(2.2)), haze_colour.rgb, clamp(haze, 0.0, 1.0));
	ALBEDO = colour * (1.0 - 0.55 * darkness);
	ROUGHNESS = 0.95;
	SPECULAR = 0.1;
}

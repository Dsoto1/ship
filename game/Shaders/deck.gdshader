// Ship's deck planking (ART_BIBLE.md 8 "Wood"): planks run fore and aft, each a slightly different
// tone, with dark caulked seams and staggered butt joints. UV is the deck position in metres.
shader_type spatial;

uniform vec3 wood : source_color = vec3(0.58, 0.45, 0.31);
uniform vec3 seam : source_color = vec3(0.16, 0.11, 0.08);
uniform float plank_width = 0.24;
uniform float plank_length = 5.5;
uniform float wetness = 0.0;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

void fragment() {
	float column = floor(UV.x / plank_width);
	float shift = hash(vec2(column, 3.0)) * plank_length;
	float row = floor((UV.y + shift) / plank_length);
	float tone = hash(vec2(column, row));
	vec3 colour = wood * (0.86 + tone * 0.22);

	// Grain: fine streaks along the plank.
	float grain = fract(sin(UV.x * 310.0 + tone * 20.0) * 0.5 + UV.y * 0.15);
	colour *= 0.95 + 0.06 * smoothstep(0.3, 0.7, grain);

	float across = fract(UV.x / plank_width);
	float along = fract((UV.y + shift) / plank_length);
	float seamLine = 1.0 - smoothstep(0.0, 0.06, min(across, 1.0 - across));
	float butt = 1.0 - smoothstep(0.0, 0.006, min(along, 1.0 - along));
	colour = mix(colour, seam, max(seamLine, butt) * 0.85);

	ALBEDO = colour * (1.0 - wetness * 0.35);
	ROUGHNESS = mix(0.85, 0.35, wetness);
}

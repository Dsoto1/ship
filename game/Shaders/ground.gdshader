// Waterfront ground (ART_BIBLE.md 8 "Stone", 7.1): cobbles in the square and streets, big dressed slabs
// on the quay, worn and uneven in tone. UV is the ground position in metres.
shader_type spatial;

uniform vec3 stone : source_color = vec3(0.55, 0.5, 0.44);
uniform vec3 joint : source_color = vec3(0.25, 0.22, 0.19);
uniform float slab_size = 0.45;
uniform float quay_from = 26.0;
uniform float wetness = 0.0;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

void fragment() {
	bool quay = UV.y >= quay_from;
	vec2 size = quay ? vec2(1.4, 0.9) : vec2(slab_size, slab_size * 0.8);
	vec2 cell = UV / size;
	// Offset every other row, like laid stone.
	cell.x += mod(floor(cell.y), 2.0) * 0.5;
	vec2 id = floor(cell);
	vec2 f = fract(cell);
	float edge = min(min(f.x, 1.0 - f.x) * size.x, min(f.y, 1.0 - f.y) * size.y);
	float gap = smoothstep(0.015, quay ? 0.05 : 0.035, edge);
	float tone = 0.8 + hash(id) * 0.3;
	vec3 colour = stone * tone * (quay ? 0.9 : 1.0);
	colour *= 0.92 + 0.16 * noise(UV * 3.0);
	// Dirt gathers in the joints and where people walk.
	colour = mix(joint, colour, gap);
	colour = mix(colour, colour * vec3(0.85, 0.8, 0.72), smoothstep(0.4, 0.8, noise(UV * 0.25)) * 0.5);
	ALBEDO = colour * (1.0 - wetness * 0.3);
	ROUGHNESS = mix(0.95, 0.4, wetness);
}

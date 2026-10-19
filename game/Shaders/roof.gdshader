// Clay roof tiles (ART_BIBLE.md 7.1): rows of curved tiles in faded terracotta, varied and weathered.
// UV is along the roof (x) and down the slope (y), in metres.
shader_type spatial;

uniform vec3 clay : source_color = vec3(0.62, 0.32, 0.2);

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

void fragment() {
	vec2 cell = UV / vec2(0.32, 0.4);
	cell.x += mod(floor(cell.y), 2.0) * 0.5;
	vec2 id = floor(cell);
	vec2 f = fract(cell);
	float ridge = sin(f.x * PI);
	float overlap = smoothstep(0.0, 0.25, f.y);
	vec3 colour = clay * (0.75 + 0.35 * hash(id)) * (0.7 + 0.3 * ridge) * (0.75 + 0.25 * overlap);
	colour = mix(colour, vec3(0.32, 0.33, 0.28), hash(id + 7.0) > 0.93 ? 0.4 : 0.0);
	ALBEDO = colour;
	ROUGHNESS = 0.9;
}

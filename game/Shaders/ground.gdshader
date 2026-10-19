// Waterfront ground (ART_BIBLE.md 8 "Stone", 7.1; PORT_VISUAL_PASS §3.3): cobbles in the square and
// streets, big dressed slabs on the quay, worn and uneven in tone. UV is the ground position in metres.
// A per-cell detail map (GroundDetail.cs) says how the ground is used: worn routes, grime against walls,
// damp near the water and the fish trade, and straw, sawdust or fish scales where that work is done.
shader_type spatial;

uniform vec3 stone : source_color = vec3(0.55, 0.5, 0.44);
uniform vec3 joint : source_color = vec3(0.25, 0.22, 0.19);
uniform float slab_size = 0.45;
uniform float quay_from = 26.0;
uniform float wetness = 0.0;
uniform sampler2D detail : filter_linear, repeat_disable;
uniform vec2 map_size = vec2(80.0, 46.0);

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

void fragment() {
	vec2 cellUv = (UV + 0.5) / map_size;
	bool inside = all(greaterThanEqual(cellUv, vec2(0.0))) && all(lessThanEqual(cellUv, vec2(1.0)));
	vec4 use = inside ? texture(detail, cellUv) : vec4(0.0);
	// Break the edges of every zone with noise so nothing reads as a painted rectangle.
	float breakup = noise(UV * 1.7) * 0.5 + noise(UV * 5.3) * 0.5;
	float worn = smoothstep(0.15, 0.85, use.r + (breakup - 0.5) * 0.35);
	float grime = smoothstep(0.1, 0.9, use.g * (0.7 + breakup * 0.6));
	float damp = clamp(smoothstep(0.1, 0.8, use.b + (breakup - 0.5) * 0.3) + wetness * 0.75, 0.0, 1.0);

	bool quay = UV.y >= quay_from;
	vec2 size = quay ? vec2(1.4, 0.9) : vec2(slab_size, slab_size * 0.8);
	vec2 cell = UV / size;
	// Offset every other row, like laid stone.
	cell.x += mod(floor(cell.y), 2.0) * 0.5;
	vec2 id = floor(cell);
	vec2 f = fract(cell);
	float edge = min(min(f.x, 1.0 - f.x) * size.x, min(f.y, 1.0 - f.y) * size.y);
	// Worn routes: stones polished smooth and pale, joints packed with dirt.
	float gap = smoothstep(0.015, quay ? 0.05 : 0.035, edge);
	float tone = 0.8 + hash(id) * 0.3;
	vec3 colour = stone * tone * (quay ? 0.9 : 1.0);
	colour *= 0.92 + 0.16 * noise(UV * 3.0);
	colour = mix(colour, colour * 1.08 + vec3(0.02, 0.018, 0.012), worn * 0.6);
	vec3 packed = mix(joint, vec3(0.32, 0.27, 0.21), worn);
	colour = mix(packed, colour, gap);
	// General dirt where nobody sweeps, and grime against walls and stacks.
	colour = mix(colour, colour * vec3(0.85, 0.8, 0.72), smoothstep(0.4, 0.8, noise(UV * 0.25)) * 0.5 * (1.0 - worn * 0.6));
	colour = mix(colour, colour * vec3(0.62, 0.58, 0.52), grime * 0.55);

	// Litter by trade: straw, sawdust or fish scales, a few flecks thinning out from where the work is done.
	float band = use.a * 3.0;
	float kind = floor(band);
	float amount = clamp(fract(band) / 0.95, 0.0, 1.0) * step(0.02, use.a);
	float fleck = step(1.0 - amount * 0.22, hash(floor(UV * 13.0))) * (1.0 - worn * 0.8);
	vec3 litter = kind < 0.5 ? vec3(0.72, 0.62, 0.36) : kind < 1.5 ? vec3(0.8, 0.72, 0.55) : vec3(0.72, 0.76, 0.78);
	colour = mix(colour, litter, fleck * 0.32);

	// A drainage gutter down the quay, in front of the vaults, darker and damp.
	float gutter = (1.0 - smoothstep(0.05, 0.18, abs(UV.y - (quay_from + 0.2)))) * (inside ? 1.0 : 0.0);
	colour = mix(colour, joint * 0.8, gutter * 0.6);
	damp = max(damp, gutter * 0.8);

	// Wet stone: darker, richer, glossier.
	colour *= 1.0 - damp * 0.32;
	ALBEDO = colour;
	ROUGHNESS = mix(0.95, 0.35, damp) - worn * 0.08;
	SPECULAR = mix(0.4, 0.6, damp);
}

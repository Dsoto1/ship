// Waterfront ground (ART_BIBLE.md 8 "Stone", 7.1; PORT_VISUAL_PASS §3.3): cobbles in the square and
// streets, big dressed blocks on the quay, worn and uneven in tone. UV is the ground position in metres.
// A per-cell detail map (GroundDetail.cs) says how the ground is used: worn routes, grime against walls,
// damp near the water and the fish trade, and straw, sawdust or fish scales where that work is done.
shader_type spatial;

uniform vec3 stone : source_color = vec3(0.55, 0.5, 0.44);
uniform vec3 joint : source_color = vec3(0.25, 0.22, 0.19);
uniform float quay_from = 26.0;
uniform float wetness = 0.0;
uniform sampler2D detail : filter_linear, repeat_disable;
uniform vec2 map_size = vec2(80.0, 46.0);
uniform sampler2D cobble_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D cobble_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D cobble_rough : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform sampler2DArray scenery_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2DArray scenery_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2DArray scenery_rough : hint_default_white, filter_linear_mipmap, repeat_enable;

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

	// The stone itself is photographed (CC0, Assets/CREDITS.md): cobbles in the streets and the square,
	// big sea-worn blocks on the quay. Divided by the photograph's own average and tinted by the
	// palette's stone, so the art direction keeps the colour and the photograph gives the detail. A
	// second, larger and turned sample is blended in by noise so the repeat never shows.
	bool quay = UV.y >= quay_from;
	vec2 street_uv = UV / 2.0;
	vec2 quay_uv = UV / 2.6;
	vec2 turned = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8)) * UV / 3.1 + 0.37;
	float mixing = smoothstep(0.35, 0.65, noise(UV * 0.11));
	vec3 photo;
	vec3 bump;
	float rough;
	if (quay) {
		photo = mix(texture(scenery_tex, vec3(quay_uv, 1.0)).rgb, texture(scenery_tex, vec3(turned, 1.0)).rgb, mixing * 0.5);
		photo /= max(textureLod(scenery_tex, vec3(0.5, 0.5, 1.0), 12.0).rgb, vec3(0.02));
		bump = texture(scenery_normal, vec3(quay_uv, 1.0)).xyz * 2.0 - 1.0;
		rough = texture(scenery_rough, vec3(quay_uv, 1.0)).g;
	} else {
		photo = mix(texture(cobble_tex, street_uv).rgb, texture(cobble_tex, turned).rgb, mixing);
		photo /= max(textureLod(cobble_tex, vec2(0.5), 12.0).rgb, vec3(0.02));
		bump = mix(texture(cobble_normal, street_uv).xyz, texture(cobble_normal, turned).xyz, mixing) * 2.0 - 1.0;
		rough = texture(cobble_rough, street_uv).g;
	}

	// Photographed stones are bluish grey; ours are the palette's warm stone, a little desaturated.
	float luma = dot(photo, vec3(0.299, 0.587, 0.114));
	// Keep about half the photograph's contrast: at this distance the full stone-by-stone contrast is noise.
	vec3 colour = stone * mix(vec3(1.0), vec3(luma), quay ? 0.7 : 0.6) * (quay ? 0.88 : 0.9);
	colour *= 0.92 + 0.16 * noise(UV * 0.6);
	// Worn routes: stones polished smooth and pale, joints packed with dirt; relief worn down.
	float joints = smoothstep(0.75, 0.45, luma);
	colour = mix(colour, colour * 1.08 + vec3(0.02, 0.018, 0.012), worn * 0.6);
	colour = mix(colour, mix(joint, vec3(0.32, 0.27, 0.21), worn) * 1.3, joints * 0.3);
	bump.xy *= 0.7 - worn * 0.35;
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
	vec3 up = normalize(vec3(bump.x, max(bump.z, 0.2), -bump.y));
	NORMAL = normalize((VIEW_MATRIX * vec4(up, 0.0)).xyz);
	ROUGHNESS = mix(clamp(rough * 1.05, 0.6, 1.0), 0.35, damp) - worn * 0.1;
	SPECULAR = mix(0.4, 0.6, damp);
}

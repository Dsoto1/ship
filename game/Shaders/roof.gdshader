// Clay roof tiles (ART_BIBLE.md 7.1): rows of curved tiles in faded terracotta, varied and weathered.
// The tiles are photographed (CC0, Assets/CREDITS.md), divided by their own average and tinted with
// the palette's clay, with lichen and soot coming and going across the roof so no two look alike.
// UV is along the roof (x) and down the slope (y), in metres.
shader_type spatial;

uniform vec3 clay : source_color = vec3(0.62, 0.32, 0.2);
uniform sampler2D tiles : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D tiles_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D tiles_rough : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform float tile_metres = 2.5;
uniform float wetness = 0.0;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

void fragment() {
	vec2 uv = UV / tile_metres;
	vec3 photo = texture(tiles, uv).rgb / max(textureLod(tiles, vec2(0.5), 12.0).rgb, vec3(0.02));
	vec3 colour = clay * mix(vec3(dot(photo, vec3(0.299, 0.587, 0.114))), photo, 0.6);
	// Weathering: grey lichen in patches, soot darkening toward the ridge.
	float lichen = smoothstep(0.62, 0.9, noise(UV * 1.3 + 3.0)) * 0.22;
	colour = mix(colour, vec3(0.3, 0.29, 0.24), lichen);
	colour *= 0.85 + 0.15 * noise(UV * 0.3);
	ALBEDO = colour * (1.0 - wetness * 0.3);
	NORMAL_MAP = texture(tiles_normal, uv).xyz;
	ROUGHNESS = mix(clamp(texture(tiles_rough, uv).g, 0.6, 1.0), 0.4, wetness);
}

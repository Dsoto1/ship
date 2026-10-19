// Ship's deck planking (ART_BIBLE.md 8 "Wood"): planks run fore and aft, with dark caulked seams.
// The planks are photographed (CC0, Assets/CREDITS.md), divided by their own average colour and tinted
// with the palette's deck wood, so the art direction keeps the colour. UV is the deck position in
// metres; the photograph's planks run along its width, so it is turned to run along the ship.
shader_type spatial;

uniform vec3 wood : source_color = vec3(0.58, 0.45, 0.31);
uniform sampler2D planks : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D planks_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D planks_rough : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform float tile_metres = 2.6;
uniform float wetness = 0.0;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

void fragment() {
	vec2 uv = vec2(UV.y, UV.x) / tile_metres;
	vec3 photo = texture(planks, uv).rgb / max(textureLod(planks, vec2(0.5), 12.0).rgb, vec3(0.02));
	// The photograph is a stained floor: keep its grain and seams, take only a little of its hue.
	float luma = dot(photo, vec3(0.299, 0.587, 0.114));
	vec3 colour = wood * mix(1.0, luma, 0.6) * 0.92;
	// Scrubbed paler down the middle where the crew walk, darker by the bulwarks.
	colour *= 0.94 + 0.08 * hash(floor(uv * vec2(1.0, 7.0)));
	ALBEDO = colour * (1.0 - wetness * 0.35);
	vec3 n = texture(planks_normal, uv).xyz;
	NORMAL_MAP = vec3(n.y, n.x, n.z);
	ROUGHNESS = mix(clamp(texture(planks_rough, uv).g * 1.05, 0.6, 1.0), 0.35, wetness);
}

// Persistent people (C-1, CHARACTER_ART §14–25, D-028): one material for the whole person, so a
// figure is one draw call. The model (tools/characters/build.py) carries in its second UV channel
// what each vertex is made of (x: 0 skin, 1 hair, 2 eye, 3 linen, 4 wool, 5 leather, 6 felt, 7 metal,
// 8 wood, 9 straw, 10 velvet) and its height in the rest pose (y, metres); in its colour the record's
// sRGB colour (for skin the fraction of x is how sunken the surface is), and in the colour's alpha how worn the cloth is, how weathered the skin, how grey the hair.
// Skin and hair take their detail from the shared sheet, divided by its own typical colour, so the
// record's colour is the colour. Cloth has no texture: a soft mottle and fading where it is worn.
// Condition (§22, §25) comes per instance: wet darkens and glosses cloth, hair and skin, from the
// shoulders and hat down and from the hems up as it soaks; dirt gathers at hems, knees and hands;
// fatigue greys the skin a little. None of it is stored here: the simulation decides.
shader_type spatial;
render_mode cull_back;

// Not source_color: the sheet holds ratios to a typical colour (0.5 = as is), not colours.
uniform sampler2D atlas : filter_linear_mipmap_anisotropic;
instance uniform float wet = 0.0;
instance uniform float dirt = 0.0;
instance uniform float fatigue = 0.0;
instance uniform float highlight = 0.0;
instance uniform float dead = 0.0;
// The recruiting rim, drawn by the next pass (outline.gdshader); declared here so the slots agree.
instance uniform vec4 rim = vec4(0.0);

varying vec3 tint;
varying float amount;
varying float rest_height;
varying flat int kind;
varying float crease;

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(vec3(0.04045), c));
}

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

void vertex() {
	tint = to_linear(COLOR.rgb);
	amount = COLOR.a;
	kind = int(UV2.x + 0.5);
	// Skin keeps how sunken the surface is in the fraction (0..0.45): eye sockets, the mouth's folds.
	crease = kind == 0 ? clamp(UV2.x / 0.45, 0.0, 1.0) : 0.0;
	rest_height = UV2.y;
}

void fragment() {
	vec3 albedo;
	float rough = 0.9;
	float metal = 0.0;
	float spec = 0.4;
	float mottle = noise(UV * vec2(9.0, 7.0)) * 0.6 + noise(UV * vec2(31.0, 23.0)) * 0.4;
	if (kind == 0) {
		vec4 sheet = texture(atlas, UV);
		vec3 detail = sheet.rgb * 2.0;
		// Outdoor skin: ruddier and deeper with weathering, a little grey with fatigue.
		vec3 weathered = mix(vec3(1.0), vec3(1.0, 0.94, 0.9), amount);
		albedo = tint * detail * weathered;
		// Age and weather deepen the folds and sockets.
		albedo *= 1.0 - crease * (0.2 + 0.45 * amount) * vec3(0.95, 1.0, 1.0);
		// The sheet's alpha holds the age lines of the shared face (tools/characters/age.py).
		albedo *= 1.0 - (1.0 - sheet.a) * smoothstep(0.3, 0.9, amount) * 0.6 * vec3(0.92, 1.0, 1.0);
		float grey = dot(albedo, vec3(0.3, 0.55, 0.15));
		albedo = mix(albedo, vec3(grey) * vec3(0.98, 1.0, 1.02), fatigue * 0.35);
		rough = mix(0.62, 0.72, amount);
		spec = 0.35;
	} else if (kind == 1) {
		vec3 detail = texture(atlas, UV).rgb * 2.0;
		// Grey comes strand by strand: amount is the share of grey; the sculpted strands that catch
		// the light go grey first, so it follows the hair rather than speckling it.
		float strand = clamp((detail.g - 0.8) * 1.6, 0.0, 1.0) * 0.7 + noise(UV * vec2(300.0, 90.0)) * 0.3;
		vec3 grey = vec3(0.7, 0.68, 0.64) * (0.8 + 0.2 * detail);
		albedo = mix(tint * detail, grey, clamp((amount - 1.0 + strand) * 1.4 + amount * 0.3, 0.0, 1.0));
		rough = 0.6;
		spec = 0.3;
	} else if (kind == 2) {
		albedo = tint;
		rough = 0.25;
		spec = 0.6;
	} else {
		// Cloth, leather, wood, metal: the colour, a soft mottle, and fading where worn.
		float grain = kind == 3 ? 0.08 : kind == 4 ? 0.12 : kind == 5 ? 0.18 : kind == 8 ? 0.25 : 0.1;
		albedo = tint * (1.0 - grain * 0.5 + grain * mottle);
		float faded = smoothstep(0.55, 1.0, mottle + amount * 0.35) * amount;
		albedo = mix(albedo, albedo * 0.8 + vec3(0.06, 0.055, 0.045), faded * 0.5);
		rough = kind == 5 ? mix(0.55, 0.8, amount) : kind == 7 ? 0.45 : kind == 8 ? 0.75 : kind == 10 ? 0.8 : 0.93;
		metal = kind == 7 ? 0.7 : 0.0;
		spec = kind == 5 ? 0.45 : 0.3;
		if (kind == 10) {
			// Velvet: the pile catches the light at grazing angles.
			float pile = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.0);
			albedo += tint * pile * 0.6;
		}
		// Dirt at hems, knees and shoes.
		float low = 1.0 - smoothstep(0.15, 0.62, rest_height);
		albedo = mix(albedo, albedo * vec3(0.62, 0.55, 0.47), clamp(dirt * (low * 0.9 + mottle * 0.3), 0.0, 1.0));
	}

	// Wet from the top down and the hems up, soaking through as it goes on.
	float top = smoothstep(1.0, 1.65, rest_height);
	float hem = 1.0 - smoothstep(0.05, 0.45, rest_height);
	float reach = max(top, hem * 0.8);
	float soaked = clamp(wet * 1.6 - (1.0 - reach) * 0.9 + (mottle - 0.5) * 0.25, 0.0, 1.0);
	soaked = max(soaked, smoothstep(0.75, 1.0, wet));
	if (kind >= 3 && kind != 7) {
		albedo *= mix(1.0, 0.5, soaked);
		rough = mix(rough, 0.32, soaked);
		spec = mix(spec, 0.5, soaked);
	} else if (kind == 1) {
		albedo *= mix(1.0, 0.7, soaked);
		rough = mix(rough, 0.3, soaked);
	} else if (kind == 0) {
		rough = mix(rough, 0.35, soaked);
		spec = mix(spec, 0.55, soaked);
	}

	albedo = mix(albedo, vec3(dot(albedo, vec3(0.33))) * 0.7, dead * 0.8);
	ALBEDO = albedo;
	ROUGHNESS = rough;
	METALLIC = metal;
	SPECULAR = spec;
	EMISSION = vec3(0.9, 0.75, 0.4) * highlight * 0.08 * pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
}

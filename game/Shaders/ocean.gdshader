// Open-ocean surface (ART_BIBLE.md 7.2, 8 "Water"): painterly rather than physical. Gerstner swells
// move the surface; colour comes from wave height, a fresnel sky tint, crest foam, a wake behind the
// ship and drifting cloud shadows. All inputs are uniforms set from the simulation each frame, so the
// sea shows what the simulation says (time of day now, weather later) rather than decoration.
shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform vec3 deep_colour : source_color = vec3(0.05, 0.20, 0.27);
uniform vec3 crest_colour : source_color = vec3(0.17, 0.42, 0.47);
uniform vec3 sky_colour : source_color = vec3(0.62, 0.74, 0.82);
uniform vec3 foam_colour : source_color = vec3(0.92, 0.94, 0.93);
uniform vec3 haze_colour : source_color = vec3(0.55, 0.66, 0.72);

// 0 = flat calm, 1 = moderate swell, 2+ = heavy sea.
uniform float sea_state = 1.0;
// How far the water has moved past the ship (metres), so the ship appears to make way.
uniform vec2 flow = vec2(0.0);
// Hull footprint for the wake: centre x, centre z, half beam, half length. Bow towards -z.
uniform vec4 hull = vec4(4.0, 10.5, 4.0, 11.0);
uniform float hull_yaw = 0.0;
// Point the camera looks at; haze grows with distance from it.
uniform vec3 focus = vec3(4.0, 0.0, 10.0);
uniform float haze_start = 28.0;
uniform float haze_end = 70.0;
uniform float cloud_shadow = 0.25;
uniform vec2 wind = vec2(0.6, 1.4);

varying float height;
varying vec3 world;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

float fbm(vec2 p) {
	float total = 0.0;
	float amplitude = 0.5;
	for (int i = 0; i < 4; i++) {
		total += noise(p) * amplitude;
		p = p * 2.03 + vec2(1.7, 9.2);
		amplitude *= 0.5;
	}
	return total;
}

// One Gerstner wave: direction (xy), steepness (z), wavelength (w). Adds to tangent and binormal.
vec3 gerstner(vec4 wave, vec2 p, float t, inout vec3 tangent, inout vec3 binormal) {
	float steepness = wave.z * clamp(sea_state, 0.0, 2.5) * 0.5;
	float k = 2.0 * PI / wave.w;
	float c = sqrt(9.8 / k);
	vec2 d = normalize(wave.xy);
	float f = k * (dot(d, p) - c * t);
	float a = steepness / k;
	tangent += vec3(-d.x * d.x * steepness * sin(f), d.x * steepness * cos(f), -d.x * d.y * steepness * sin(f));
	binormal += vec3(-d.x * d.y * steepness * sin(f), d.y * steepness * cos(f), -d.y * d.y * steepness * sin(f));
	return vec3(d.x * a * cos(f), a * sin(f), d.y * a * cos(f));
}

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec2 p = world.xz + flow;
	vec3 tangent = vec3(1.0, 0.0, 0.0);
	vec3 binormal = vec3(0.0, 0.0, 1.0);
	vec3 offset = vec3(0.0);
	offset += gerstner(vec4(0.3, 1.0, 0.16, 23.0), p, TIME, tangent, binormal);
	offset += gerstner(vec4(-0.4, 0.9, 0.12, 13.0), p, TIME, tangent, binormal);
	offset += gerstner(vec4(0.8, 0.6, 0.10, 7.0), p, TIME, tangent, binormal);
	offset += gerstner(vec4(0.1, -1.0, 0.06, 4.1), p, TIME, tangent, binormal);
	VERTEX += offset;
	height = offset.y;
	NORMAL = normalize(cross(binormal, tangent));
}

void fragment() {
	vec2 p = world.xz + flow;
	float swell = clamp(sea_state, 0.2, 2.5);

	// Small ripples in the wind direction, so even calm water has texture.
	float ripple = fbm(p * 0.9 + wind * TIME * 0.35);
	vec3 n = normalize(NORMAL + vec3(ripple - 0.5, 0.0, fbm(p * 0.9 - TIME * 0.2) - 0.5) * 0.25);

	float crest = clamp(height / (0.55 * swell) * 0.5 + 0.5, 0.0, 1.0);
	vec3 colour = mix(deep_colour, crest_colour, crest);

	float fresnel = pow(1.0 - clamp(dot(n, VIEW), 0.0, 1.0), 4.0);
	colour = mix(colour, sky_colour, fresnel * 0.35);

	// Whitecaps on the highest crests, more of them in rough weather.
	float foam = smoothstep(0.86 - 0.12 * (swell - 1.0), 1.0, crest + (fbm(p * 1.7 + TIME * 0.3) - 0.5) * 0.35) * 0.7;

	// Wake: churned water along the hull and a widening trail astern (in her own frame she sails towards -z).
	vec2 offset = vec2(world.x - hull.x, world.z - hull.y);
	vec2 local = vec2(offset.x * cos(hull_yaw) - offset.y * sin(hull_yaw), offset.x * sin(hull_yaw) + offset.y * cos(hull_yaw));
	float along = local.y / hull.w;
	float sideways = abs(local.x) / max(hull.z, 0.1);
	float hullEdge = 1.0 - smoothstep(0.0, 0.35, abs(sideways - (1.0 - along * along * 0.6)) * 2.5);
	hullEdge *= step(abs(along), 1.05);
	float astern = max(local.y - hull.w, 0.0);
	float trail = (1.0 - smoothstep(0.0, hull.z * 0.45 + astern * 0.18, abs(local.x))) * exp(-astern * 0.06) * step(hull.w, local.y) * 0.8;
	float churn = fbm(vec2(local.x * 1.3, local.y * 0.7 - TIME * 1.5));
	foam = max(foam, clamp((hullEdge * 0.7 + trail) * smoothstep(0.45, 0.8, churn + 0.2), 0.0, 1.0));

	// Moving cloud shadows: large, soft shapes (ART_BIBLE.md 11: large simplified colour masses).
	float cloud = smoothstep(0.45, 0.75, fbm(world.xz * 0.018 + wind * TIME * 0.012));
	colour *= 1.0 - cloud * cloud_shadow;

	colour = mix(colour, foam_colour, foam * 0.85);

	float haze = smoothstep(haze_start, haze_end, length(world.xz - focus.xz));
	ALBEDO = mix(colour, haze_colour, haze);
	ROUGHNESS = mix(0.12, 0.6, foam);
	SPECULAR = 0.35;
	METALLIC = 0.0;
}

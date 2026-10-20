// The painterly pass for portraits (D-028: portraits are rendered from the person's own 3D head,
// then painted). A full-screen quad in front of the portrait camera reads what was rendered and
// repaints it with a Kuwahara filter: each pixel takes the mean colour of whichever of four
// neighbouring squares is most even, which flattens shading into brush-like patches and keeps
// edges. Then a little warmth, a paper grain, and a soft darkening towards the corners.
shader_type spatial;
render_mode unshaded, depth_test_disabled, cull_disabled, fog_disabled;

uniform sampler2D screen : hint_screen_texture, filter_linear_mipmap;
uniform int radius = 4;
uniform float grain = 0.035;

void vertex() {
	POSITION = vec4(VERTEX.xy * 2.0, 1.0, 1.0);
}

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
}

void fragment() {
	vec2 texel = 1.0 / VIEWPORT_SIZE;
	vec2 uv = SCREEN_UV;
	vec3 mean[4];
	vec3 square[4];
	for (int k = 0; k < 4; k++) {
		mean[k] = vec3(0.0);
		square[k] = vec3(0.0);
	}
	float n = float((radius + 1) * (radius + 1));
	for (int j = 0; j <= radius; j++) {
		for (int i = 0; i <= radius; i++) {
			vec3 a = textureLod(screen, uv + vec2(float(-i), float(-j)) * texel, 0.0).rgb;
			vec3 b = textureLod(screen, uv + vec2(float(i), float(-j)) * texel, 0.0).rgb;
			vec3 c = textureLod(screen, uv + vec2(float(-i), float(j)) * texel, 0.0).rgb;
			vec3 d = textureLod(screen, uv + vec2(float(i), float(j)) * texel, 0.0).rgb;
			mean[0] += a; square[0] += a * a;
			mean[1] += b; square[1] += b * b;
			mean[2] += c; square[2] += c * c;
			mean[3] += d; square[3] += d * d;
		}
	}
	vec3 result = vec3(0.0);
	float least = 1e9;
	for (int k = 0; k < 4; k++) {
		vec3 m = mean[k] / n;
		vec3 v = abs(square[k] / n - m * m);
		float spread = v.r + v.g + v.b;
		if (spread < least) {
			least = spread;
			result = m;
		}
	}
	// Warm the shadows a touch, as oil over a warm ground would.
	float lum = dot(result, vec3(0.3, 0.59, 0.11));
	result = mix(result, result * vec3(1.06, 0.98, 0.9), (1.0 - lum) * 0.5);
	result += (hash(floor(FRAGCOORD.xy / 2.0)) - 0.5) * grain;
	vec2 centred = uv - 0.5;
	result *= 1.0 - dot(centred, centred) * 0.55;
	ALBEDO = result;
}

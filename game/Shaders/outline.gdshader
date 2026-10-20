// The rim drawn round people the captain could recruit (PortHud): a second pass on a figure's own
// material that pushes the surface out along its normals and draws only its back faces, so a thin
// band of colour shows round the silhouette. The colour is per figure; a clear colour draws nothing
// (the vertices collapse), so figures without a rim cost no pixels.
shader_type spatial;
render_mode unshaded, cull_front, shadows_disabled, fog_disabled;

// The same per-figure values as character.gdshader, in the same order: a figure's passes share them.
instance uniform float wet = 0.0;
instance uniform float dirt = 0.0;
instance uniform float fatigue = 0.0;
instance uniform float highlight = 0.0;
instance uniform float dead = 0.0;
instance uniform vec4 rim = vec4(0.0);
uniform float width = 0.018;

void vertex() {
	if (rim.a <= 0.0) {
		VERTEX = vec3(0.0);
	} else {
		VERTEX += NORMAL * width;
	}
}

void fragment() {
	ALBEDO = rim.rgb;
}

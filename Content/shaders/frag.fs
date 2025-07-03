#version 330

// Input vertex attributes (from vertex shader)
in vec2 fragTexCoord;
in vec4 fragColor; // this is the tint Color passed from Raylib code

// Input uniform values
uniform sampler2D texture0;
uniform vec4 colDiffuse; // Raylib sets this to white by default. Maybe set here https://github.com/raysan5/raylib/blob/bdda18656b301303b711785db48ac311655bb3d9/src/rlgl.h#L518

// Output fragment color
out vec4 finalColor;

void main()
{
    // Texel color fetching from texture sampler
    vec4 texelColor = texture(texture0, fragTexCoord) * colDiffuse;
	
	vec3 zero = vec3(0, 0, 0);
	if (fragColor.rgb == zero) {
		// Flashing white effect if rgb = 0
		finalColor.rgb = texelColor.rgb + fragColor.a;
		finalColor.a = texelColor.a;
	} else {
		// otherwise modulate with vertex color
		finalColor = texelColor * fragColor;
	}
}
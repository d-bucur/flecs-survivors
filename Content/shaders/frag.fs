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
	finalColor.a = texelColor.a * fragColor.a;

	// Use red channel for a white flashing effect (when taking damage)
	finalColor.rgb = texelColor.rgb + fragColor.r;

    // finalColor = vec4(gray, gray, gray, texelColor.a);
}
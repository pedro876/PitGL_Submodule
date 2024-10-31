#ifndef PIT_ENCODING
#define PIT_ENCODING

//https://www.shadertoy.com/view/WsVGzm
float3 Float_To_RGB(float v) {
    float r = v;
	float g = frac(v*255.0);
	r-= g/255.0;
	float b = frac(v*255.0*255.0);
	g-=b/255.0;
	return float3(r,g,b);

    // Assume depth is in the [0, 1] range, normalized to 24 bits
    // float depthScaled = v * 16777215.0; // 16777215 is (2^24 - 1)

    // float3 encoded;
    // encoded.b = floor(depthScaled / (255.0 * 255.0)); // Upper 8 bits
    // depthScaled -= encoded.b * 255.0 * 255.0;
    // encoded.g = floor(depthScaled / 255.0); // Middle 8 bits
    // depthScaled -= encoded.g * 255.0;
    // encoded.r = depthScaled; // Lower 8 bits

    // // Normalize to [0, 1] for storage
    // return encoded / 255.0;
}

float RGB_To_Float(float3 encoded) {
    return encoded.r +
		(encoded.g/255.0)+
		(encoded.b/(255.0*255.0));


    // Bring the encoded values back to [0, 255] range
    //encoded *= 255.0;
    //
    //// Recombine the RGB channels back into a single float depth value
    //float depthScaled = encoded.r + (encoded.g * 255.0) + (encoded.b * 255.0 * 255.0);
    //
    //// Normalize the value back to [0, 1] range
    //return depthScaled / 16777215.0; // 16777215 is (2^24 - 1)
}

#endif
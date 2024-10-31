#ifndef PIT_BILINEAR_FUNCS
#define PIT_BILINEAR_FUNCS

struct BilinearInfo
{
	int3 pixel00;
	int3 pixel01;
	int3 pixel10;
	int3 pixel11;

	half weight00;
	half weight01;
	half weight10;
	half weight11;
};

BilinearInfo GetBilinearInfo(uniform float4 texelSize, half2 uv)
{
	BilinearInfo info;
	
    float2 floatPixel = (uv * texelSize.zw);
    float2 fracPixel = frac(floatPixel);
	
    int2 pixel = floatPixel;
    int x0 = pixel.x;
    int y0 = pixel.y;
    int x1 = pixel.x + (fracPixel.x > 0.5 ? 1 : -1);
    int y1 = pixel.y + (fracPixel.y > 0.5 ? 1 : -1);
	
	int2 textureSize = texelSize.zw;
    x1 = clamp(x1, 0, textureSize.x - 1);
    y1 = clamp(y1, 0, textureSize.y - 1);

	info.pixel00 = int3(x0, y0, 0);
	info.pixel01 = int3(x0, y1, 0);
	info.pixel10 = int3(x1, y0, 0);
	info.pixel11 = int3(x1, y1, 0);
	
    half weightX1 = abs(0.5 - fracPixel.x);
    half weightY1 = abs(0.5 - fracPixel.y);
    half weightX0 = 1.0 - weightX1;
    half weightY0 = 1.0 - weightY1;

	info.weight00 = weightX0 * weightY0;
	info.weight01 = weightX0 * weightY1;
	info.weight10 = weightX1 * weightY0;
	info.weight11 = weightX1 * weightY1;

	return info;
}

#define BILINEAR_MIX(c00, c01, c10, c11, w00, w01, w10, w11)((c00*w00+c01*w01+c10*w10+c11*w11)/(w00+w01+w10+w11))

inline half4 BilinearMix(half4 color00, half4 color01, half4 color10, half4 color11,
	half weight00, half weight01, half weight10, half weight11)
{
	half totalWeight = (weight00 + weight01 + weight10 + weight11);
	half4 colorMid = color00 * weight00 + color01 * weight01 + color10 * weight10 + color11 * weight11;
	colorMid = totalWeight <= 0.0 ? half4(0,0,0,0) : colorMid / totalWeight;
	return colorMid;
}

half4 BilinearSample(const Texture2D tex, uniform half4 texelSize, half2 uv)
{
	BilinearInfo info = GetBilinearInfo(texelSize, uv);

	half4 color00 = tex.Load(info.pixel00);
	half4 color01 = tex.Load(info.pixel01);
	half4 color10 = tex.Load(info.pixel10);
	half4 color11 = tex.Load(info.pixel11);

	return BilinearMix(color00, color01, color10, color11, info.weight00, info.weight01, info.weight10, info.weight11);
}

#endif
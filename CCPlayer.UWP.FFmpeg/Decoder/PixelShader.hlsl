struct VSOutput
{
    float4 Pos : SV_POSITION;              
    float2 Tex : TEXCOORD0;
};

SamplerState ySampler : register(s0);
SamplerState uSampler : register(s1);
SamplerState vSampler : register(s2);

Texture2D<float4> yTexture : register(t0);
Texture2D<float4> uTexture : register(t1);
Texture2D<float4> vTexture : register(t2);

float4 PSMain( VSOutput Index ) : SV_Target0
{
	/*
	float4 Y = yTexture.Sample(ySampler, Index.Tex) * 255;
	float4 U = uTexture.Sample(uSampler, Index.Tex) * 255;
	float4 V = vTexture.Sample(vSampler, Index.Tex) * 255;

	float4 color = float4(0, 0, 0, 1.0f);

	color.b = clamp(1.164383561643836*(Y.a - 16) + 2.017232142857142*(U.a - 128), 0, 255) / 255.0f;
	color.g = clamp(1.164383561643836*(Y.a - 16) - 0.812967647237771*(V.a - 128) - 0.391762290094914*(U.a - 128), 0, 255) / 255.0f;
	color.r = clamp(1.164383561643836*(Y.a - 16) + 1.596026785714286*(V.a - 128), 0, 255) / 255.0f;
    */
	
	float y = yTexture.Sample(ySampler, Index.Tex).a;
	float u = uTexture.Sample(uSampler, Index.Tex).a;
	float v = vTexture.Sample(vSampler, Index.Tex).a;

	y = 1.1643 * (y - 0.0625);
	u = u - 0.5;
	v = v - 0.5;

	float r = y + 1.5958 * v;
	float g = y - 0.39173 * u - 0.81290 * v;
	float b = y + 2.017 * u;
	
	float4 color = float4(r, g, b, 1.0f);
	
	return color;
}
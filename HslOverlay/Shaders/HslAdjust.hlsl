Texture2D CapturedFrame : register(t0);
SamplerState FrameSampler : register(s0);

cbuffer Bands : register(b0)
{
    // center hue, width, hue shift, saturation scale
    float4 BandParams[8];
    // luminance/value shift in x; the rest is reserved
    float4 LumParams[8];
    // x = output rotation: 0, 1, 2 or 3 quarter turns
    float4 RenderParams;
};

struct VertexOutput
{
    float4 Position : SV_POSITION;
    float2 Uv : TEXCOORD0;
};

VertexOutput VSMain(uint vertexId : SV_VertexID)
{
    VertexOutput output;
    float2 uv = float2((vertexId << 1) & 2, vertexId & 2);
    output.Uv = uv;
    output.Position = float4(uv.x * 2.0 - 1.0,
        1.0 - uv.y * 2.0, 0.0, 1.0);
    return output;
}

float AngleDiff(float a, float b)
{
    return fmod(a - b + 540.0, 360.0) - 180.0;
}

float BandWeight(float hue, float center, float width)
{
    float halfWidth = max(width * 0.5, 0.001);
    float distance = AngleDiff(hue, center);
    if (abs(distance) >= halfWidth)
        return 0.0;
    return 0.5 * (1.0 + cos(3.14159265359 * distance / halfWidth));
}

float3 RgbToHsv(float3 rgb)
{
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 p = lerp(float4(rgb.bg, K.wz), float4(rgb.gb, K.xy),
        step(rgb.b, rgb.g));
    float4 q = lerp(float4(p.xyw, rgb.r), float4(rgb.r, p.yzx),
        step(p.x, rgb.r));
    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)),
        d / (q.x + e), q.x);
}

float3 HsvToRgb(float3 hsv)
{
    float3 p = abs(frac(hsv.xxx + float3(0.0, 2.0 / 3.0, 1.0 / 3.0))
        * 6.0 - 3.0);
    return hsv.z * lerp(float3(1.0, 1.0, 1.0),
        saturate(p - 1.0), hsv.y);
}

float2 RotateUv(float2 uv, int rotation)
{
    if (rotation == 1)
        return float2(uv.y, 1.0 - uv.x);
    if (rotation == 2)
        return 1.0 - uv;
    if (rotation == 3)
        return float2(1.0 - uv.y, uv.x);
    return uv;
}

float4 PSMain(VertexOutput input) : SV_TARGET
{
    int rotation = (int)(RenderParams.x + 0.5);
    float3 rgb = CapturedFrame.Sample(FrameSampler,
        RotateUv(input.Uv, rotation)).rgb;
    float3 hsv = RgbToHsv(rgb);
    float hueDegrees = hsv.x * 360.0;
    float totalWeight = 0.0;
    float hueShift = 0.0;
    float saturationDelta = 0.0;
    float valueShift = 0.0;

    [unroll]
    for (int i = 0; i < 8; i++)
    {
        float weight = BandWeight(hueDegrees,
            BandParams[i].x, BandParams[i].y);
        totalWeight += weight;
        hueShift += weight * BandParams[i].z;
        saturationDelta += weight * (BandParams[i].w - 1.0);
        valueShift += weight * LumParams[i].x;
    }

    if (totalWeight > 0.000001)
    {
        hueShift /= totalWeight;
        saturationDelta /= totalWeight;
        valueShift /= totalWeight;
    }

    hsv.x = frac((hueDegrees + hueShift + 360.0) / 360.0);
    hsv.y = saturate(hsv.y * (1.0 + saturationDelta));
    hsv.z = saturate(hsv.z + valueShift);
    return float4(HsvToRgb(hsv), 1.0);
}

public module sky_transmit;

import lib.sky;

float3 getValFromTLUT(Texture2D<float4> tex, SamplerState sampler, float3 pos, float3 sunDir) {
    float height = length(pos);
    float3 up = pos / height;
    float sunCosZenithAngle = dot(sunDir, up);

    float2 texcoord = saturate(float2(
        sunCosZenithAngle * 0.5 + 0.5,
        (height - GroundRadius_MM) / (AtmosphereRadius_MM - GroundRadius_MM)));

    return tex.SampleLevel(sampler, texcoord, 0).rgb;
}

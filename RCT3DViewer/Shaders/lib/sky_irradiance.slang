public module sky_irradiance;

import lib.erp;

float3 sampleSkyIrradiance(Texture2D tex, SamplerState sampler, float3 rayDir) {
    float2 texcoord = ERP::ToUV(rayDir);
    return tex.SampleLevel(sampler, texcoord, 0).rgb;
}

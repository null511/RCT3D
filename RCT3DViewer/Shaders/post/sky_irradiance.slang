import lib.common;
import lib.uniforms;
import lib.colors;
import lib.ign;
import lib.erp;
import lib.sky;
import lib.sky_view;

[[vk::binding(2, 0)]] ConstantBuffer<SceneUniforms> sceneUniforms;

[[vk::binding(0, 1)]] Texture2D<float4> skyView;
[[vk::binding(1, 1)]] Texture2D<float4> skyIrradiance;
[[vk::binding(2, 1)]] SamplerState linearSampler;
[[vk::binding(3, 1), vk::image_format("rgba16f")]] RWTexture2D<float4> skyIrradianceOutput;

static const uint2 BufferSize = uint2(16, 16);

[shader("compute")]
[numthreads(16, 16)]
void main(uint2 globalPos: SV_DispatchThreadID) {
    if (any(globalPos >= BufferSize)) return;

    float2 texcoord = (globalPos + 0.5) / BufferSize;
    float3 localViewDir = ERP::FromUV(texcoord);

    float3 irradiance = CalculateIrradiance(globalPos, localViewDir);

    // TODO: This is a hack to make the irradiance map brighter. The irradiance should be calculated in a more physically correct way.
    irradiance *= 2.0;

    if (sceneUniforms.frameCounter > 0) {
        const float blendFactor = 1.0/30.0;

        float3 irradiancePrev = skyIrradiance.Load(int3(globalPos, 0)).xyz;
        irradiance = lerp(irradiancePrev, irradiance, blendFactor);
    }

    skyIrradianceOutput[globalPos] = float4(irradiance, 1.0);
}

float3 CalculateIrradiance(const in int2 globalPos, const in float3 normal) {
    //const float2 sampleDelta = float2(0.2, 0.1); // 0.025;
    //const int2 stepCount = int2(PI * float2(2.0, 0.5) / sampleDelta);

    const int2 stepCount = int2(4, 4);
    const float2 sampleDelta = (PI * float2(2.0, 0.5)) / stepCount;

    float3 up = float3(0.0, 1.0, 0.0);
    float3 right = normalize(cross(up, normal));
    up = normalize(cross(normal, right));

    float3x3 tbn = float3x3(right, up, normal);

    float dither1 = IGN::getAnimated(globalPos, sceneUniforms.frameCounter) * sampleDelta.x;
    float dither2 = IGN::getAnimated(globalPos, sceneUniforms.frameCounter + 18) * sampleDelta.y;

    float3 irradiance = float3(0.0);

    float phi = dither1;
    for (int x = 0; x < stepCount.x; x++) {
        float cos_phi = cos(phi);
        float sin_phi = sin(phi);

        float theta = dither2;
        for (int y = 0; y < stepCount.y; y++) {
            float cos_theta = cos(theta);
            float sin_theta = sin(theta);

            float3 tangentSample = float3(
                sin_theta * cos_phi,
                sin_theta * sin_phi,
                cos_theta);

            float3 sampleVec = normalize(mul(tangentSample, tbn));
            float3 skyColor = getValFromSkyLUT(skyView, linearSampler, sampleVec, sceneUniforms.sunDirection.xyz);

            irradiance += skyColor * (cos_theta * sin_theta);

            theta += sampleDelta.y;
        }

        phi += sampleDelta.x;
    }

    int nrSamples = stepCount.x * stepCount.y;
    return irradiance / nrSamples;
}

import lib.common;
import lib.uniforms;
import lib.colors;
import lib.ign;
import lib.erp;
import lib.sky;
import lib.sky_transmit;
import lib.sky_view;

[[vk::binding(2, 0)]] ConstantBuffer<SceneUniforms> sceneUniforms;

[[vk::binding(0, 1)]] Texture2D<float4> skyView;
[[vk::binding(1, 1)]] Texture2D<float4> skyTransmit;
[[vk::binding(2, 1)]] Texture2D<float4> skyIrradiance;
[[vk::binding(3, 1)]] SamplerState linearSampler;
[[vk::binding(4, 1), vk::image_format("rgba16f")]] RWTexture2D<float4> skyIrradianceOutput;

static const uint2 BufferSize = uint2(16, 16);

[shader("compute")]
[numthreads(16, 16)]
void main(uint2 globalPos: SV_DispatchThreadID) {
    if (any(globalPos >= BufferSize)) return;

    float2 texcoord = (globalPos + 0.5) / BufferSize;
    float3 localViewDir = ERP::FromUV(texcoord);

    float3 irradiance = CalculateIrradiance(globalPos, localViewDir);

    if (sceneUniforms.frameCounter > 0) {
        const float blendFactor = 1.0/30.0;

        float3 irradiancePrev = skyIrradiance.Load(int3(globalPos, 0)).xyz;
        irradiance = lerp(irradiancePrev, irradiance, blendFactor);
    }

    skyIrradianceOutput[globalPos] = float4(irradiance, 1.0);
}

float3 CalculateIrradiance(const in int2 globalPos, const in float3 normal) {
    const int2 stepCount = int2(4, 4);

    float3 sunDir = sceneUniforms.sunDirection.xyz;

    float3 n = normalize(normal);
    float3 referenceAxis = abs(n.y) < 0.999 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
    float3 right = normalize(cross(referenceAxis, n));
    float3 up = cross(n, right);

    float3x3 tbn = float3x3(right, up, n);
    float3 skyUp, skyRight, skyForward;
    float horizonAngle;
    getSkyViewParameters(sunDir, skyUp, skyRight, skyForward, horizonAngle);

    float dither1 = IGN::getAnimated(globalPos, sceneUniforms.frameCounter);
    float dither2 = IGN::getAnimated(globalPos, sceneUniforms.frameCounter + 18);

    float3 irradiance = float3(0.0);

    for (int x = 0; x < stepCount.x; x++) {
        float phi = ((x + dither1) / stepCount.x) * (2.0 * PI);
        float cos_phi = cos(phi);
        float sin_phi = sin(phi);

        for (int y = 0; y < stepCount.y; y++) {
            float sampleY = (y + dither2) / stepCount.y;
            float cos_theta = sqrt(1.0 - sampleY);
            float sin_theta = sqrt(sampleY);

            float3 tangentSample = float3(
                sin_theta * cos_phi,
                sin_theta * sin_phi,
                cos_theta);

            float3 sampleVec = mul(tangentSample, tbn);
            float groundDist = rayIntersectSphere(sky_viewPos, sampleVec, GroundRadius_MM);

            if (groundDist > 0.0) {
                // hit ground
                float3 skyLight = SunLux * max(sceneUniforms.sunDirection.y, 0.0) * getValFromTLUT(skyTransmit, linearSampler, sky_viewPos, sunDir);
                skyLight += getValFromSkyLUT(skyView, linearSampler, float3(0, 1, 0), sunDir);
                irradiance += srgbToLinear(groundAlbedo) * skyLight/PI;
            }
            else {
                // sky
                irradiance += getValFromSkyLUT(skyView, linearSampler, sampleVec, skyUp, skyRight, skyForward, horizonAngle);
            }
        }
    }

    int nrSamples = stepCount.x * stepCount.y;
    return irradiance / nrSamples;
}

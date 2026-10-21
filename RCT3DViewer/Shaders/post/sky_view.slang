import lib.common;
import lib.uniforms;
import lib.sky;
import lib.sky_transmit;
import lib.sky_multiscatter;

[[vk::binding(2, 0)]] ConstantBuffer<SceneUniforms> sceneUniforms;

[[vk::binding(0, 1)]] Texture2D<float4> skyTransmit;
[[vk::binding(1, 1)]] Texture2D<float4> skyMultiscatter;
[[vk::binding(2, 1)]] SamplerState linearSampler;
[[vk::binding(3, 1), vk::image_format("rgba16f")]] RWTexture2D<float4> skyViewOutput;

static const int2 BufferSize = int2(256, 256);
static const int numScatteringSteps = 32;

[shader("compute")]
[numthreads(16, 16)]
void main(uint2 globalPos: SV_DispatchThreadID) {
    if (any(globalPos >= BufferSize)) return;
    float2 texcoord = (globalPos.xy + 0.5) / BufferSize;

    float azimuthAngle = (texcoord.x - 0.5) * 2.0 * PI;

    // Non-linear mapping of altitude. See Section 5.3 of the paper.
    float adjV;
    if (texcoord.y < 0.5) {
        float coord = 1.0 - 2.0 * texcoord.y;
        adjV = -coord * coord;
    } else {
        float coord = texcoord.y * 2.0 - 1.0;
        adjV = coord * coord;
    }

    float height = length(sky_viewPos);
    float3 up = sky_viewPos / height;
    float horizonAngle = safeacos(sqrt(height * height - GroundRadius_MM * GroundRadius_MM) / height) - 0.5 * PI;
    float altitudeAngle = adjV * 0.5 * PI - horizonAngle;

    float cosAltitude = cos(altitudeAngle);
    float3 rayDir = float3(cosAltitude * sin(azimuthAngle), sin(altitudeAngle), -cosAltitude * cos(azimuthAngle));

    float sunAltitude = (0.5 * PI) - acos(dot(sceneUniforms.sunDirection.xyz, up));
    float3 sunDir = float3(0.0, sin(sunAltitude), -cos(sunAltitude));

    float atmoDist = rayIntersectSphere(sky_viewPos, rayDir, AtmosphereRadius_MM);
    float groundDist = rayIntersectSphere(sky_viewPos, rayDir, GroundRadius_MM);
    float tMax = (groundDist < 0.0) ? atmoDist : groundDist;
    float3 lum = raymarchScattering(sky_viewPos, rayDir, sunDir, tMax, float(numScatteringSteps));

    skyViewOutput.Store(globalPos, float4(lum, 1.0));
}

float3 raymarchScattering(float3 pos, float3 rayDir, float3 sunDir, float tMax, float numSteps) {
    float cosTheta = dot(rayDir, sunDir);

    float miePhaseValue = getMiePhase(cosTheta);
    float rayleighPhaseValue = getRayleighPhase(-cosTheta);

    float3 lum = 0.0;
    float3 transmittance = 1.0;
    float t = 0.0;

    for (float i = 0.0; i < numSteps; i += 1.0) {
        float newT = ((i + 0.3) / numSteps) * tMax;
        float dt = newT - t;
        t = newT;

        float3 newPos = pos + t * rayDir;

        float3 rayleighScattering, extinction;
        float mieScattering;
        getScatteringValues(newPos, rayleighScattering, mieScattering, extinction);

        float3 sampleTransmittance = exp(-dt * extinction);

        float3 sunTransmittance = getValFromTLUT(skyTransmit, linearSampler, newPos, sunDir);
        float3 psiMS = getValFromMultiScattLUT(skyMultiscatter, linearSampler, newPos, sunDir);

        float3 rayleighInScattering = rayleighScattering * (rayleighPhaseValue * sunTransmittance + psiMS);
        float3 mieInScattering = mieScattering * (miePhaseValue * sunTransmittance + psiMS);
        float3 inScattering = (rayleighInScattering + mieInScattering);

        // Integrated scattering within path segment.
        float3 scatteringIntegral = (inScattering - inScattering * sampleTransmittance) / extinction;

        lum += scatteringIntegral * transmittance;

        transmittance *= sampleTransmittance;
    }

    return lum;
}

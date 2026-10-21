import lib.uniforms;
import lib.sky;

[[vk::binding(0, 1), vk::image_format("rgba16f")]] RWTexture2D<float4> skyTransmitOutput;

static const int2 BufferSize = int2(256, 64);
static const int sunTransmittanceSteps = 40;

[shader("compute")]
[numthreads(16, 16)]
void main(uint2 globalPos: SV_DispatchThreadID) {
    if (any(globalPos >= BufferSize)) return;
    float2 texcoord = (globalPos.xy + 0.5) / BufferSize;

    float sunCosTheta = 2.0 * texcoord.x - 1.0;
    float sunTheta = safeacos(sunCosTheta);
    float height = lerp(GroundRadius_MM, AtmosphereRadius_MM, texcoord.y);

    float3 pos = float3(0.0, height, 0.0);
    float3 sunDir = normalize(float3(0.0, sunCosTheta, -sin(sunTheta)));

    float3 transmittance = getSunTransmittance(pos, sunDir);

    float3 color = saturate(transmittance);
    skyTransmitOutput.Store(globalPos, float4(color, 1.0));
}

float3 getSunTransmittance(float3 pos, float3 sunDir) {
    if (rayIntersectSphere(pos, sunDir, GroundRadius_MM) > 0.0) {
        return 0.0;
    }

    float atmoDist = rayIntersectSphere(pos, sunDir, AtmosphereRadius_MM);
    float stepDist = atmoDist / sunTransmittanceSteps;

    float3 transmittance = 1.0;

    for (int i = 0; i < sunTransmittanceSteps; i++) {
        float t = (i + 0.3) * stepDist;
        float3 newPos = pos + t * sunDir;

        float mieScattering;
        float3 rayleighScattering, extinction;
        getScatteringValues(newPos, rayleighScattering, mieScattering, extinction);

        transmittance *= exp(-stepDist * extinction);
    }

    return transmittance;
}

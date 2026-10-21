import lib.common;
import lib.sky;
import lib.sky_transmit;

[[vk::binding(0, 1)]] Texture2D<float4> skyTransmit;
[[vk::binding(1, 1)]] SamplerState linearSampler;
[[vk::binding(2, 1), vk::image_format("rgba16f")]] RWTexture2D<float4> skyMultiscatterOutput;

static const int2 BufferSize = int2(32, 32);
static const float mulScattSteps = 20.0;
static const int sqrtSamples = 8;

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

    float3 lum, f_ms;
    getMulScattValues(pos, sunDir, lum, f_ms);

    // Equation 10 from the paper.
    float3 psi = lum / (1.0 - f_ms);

	skyMultiscatterOutput.Store(globalPos, float4(psi, 1.0));
}

void getMulScattValues(float3 pos, float3 sunDir, out float3 lumTotal, out float3 fms) {
    lumTotal = 0.0;
    fms = 0.0;

    float invSamples = 1.0 / float(sqrtSamples * sqrtSamples);
    for (int i = 0; i < sqrtSamples; i++) {
        for (int j = 0; j < sqrtSamples; j++) {
            // This integral is symmetric about theta = 0 (or theta = PI), so we
            // only need to integrate from zero to PI, not zero to 2*PI.
            float theta = PI * (float(i) + 0.5) / float(sqrtSamples);
            float phi = safeacos(1.0 - 2.0 * (float(j) + 0.5) / float(sqrtSamples));
            float3 rayDir = getSphericalDir(theta, phi);

            float atmoDist = rayIntersectSphere(pos, rayDir, AtmosphereRadius_MM);
            float groundDist = rayIntersectSphere(pos, rayDir, GroundRadius_MM);
            float tMax = atmoDist;

            if (groundDist > 0.0) {
                tMax = groundDist;
            }

            float cosTheta = dot(rayDir, sunDir);

            float miePhaseValue = getMiePhase(cosTheta);
            float rayleighPhaseValue = getRayleighPhase(-cosTheta);

            float3 lum = 0.0, lumFactor = 0.0, transmittance = 1.0;
            float t = 0.0;

            for (float stepI = 0.0; stepI < mulScattSteps; stepI += 1.0) {
                float newT = ((stepI + 0.3) / mulScattSteps) * tMax;
                float dt = newT - t;
                t = newT;

                float3 newPos = pos + t * rayDir;

                float3 rayleighScattering, extinction;
                float mieScattering;
                getScatteringValues(newPos, rayleighScattering, mieScattering, extinction);

                float3 sampleTransmittance = exp(-dt * extinction);

                // Integrate within each segment.
                float3 scatteringNoPhase = rayleighScattering + mieScattering;
                float3 scatteringF = (scatteringNoPhase - scatteringNoPhase * sampleTransmittance) / extinction;
                lumFactor += transmittance * scatteringF;

                // This is slightly different from the paper, but I think the paper has a mistake?
                // In equation (6), I think S(x,w_s) should be S(x-tv,w_s).
                float3 sunTransmittance = getValFromTLUT(skyTransmit, linearSampler, newPos, sunDir);

                float3 rayleighInScattering = rayleighScattering * rayleighPhaseValue;
                float mieInScattering = mieScattering * miePhaseValue;
                float3 inScattering = (rayleighInScattering + mieInScattering) * sunTransmittance;

                // Integrated scattering within path segment.
                float3 scatteringIntegral = (inScattering - inScattering * sampleTransmittance) / extinction;

                lum += scatteringIntegral * transmittance;
                transmittance *= sampleTransmittance;
            }

            if (groundDist > 0.0) {
                float3 hitPos = pos + groundDist * rayDir;

                if (dot(pos, sunDir) > 0.0) {
                    hitPos = normalize(hitPos) * GroundRadius_MM;
                    lum += transmittance * groundAlbedo * getValFromTLUT(skyTransmit, linearSampler, hitPos, sunDir);
                }
            }

            fms += lumFactor * invSamples;
            lumTotal += lum * invSamples;
        }
    }
}

float3 getSphericalDir(float theta, float phi) {
    float cosPhi = cos(phi);
    float sinPhi = sin(phi);
    float cosTheta = cos(theta);
    float sinTheta = sin(theta);

    return float3(sinPhi * sinTheta, cosPhi, sinPhi * cosTheta);
}

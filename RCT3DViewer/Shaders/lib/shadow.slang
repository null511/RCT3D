public module shadow;

import lib.common;
import lib.uniforms;

extern static const int Shadow_Resolution = 1024;
extern static const int Shadow_PcssSamples = 4;
extern static const int Shadow_PcfSamples = 4;

static const float Shadow_PenumbraScale = 8.0;

int getShadowCascade(ShadowUniforms uniforms, float viewDepth) {
    if (viewDepth > uniforms.cascadeSplits.w) return -1;
    if (viewDepth > uniforms.cascadeSplits.z) return 3;
    if (viewDepth > uniforms.cascadeSplits.y) return 2;
    if (viewDepth > uniforms.cascadeSplits.x) return 1;
    return 0;
}

// no filtering
float getShadowVisibility(Texture2DArray<float> depthTexture, SamplerComparisonState comparisonSampler, float3 shadowPos, int cascade) {
    if (any(shadowPos.xy < 0.0) || any(shadowPos.xy > 1.0) || shadowPos.z < 0.0 || shadowPos.z > 1.0) return 1.0;

    const float bias = 0.00012;

    return depthTexture.SampleCmpLevelZero(comparisonSampler, float3(shadowPos.xy, cascade), shadowPos.z - bias * exp2(cascade));
}

// PCF
float getShadowVisibility(Texture2DArray<float> depthTexture, SamplerComparisonState comparisonSampler, float3 shadowPos, int cascade, float dither) {
    if (any(shadowPos.xy < 0.0) || any(shadowPos.xy > 1.0) || shadowPos.z < 0.0 || shadowPos.z > 1.0) return 1.0;

    const float radius = 2.0 / Shadow_Resolution;

    return SampleShadow_PCF(depthTexture, comparisonSampler, shadowPos, cascade, radius, dither);
}

// PCSS+PCF
//float getShadowVisibility(Texture2DArray<float> depthTexture, SamplerState linearSampler, SamplerComparisonState comparisonSampler, float3 shadowPos, int cascade, float dither) {
//    if (any(shadowPos.xy < 0.0) || any(shadowPos.xy > 1.0) || shadowPos.z < 0.0 || shadowPos.z > 1.0) return 1.0;

//    const float blockerRadius = 0.0;

//    float blockerDistance = getShadowBlockerDistance(depthTexture, linearSampler, shadowPos, cascade, blockerRadius, projectionScale.z, dither);

//    const float shadowPixelSize = rcp(Shadow_Resolution);
//    const float minShadowPixelRadius = shadowPixelSize;

//    float2 pixelRadius = (blockerDistance / Shadow_PenumbraScale) * projectionScale.xy;
//    pixelRadius = clamp(pixelRadius, float2(minShadowPixelRadius), maxPixelRadius);
//    return SampleShadow_PCF(depthTexture, comparisonSampler, shadowPos, cascade, pixelRadius, dither);
//}

float SampleShadow_PCF(Texture2DArray<float> depthTexture, SamplerComparisonState comparisonSampler, const in float3 shadowPos, const in int cascade, const in float2 pixelRadius, const in float dither) {
    const float bias = 0.00012;

    float2x2 rotation = GetSampleRotation(fract(dither + 0.183));

    float shadowFinal = 0.0;
    for (int i = 0; i < Shadow_PcfSamples; i++) {
        float r = sqrt((i + 0.5) / Shadow_PcfSamples);
        float theta = i * GoldenAngle + PHI;

        float2 pcfDiskOffset = r * float2(cos(theta), sin(theta));
        float2 pixelOffset = mul(rotation, pcfDiskOffset) * pixelRadius;
        float3 sampleShadowPos = shadowPos + float3(pixelOffset, 0.0);

        // shadowFinal += SampleShadow(sampleShadowPos, cascade);
        shadowFinal += depthTexture.SampleCmpLevelZero(comparisonSampler, float3(sampleShadowPos.xy, cascade), sampleShadowPos.z - bias * exp2(cascade));
    }

    return shadowFinal / Shadow_PcfSamples;
}

float getShadowBlockerDistance(Texture2DArray<float> depthTexture, SamplerState linearSampler, const in float3 shadowPos, const in int cascade, const in float2 pixelRadius, const in float zRange, const in float dither) {
    float2x2 rotation = GetSampleRotation(fract(dither + 0.681));

    float blockers = 0.0;
    float avgDist = 0.0;
    for (int i = 0; i < Shadow_PcssSamples; i++) {
        float r = sqrt((i + 0.5) / Shadow_PcssSamples);
        float theta = i * GoldenAngle + PHI;

        float2 diskOffset = r * float2(cos(theta), sin(theta));
        float2 pixelOffset = mul(rotation, diskOffset) * pixelRadius;

        float3 sampleShadowPos = shadowPos;
        sampleShadowPos.xy += pixelOffset;

        float texDepth = depthTexture.Sample(linearSampler, float3(sampleShadowPos.xy, cascade)).r;
        float hitDist = max((sampleShadowPos.z - texDepth) / -zRange, 0.0);

        avgDist += hitDist;
        blockers++; // += step(0.0, hitDist);
    }

    // return blockers > 0.0 ? avgDist / blockers : -1.0;
    return avgDist / blockers;
}

float2x2 GetSampleRotation(const in float dither) {
    float angle = dither * (2.0 * PI);
    float s = sin(angle);
    float c = cos(angle);

    return float2x2(
         c, s,
        -s, c);
}

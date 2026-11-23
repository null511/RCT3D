public module shadow;

import lib.common;
import lib.uniforms;

extern static const int Shadow_Resolution = 1024;
extern static const int Shadow_PcssSamples = 4;
extern static const int Shadow_PcfSamples = 4;

static const float Shadow_PenumbraScale = 64.0;
static const float Shadow_MaxRadius = 1.0;

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
float getShadowVisibility(Texture2DArray<float> depthTexture, SamplerComparisonState comparisonSampler, float3 shadowPos, float3 projectionScale, int cascade, float dither) {
    if (any(shadowPos.xy < 0.0) || any(shadowPos.xy > 1.0) || shadowPos.z < 0.0 || shadowPos.z > 1.0) return 1.0;

    float2 uvScale = 0.5 * abs(projectionScale.xy);
    float2 maxPixelRadius = Shadow_MaxRadius * uvScale;

    float blockerDistance = getShadowBlockerDistance(depthTexture, shadowPos, cascade, maxPixelRadius, projectionScale.z, dither);

    float2 pixelRadius = (blockerDistance / Shadow_PenumbraScale) * uvScale;
    pixelRadius = clamp(pixelRadius, float2(0.0), maxPixelRadius);
    return SampleShadow_PCF(depthTexture, comparisonSampler, shadowPos, cascade, pixelRadius, dither);
}

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

float getShadowBlockerDistance(Texture2DArray<float> depthTexture, const in float3 shadowPos, const in int cascade, const in float2 pixelRadius, const in float zRange, const in float dither) {
    const float bias = 0.00012;
    float receiverDepth = shadowPos.z - bias * exp2(cascade);
    float2x2 rotation = GetSampleRotation(fract(dither + 0.681));

    uint width, height, layers;
    depthTexture.GetDimensions(width, height, layers);
    int2 dimensions = int2(width, height);

    int blockers = 0;
    float avgDist = 0.0;

    for (int i = 0; i < Shadow_PcssSamples; i++) {
        float r = sqrt((i + 0.5) / Shadow_PcssSamples);
        float theta = i * GoldenAngle + PHI;

        float2 diskOffset = r * float2(cos(theta), sin(theta));
        float2 pixelOffset = mul(rotation, diskOffset) * pixelRadius;

        float3 sampleShadowPos = shadowPos;
        sampleShadowPos.xy += pixelOffset;

        // Match the comparison sampler's fully lit border without out-of-bounds loads.
        if (any(sampleShadowPos.xy < 0.0) || any(sampleShadowPos.xy > 1.0)) continue;

        int2 texel = min(int2(sampleShadowPos.xy * dimensions), dimensions - 1);
        float texDepth = depthTexture.Load(int4(texel, cascade, 0));
        if (texDepth < receiverDepth) {
            avgDist += shadowPos.z - texDepth;
            blockers++;
        }
    }

    if (blockers == 0) return 0.0;
    return avgDist / blockers / -zRange;
}

float2x2 GetSampleRotation(const in float dither) {
    float angle = dither * (2.0 * PI);
    float s = sin(angle);
    float c = cos(angle);

    return float2x2(
         c, s,
        -s, c);
}

float3 GetShadowProjectionScale(const in float4x4 projection) {
    return float3(
        projection[0].x,
        projection[1].y,
        projection[2].z);
}

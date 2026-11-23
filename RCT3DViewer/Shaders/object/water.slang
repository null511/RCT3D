import lib.common;
import lib.uniforms;
import lib.colors;
import lib.shadow;
import lib.fresnel;
import lib.celestial;
import lib.ign;
import lib.ggx;
import lib.sky;
import lib.sky_transmit;
import lib.sky_view;

[[vk::binding(0, 0)]] ConstantBuffer<CameraUniforms> cameraUniforms;
[[vk::binding(2, 0)]] ConstantBuffer<SceneUniforms> sceneUniforms;
//[[vk::binding(3, 0)]] ConstantBuffer<ShadowUniforms> shadowUniforms;

[[vk::binding(0, 1)]] Texture2D<float4> sky_transmit;
[[vk::binding(1, 1)]] Texture2D<float4> sky_view;
[[vk::binding(2, 1)]] SamplerState skySampler;
[[vk::binding(7, 1)]] Texture2DArray<float> shadowDepth;
[[vk::binding(8, 1)]] SamplerState linearSampler;
[[vk::binding(9, 1)]] SamplerComparisonState linearShadowSampler;

struct VertexInput {
    float3 position : POSITION;
    float3 normal : NORMAL;
};

struct VertexStageOutput {
    float4 position : SV_Position;
    float3 worldPosition : TEXCOORD0;
    float viewPositionZ : TEXCOORD1;
    float3 normal : NORMAL;
};

[shader("vertex")]
VertexStageOutput vertexMain(VertexInput input)
{
    float4 viewPosition = mul(float4(input.position, 1.0), cameraUniforms.view);

    VertexStageOutput output;
    output.position = mul(viewPosition, cameraUniforms.projection);
    output.worldPosition = input.position;
    output.viewPositionZ = viewPosition.z;
    output.normal = input.normal;

    output.position.xy += (sceneUniforms.taa_jitter / sceneUniforms.renderDimensions) * (2.0 * output.position.w);

    return output;
}

[shader("fragment")]
float4 fragmentMain(VertexStageOutput input) : SV_Target
{
    float3 normal = normalize(input.normal);

    // shadow
    float3 sunDir = normalize(sceneUniforms.sunDirection.xyz);
    int cascade = getShadowCascade(-input.viewPositionZ);
    float shadowVisibility = 1.0;

    if (cascade >= 0) {
        float3 shadowPos = mul_project(input.worldPosition, shadowUniforms.shadowViewProjection[cascade]);
        shadowPos.xy = shadowPos.xy * 0.5 + 0.5;

        float dither = IGN::getAnimated(int2(input.position.xy), sceneUniforms.frameCounter);

        // shadowVisibility = getShadowVisibility(shadowDepth, linearSampler, linearShadowSampler, shadowPos, cascade, dither);
        shadowVisibility = getShadowVisibility(shadowDepth, linearShadowSampler, shadowPos, cascade, dither);
    }

    // specular
    const float f0 = 0.02;
    const float roughness = 0.02;

    float3 viewDirection = normalize(cameraUniforms.cameraPosition.xyz - input.worldPosition);
    float3 reflectDir = normalize(reflect(-viewDirection, normal));

    //float3 halfDirection = normalize(sunDir + viewDirection);
    //float nDotL = max(dot(normal, sunDir), 0.0);
    float nDotV = max(dot(normal, viewDirection), 0.0001);
    //float nDotH = max(dot(normal, halfDirection), 0.0);
    //float hDotV = max(dot(halfDirection, viewDirection), 0.0);

    //float3 fresnel_sun = FresnelSchlick(hDotV, f0);
    //float distribution = DistributionGgx(nDotH, roughness);
    //float geometry = GeometrySchlickGgx(nDotV, roughness) * GeometrySchlickGgx(nDotL, roughness);
    //float3 directSpecular = distribution * geometry * fresnel_sun / max(4.0 * nDotV * nDotL, 0.0001);

    // Sky Reflections
    float fresnel_sky = FresnelSchlick(nDotV, f0);
    float3 reflectColor = getValFromSkyLUT(sky_view, linearSampler, reflectDir, sunDir);

    if (rayIntersectSphere(sky_viewPos, reflectDir, GroundRadius_MM) < 0.0) {
        // apply transmittance to all celestial objects (sun, moon, stars)
        float3 spaceTransmit = getValFromTLUT(sky_transmit, linearSampler, reflectDir, sunDir);

        reflectColor += SunLux * renderSun(reflectDir, sunDir) * spaceTransmit;
    }

    // final
    float4 color = float4(0.08, 0.38, 0.52, 0.55);
    color.rgb = srgbToLinear(color.rgb);

    //color.rgb *= shadowVisibility;
    //color.rgb *= 1.0 - fresnel_sky;

    float3 sunLight = SunLux * getValFromTLUT(sky_transmit, linearSampler, sky_viewPos, sunDir);
    color.rgb += reflectColor;// + directSpecular * nDotL * shadowVisibility * sunLight;
    color.a = fresnel_sky;

    return color;
}

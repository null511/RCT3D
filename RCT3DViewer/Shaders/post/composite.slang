import lib.common;
import lib.uniforms;
import lib.colors;
import lib.octohedral;
import lib.celestial;
import lib.sky;
import lib.sky_transmit;
import lib.sky_view;
import lib.sky_irradiance;
import lib.shadow;

[[vk::binding(0, 0)]] ConstantBuffer<CameraUniforms> cameraUniforms;
[[vk::binding(2, 0)]] ConstantBuffer<SceneUniforms> sceneUniforms;
[[vk::binding(3, 0)]] ConstantBuffer<ShadowUniforms> shadowUniforms;

struct ComputeInput {
    uint2 globalPos : SV_DispatchThreadID;
};

[[vk::binding(0, 1)]] Texture2D<float4> defer_albedo;
[[vk::binding(1, 1)]] Texture2D<float4> defer_normals;
[[vk::binding(2, 1)]] Texture2D<float4> defer_specular;
[[vk::binding(3, 1)]] Texture2D<float> defer_depth;
[[vk::binding(4, 1)]] Texture2D<float4> sky_transmit;
[[vk::binding(5, 1)]] Texture2D<float4> sky_view;
[[vk::binding(6, 1)]] Texture2D<float4> texSkyIrradiance;
[[vk::binding(7, 1)]] Texture2D<float> shadowDepth;
[[vk::binding(8, 1)]] SamplerState linearSampler;
[[vk::binding(9, 1)]] SamplerComparisonState linearShadowSampler;
[[vk::binding(10, 1), vk::image_format("rgba16f")]] RWTexture2D<float4> sceneOutput;

[shader("compute")]
[numthreads(16, 16)]
void main(ComputeInput input) {
    if (any(input.globalPos >= sceneUniforms.renderDimensions)) return;

    int3 pos = int3(input.globalPos, 0);
    float depth = defer_depth.Load(pos);
    float3 color;

    float3 viewPosition = reconstructViewPosition(
        input.globalPos, depth, sceneUniforms.renderDimensions, cameraUniforms.projectionInverse);
    float3 worldPosition = mul(float4(viewPosition, 1.0), cameraUniforms.viewToWorld).xyz;
    float3 localSunDir = normalize(sceneUniforms.sunDirection.xyz);

    if (depth < 1.0) {
        float4 albedoMap = defer_albedo.Load(pos);
        float4 normalsMap = defer_normals.Load(pos);
        float4 specularMap = defer_specular.Load(pos);

        float3 albedo = albedoMap.rgb;
        float3 geoNormal = octohedral::decode(normalsMap.xy);
        float3 texNormal = octohedral::decode(normalsMap.zw);

        float ao = 1.0; // TODO: not stored in gbuffer data
        float roughness = max(specularMap.r, 0.04);
        float metalness = specularMap.g;
        float sss = specularMap.b;
        float emission = fract(specularMap.a);

        float3 sunDir = normalize(sceneUniforms.sunDirection.xyz);
        float shadowVisibility = getShadowVisibility(worldPosition);

        float3 viewDirection = normalize(cameraUniforms.cameraPosition.xyz - worldPosition);
        float3 halfDirection = normalize(sunDir + viewDirection);
        float nDotL = max(dot(texNormal, sunDir), 0.0);
        float nDotV = max(dot(texNormal, viewDirection), 0.0001);
        float nDotH = max(dot(texNormal, halfDirection), 0.0);
        float hDotV = max(dot(halfDirection, viewDirection), 0.0);

        float smoothness = 1.0 - roughness;
        smoothness = pow2(smoothness);

        float3 f0 = lerp(0.04, albedo, metalness);
        float3 fresnel_sun = FresnelSchlick(hDotV, f0);
        float distribution = DistributionGgx(nDotH, roughness);
        float geometry = GeometrySchlickGgx(nDotV, roughness) * GeometrySchlickGgx(nDotL, roughness);
        float3 directSpecular = distribution * geometry * fresnel_sun / max(4.0 * nDotV * nDotL, 0.0001);

        // Sky Reflections
        float3 reflectDir = normalize(reflect(-viewDirection, texNormal));
        float3 reflectColor = getValFromSkyLUT(sky_view, linearSampler, reflectDir, localSunDir);
        float3 fresnel_sky = FresnelSchlick(nDotV, f0);
        float3 skySpecular = reflectColor * fresnel_sky * smoothness;

        float3 sunLight = SunLux * getValFromTLUT(sky_transmit, linearSampler, sky_viewPos, sunDir);
        float3 specular = (directSpecular * nDotL * shadowVisibility + skySpecular) * sunLight;

        // Metal Diffuse Weighting
        // float3 diffuseWeight = (1.0 - fresnel) * (1.0 - metalness);
        float3 diffuseWeight = 1.0 - metalness * smoothness;

        // SSS
        float wrappedNdotL = saturate((nDotL + sss) / (1.0 + sss));
        float diffuseNdotL = lerp(nDotL, wrappedNdotL, sss);

        // Metal Tinting
        specular *= lerp(1.0, albedo, metalness);

        float3 ambientLight = sampleSkyIrradiance(texSkyIrradiance, linearSampler, texNormal);



        color = ambientLight * (1.0 - fresnel_sky) * ao;

        color += (1.0 - fresnel_sun) * (diffuseNdotL / PI) * sunLight * shadowVisibility;

        color *= diffuseWeight;

        color += emission * 8.0;

        color *= albedo;

        color += specular;



        //float3 directLighting = (diffuseWeight * albedo * (diffuseNdotL / PI) + specular * nDotL) * sunLight;
        //float3 ambientLighting = ambientLight * albedo * ao;
        //color = directLighting + ambientLighting + albedo * emission * 8.0;
    }
    else {
        float3 localViewDir = normalize(worldPosition - cameraUniforms.cameraPosition.xyz);
        //float3 localSunDir = normalize(sceneUniforms.sunDirection.xyz);

        color = 0.0;

        if (rayIntersectSphere(sky_viewPos, localViewDir, GroundRadius_MM) < 0.0) {
            color += SunLux * renderSun(localViewDir, localSunDir);
            // TODO: moon

            // apply transmittance to all celestial objects (sun, moon, stars)
            color *= getValFromTLUT(sky_transmit, linearSampler, localViewDir, localSunDir);
        }

        // Add atmosphere
        color += getValFromSkyLUT(sky_view, linearSampler, localViewDir, localSunDir);
    }

    sceneOutput[input.globalPos] = float4(color, 1.0);
}

float getShadowVisibility(float3 worldPosition) {
    float4 shadowClip = mul(float4(worldPosition, 1.0), shadowUniforms.shadowViewProjection);
    shadowClip = distortShadowClip(shadowClip, shadowUniforms.distortionFactor);
    float3 shadowNdc = shadowClip.xyz / shadowClip.w;
    float2 shadowUv = shadowNdc.xy * 0.5 + 0.5;

    if (any(shadowUv < 0.0) || any(shadowUv > 1.0) || shadowNdc.z < 0.0 || shadowNdc.z > 1.0)
        return 1.0;

    // float storedDepth = shadowDepth.SampleLevel(linearSampler, shadowUv, 0.0);
    // return shadowNdc.z <= storedDepth + 0.0015 ? 1.0 : 0.0;
    return shadowDepth.SampleCmpLevelZero(linearShadowSampler, shadowUv, shadowNdc.z - 0.0015);
}

float3 FresnelSchlick(float cosine, float3 f0) {
    return f0 + (1.0 - f0) * pow(1.0 - saturate(cosine), 5.0);
}

float DistributionGgx(float nDotH, float roughness) {
    float alpha = roughness * roughness;
    float alphaSquared = alpha * alpha;
    float denominator = nDotH * nDotH * (alphaSquared - 1.0) + 1.0;
    return alphaSquared / max(PI * denominator * denominator, 0.000001);
}

float GeometrySchlickGgx(float nDotDirection, float roughness) {
    float k = (roughness + 1.0) * (roughness + 1.0) / 8.0;
    return nDotDirection / max(nDotDirection * (1.0 - k) + k, 0.000001);
}

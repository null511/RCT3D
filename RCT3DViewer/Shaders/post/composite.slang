import lib.common;
import lib.uniforms;
import lib.colors;
import lib.octohedral;
import lib.celestial;
import lib.fresnel;
import lib.ign;
import lib.ggx;
import lib.sky;
import lib.sky_transmit;
import lib.sky_view;
import lib.sky_irradiance;
import lib.shadow;

[[vk::binding(0, 0)]] ConstantBuffer<CameraUniforms> cameraUniforms;
[[vk::binding(2, 0)]] ConstantBuffer<SceneUniforms> sceneUniforms;
[[vk::binding(3, 0)]] ConstantBuffer<ShadowUniforms> shadowUniforms;

[[vk::binding(0, 1)]] Texture2D<float4> defer_albedo;
[[vk::binding(1, 1)]] Texture2D<float4> defer_normals;
[[vk::binding(2, 1)]] Texture2D<float4> defer_specular;
[[vk::binding(3, 1)]] Texture2D<float> defer_depth;
[[vk::binding(4, 1)]] Texture2D<float4> sky_transmit;
[[vk::binding(5, 1)]] Texture2D<float4> sky_view;
[[vk::binding(6, 1)]] Texture2D<float4> texSkyIrradiance;
[[vk::binding(7, 1)]] Texture2DArray<float> shadowDepth;
[[vk::binding(8, 1)]] Texture2D<float4> waterStaging;
[[vk::binding(9, 1)]] SamplerState linearSampler;
[[vk::binding(10, 1)]] SamplerComparisonState linearShadowSampler;
[[vk::binding(11, 1), vk::image_format("rgba16f")]] RWTexture2D<float4> sceneOutput;

[shader("compute")]
[numthreads(16, 16)]
void main(uint2 globalPos: SV_DispatchThreadID) {
    if (any(globalPos >= sceneUniforms.renderDimensions)) return;

    int3 pos = int3(globalPos, 0);
    float depth = defer_depth.Load(pos);
    float3 color;

    float3 viewPosition = reconstructViewPosition(globalPos, depth, sceneUniforms.renderDimensions, cameraUniforms.projectionInverse);
    float3 worldPosition = mul(float4(viewPosition, 1.0), cameraUniforms.viewInverse).xyz;
    float3 localSunDir = normalize(sceneUniforms.sunDirection.xyz);

    if (depth < 1.0) {
        float4 albedoMap = defer_albedo.Load(pos);
        float4 normalsMap = defer_normals.Load(pos);
        float4 specularMap = defer_specular.Load(pos);

        float3 albedo = albedoMap.rgb;
        float3 geoNormal = octohedral::decode(normalsMap.xy);
        float3 texNormal = octohedral::decode(normalsMap.zw);

        float ao = albedoMap.a;
        float roughness = max(specularMap.r, 0.04);
        float metalness = specularMap.g;
        float sss = specularMap.b;
        float emission = fract(specularMap.a);

        float3 sunDir = normalize(sceneUniforms.sunDirection.xyz);
        float shadowVisibility = 1.0;

        if (sceneUniforms.shadowMappingEnabled != 0) {
            int cascade = getShadowCascade(shadowUniforms, -viewPosition.z);

            if (cascade >= 0) {
                float3 shadowPos = mul_project(worldPosition, shadowUniforms.shadowViewProjection[cascade]);
                shadowPos.xy = shadowPos.xy * 0.5 + 0.5;

                float dither = IGN::getAnimated(globalPos, sceneUniforms.frameCounter);
                // shadowVisibility = getShadowVisibility(shadowDepth, linearSampler, linearShadowSampler, shadowPos, cascade, dither);
                shadowVisibility = getShadowVisibility(shadowDepth, linearShadowSampler, shadowPos, cascade, dither);
            }
        }

        float3 viewDirection = normalize(cameraUniforms.cameraPosition.xyz - worldPosition);

        float3 halfDirection = normalize(sunDir + viewDirection);
        float nDotL = max(dot(texNormal, sunDir), 0.0);
        float nDotV = max(dot(texNormal, viewDirection), 0.0001);
        float nDotH = max(dot(texNormal, halfDirection), 0.0);
        float hDotV = max(dot(halfDirection, viewDirection), 0.0);

        float smoothness = 1.0 - roughness;
        //smoothness = pow2(smoothness);

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
        float3 specular = directSpecular * nDotL * shadowVisibility * sunLight + skySpecular;

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

    float4 water = waterStaging.Load(pos);
    if (water.a > 0.0) {
        color *= srgbToLinear(float3(0.235, 0.627, 0.702));
        color = color * (1.0 - water.a) + water.rgb;
    }

    sceneOutput[globalPos] = float4(color, 1.0);
}

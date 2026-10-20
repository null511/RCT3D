import lib.common;
import lib.uniforms;


struct VertexInput
{
    float3 position  : POSITION;
    float3 normal    : NORMAL;
    float2 texCoord  : TEXCOORD0;
    float materialId : TEXCOORD1;
};

struct VertexStageOutput
{
    float4 position  : SV_Position;
    float3 worldPosition : TEXCOORD2;
    float3 normal    : NORMAL;
    float2 texCoord  : TEXCOORD0;
    nointerpolation uint materialId : TEXCOORD1;
};

[[vk::binding(0, 0)]]
Texture2DArray<float4> terrainTextures;

[[vk::binding(1, 0)]]
SamplerState terrainSampler;

[[vk::binding(2, 0)]]
Texture2DArray<float4> terrainNormalTextures;

[[vk::binding(3, 0)]]
Texture2DArray<float4> terrainSpecularTextures;

[shader("vertex")]
VertexStageOutput vertexMain(VertexInput input)
{
    VertexStageOutput output;
    
    output.position = mul(float4(input.position, 1.0), globalUniforms.viewProjection);
    output.worldPosition = input.position;
    output.normal = input.normal;
    output.texCoord = input.texCoord;
    output.materialId = (uint)input.materialId;
    
    return output;
}

float3 FresnelSchlick(float cosine, float3 f0)
{
    return f0 + (1.0 - f0) * pow(1.0 - saturate(cosine), 5.0);
}

float DistributionGgx(float nDotH, float roughness)
{
    float alpha = roughness * roughness;
    float alphaSquared = alpha * alpha;
    float denominator = nDotH * nDotH * (alphaSquared - 1.0) + 1.0;
    return alphaSquared / max(PI * denominator * denominator, 0.000001);
}

float GeometrySchlickGgx(float nDotDirection, float roughness)
{
    float k = (roughness + 1.0) * (roughness + 1.0) / 8.0;
    return nDotDirection / max(nDotDirection * (1.0 - k) + k, 0.000001);
}

[shader("fragment")]
float4 fragmentMain(VertexStageOutput input) : SV_Target
{
    float3 albedo = terrainTextures.Sample(terrainSampler, float3(input.texCoord, input.materialId)).rgb;
    float3 normalMap = terrainNormalTextures.Sample(terrainSampler, float3(input.texCoord, input.materialId)).rgb;
    float4 specularMap = terrainSpecularTextures.Sample(terrainSampler, float3(input.texCoord, input.materialId));

    float3 geometricNormal = normalize(input.normal);
    float2 uvDx = ddx(input.texCoord);
    float2 uvDy = ddy(input.texCoord);
    float3 positionDx = ddx(input.worldPosition);
    float3 positionDy = ddy(input.worldPosition);
    float uvDeterminant = uvDx.x * uvDy.y - uvDx.y * uvDy.x;
    float inverseUvDeterminant = (uvDeterminant < 0.0 ? -1.0 : 1.0) / max(abs(uvDeterminant), 0.000001);
    float3 tangent = normalize((positionDx * uvDy.y - positionDy * uvDx.y) * inverseUvDeterminant);
    tangent = normalize(tangent - geometricNormal * dot(geometricNormal, tangent));
    float3 bitangent = -normalize(cross(geometricNormal, tangent));

    float2 tangentNormalXY = normalMap.rg * 2.0 - 1.0;
    float3 tangentNormal = float3(tangentNormalXY, sqrt(saturate(1.0 - dot(tangentNormalXY, tangentNormalXY))));
    float3 normal = normalize(tangent * tangentNormal.x + bitangent * tangentNormal.y + geometricNormal * tangentNormal.z);

    float ao = normalMap.b;
    float roughness = max(specularMap.r, 0.04);
    float metalness = specularMap.g;
    float sss = specularMap.b;
    float emission = fract(specularMap.a);

    float3 lightDirection = normalize(globalUniforms.sunDirection.xyz);
    float3 viewDirection = normalize(globalUniforms.cameraPosition.xyz - input.worldPosition);
    float3 halfDirection = normalize(lightDirection + viewDirection);
    float nDotL = max(dot(normal, lightDirection), 0.0);
    float nDotV = max(dot(normal, viewDirection), 0.0001);
    float nDotH = max(dot(normal, halfDirection), 0.0);
    float hDotV = max(dot(halfDirection, viewDirection), 0.0);

    float3 f0 = lerp(0.04, albedo, metalness);
    float3 fresnel = FresnelSchlick(hDotV, f0);
    float distribution = DistributionGgx(nDotH, roughness);
    float geometry = GeometrySchlickGgx(nDotV, roughness) * GeometrySchlickGgx(nDotL, roughness);
    float3 specular = distribution * geometry * fresnel / max(4.0 * nDotV * nDotL, 0.0001);
    float3 diffuseWeight = (1.0 - fresnel) * (1.0 - metalness);
    float wrappedNdotL = saturate((nDotL + sss) / (1.0 + sss));
    float diffuseNdotL = lerp(nDotL, wrappedNdotL, sss);
    float3 directLighting = (diffuseWeight * albedo * (diffuseNdotL / PI) + specular * nDotL) * globalUniforms.lighting.y;
    float3 ambientLighting = diffuseWeight * albedo * globalUniforms.lighting.x * ao;
    float3 color = directLighting + ambientLighting + albedo * emission * 8.0;

    //return float4(normal * 0.5 + 0.5, 1.0);

    return float4(color, 1.0);
}

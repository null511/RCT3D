import lib.common;
import lib.colors;
import lib.uniforms;
import lib.normals;
import lib.octohedral;

[[vk::binding(0, 0)]] ConstantBuffer<CameraUniforms> cameraUniforms;
[[vk::binding(2, 0)]] ConstantBuffer<SceneUniforms> sceneUniforms;


struct VertexInput {
    [[vk::location(0)]] float3 position;
    [[vk::location(1)]] float3 normal;
    [[vk::location(2)]] float2 texCoord;
    [[vk::location(3)]] float4 transformRow0;
    [[vk::location(4)]] float4 transformRow1;
    [[vk::location(5)]] float4 transformRow2;
    [[vk::location(6)]] float4 transformRow3;
    [[vk::location(7)]] float4 color;
};

struct VertexStageOutput {
    float4 position : SV_Position;
    float3 worldPosition : TEXCOORD2;
    float3 normal : NORMAL;
	float2 texCoord : TEXCOORD0;
    nointerpolation float3 color : COLOR0;
};

struct GBufferOutput {
	float4 albedo : SV_Target0;
	float4 normals : SV_Target1;
	float4 specular : SV_Target2;
};

[[vk::binding(0, 1)]] Texture2D<float4> modelTextures[256];
[[vk::binding(0, 2)]] SamplerState modelSampler;

struct ModelMaterialPushConstants
{
    uint baseTextureIndex;
};

[[vk::push_constant]]
ModelMaterialPushConstants materialPushConstants;

[shader("vertex")]
VertexStageOutput vertexMain(VertexInput input)
{
	float4 worldPosition = input.position.x * input.transformRow0
		+ input.position.y * input.transformRow1
		+ input.position.z * input.transformRow2
		+ input.transformRow3;

	float3 worldNormal = input.normal.x * input.transformRow0.xyz
		+ input.normal.y * input.transformRow1.xyz
		+ input.normal.z * input.transformRow2.xyz;

	VertexStageOutput output;
    output.position = mul(worldPosition, cameraUniforms.viewProjection);
    output.worldPosition = worldPosition.xyz;
    output.normal = normalize(worldNormal);
	output.texCoord = input.texCoord;
    output.color = srgbToLinear(input.color.rgb);

    output.position.xy += (sceneUniforms.taa_jitter / sceneUniforms.renderDimensions) * (2.0 * output.position.w);

    return output;
}

[shader("fragment")]
GBufferOutput fragmentMain(VertexStageOutput input)
{
    uint baseTextureIndex = materialPushConstants.baseTextureIndex;
    float4 albedo = modelTextures[baseTextureIndex].Sample(modelSampler, input.texCoord);

    if (albedo.a < AlphaThreshold) discard;

    float4 normalMap = modelTextures[baseTextureIndex + 1].Sample(modelSampler, input.texCoord);
    float4 specularMap = modelTextures[baseTextureIndex + 2].Sample(modelSampler, input.texCoord);


    float3 geoNormal = normalize(input.normal);
    float3 tangentNormal = restoreNormal(normalMap.rg * 2.0 - 1.0);

    float3 tangent, bitangent;
    getTangent(input.texCoord, input.worldPosition, geoNormal, tangent, bitangent);
    float3 texNormal = normalize(tangent * tangentNormal.x + bitangent * tangentNormal.y + geoNormal * tangentNormal.z);

    GBufferOutput output;
    output.albedo = float4(albedo.rgb * input.color, normalMap.b);
    output.normals.xy = octohedral::encode(geoNormal);
    output.normals.zw = octohedral::encode(texNormal);
    output.specular = specularMap;
    return output;
}

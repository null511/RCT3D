import lib.uniforms;

[[vk::binding(0, 0)]] ConstantBuffer<CameraUniforms> cameraUniforms;
[[vk::binding(2, 0)]] ConstantBuffer<SceneUniforms> sceneUniforms;
import lib.normals;
import lib.octohedral;


struct VertexInput {
	float3 position : POSITION;
	float3 normal : NORMAL;
	float2 texCoord : TEXCOORD0;
	float materialId : TEXCOORD1;
};

struct VertexStageOutput {
    float4 position : SV_Position;
    float3 worldPosition : TEXCOORD2;
    float3 normal : NORMAL;
	float2 texCoord : TEXCOORD0;
	nointerpolation uint materialId : TEXCOORD1;
};

struct GBufferOutput {
    float4 albedo : SV_Target0;
    float4 normals : SV_Target1;
	float4 specular : SV_Target2;
};

[[vk::binding(0, 1)]] Texture2DArray<float4> terrainTextures;
[[vk::binding(1, 1)]] SamplerState terrainSampler;
[[vk::binding(2, 1)]] Texture2DArray<float4> terrainNormalTextures;
[[vk::binding(3, 1)]] Texture2DArray<float4> terrainSpecularTextures;

[shader("vertex")]
VertexStageOutput vertexMain(VertexInput input)
{
    VertexStageOutput output;
    output.position = mul(float4(input.position, 1.0), cameraUniforms.viewProjection);
    output.worldPosition = input.position;
    output.normal = input.normal;
	output.texCoord = input.texCoord;
    output.materialId = (uint)input.materialId;

    output.position.xy += (sceneUniforms.taa_jitter / sceneUniforms.renderDimensions) * (2.0 * output.position.w);

    return output;
}

[shader("fragment")]
GBufferOutput fragmentMain(VertexStageOutput input)
{
    float3 layer = float3(input.texCoord, input.materialId);
    float4 albedo = terrainTextures.SampleBias(terrainSampler, layer, sceneUniforms.mipBias);
    float4 normalMap = terrainNormalTextures.SampleBias(terrainSampler, layer, sceneUniforms.mipBias);
    float4 specularMap = terrainSpecularTextures.SampleBias(terrainSampler, layer, sceneUniforms.mipBias);

    float3 geoNormal = normalize(input.normal);
    float3 tangentNormal = restoreNormal(normalMap.rg * 2.0 - 1.0);

    float3 tangent, bitangent;
    getTangent(input.texCoord, input.worldPosition, geoNormal, tangent, bitangent);
    float3 texNormal = normalize(tangent * tangentNormal.x + bitangent * tangentNormal.y + geoNormal * tangentNormal.z);

    // TODO: AO is not being stored in the GBuffer

    GBufferOutput output;
	output.albedo = albedo;
    output.normals.xy = octohedral::encode(geoNormal);
    output.normals.zw = octohedral::encode(texNormal);
    output.specular = specularMap;
	return output;
}

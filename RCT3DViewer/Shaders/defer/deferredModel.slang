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
	float4 transformRow0 : TEXCOORD2;
	float4 transformRow1 : TEXCOORD3;
	float4 transformRow2 : TEXCOORD4;
	float4 transformRow3 : TEXCOORD5;
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
    output.materialId = (uint)input.materialId;

    output.position.xy += (sceneUniforms.taa_jitter / sceneUniforms.renderDimensions) * (2.0 * output.position.w);

    return output;
}

[shader("fragment")]
GBufferOutput fragmentMain(VertexStageOutput input)
{
    // GBufferOutput output;
    // output.albedo = terrainTextures.Sample(terrainSampler, float3(input.texCoord, input.materialId));
    // output.normals = float4(normalize(input.normal), 1.0);
    // output.specular = float4(0.5, 0.0, 0.0, 1.0);
    // return output;
    float3 layer = float3(input.texCoord, input.materialId);
    float4 albedo = terrainTextures.Sample(terrainSampler, layer);
    // float4 normalMap = terrainNormalTextures.Sample(terrainSampler, layer);
    // float4 specularMap = terrainSpecularTextures.Sample(terrainSampler, layer);
    float4 normalMap = float4(0.5, 0.5, 1.0, 1.0);
    float4 specularMap = float4(0.5, 0.0, 0.0, 1.0);

    float3 geoNormal = normalize(input.normal);
    //float3 tangentNormal = restoreNormal(normalMap.rg * 2.0 - 1.0);

    // float3 tangent, bitangent;
    // getTangent(input.texCoord, input.worldPosition, geoNormal, tangent, bitangent);
    // float3 texNormal = normalize(tangent * tangentNormal.x + bitangent * tangentNormal.y + geoNormal * tangentNormal.z);
    float3 texNormal = geoNormal;

    // TODO: AO is not being stored in the GBuffer

    GBufferOutput output;
    output.albedo = albedo;
    output.normals.xy = octohedral::encode(geoNormal);
    output.normals.zw = octohedral::encode(texNormal);
    output.specular = specularMap;
    return output;
}

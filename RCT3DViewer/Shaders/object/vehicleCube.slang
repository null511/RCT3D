import lib.uniforms;

[[vk::binding(0, 0)]] ConstantBuffer<CameraUniforms> cameraUniforms;
[[vk::binding(2, 0)]] ConstantBuffer<SceneUniforms> sceneUniforms;
import lib.octohedral;

struct VertexInput {
	[[vk::location(0)]] float3 position;
	[[vk::location(1)]] float3 normal;
	[[vk::location(2)]] float2 texCoord;
	[[vk::location(3)]] float4 transformRow0;
	[[vk::location(4)]] float4 transformRow1;
	[[vk::location(5)]] float4 transformRow2;
	[[vk::location(6)]] float4 transformRow3;
};

struct VertexStageOutput {
	float4 position : SV_Position;
	float3 normal : NORMAL;
};

struct GBufferOutput {
	float4 albedo : SV_Target0;
	float4 normals : SV_Target1;
	float4 specular : SV_Target2;
};

[shader("vertex")]
VertexStageOutput vertexMain(VertexInput input) {
	float4 worldPosition = input.position.x * input.transformRow0
		+ input.position.y * input.transformRow1
		+ input.position.z * input.transformRow2
		+ input.transformRow3;

	float3 worldNormal = input.normal.x * input.transformRow0.xyz
		+ input.normal.y * input.transformRow1.xyz
		+ input.normal.z * input.transformRow2.xyz;

	VertexStageOutput output;
	output.position = mul(worldPosition, cameraUniforms.viewProjection);
    output.normal = normalize(worldNormal);

    output.position.xy += (sceneUniforms.taa_jitter / sceneUniforms.renderDimensions) * (2.0 * output.position.w);

    return output;
}

[shader("fragment")]
GBufferOutput fragmentMain(VertexStageOutput input) {
    float4 albedo = float4(1.0, 0.0, 0.0, 1.0);
    float4 normalMap = float4(0.5, 0.5, 1.0, 1.0);
    float4 specularMap = float4(0.5, 0.0, 0.0, 1.0);

    float3 geoNormal = normalize(input.normal);
    float3 texNormal = geoNormal;

    GBufferOutput output;
    output.albedo = albedo;
	output.normals.xy = octohedral::encode(geoNormal);
	output.normals.zw = octohedral::encode(texNormal);
    output.specular = specularMap;
    return output;
}

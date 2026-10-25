import lib.uniforms;
import lib.colors;
import lib.normals;
import lib.octohedral;

[[vk::binding(0, 0)]] ConstantBuffer<CameraUniforms> cameraUniforms;
[[vk::binding(2, 0)]] ConstantBuffer<SceneUniforms> sceneUniforms;

struct VertexInput {
	[[vk::location(0)]] float3 position : POSITION;
	[[vk::location(1)]] float3 normal : NORMAL;
	[[vk::location(2)]] float4 color : COLOR0;
	[[vk::location(3)]] float2 specularData : TEXCOORD0;
};

struct VertexStageOutput {
	float4 position : SV_Position;
	[[vk::location(0)]] float3 worldPosition : TEXCOORD1;
	[[vk::location(1)]] float3 normal : NORMAL;
	[[vk::location(2)]] float4 color : COLOR0;
	[[vk::location(3)]] nointerpolation float2 specularData : TEXCOORD0;
};

struct GBufferOutput {
	float4 albedo : SV_Target0;
	float4 normals : SV_Target1;
	float4 specular : SV_Target2;
};

[shader("vertex")]
VertexStageOutput vertexMain(VertexInput input)
{
	VertexStageOutput output;
	output.position = mul(float4(input.position, 1.0), cameraUniforms.viewProjection);
	output.worldPosition = input.position;
	output.normal = input.normal;
	output.color = input.color;
	output.specularData = input.specularData;

	output.position.xy += (sceneUniforms.taa_jitter / sceneUniforms.renderDimensions) * (2.0 * output.position.w);

	return output;
}

[shader("fragment")]
GBufferOutput fragmentMain(VertexStageOutput input)
{
	float3 normal = normalize(input.normal);

    float4 color = input.color;
    color.rgb = srgbToLinear(color.rgb);

    GBufferOutput output;
    output.albedo = color;
	output.normals.xy = octohedral::encode(normal);
	output.normals.zw = octohedral::encode(normal);
	output.specular = float4(input.specularData, 0.0, 1.0);
	return output;
}

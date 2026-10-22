import lib.uniforms;
import lib.normals;
import lib.octohedral;

[[vk::binding(0, 0)]] ConstantBuffer<CameraUniforms> cameraUniforms;
[[vk::binding(2, 0)]] ConstantBuffer<SceneUniforms> sceneUniforms;
[[vk::binding(0, 1)]] Texture2D<float4> paletteLut;
[[vk::binding(1, 1)]] SamplerState paletteSampler;

struct VertexInput {
	[[vk::location(0)]] float3 position : POSITION;
	[[vk::location(1)]] float3 normal : NORMAL;
	[[vk::location(2)]] uint paletteIndex : TEXCOORD0;
	[[vk::location(3)]] float2 specularData : TEXCOORD1;
	[[vk::location(4)]] float untinted : TEXCOORD2;
};

struct VertexStageOutput {
	float4 position : SV_Position;
	[[vk::location(0)]] float3 worldPosition : TEXCOORD1;
	[[vk::location(1)]] float3 normal : NORMAL;
	[[vk::location(2)]] nointerpolation uint paletteIndex : TEXCOORD0;
	[[vk::location(3)]] nointerpolation float2 specularData : TEXCOORD1;
	[[vk::location(4)]] nointerpolation float untinted : TEXCOORD2;
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
	output.paletteIndex = input.paletteIndex;
	output.specularData = input.specularData;
	output.untinted = input.untinted;
	output.position.xy += (sceneUniforms.taa_jitter / sceneUniforms.renderDimensions) * (2.0 * output.position.w);
	return output;
}

[shader("fragment")]
GBufferOutput fragmentMain(VertexStageOutput input)
{
	float paletteU = (float(input.paletteIndex) + 0.5) / 256.0;
	float4 albedo = input.untinted > 0.5
		? float4(1.0, 1.0, 1.0, 1.0)
		: paletteLut.Sample(paletteSampler, float2(paletteU, 0.5));
	float3 normal = normalize(input.normal);

	GBufferOutput output;
	output.albedo = albedo;
	output.normals.xy = octohedral::encode(normal);
	output.normals.zw = octohedral::encode(normal);
	output.specular = float4(input.specularData, 0.0, 1.0);
	return output;
}

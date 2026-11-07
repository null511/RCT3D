import lib.uniforms;
import lib.colors;

[[vk::binding(0, 0)]] ConstantBuffer<CameraUniforms> cameraUniforms;
[[vk::binding(2, 0)]] ConstantBuffer<SceneUniforms> sceneUniforms;

struct VertexInput {
	[[vk::location(0)]] float3 position;
	[[vk::location(2)]] float4 color;
};

struct VertexOutput {
	float4 position : SV_Position;
	[[vk::location(0)]] float4 color;
};

struct GBufferOutput {
	float4 albedo : SV_Target0;
	float4 normals : SV_Target1;
	float4 specular : SV_Target2;
};

[shader("vertex")]
VertexOutput vertexMain(VertexInput input)
{
	VertexOutput output;
	output.position = mul(float4(input.position, 1.0), cameraUniforms.viewProjection);
	output.position.xy += (sceneUniforms.taa_jitter / sceneUniforms.renderDimensions) * (2.0 * output.position.w);
	output.color = input.color;
	return output;
}

[shader("fragment")]
GBufferOutput fragmentMain(VertexOutput input)
{
	GBufferOutput output;
	output.albedo = float4(srgbToLinear(input.color.rgb), 1.0);
	// Negative encoded normal marks diagnostic pixels as unlit in the composite pass.
	output.normals = float4(-1.0, 0.5, 0.5, 0.5);
	output.specular = float4(1.0, 0.0, 0.0, 1.0);
	return output;
}

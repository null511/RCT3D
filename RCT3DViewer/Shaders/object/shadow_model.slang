import lib.uniforms;
import lib.shadow;

[[vk::binding(3, 0)]] ConstantBuffer<ShadowUniforms> shadowUniforms;

struct VertexInput {
	[[vk::location(0)]] float3 position;
	[[vk::location(4)]] float4 transformRow0;
	[[vk::location(5)]] float4 transformRow1;
	[[vk::location(6)]] float4 transformRow2;
	[[vk::location(7)]] float4 transformRow3;
};

struct VertexOutput {
	float4 position : SV_Position;
};

[shader("vertex")]
VertexOutput vertexMain(VertexInput input)
{
	float4 worldPosition = input.position.x * input.transformRow0
		+ input.position.y * input.transformRow1
		+ input.position.z * input.transformRow2
		+ input.transformRow3;

	VertexOutput output;
	output.position = distortShadowClip(mul(worldPosition, shadowUniforms.shadowViewProjection), shadowUniforms.distortionFactor);
	return output;
}

[shader("fragment")]
void fragmentMain()
{
}

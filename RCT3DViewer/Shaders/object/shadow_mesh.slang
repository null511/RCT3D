import lib.uniforms;
import lib.shadow;

[[vk::binding(3, 0)]] ConstantBuffer<ShadowUniforms> shadowUniforms;

struct VertexInput {
	[[vk::location(0)]] float3 position;
};

struct VertexOutput {
	float4 position : SV_Position;
};

[shader("vertex")]
VertexOutput vertexMain(VertexInput input)
{
	VertexOutput output;
	output.position = distortShadowClip(mul(float4(input.position, 1.0), shadowUniforms.shadowViewProjection), shadowUniforms.distortionFactor);
	return output;
}

[shader("fragment")]
void fragmentMain()
{
}

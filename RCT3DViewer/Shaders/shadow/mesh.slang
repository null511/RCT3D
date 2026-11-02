import lib.uniforms;

[[vk::binding(3, 0)]] ConstantBuffer<ShadowUniforms> shadowUniforms;

struct ShadowPushConstants {
	uint baseTextureIndex;
	uint cascadeIndex;
};

[[vk::push_constant]]
ShadowPushConstants shadowPushConstants;

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
	float4x4 shadowViewProjection = shadowPushConstants.cascadeIndex == 0 ? shadowUniforms.shadowViewProjection0
		: shadowPushConstants.cascadeIndex == 1 ? shadowUniforms.shadowViewProjection1
		: shadowPushConstants.cascadeIndex == 2 ? shadowUniforms.shadowViewProjection2
		: shadowUniforms.shadowViewProjection3;
	output.position = mul(float4(input.position, 1.0), shadowViewProjection);
	return output;
}

[shader("fragment")]
void fragmentMain()
{
}

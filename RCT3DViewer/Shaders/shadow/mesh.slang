import lib.uniforms;
import lib.shadow;

//[[vk::binding(3, 0)]] ConstantBuffer<ShadowUniforms> shadowUniforms;

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
	output.position = mul(float4(input.position, 1.0), shadowUniforms.shadowViewProjection[shadowPushConstants.cascadeIndex]);
	return output;
}

[shader("fragment")]
void fragmentMain()
{
	//
}

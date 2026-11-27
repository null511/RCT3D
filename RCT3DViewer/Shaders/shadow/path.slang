import lib.common;
import lib.uniforms;
import lib.shadow;

[[vk::binding(0, 1)]] Texture2DArray<float4> pathColors[64];
[[vk::binding(3, 1)]] SamplerState pathSampler;

struct ShadowPushConstants {
	uint unused;
	uint cascadeIndex;
};
[[vk::push_constant]] ShadowPushConstants shadowPushConstants;

struct VertexInput {
	[[vk::location(0)]] float3 position;
	[[vk::location(2)]] float2 texCoord;
	[[vk::location(3)]] uint descriptorIndex;
	[[vk::location(4)]] uint layer;
};
struct VertexOutput {
	float4 position : SV_Position;
	float2 texCoord : TEXCOORD0;
	nointerpolation uint descriptorIndex : TEXCOORD1;
	nointerpolation uint layer : TEXCOORD2;
};

[shader("vertex")]
VertexOutput vertexMain(VertexInput input)
{
	VertexOutput output;
	output.position = mul(float4(input.position, 1.0), shadowUniforms.shadowViewProjection[shadowPushConstants.cascadeIndex]);
	output.texCoord = input.texCoord;
	output.descriptorIndex = input.descriptorIndex;
	output.layer = input.layer;
	return output;
}

[shader("fragment")]
void fragmentMain(VertexOutput input)
{
	float2 dx = ddx(input.texCoord);
	float2 dy = ddy(input.texCoord);
	uint index = NonUniformResourceIndex(input.descriptorIndex);
	float alpha = pathColors[index].SampleGrad(pathSampler, float3(input.texCoord, input.layer), dx, dy).a;
	if (alpha < AlphaThreshold) discard;
}

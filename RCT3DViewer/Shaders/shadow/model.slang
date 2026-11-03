import lib.common;
import lib.uniforms;

[[vk::binding(3, 0)]] ConstantBuffer<ShadowUniforms> shadowUniforms;

struct VertexInput {
	[[vk::location(0)]] float3 position;
    [[vk::location(1)]] float3 normal;
    [[vk::location(2)]] float2 texCoord;
    [[vk::location(3)]] float4 transformRow0;
    [[vk::location(4)]] float4 transformRow1;
    [[vk::location(5)]] float4 transformRow2;
    [[vk::location(6)]] float4 transformRow3;
};

struct VertexOutput {
    float4 position : SV_Position;
    float2 texCoord : TEXCOORD0;
};

[[vk::binding(0, 1)]] Texture2D<float4> modelTextures[256];
[[vk::binding(0, 2)]] SamplerState modelSampler;

struct ModelMaterialPushConstants
{
    uint baseTextureIndex;
    uint cascadeIndex;
};

[[vk::push_constant]]
ModelMaterialPushConstants materialPushConstants;

[shader("vertex")]
VertexOutput vertexMain(VertexInput input)
{
	float4 worldPosition = input.position.x * input.transformRow0
		+ input.position.y * input.transformRow1
		+ input.position.z * input.transformRow2
		+ input.transformRow3;

    VertexOutput output;
    output.position = mul(worldPosition, shadowUniforms.shadowViewProjection[materialPushConstants.cascadeIndex]);
    output.texCoord = input.texCoord;
    return output;
}

[shader("fragment")]
void fragmentMain(VertexOutput input)
{
    float alpha = modelTextures[materialPushConstants.baseTextureIndex].Sample(modelSampler, input.texCoord).a;

    if (alpha < AlphaThreshold) discard;
}

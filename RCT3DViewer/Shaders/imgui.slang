struct PushConstants
{
	float2 scale;
	float2 translate;
};

[[vk::push_constant]]
PushConstants pushConstants;

struct VertexInput
{
	float2 position : POSITION;
	float2 texCoord : TEXCOORD0;
	float4 color : COLOR0;
};

struct VertexOutput
{
	float4 position : SV_Position;
	float2 texCoord : TEXCOORD0;
	float4 color : COLOR0;
};

[[vk::binding(0, 0)]]
Texture2D<float4> fontTexture;

[[vk::binding(1, 0)]]
SamplerState fontSampler;

[shader("vertex")]
VertexOutput vertexMain(VertexInput input)
{
	VertexOutput output;
	output.position = float4(input.position * pushConstants.scale + pushConstants.translate, 0.0, 1.0);
	output.texCoord = input.texCoord;
	output.color = input.color;
	return output;
}

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
	return input.color * fontTexture.Sample(fontSampler, input.texCoord);
}

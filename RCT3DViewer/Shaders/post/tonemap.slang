import lib.colors;

struct FullscreenVertexOutput {
	float4 position : SV_Position;
	float2 texCoord : TEXCOORD0;
};

[[vk::binding(0, 0)]] Texture2D<float4> sceneOutput;
[[vk::binding(1, 0)]] SamplerState linearSampler;

[shader("vertex")]
FullscreenVertexOutput vertexMain(uint vertexId : SV_VertexID)
{
	FullscreenVertexOutput output;
	float2 uv = float2((vertexId << 1) & 2, vertexId & 2);
    output.position = float4(uv * 2.0 + float2(-1.0, -1.0), 0.0, 1.0);
	output.texCoord = uv;
	return output;
}

[shader("fragment")]
float4 fragmentMain(FullscreenVertexOutput input) : SV_Target
{
	float3 color = sceneOutput.Sample(linearSampler, input.texCoord).rgb;

    //color = max(color, 0.0);
    //color /= max(exposure[0].average, 1e-8);

    //color = getColorSpace().toLinear(color);

     color = aces_fit_tonemap(color);

    color = linearToSrgb(color);

    return float4(color, 1.0);
}

float3 aces_fit_tonemap(const in float3 x) {
    const float a = 2.51f;
    const float b = 0.03f;
    const float c = 2.43f;
    const float d = 0.59f;
    const float e = 0.14f;

    return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
}

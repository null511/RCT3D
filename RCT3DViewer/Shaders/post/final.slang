import lib.colors;
import lib.bayer;

[[vk::binding(0, 1)]] Texture2D<float4> sceneOutput;

struct FullscreenVertexOutput {
	float4 position : SV_Position;
	float2 texCoord : TEXCOORD0;
};

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
    uint2 uv = uint2(input.position.xy);
    float3 color = sceneOutput[uv].rgb;

    color = linearToSrgb(color);

    const float ditherScale = rcp(255.0);
    color += (GetBayerValue(uv) - 0.5) * ditherScale;

    return float4(color, 1.0);
}

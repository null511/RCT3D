import lib.colors;
import lib.uniforms;
import lib.octohedral;
import lib.bayer;

struct FullscreenVertexOutput {
	float4 position : SV_Position;
	float2 texCoord : TEXCOORD0;
};

[[vk::binding(2, 0)]] ConstantBuffer<SceneUniforms> sceneUniforms;
[[vk::binding(0, 1)]] Texture2D<float4> sceneOutput;
[[vk::binding(1, 1)]] Texture2D<float4> defer_albedo;
[[vk::binding(2, 1)]] Texture2D<float4> defer_normals;
[[vk::binding(3, 1)]] SamplerState linearSampler;
[[vk::binding(4, 1)]] SamplerState nearestSampler;

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

    //color = max(color, 0.0);
    //color /= max(exposure[0].average, 1e-8);

    color *= 0.6;

    //color = getColorSpace().toLinear(color);

    color = aces_fit_tonemap(color);

    switch (sceneUniforms.debugView) {
        case DebugView.GeoNormal:
            color = octohedral::decode(defer_normals[uv].xy) * 0.5 + 0.5;
            break;
        case DebugView.TexNormal:
            color = octohedral::decode(defer_normals[uv].zw) * 0.5 + 0.5;
            break;
    }

    color = linearToSrgb(color);

    switch (sceneUniforms.debugView) {
        case DebugView.Albedo:
            color = defer_albedo[uv].rgb;
            break;
    }

    // TODO: dither
    const float ditherScale = rcp(255.0);
    color += (GetBayerValue(uv) - 0.5) * ditherScale;

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

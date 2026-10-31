import lib.colors;
import lib.uniforms;
import lib.octohedral;

[[vk::binding(2, 0)]] ConstantBuffer<SceneUniforms> sceneUniforms;

[[vk::binding(0, 1)]] Texture2D<float4> defer_albedo;
[[vk::binding(1, 1)]] Texture2D<float4> defer_normals;
[[vk::binding(2, 1)]] Texture2D<float4> defer_specular;
[[vk::binding(4, 1)]] RWTexture2D<float4> sceneOutput;

[shader("compute")]
[numthreads(16, 16)]
void main(uint2 globalPos: SV_DispatchThreadID) {
    if (any(globalPos >= sceneUniforms.renderDimensions)) return;

    float3 color = sceneOutput[globalPos].rgb;
    float4 specularMap = defer_specular[globalPos];

    switch (sceneUniforms.debugView) {
        case DebugView.GeoNormal:
            color = octohedral::decode(defer_normals[globalPos].xy) * 0.5 + 0.5;
            break;
        case DebugView.TexNormal:
            color = octohedral::decode(defer_normals[globalPos].zw) * 0.5 + 0.5;
            break;
        case DebugView.Albedo:
            color = defer_albedo[globalPos].rgb;
            color = srgbToLinear(color);
            break;
        case DebugView.SpecularRoughness:
            color = float3(specularMap.r, specularMap.r, specularMap.r);
            break;
        case DebugView.SpecularMetalness:
            color = float3(specularMap.g, specularMap.g, specularMap.g);
            break;
        case DebugView.SpecularSss:
            color = float3(specularMap.b, specularMap.b, specularMap.b);
            break;
        case DebugView.SpecularEmission:
            float emission = frac(specularMap.a);
            color = float3(emission, emission, emission);
            break;
    }

    sceneOutput[globalPos] = float4(color, 1.0);
}

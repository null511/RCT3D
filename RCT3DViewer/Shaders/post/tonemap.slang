import lib.uniforms;

[[vk::binding(2, 0)]] ConstantBuffer<SceneUniforms> sceneUniforms;
[[vk::binding(0, 1), vk::image_format("rgba16f")]] RWTexture2D<float4> sceneOutput;

[shader("compute")]
[numthreads(16, 16)]
void main(uint2 globalPos: SV_DispatchThreadID) {
    if (any(globalPos >= sceneUniforms.renderDimensions)) return;

    float3 color = sceneOutput[globalPos].rgb;

    // safety clamp
    color = max(color, 0.0);

    // exposure
    //color /= max(exposure[0].average, 1e-8);
    color *= 0.6;

    // ap1 to linear
    //color = getColorSpace().toLinear(color);

    color = aces_fit_tonemap(color);

    sceneOutput[globalPos] = float4(color, 1.0);
}

float3 aces_fit_tonemap(const in float3 x) {
    const float a = 2.51f;
    const float b = 0.03f;
    const float c = 2.43f;
    const float d = 0.59f;
    const float e = 0.14f;

    return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
}

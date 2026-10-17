struct Uniforms
{
    float4x4 viewProjection;
};

// Directly maps to the CmdPushConstants layout passed during OnRenderFrame
[[vk::push_constant]]
Uniforms globalUniforms;

struct VertexInput
{
    float3 position  : POSITION;
    float3 normal    : NORMAL;
    float2 texCoord  : TEXCOORD0;
    float materialId : TEXCOORD1;
};

struct VertexStageOutput
{
    float4 position  : SV_Position;
    float3 normal    : NORMAL;
    float2 texCoord  : TEXCOORD0;
    nointerpolation uint materialId : TEXCOORD1;
};

[[vk::binding(0, 0)]]
Texture2DArray<float4> terrainTextures;

[[vk::binding(1, 0)]]
SamplerState terrainSampler;

[shader("vertex")]
VertexStageOutput vertexMain(VertexInput input)
{
    VertexStageOutput output;
    
    output.position = mul(float4(input.position, 1.0), globalUniforms.viewProjection);
    output.normal = input.normal;
    output.texCoord = input.texCoord;
    output.materialId = (uint)input.materialId;
    
    return output;
}

[shader("fragment")]
float4 fragmentMain(VertexStageOutput input) : SV_Target
{
    float4 color = terrainTextures.Sample(terrainSampler, float3(input.texCoord, input.materialId));

    const float3 sunDir = normalize(float3(0.5, 1.0, 0.5));   // Example sun direction
    color *= max(dot(input.normal, sunDir), 0.0) * 0.8 + 0.2; // Simple Lambertian lighting with ambient term

    return color;
}

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
    float3 color     : COLOR;
};

struct VertexStageOutput
{
    float4 position  : SV_Position;
    float3 normal    : NORMAL;
    float3 color     : COLOR;
};

[shader("vertex")]
VertexStageOutput vertexMain(VertexInput input)
{
    VertexStageOutput output;
    
    output.position = mul(float4(input.position, 1.0), globalUniforms.viewProjection);
    output.normal = input.normal;
    output.color = input.color;
    
    return output;
}

[shader("fragment")]
float4 fragmentMain(VertexStageOutput input) : SV_Target
{
    // Directional shading to emphasize elevation slopes
    float3 lightDir = normalize(float3(0.4, 0.9, 0.2));
    float ambient = 0.4;
    float diffuse = max(dot(normalize(input.normal), lightDir), 0.0) * 0.6;
    
    float3 finalColor = input.color * (ambient + diffuse);
    return float4(finalColor, 1.0);
}

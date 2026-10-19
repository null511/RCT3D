import lib.uniforms;


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

    float3 sunDir = normalize(globalUniforms.sunDirection.xyz);
    float3 geoNormal = normalize(input.normal);
    color.rgb *= max(dot(geoNormal, sunDir), 0.0) * globalUniforms.lighting.y + globalUniforms.lighting.x;

    //return float4(geoNormal * 0.5 + 0.5, 1.0);

    return color;
}

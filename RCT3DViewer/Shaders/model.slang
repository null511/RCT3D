import lib.uniforms;


struct VertexInput
{
	float3 position     : POSITION;
	float3 normal       : NORMAL;
	float2 texCoord     : TEXCOORD0;
	float materialId    : TEXCOORD1;
	float4 transformRow0 : TEXCOORD2;
	float4 transformRow1 : TEXCOORD3;
	float4 transformRow2 : TEXCOORD4;
	float4 transformRow3 : TEXCOORD5;
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
	float4 worldPosition = input.position.x * input.transformRow0
		+ input.position.y * input.transformRow1
		+ input.position.z * input.transformRow2
		+ input.transformRow3;
	float3 worldNormal = input.normal.x * input.transformRow0.xyz
		+ input.normal.y * input.transformRow1.xyz
		+ input.normal.z * input.transformRow2.xyz;

	output.position = mul(worldPosition, globalUniforms.viewProjection);
	output.normal = normalize(worldNormal);
	output.texCoord = input.texCoord;
	output.materialId = (uint)input.materialId;
	return output;
}

[shader("fragment")]
float4 fragmentMain(VertexStageOutput input) : SV_Target
{
	float4 color = terrainTextures.Sample(terrainSampler, float3(input.texCoord, input.materialId));

	float3 sunDir = normalize(globalUniforms.sunDirection.xyz);
	color.rgb *= max(dot(normalize(input.normal), sunDir), 0.0) * globalUniforms.lighting.y + globalUniforms.lighting.x;
	return color;
}

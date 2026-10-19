import lib.uniforms;

struct VertexInput
{
	float3 position      : POSITION;
	float3 normal        : NORMAL;
	float2 texCoord      : TEXCOORD0;
	float materialId     : TEXCOORD1;
	float4 transformRow0 : TEXCOORD2;
	float4 transformRow1 : TEXCOORD3;
	float4 transformRow2 : TEXCOORD4;
	float4 transformRow3 : TEXCOORD5;
};

struct VertexStageOutput
{
	float4 position : SV_Position;
};

[shader("vertex")]
VertexStageOutput vertexMain(VertexInput input)
{
	VertexStageOutput output;
	float4 worldPosition = input.position.x * input.transformRow0
		+ input.position.y * input.transformRow1
		+ input.position.z * input.transformRow2
		+ input.transformRow3;
	output.position = mul(worldPosition, globalUniforms.viewProjection);
	return output;
}

[shader("fragment")]
float4 fragmentMain() : SV_Target
{
	return float4(1.0, 0.0, 0.0, 1.0);
}

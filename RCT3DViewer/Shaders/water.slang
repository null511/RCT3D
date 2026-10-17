import lib.uniforms;


struct VertexInput
{
	float3 position : POSITION;
};

struct VertexStageOutput
{
	float4 position : SV_Position;
};

[shader("vertex")]
VertexStageOutput vertexMain(VertexInput input)
{
	VertexStageOutput output;
	output.position = mul(float4(input.position, 1.0), globalUniforms.viewProjection);
	return output;
}

[shader("fragment")]
float4 fragmentMain() : SV_Target
{
	return float4(0.08, 0.38, 0.52, 0.55);
}

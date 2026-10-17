public module uniforms;


[[vk::push_constant]]
Uniforms globalUniforms;

struct Uniforms {
	float4x4 viewProjection;
	float4 sunDirection;
	float4 lighting;
};

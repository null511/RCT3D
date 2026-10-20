public module uniforms;

struct CameraUniforms {
	float4 cameraPosition;
	float4x4 viewProjection;
	float4x4 viewProjectionInverse;
};

struct TemporalUniforms {
	float4 cameraPositionLast;
	float4x4 viewProjectionLast;
};

struct SceneUniforms {
	uint2 renderDimensions;
	float2 taa_jitter;
	float4 sunDirection;
	float4 lighting;
	uint frameCounter;
	float taa_sharpen;
	float mipBias;
	float padding;
};

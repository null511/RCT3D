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
	uint frameCounter;
	float taa_sharpen;
    float mipBias;
    DebugView debugView;
};

struct ShadowUniforms {
	float4x4 shadowViewProjection;
	float distortionFactor;
	float3 padding;
};

enum DebugView : uint {
    None = 0,
    Albedo = 1,
    GeoNormal = 2,
    TexNormal = 3,
};

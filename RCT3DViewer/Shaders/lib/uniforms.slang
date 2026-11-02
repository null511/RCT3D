public module uniforms;

struct CameraUniforms {
	float4 cameraPosition;
	float4x4 viewProjection;
	float4x4 projectionInverse;
	float4x4 viewToWorld;
};

struct TemporalUniforms {
	float4x4 currentViewToPreviousClip;
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
	float4x4 shadowViewProjection0;
	float4x4 shadowViewProjection1;
	float4x4 shadowViewProjection2;
	float4x4 shadowViewProjection3;
	float4 cascadeSplits;
};

enum DebugView : uint {
    None = 0,
    Albedo = 1,
    GeoNormal = 2,
    TexNormal = 3,
	SpecularRoughness = 4,
	SpecularMetalness = 5,
	SpecularSss = 6,
	SpecularEmission = 7,
};

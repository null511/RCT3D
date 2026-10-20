import lib.common;
import lib.uniforms;

[[vk::binding(0, 0)]] ConstantBuffer<CameraUniforms> cameraUniforms;
[[vk::binding(1, 0)]] ConstantBuffer<TemporalUniforms> temporalUniforms;
[[vk::binding(2, 0)]] ConstantBuffer<SceneUniforms> sceneUniforms;

struct ComputeInput {
    uint groupIndex : SV_GroupIndex;
    uint2 groupPos : SV_GroupID;
    uint2 localPos : SV_GroupThreadID;
    uint2 globalPos : SV_DispatchThreadID;
};

[[vk::binding(0, 1)]] Texture2D<float4> sceneOutput;
[[vk::binding(1, 1)]] Texture2D<float4> taaHistory;
[[vk::binding(2, 1)]] Texture2D<float> defer_depth;
[[vk::binding(3, 1)]] SamplerState linearSampler;
[[vk::binding(4, 1), vk::image_format("rgba16f")]] RWTexture2D<float4> resolvedOutput;

extern static const float TAA_RejectionStrength = 0.1;
extern static const int TAA_MaxAccumFrames = 8;

static const uint sharedBufferRes = 18;
static const uint sharedBufferSize = sharedBufferRes * sharedBufferRes;
groupshared float3 sharedBuffer[sharedBufferSize];

[shader("compute")]
[numthreads(16, 16)]
void main(ComputeInput input) {
    uint i_base = input.groupIndex * 2u;

    if (i_base < sharedBufferSize) {
        uint2 uv_base = input.groupPos * 16u - 1u;

        [ForceUnroll]
        for (uint i = 0u; i < 2u; i++) {
            uint i_shared = i_base + i;

            if (i_shared < sharedBufferSize) {
                int2 uv_i = getSharedUV(i_shared);
                int3 pos = int3(uv_base + uv_i, 0);
                pos.xy = clamp(pos.xy, int2(0), int2(sceneUniforms.renderDimensions) - 1);

                sharedBuffer[i_shared] = sceneOutput.Load(pos).rgb;
            }
        }
    }

    GroupMemoryBarrierWithGroupSync();

    if (all(input.globalPos < sceneUniforms.renderDimensions)) {
        float2 texcoord = (input.globalPos + 0.5) / sceneUniforms.renderDimensions;
        float depth = defer_depth.Load(int3(input.globalPos, 0));

        float3 clipPos = float3(texcoord * 2.0 - 1.0, depth);
        float3 clipPosLast = reproject(clipPos);
        float2 uv_prev = clipPosLast.xy * 0.5 + 0.5;

        float4 lastColor = taaHistory.SampleLevel(linearSampler, uv_prev, 0);

        float mixRate = TAA_MaxAccumFrames; // clamp(lastColor.a, 0.0, TAA_MaxAccumFrames);
        if (!all(saturate(uv_prev) == uv_prev)) mixRate = 0.0;

        int2 luv = input.localPos + 1;
        float3 in0 = sharedBuffer[getSharedIndex(luv)];

        float3 color_min = in0;
        float3 color_max = in0;

        for (int i = 0; i < 8; i++) {
            float3 in1 = sharedBuffer[getSharedIndex(luv + sample_offsets[i])];

            color_min = min(color_min, in1);
            color_max = max(color_max, in1);
        }

        float3 colorFinal = lerp(lastColor.rgb, in0, 1.0 / ++mixRate);

        if (sceneUniforms.frameCounter == 0) colorFinal = in0;

        colorFinal = clamp(colorFinal, color_min, color_max);

        colorFinal = clamp(colorFinal, 0.0, 65504.0);

        resolvedOutput.Store(input.globalPos, float4(colorFinal, 1.0));
    }
}

uint2 getSharedUV(const in uint z) {
    return uint2(
        z % sharedBufferRes,
        uint(z / float(sharedBufferRes))
    );
}

int getSharedIndex(const in int2 uv) {
    return uv.y * sharedBufferRes + uv.x;
}

float3 reproject(float3 clipPos) {
    float3 localPos = mul_project(clipPos, cameraUniforms.viewProjectionInverse);
    return mul_project(localPos, temporalUniforms.viewProjectionLast);
}

static const int2 sample_offsets[] = {
    int2(-1, -1),
    int2(0, -1),
    int2(1, -1),
    int2(-1, 0),
    int2(1, 0),
    int2(-1, 1),
    int2(0, 1),
    int2(1, 1),
};

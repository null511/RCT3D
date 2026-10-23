import lib.common;
import lib.uniforms;

[[vk::binding(2, 0)]] ConstantBuffer<SceneUniforms> sceneUniforms;

[[vk::binding(0, 1)]] Texture2D<float4> taaResolved;
[[vk::binding(1, 1)]] SamplerState linearSampler;
[[vk::binding(2, 1), vk::image_format("rgba16f")]] RWTexture2D<float4> sceneOutput;
[[vk::binding(3, 1), vk::image_format("rgba16f")]] RWTexture2D<float4> taaHistory;

static const uint sharedBufferRes = 18;
static const uint sharedBufferSize = sharedBufferRes * sharedBufferRes;
groupshared float3 sharedBuffer[sharedBufferSize];

struct ComputeInput {
    uint groupIndex : SV_GroupIndex;
    uint2 groupPos : SV_GroupID;
    uint2 localPos : SV_GroupThreadID;
    uint2 globalPos : SV_DispatchThreadID;
};

[shader("compute")]
[numthreads(16, 16)]
void main(ComputeInput input) {
    uint i_base = input.groupIndex * 2u;

    if (i_base < sharedBufferSize) {
        // preload shared memory
        uint2 uv_base = input.groupPos * 16u - 1u;

        [ForceUnroll]
        for (uint i = 0u; i < 2u; i++) {
            uint i_shared = i_base + i;

            if (i_shared < sharedBufferSize) {
                int2 uv_i = getSharedUV(i_shared);
                int3 pos = int3(uv_base + uv_i, 0);
                pos.xy = clamp(pos.xy, int2(0), int2(sceneUniforms.renderDimensions) - 1);

                float3 color = taaResolved.Load(pos).rgb;
                sharedBuffer[i_shared] = saturate(color);
            }
        }
    }

    GroupMemoryBarrierWithGroupSync();

    if (all(input.globalPos < sceneUniforms.renderDimensions)) {
        // Simplified version of "slow" CAS without upscaling or better diagonals
        // https://github.com/GPUOpen-Effects/FidelityFX-CAS/blob/master/ffx-cas/ffx_cas.h#L423

        const float peak = -rcp(lerp(8.0, 5.0, sceneUniforms.taa_sharpen));

        int2 uv = input.localPos + 1;
        float3 b = sharedBuffer[getSharedIndex(uv + int2(0, -1))];
        float3 d = sharedBuffer[getSharedIndex(uv + int2(-1, 0))];
        float3 e = sharedBuffer[getSharedIndex(uv)];
        float3 f = sharedBuffer[getSharedIndex(uv + int2(1, 0))];
        float3 h = sharedBuffer[getSharedIndex(uv + int2(0, 1))];

        float3 area_min = minOf(minOf(d, e, f), b, h);
        float3 area_max = maxOf(maxOf(d, e, f), b, h);

        float3 amp = min(area_min, 1.0 - area_max) * rcp(area_max);
        float3 w = sqrt(saturate(amp)) * peak;

        float3 weight_inv = rcp(1.0 + 4.0 * w);
        float3 color = ((b + d + f + h) * w + e) * weight_inv;

        color = e;

        sceneOutput.Store(input.globalPos, float4(color, 1.0));
        taaHistory.Store(input.globalPos, float4(e, 1.0));
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

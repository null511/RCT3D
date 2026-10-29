public module common;

static const float PI = 3.14159265358979323846;

static const float AlphaThreshold = 0.2;

float3 mul3(const in float3 vector, const in float4x4 matrix) {
    return mul(float4(vector, 1.0), matrix).xyz;
}

float3 mul_project(const in float3 vector, const in float4x4 matrix) {
    float4 result = mul(float4(vector, 1.0), matrix);
    return result.xyz / result.w;
}

float3 reconstructViewPosition(const in uint2 pixelPosition, const in float depth,
    const in uint2 renderDimensions, const in float4x4 projectionInverse) {
    float2 uv = (pixelPosition + 0.5) / float2(renderDimensions);
    float3 clipPosition = float3(uv * 2.0 - 1.0, depth);
    return mul_project(clipPosition, projectionInverse);
}

float3 maxOf(float3 x, float3 y, float3 z) {
    return max(x, max(y, z));
}

float3 minOf(float3 x, float3 y, float3 z) {
    return min(x, min(y, z));
}

T pow2<T>(const in T value) where T : IArithmetic {
    return value * value;
}

T sumOf<T>(const in vector<T, 2> vec) where T : IArithmetic {
    return vec.x + vec.y;
}

T sumOf<T>(const in vector<T, 3> vec) where T : IArithmetic {
    return vec.x + vec.y + vec.z;
}

T unmix<T>(const in T valueMin, const in T valueMax, const in T value) where T : IFloat {
    return (value - valueMin) / (valueMax - valueMin);
}

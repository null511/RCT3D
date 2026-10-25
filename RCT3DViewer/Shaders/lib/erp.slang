public module ERP;

import lib.common;

namespace ERP {

    static const float2 TAU_PI = float2(2.0, 1.0) * PI;
    static const float2 TAU_PI_INV = 1.0 / TAU_PI;

    float3 FromUV(in float2 uv) {
        // uv.y = 1.0 - uv.y;

        float2 sphereCoord = (uv - float2(0.5, 0.0)) * TAU_PI;

        float2 _sin = sin(sphereCoord);
        float2 _cos = cos(sphereCoord);

        return float3(_cos.xy, _sin.x) * float3(_sin.y, 1.0, _sin.y);
    }

    float2 ToUV(const in float3 dir) {
        float2 uv = float2(0.5, sign(-dir.y));

        if (abs(dir.y) < 0.9999) {
            uv = float2(atan2(dir.z, dir.x), acos(dir.y));
            uv = (uv * TAU_PI_INV) + float2(0.5, 0.0);

            // uv.y = 1.0 - uv.y;
        }

        return uv;
    }

}

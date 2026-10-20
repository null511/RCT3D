public module octohedral;

import lib.common;


namespace octohedral {

    internal float2 wrap(const in float2 v) {
        return (1.0 - abs(v.yx)) * select(v.xy >= 0.0, 1.0, -1.0);
    }

    public float2 encode(float3 n) {
        n /= sumOf(abs(n));
        n.xy = n.z >= 0.0 ? n.xy : wrap(n.xy);
        n.xy = n.xy * 0.5 + 0.5;
        return n.xy;
    }

    public float3 decode(float2 f) {
        f = f * 2.0 - 1.0;
    
        // https://twitter.com/Stubbesaurus/status/937994790553227264
        float3 n = float3(f.xy, 1.0 - sumOf(abs(f.xy)));
        float t = saturate(-n.z);
        n.xy += select(n.xy >= 0.0, -t, t);
        return normalize(n);
    }

}

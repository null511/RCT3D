public module fresnel;

import lib.common;

float FresnelSchlick(float cosine, float f0) {
    return f0 + (1.0 - f0) * pow5(1.0 - saturate(cosine));
}

float3 FresnelSchlick(float cosine, float3 f0) {
    return f0 + (1.0 - f0) * pow5(1.0 - saturate(cosine));
}

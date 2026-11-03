public module ggx;

import lib.common;

float DistributionGgx(float nDotH, float roughness) {
    float alpha = roughness * roughness;
    float alphaSquared = alpha * alpha;

    float denominator = nDotH * nDotH * (alphaSquared - 1.0) + 1.0;
    return alphaSquared / max(PI * denominator * denominator, 0.000001);
}

float GeometrySchlickGgx(float nDotDirection, float roughness) {
    float k = (roughness + 1.0) * (roughness + 1.0) / 8.0;
    return nDotDirection / max(nDotDirection * (1.0 - k) + k, 0.000001);
}

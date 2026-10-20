public module celestial;

static const float SunAngularRadius = 0.0098;
static const float MoonAngularRadius = 0.0098;

float skySphere(const in float3 rayDir, const in float3 lightDir, const in float solidAngle) {
    float minCosTheta = cos(solidAngle);
    float cosTheta = dot(rayDir, lightDir);
    return saturate((cosTheta - minCosTheta) / (1.0 - minCosTheta));
}

float renderSun(float3 rayDir, float3 sunDir) {
    return skySphere(rayDir, sunDir, SunAngularRadius);
}

float renderMoon(float3 rayDir, float3 moonDir) {
    return skySphere(rayDir, moonDir, MoonAngularRadius);
}

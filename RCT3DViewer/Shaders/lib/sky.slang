public module sky;

import lib.common;
import lib.uniforms;

static const float3 groundAlbedo = float3(0.376, 0.4, 0.29);
static const bool planetSurfaceEnabled = true;

static const float SunLux = 8.0;// * transformer.fromGamma(1.0);
//static const float3 MoonColor = 0.2;// * transformer.fromGamma(1.0);

// TODO: use camera height?
static const float3 sky_viewPos = float3(0.0, GroundRadius_MM + 0.0002, 0.0);

static const float GroundRadius_MM = 6.371;
static const float AtmosphereRadius_MM = 6.471;
static const float SunAngularRadius = 0.0093;
static const float MoonAngularRadius = 0.0092;

static const float3 rayleighScatteringBase = float3(5.802, 13.558, 33.1);
static const float rayleighAbsorptionBase = 0.0;

static const float mieScatteringBase = 3.996;
static const float mieAbsorptionBase = 4.4;

static const float3 ozoneAbsorptionBase = float3(0.650, 1.881, 0.085);


float safeacos(const in float x) {
    return acos(clamp(x, -1.0, 1.0));
}

// From https://gamedev.stackexchange.com/questions/96459/fast-ray-sphere-collision-code.
float rayIntersectSphere(const in float3 center, const in float3 direction, const in float radius) {
    float b = dot(center, direction);
    float c = dot(center, center) - pow2(radius);
    if (c > 0.0f && b > 0.0) return -1.0;

    float discr = b*b - c;
    if (discr < 0.0) return -1.0;

    // Special case: inside sphere, use far discriminant
    if (discr > b*b) return (-b + sqrt(discr));
    return -b - sqrt(discr);
}

void getScatteringValues(float3 pos, out float3 rayleighScattering, out float mieScattering, out float3 extinction) {
    float altitude_KM = (length(pos) - GroundRadius_MM) * 1000.0;

    // Note: Paper gets these switched up.
    float rayleighDensity = exp(-altitude_KM / 8.0);
    float mieDensity = exp(-altitude_KM / 1.2);

    rayleighScattering = rayleighScatteringBase * rayleighDensity;
    float rayleighAbsorption = rayleighAbsorptionBase * rayleighDensity;

    mieScattering = mieScatteringBase * mieDensity;
    float mieAbsorption = mieAbsorptionBase * mieDensity;

    float3 ozoneAbsorption = ozoneAbsorptionBase * max(0.0, 1.0 - abs(altitude_KM - 25.0) / 15.0);

    extinction = rayleighScattering + rayleighAbsorption + mieScattering + mieAbsorption + ozoneAbsorption;
}

float getMiePhase(float cosTheta) {
    const float g = 0.8;
    const float scale = 3.0 / (8.0 * PI);

    float num = (1.0 - g * g) * (1.0 + cosTheta * cosTheta);
    float denom = (2.0 + g * g) * pow((1.0 + g * g - 2.0 * g * cosTheta), 1.5);

    return scale * num / denom;
}

float getRayleighPhase(float cosTheta) {
    const float k = 3.0 / (16.0 * PI);
    return k * (1.0 + cosTheta * cosTheta);
}

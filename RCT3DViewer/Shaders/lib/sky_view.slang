public module sky_view;

import lib.common;
import lib.sky;

void getSkyViewParameters(float3 sunDir, out float3 up, out float3 right, out float3 forward, out float horizonAngle) {
    float height = length(sky_viewPos);
    up = sky_viewPos / height;

    horizonAngle = safeacos(sqrt(height * height - GroundRadius_MM * GroundRadius_MM) / height);
    right = cross(sunDir, up);

    if (dot(right, right) < 1e-6) {
        right = cross(float3(0.0, 0.0, 1.0), up);
    }

    forward = cross(up, right);
}

float3 getValFromSkyLUT(Texture2D tex, SamplerState sampler, float3 rayDir, float3 up, float3 right, float3 forward, float horizonAngle) {
    float altitudeAngle = horizonAngle - safeacos(dot(rayDir, up)); // Between -PI/2 and PI/2
    float azimuthAngle;                                         // Between 0 and 2*PI

    if (abs(altitudeAngle) > (0.5 * PI - .0001)) {
        // Looking nearly straight up or down.
        azimuthAngle = 0.0;
    } else {
        float3 projectedDir = rayDir - up * (dot(rayDir, up));
        float sinTheta = dot(projectedDir, right);
        float cosTheta = dot(projectedDir, forward);
        azimuthAngle = atan2(sinTheta, cosTheta) + PI;
    }

    // Non-linear mapping of altitude angle. See Section 5.3 of the paper.
    float v = 0.5 + 0.5 * sign(altitudeAngle) * sqrt(abs(altitudeAngle) * 2.0 / PI);
    float2 texcoord = float2(azimuthAngle / (2.0 * PI), v);

    return tex.SampleLevel(sampler, texcoord, 0).rgb;
}

float3 getValFromSkyLUT(Texture2D tex, SamplerState sampler, float3 rayDir, float3 sunDir) {
    float horizonAngle;
    float3 up, right, forward;
    getSkyViewParameters(sunDir, up, right, forward, horizonAngle);
    return getValFromSkyLUT(tex, sampler, rayDir, up, right, forward, horizonAngle);
}

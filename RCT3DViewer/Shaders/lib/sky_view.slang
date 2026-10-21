public module sky_view;

import lib.common;
import lib.sky;

float3 getValFromSkyLUT(Texture2D tex, SamplerState sampler, float3 rayDir, float3 sunDir) {
    float height = length(sky_viewPos);
    float3 up = sky_viewPos / height;

    float horizonAngle = safeacos(sqrt(height * height - GroundRadius_MM * GroundRadius_MM) / height);
    float altitudeAngle = horizonAngle - acos(dot(rayDir, up)); // Between -PI/2 and PI/2
    float azimuthAngle;                                         // Between 0 and 2*PI

    if (abs(altitudeAngle) > (0.5 * PI - .0001)) {
        // Looking nearly straight up or down.
        azimuthAngle = 0.0;
    } else {
        float3 right = cross(sunDir, up);
        float3 forward = cross(up, right);

        float3 projectedDir = normalize(rayDir - up * (dot(rayDir, up)));
        float sinTheta = dot(projectedDir, right);
        float cosTheta = dot(projectedDir, forward);
        azimuthAngle = atan2(sinTheta, cosTheta) + PI;
    }

    // Non-linear mapping of altitude angle. See Section 5.3 of the paper.
    float v = 0.5 + 0.5 * sign(altitudeAngle) * sqrt(abs(altitudeAngle) * 2.0 / PI);
    float2 texcoord = float2(azimuthAngle / (2.0 * PI), v);

    return tex.SampleLevel(sampler, texcoord, 0).rgb;
}

public module shadow;

float4 distortShadowClip(float4 clipPosition, float distortionFactor)
{
	float2 ndc = clipPosition.xy / clipPosition.w;
    float radialScale = length(ndc) + distortionFactor;
    clipPosition.xy = ndc / radialScale;
	return clipPosition;
}

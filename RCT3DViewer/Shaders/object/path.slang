import lib.common;
import lib.uniforms;
import lib.normals;
import lib.octohedral;

[[vk::binding(0, 0)]] ConstantBuffer<CameraUniforms> cameraUniforms;
[[vk::binding(2, 0)]] ConstantBuffer<SceneUniforms> sceneUniforms;

[[vk::binding(0, 1)]] Texture2DArray<float4> pathColors[64];
[[vk::binding(1, 1)]] Texture2DArray<float4> pathNormals[64];
[[vk::binding(2, 1)]] Texture2DArray<float4> pathSpecular[64];
[[vk::binding(3, 1)]] SamplerState pathSampler;

struct VertexInput {
	[[vk::location(0)]] float3 position;
	[[vk::location(1)]] float3 normal;
	[[vk::location(2)]] float2 texCoord;
	[[vk::location(3)]] uint descriptorIndex;
	[[vk::location(4)]] uint layer;
};

struct VertexOutput {
	float4 position : SV_Position;
	float3 worldPosition : TEXCOORD0;
	float3 normal : TEXCOORD1;
	float2 texCoord : TEXCOORD2;
	nointerpolation uint descriptorIndex : TEXCOORD3;
	nointerpolation uint layer : TEXCOORD4;
};

struct GBufferOutput {
	float4 albedo : SV_Target0;
	float4 normals : SV_Target1;
	float4 specular : SV_Target2;
};

[shader("vertex")]
VertexOutput vertexMain(VertexInput input)
{
	VertexOutput output;
	output.position = mul(float4(input.position, 1.0), cameraUniforms.viewProjection);
	output.position.xy += (sceneUniforms.taa_jitter / sceneUniforms.renderDimensions) * (2.0 * output.position.w);
	output.worldPosition = input.position;
	output.normal = input.normal;
	output.texCoord = input.texCoord;
	output.descriptorIndex = input.descriptorIndex;
	output.layer = input.layer;
	return output;
}

[shader("fragment")]
GBufferOutput fragmentMain(VertexOutput input)
{
	float2 dx = ddx(input.texCoord) * exp2(sceneUniforms.mipBias);
	float2 dy = ddy(input.texCoord) * exp2(sceneUniforms.mipBias);
	float3 uv = float3(input.texCoord, input.layer);
	uint index = NonUniformResourceIndex(input.descriptorIndex);
	float4 albedo = pathColors[index].SampleGrad(pathSampler, uv, dx, dy);
	float4 normalMap = pathNormals[index].SampleGrad(pathSampler, uv, dx, dy);
	float4 specular = pathSpecular[index].SampleGrad(pathSampler, uv, dx, dy);
	float3 geoNormal = normalize(input.normal);
	float3 tangent, bitangent;
	getTangent(input.texCoord, input.worldPosition, geoNormal, tangent, bitangent);
	float3 tangentNormal = restoreNormal(normalMap.rg * 2.0 - 1.0);
	float3 texNormal = normalize(tangent * tangentNormal.x + bitangent * tangentNormal.y + geoNormal * tangentNormal.z);
	if (albedo.a < AlphaThreshold) discard;
	GBufferOutput output;
	output.albedo = float4(albedo.rgb, normalMap.b);
	output.normals.xy = octohedral::encode(geoNormal);
	output.normals.zw = octohedral::encode(texNormal);
	output.specular = specular;
	return output;
}

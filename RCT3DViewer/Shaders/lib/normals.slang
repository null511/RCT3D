public module normals;


void getTangent(in float2 texcoord, in float3 worldPosition, in float3 geoNormal, out float3 tangent, out float3 bitangent) {
    float2 uvDx = ddx(texcoord);
    float2 uvDy = ddy(texcoord);

    float3 positionDx = ddx(worldPosition);
    float3 positionDy = ddy(worldPosition);

    float uvDeterminant = uvDx.x * uvDy.y - uvDx.y * uvDy.x;
    float inverseUvDeterminant = (uvDeterminant < 0.0 ? -1.0 : 1.0) / max(abs(uvDeterminant), 0.000001);
    tangent = normalize((positionDx * uvDy.y - positionDy * uvDx.y) * inverseUvDeterminant);

    float3 tangent2 = normalize(tangent - geoNormal * dot(geoNormal, tangent));
    bitangent = -normalize(cross(geoNormal, tangent2));
}

float3 restoreNormal(const in float2 normal_xy) {
    return float3(
        normal_xy,
        sqrt(saturate(1.0 - dot(normal_xy, normal_xy)))
    );
}

using RCT3D.Interop;
using System.Numerics;

namespace RCT3D.Render.Rides.Tracks;

internal delegate bool TryGetCenterlinePoseDelegate(
    in RCTVehicleData previous,
    in RCTVehicleData current,
    float amount,
    out Vector3 position,
    out Vector3 tangent,
    out float rollRadians);

internal static class TrackCenterlinePoseResolver
{
    public static bool TryResolve(
        in RCTVehicleData vehicle,
        ReadOnlySpan<TrackCenterlinePiece> pieces,
        out Vector3 position,
        out Vector3 tangent,
        out float rollRadians)
    {
        position = default;
        tangent = default;
        rollRadians = 0.0f;

        if (vehicle.TrackProgressTotal == 0 || vehicle.TrackProgress >= vehicle.TrackProgressTotal)
            return false;

        var trackType = (RCTTrackElementType)(vehicle.TrackTypeAndDirection >> 2);
        var direction = (byte)(vehicle.TrackTypeAndDirection & 3);
        var trackTileX = vehicle.TrackLocationX >> 5;
        var trackTileY = vehicle.TrackLocationY >> 5;
        var baseHeight = vehicle.TrackLocationZ >> 3;

        foreach (ref readonly var piece in pieces) {
            if (piece.RideId != vehicle.RideId || piece.TrackType != trackType || piece.Direction != direction)
                continue;

            foreach (ref readonly var sequence in piece.Sequences.AsSpan()) {
                if (sequence.TileX != trackTileX || sequence.TileY != trackTileY || sequence.BaseHeight != baseHeight)
                    continue;

                var progress = (float)vehicle.TrackProgress / vehicle.TrackProgressTotal;
                return TrySample(sequence.Path, progress, out position, out tangent, out rollRadians);
            }
        }

        return false;
    }

    public static bool TryInterpolate(
        in RCTVehicleData previous,
        in RCTVehicleData current,
        float amount,
        ReadOnlySpan<TrackCenterlinePiece> pieces,
        out Vector3 position,
        out Vector3 tangent,
        out float rollRadians)
    {
        if (!TryResolve(current, pieces, out var currentPosition, out var currentTangent, out var currentRoll)) {
            position = default;
            tangent = default;
            rollRadians = 0.0f;
            return false;
        }

        if (!TryResolve(previous, pieces, out var previousPosition, out var previousTangent, out var previousRoll)) {
            position = currentPosition;
            tangent = currentTangent;
            rollRadians = currentRoll;
            return true;
        }

        position = Vector3.Lerp(previousPosition, currentPosition, amount);
        tangent = Vector3.Lerp(previousTangent, currentTangent, amount);
        if (tangent.LengthSquared() > float.Epsilon)
            tangent = Vector3.Normalize(tangent);
        else
            tangent = currentTangent;
        rollRadians = LerpAngle(previousRoll, currentRoll, amount);
        return true;
    }

    private static bool TrySample(TrackCenterlinePath path, float progress, out Vector3 position, out Vector3 tangent, out float rollRadians)
    {
        position = default;
        tangent = default;
        rollRadians = 0.0f;
        var points = path.Points.AsSpan();
        var rolls = path.RollRadians.AsSpan();
        if (points.Length < 2 || !float.IsFinite(progress) || progress < 0.0f || progress > 1.0f)
            return false;

        var totalLength = 0.0f;
        for (var i = 0; i < points.Length - 1; i++)
            totalLength += Vector3.Distance(points[i], points[i + 1]);
        if (totalLength <= float.Epsilon)
            return false;

        var targetDistance = totalLength * progress;
        var traversedDistance = 0.0f;
        for (var i = 0; i < points.Length - 1; i++) {
            var segment = points[i + 1] - points[i];
            var segmentLength = segment.Length();
            if (segmentLength <= float.Epsilon) continue;

            if (targetDistance <= traversedDistance + segmentLength || i == points.Length - 2) {
                var segmentProgress = Math.Clamp((targetDistance - traversedDistance) / segmentLength, 0.0f, 1.0f);
                position = Vector3.Lerp(points[i], points[i + 1], segmentProgress);
                tangent = segment / segmentLength;
                if (rolls.Length == points.Length)
                    rollRadians = rolls[i] + (rolls[i + 1] - rolls[i]) * segmentProgress;
                return true;
            }
            traversedDistance += segmentLength;
        }

        return false;
    }

    private static float LerpAngle(float start, float end, float amount)
    {
        var delta = (end - start) % MathF.Tau;
        if (delta > MathF.PI) delta -= MathF.Tau;
        if (delta < -MathF.PI) delta += MathF.Tau;
        return start + delta * amount;
    }
}

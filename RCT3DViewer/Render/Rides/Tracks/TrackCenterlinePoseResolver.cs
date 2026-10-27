using RCT3D.Interop;
using System.Numerics;

namespace RCT3D.Render.Rides.Tracks;

internal delegate bool TryGetCenterlinePoseDelegate(
    in RCTVehicleData previous,
    in RCTVehicleData current,
    float amount,
    out Vector3 position,
    out TrackCenterlineFrame frame);

internal static class TrackCenterlinePoseResolver
{
    public static bool TryResolve(
        in RCTVehicleData vehicle,
        ReadOnlySpan<TrackCenterlinePiece> pieces,
        out Vector3 position,
        out TrackCenterlineFrame frame)
    {
        position = default;
        frame = default;

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
                return TrySample(sequence.Path, progress, out position, out frame);
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
        out TrackCenterlineFrame frame)
    {
        if (!TryResolve(current, pieces, out var currentPosition, out var currentFrame)) {
            position = default;
            frame = default;
            return false;
        }

        if (!TryResolve(previous, pieces, out var previousPosition, out var previousFrame)) {
            position = currentPosition;
            frame = currentFrame;
            return true;
        }

        position = Vector3.Lerp(previousPosition, currentPosition, amount);
        var tangent = NormalizeLerp(previousFrame.Tangent, currentFrame.Tangent, amount, currentFrame.Tangent);
        var side = NormalizeLerp(previousFrame.Side, currentFrame.Side, amount, currentFrame.Side);
        side -= tangent * Vector3.Dot(side, tangent);
        if (side.LengthSquared() <= float.Epsilon)
            side = currentFrame.Side - tangent * Vector3.Dot(currentFrame.Side, tangent);
        side = Vector3.Normalize(side);
        var up = Vector3.Normalize(Vector3.Cross(tangent, side));
        if (Vector3.Dot(up, currentFrame.Up) < 0.0f) {
            side = -side;
            up = -up;
        }
        frame = new TrackCenterlineFrame(tangent, side, up);
        return true;
    }

    private static bool TrySample(TrackCenterlinePath path, float progress, out Vector3 position, out TrackCenterlineFrame frame)
    {
        position = default;
        frame = default;
        var points = path.Points.AsSpan();
        var frames = path.Frames.AsSpan();
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
                if (frames.Length != points.Length) return false;

                var tangent = NormalizeLerp(frames[i].Tangent, frames[i + 1].Tangent, segmentProgress, segment / segmentLength);
                var side = NormalizeLerp(frames[i].Side, frames[i + 1].Side, segmentProgress, frames[i].Side);
                side -= tangent * Vector3.Dot(side, tangent);
                if (side.LengthSquared() <= float.Epsilon)
                    side = frames[i + 1].Side - tangent * Vector3.Dot(frames[i + 1].Side, tangent);
                side = Vector3.Normalize(side);
                var up = Vector3.Normalize(Vector3.Cross(tangent, side));
                var expectedUp = NormalizeLerp(frames[i].Up, frames[i + 1].Up, segmentProgress, frames[i].Up);
                if (Vector3.Dot(up, expectedUp) < 0.0f) {
                    side = -side;
                    up = -up;
                }
                frame = new TrackCenterlineFrame(tangent, side, up);
                return true;
            }

            traversedDistance += segmentLength;
        }

        return false;
    }

    private static Vector3 NormalizeLerp(Vector3 start, Vector3 end, float amount, Vector3 fallback)
    {
        var value = Vector3.Lerp(start, end, amount);
        return value.LengthSquared() > float.Epsilon ? Vector3.Normalize(value) : fallback;
    }

}

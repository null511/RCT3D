using RCT3D.Interop.Models;
using RCT3D.Render.Rides.Tracks;
using RCT3D.Simulation;
using System.Diagnostics;
using System.Numerics;

namespace RCT3D.Render;

internal readonly record struct VehiclePlacementOffsets(float WorldHeight, float LocalHeight);

internal readonly record struct VehiclePose(ushort RideId, Vector3 Position, Matrix4x4 Rotation, byte PrimaryPaletteIndex);

internal sealed class VehiclePoseResolver
{
    private readonly Dictionary<ushort, VehiclePose> poses = new();
    private readonly Dictionary<ushort, RCTVehicleData> previousVehicles = new();
    private VehicleSnapshot? previousSnapshot;

    public Dictionary<ushort, VehiclePose> ResolveFrame(
        VehicleSnapshotPair? snapshots,
        long timestamp,
        IReadOnlyDictionary<ushort, VehiclePlacementOffsets> offsetsByRide,
        Func<ushort, bool> usesSmoothMovement,
        TryGetCenterlinePoseDelegate tryGetCenterlinePose)
    {
        poses.Clear();
        if (snapshots is null) {
            Clear();
            return poses;
        }

        if (!ReferenceEquals(previousSnapshot, snapshots.Previous)) {
            previousVehicles.Clear();
            previousSnapshot = snapshots.Previous;
            if (previousSnapshot is not null) {
                foreach (var vehicle in previousSnapshot.Vehicles.Span)
                    previousVehicles.Add(vehicle.EntityId, vehicle);
            }
        }

        var previousTimestamp = snapshots.Previous?.Timestamp ?? snapshots.Current.Timestamp;
        var interval = snapshots.Current.Timestamp - previousTimestamp;
        var targetTimestamp = timestamp - Stopwatch.Frequency / 40;
        var amount = snapshots.Previous is null || interval <= 0
            ? 1.0f
            : Math.Clamp((float)(targetTimestamp - previousTimestamp) / interval, 0.0f, 1.0f);

        foreach (var current in snapshots.Current.Vehicles.Span) {
            var position = ToWorldPosition(current);
            var orientation = GetOrientation(current);
            var swingPosition = (float)current.SwingPosition;
            var previous = default(RCTVehicleData);
            var canInterpolate = interval > 0
                && previousVehicles.TryGetValue(current.EntityId, out previous)
                && previous.RideId == current.RideId
                && previous.TrainHeadEntityId == current.TrainHeadEntityId
                && previous.TrackSubposition == current.TrackSubposition;

            if (canInterpolate) {
                position = Vector3.Lerp(ToWorldPosition(previous), position, amount);
                orientation = Quaternion.Slerp(GetOrientation(previous), orientation, amount);
                swingPosition = previous.SwingPosition + (current.SwingPosition - previous.SwingPosition) * amount;
            }

            var rotation = Matrix4x4.CreateFromQuaternion(orientation);
            var centerlineResolved = false;

            if (canInterpolate
                && usesSmoothMovement(current.RideId)
                && tryGetCenterlinePose(previous, current, amount, out var centerlinePosition, out var centerlineFrame)) {
                position = centerlinePosition;
                rotation = centerlineFrame.ToRotation();
                centerlineResolved = true;
            }

            rotation = Matrix4x4.CreateRotationZ(swingPosition * (MathF.Tau / 65536.0f)) * rotation;
            var offsets = offsetsByRide.GetValueOrDefault(current.RideId);

            if (!centerlineResolved)
                position.Y += offsets.WorldHeight;

            position += Vector3.TransformNormal(new Vector3(0.0f, offsets.LocalHeight, 0.0f), rotation);
            poses[current.EntityId] = new VehiclePose(current.RideId, position, rotation, current.PrimaryPaletteIndex);
        }

        return poses;
    }

    public void Clear()
    {
        poses.Clear();
        previousVehicles.Clear();
        previousSnapshot = null;
    }

    private static Vector3 ToWorldPosition(in RCTVehicleData vehicle) => new(
        vehicle.X * (WorldUnits.TileSize / 32.0f),
        vehicle.Z * (WorldUnits.HeightPerBaseUnit / 8.0f),
        vehicle.Y * (WorldUnits.TileSize / 32.0f));

    private static Quaternion GetOrientation(in RCTVehicleData vehicle) =>
        GetOrientation(vehicle.Orientation, vehicle.PitchRadians, vehicle.RollRadians);

    internal static Quaternion GetOrientation(byte orientation, float pitchRadians, float rollRadians)
    {
        var yaw = orientation * (MathF.Tau / 32.0f) - MathF.PI / 2.0f;
        var rotation = Matrix4x4.CreateRotationZ(rollRadians)
            * Matrix4x4.CreateRotationX(-pitchRadians)
            * Matrix4x4.CreateRotationY(yaw);
        return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(rotation));
    }
}

using RCT3D.Simulation;
using Silk.NET.Input;
using System.Numerics;
using RCT3D.Interop.Models;

namespace RCT3D.Render;

internal sealed class PeepModeController(FlightCamera camera, ParkSceneController scene)
{
    private const float EyeHeight = 1.55f;
    private const float PlacementRadius = 4.5f;
    private const float WalkingSpeed = 2.8f;
    private const float RunningSpeed = 7.0f;
    private const float NativeMoveInterval = 1.0f / 40.0f;
    private const float NativeUnitsPerWorldUnit = 32.0f / WorldUnits.TileSize;
    private const float WorldUnitsPerNativeHeightUnit = WorldUnits.HeightPerBaseUnit / 8.0f;

    private Task<ushort>? createPeepTask;
    private ushort? peepEntityId;
    private PathPose pathPose;
    private Task? enterRideTask;
    private Task? takeControlTask;
    private ushort currentRideId;
    private byte currentStationIndex;
    private float nativeMoveElapsed;
    private Vector3 lastNativePosition;
    private bool exitAfterCreation;
    private bool rideInteractionActive;
    private bool hasRidden;
    private bool exitPoseReady;
    private PathPose exitPose;
    private bool rideCameraAttached;
    private string? statusMessage;

    public bool IsEnabled => peepEntityId.HasValue;
    public bool IsEntering => createPeepTask is not null;
    public string? StatusMessage => statusMessage;

    public void UpdateRideCamera()
    {
        var peep = scene.Simulation.Peep;
        if (!rideInteractionActive || peepEntityId != peep?.EntityId
            || peep is null || (NativePeepState)peep.State != NativePeepState.OnRide) {
            ClearRideCameraRotation();
            return;
        }

        if (!scene.TryGetVehiclePose(peep.VehicleEntityId, peep.RideId, out var pose)) {
            ClearRideCameraRotation();
            return;
        }

        camera.SetBaseRotation(pose.Rotation);
        rideCameraAttached = true;
        camera.Position = pose.Position;
    }


    public void Toggle()
    {
        if (IsEnabled || IsEntering) {
            Exit();
            return;
        }

        var navigation = scene.Navigation;
        if (navigation is null) {
            statusMessage = "Load a park before entering Peep Mode.";
            return;
        }

        if (!navigation.TrySnapToNearestPath(camera.Position, PlacementRadius, EyeHeight, out pathPose)) {
            statusMessage = "Move closer to a path to enter Peep Mode.";
            return;
        }

        camera.Position = pathPose.Position;
        camera.ResetMouseTracking();
        var nativePosition = ToNativePosition(pathPose.Position);
        createPeepTask = scene.Simulation.CreatePeepAsync(nativePosition.X, nativePosition.Y, nativePosition.Z);
        statusMessage = "Entering Peep Mode...";
    }

    public void Update(IKeyboard keyboard, float deltaTime, bool allowMovement)
    {
        if (createPeepTask is { IsCompleted: true } completedTask) {
            createPeepTask = null;
            try {
                var createdId = completedTask.GetAwaiter().GetResult();
                if (exitAfterCreation) {
                    scene.Simulation.RemovePeep(createdId);
                    exitAfterCreation = false;
                    statusMessage = null;
                    return;
                }

                peepEntityId = createdId;
                lastNativePosition = pathPose.Position;
                nativeMoveElapsed = NativeMoveInterval;
                statusMessage = null;
            }
            catch (Exception ex) {
                exitAfterCreation = false;
                statusMessage = $"Could not enter Peep Mode: {ex.Message}";
            }
        }

        if (!peepEntityId.HasValue || createPeepTask is not null) return;
        if (UpdateRideInteraction()) return;
        if (!allowMovement) return;

        var input = Vector2.Zero;
        if (keyboard.IsKeyPressed(Key.W)) input.Y += 1.0f;
        if (keyboard.IsKeyPressed(Key.S)) input.Y -= 1.0f;
        if (keyboard.IsKeyPressed(Key.D)) input.X += 1.0f;
        if (keyboard.IsKeyPressed(Key.A)) input.X -= 1.0f;

        if (input.LengthSquared() > 1.0f)
            input = Vector2.Normalize(input);

        if (input.LengthSquared() > 0.0f) {
            var horizontal = camera.HorizontalRight * input.X + camera.HorizontalForward * input.Y;
            var speed = keyboard.IsKeyPressed(Key.ShiftLeft) ? RunningSpeed : WalkingSpeed;
            var displacement = new Vector2(horizontal.X, horizontal.Z) * speed * Math.Max(deltaTime, 0.0f);
            if (scene.Navigation is { } navigation && navigation.TryMove(pathPose, displacement, EyeHeight, out var moved)) {
                pathPose = moved;
                camera.Position = moved.Position;
            }
        }

        nativeMoveElapsed += Math.Max(deltaTime, 0.0f);
        if (nativeMoveElapsed < NativeMoveInterval || Vector3.DistanceSquared(lastNativePosition, pathPose.Position) < 0.0001f)
            return;

        nativeMoveElapsed = 0.0f;
        lastNativePosition = pathPose.Position;
        var native = ToNativePosition(pathPose.Position);
        scene.Simulation.MovePeep(peepEntityId.Value, native.X, native.Y, native.Z, GetDirection(camera.HorizontalForward));
    }

    public void Exit()
    {
        if (createPeepTask is not null) {
            exitAfterCreation = true;
            statusMessage = "Cancelling Peep Mode entry...";
        }

        if (peepEntityId is ushort entityId) {
            scene.Simulation.ReleasePeep(entityId);
            peepEntityId = null;
        }

        enterRideTask = null;
        takeControlTask = null;
        rideInteractionActive = false;
        hasRidden = false;
        exitPoseReady = false;
        if (rideCameraAttached) {
            camera.ClearBaseRotationPreservingOrientation();
            rideCameraAttached = false;
        }
        statusMessage = null;
        camera.ResetMouseTracking();
    }

    public void Interact()
    {
        if (!IsEnabled || rideInteractionActive || enterRideTask is not null || takeControlTask is not null) return;

        var nearestEntrance = default(RCTRideEntranceData);
        var nearestDistanceSquared = float.PositiveInfinity;
        foreach (var entrance in scene.RideEntrances) {
            if (entrance.EntranceType != RCTRideEntranceType.Entrance) continue;

            var position = GetEntranceCameraPosition(entrance);
            var distanceSquared = Vector3.DistanceSquared(camera.Position, position);
            if (distanceSquared >= nearestDistanceSquared) continue;
            nearestDistanceSquared = distanceSquared;
            nearestEntrance = entrance;
        }

        if (nearestDistanceSquared > 3.25f * 3.25f) {
            statusMessage = "Move closer to a ride entrance and press E to join its queue.";
            return;
        }

        currentRideId = nearestEntrance.RideId;
        currentStationIndex = nearestEntrance.StationIndex;
        enterRideTask = scene.Simulation.EnterRideAsync(peepEntityId!.Value, currentRideId, currentStationIndex);
        rideInteractionActive = true;
        hasRidden = false;
        exitPoseReady = false;
        statusMessage = "Joining ride queue...";
    }

    private bool UpdateRideInteraction()
    {
        if (enterRideTask is { IsCompleted: true } completedEnterTask) {
            enterRideTask = null;
            try {
                completedEnterTask.GetAwaiter().GetResult();
                statusMessage = "Waiting in the ride queue...";
            }
            catch (Exception ex) {
                rideInteractionActive = false;
                statusMessage = $"Could not enter ride: {ex.Message}";
                return false;
            }
        }

        if (!rideInteractionActive) return false;

        if (takeControlTask is { IsCompleted: true } completedControlTask) {
            takeControlTask = null;
            try {
                completedControlTask.GetAwaiter().GetResult();
                pathPose = exitPose;
                camera.Position = exitPose.Position;
                rideInteractionActive = false;
                hasRidden = false;
                exitPoseReady = false;
                statusMessage = null;
                return false;
            }
            catch (Exception ex) {
                statusMessage = $"Could not leave ride control: {ex.Message}";
                return true;
            }
        }

        var peep = scene.Simulation.Peep;
        if (peep is null) return true;

        switch ((NativePeepState)peep.State) {
            case NativePeepState.QueuingFront:
            case NativePeepState.Queuing:
            case NativePeepState.EnteringRide:
                ClearRideCameraRotation();
                camera.Position = ToWorldCameraPosition(peep);
                statusMessage = "Waiting in the ride queue...";
                return true;
            case NativePeepState.OnRide:
                hasRidden = true;
                statusMessage = "Riding...";
                return true;
            case NativePeepState.LeavingRide:
                ClearRideCameraRotation();
                currentStationIndex = peep.StationIndex;
                // OpenRCT2 enters leavingRide before updateRideLeaveVehicle copies the
                // vehicle's current station to currentRideStation. Do not cache an exit
                // pose from this transitional snapshot; wait for walking, after native
                // ride-exit processing has selected the station and exit.
                statusMessage = "Leaving ride...";
                return true;
            case NativePeepState.Walking when hasRidden:
                ClearRideCameraRotation();
                currentStationIndex = peep.StationIndex;
                if (EnsureExitPose() && takeControlTask is null) {
                    var native = ToNativePosition(exitPose.Position);
                    takeControlTask = scene.Simulation.TakeControlPeepAsync(
                        peep.EntityId, native.X, native.Y, native.Z, GetDirection(camera.HorizontalForward));
                    camera.Position = exitPose.Position;
                }
                return true;
            default:
                camera.Position = ToWorldCameraPosition(peep);
                return true;
        }
    }

    private bool EnsureExitPose()
    {
        if (exitPoseReady) return true;

        var rideExitFound = false;
        foreach (var entrance in scene.RideEntrances) {
            if (entrance.EntranceType != RCTRideEntranceType.Exit
                || entrance.RideId != currentRideId
                || entrance.StationIndex != currentStationIndex)
                continue;

            var exitPosition = GetEntranceCameraPosition(entrance);
            if (scene.Navigation is { } navigation
                && navigation.TrySnapToNearestPath(exitPosition, PlacementRadius, EyeHeight, out exitPose)) {
                exitPoseReady = true;
                rideExitFound = true;
            }
            break;
        }

        if (!rideExitFound) {
            statusMessage = "The ride exit is not connected to a walkable path.";
            return false;
        }

        return true;
    }

    private void ClearRideCameraRotation()
    {
        if (!rideCameraAttached) return;

        camera.ClearBaseRotationPreservingOrientation();
        rideCameraAttached = false;
    }

    private static Vector3 ToWorldCameraPosition(PeepSnapshot peep) => new(
        peep.X * WorldUnits.TileSize / 32.0f,
        peep.Z * WorldUnits.HeightPerBaseUnit / 8.0f + EyeHeight,
        peep.Y * WorldUnits.TileSize / 32.0f);

    private static Vector3 GetEntranceCameraPosition(RCTRideEntranceData entrance) => new(
        (entrance.TileX + 0.5f) * WorldUnits.TileSize,
        entrance.BaseHeight * WorldUnits.HeightPerBaseUnit + EyeHeight,
        (entrance.TileY + 0.5f) * WorldUnits.TileSize);

    private static (int X, int Y, int Z) ToNativePosition(Vector3 cameraPosition) => (
        (int)MathF.Round(cameraPosition.X * NativeUnitsPerWorldUnit),
        (int)MathF.Round(cameraPosition.Z * NativeUnitsPerWorldUnit),
        (int)MathF.Round((cameraPosition.Y - EyeHeight) / WorldUnitsPerNativeHeightUnit));

    private static byte GetDirection(Vector3 forward)
    {
        var direction = (int)MathF.Round(MathF.Atan2(forward.X, forward.Z) / (MathF.PI * 0.5f));
        return (byte)((direction + 4) & 3);
    }

    private enum NativePeepState : byte
    {
        QueuingFront = 2,
        OnRide = 3,
        LeavingRide = 4,
        Walking = 5,
        Queuing = 6,
        EnteringRide = 7,
    }
}

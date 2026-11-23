using Silk.NET.Input;
using System.Numerics;

namespace RCT3D;

public class FlightCamera
{
    public const float NearPlane = 0.1f;
    public const float FarPlane = 1000.0f;

    // Start above and slightly behind the terrain, looking toward terrain center.
    public Vector3 Position = new(192.0f, 120.0f, -120.0f);
    public Vector3 Front = Vector3.Normalize(new Vector3(0.0f, -0.5f, MathF.Sqrt(3.0f) * 0.5f));
    public Vector3 Up = Vector3.UnitY;
    public float FieldOfViewDegrees { get; set; } = 45.0f;
    public Vector3 HorizontalForward => Vector3.Normalize(Vector3.TransformNormal(
        new Vector3(MathF.Cos(_yaw * MathF.PI / 180.0f), 0.0f, MathF.Sin(_yaw * MathF.PI / 180.0f)), _baseRotation));
    public Vector3 HorizontalRight => Vector3.Normalize(Vector3.Cross(Up, HorizontalForward));

    private float _yaw = 90.0f;
    private float _pitch = -30.0f;
    private Matrix4x4 _baseRotation = Matrix4x4.Identity;
    private bool _rideCameraActive;
    private float _rideLookAheadYaw;
    private float _rideLookAheadPitch;
    private float _savedRideYaw;
    private float _savedRidePitch;
    private float _savedRideLookAheadYaw;
    private float _savedRideLookAheadPitch;
    private float _speed = 40.0f;
    private const float MinimumSpeed = 0.0f;
    private const float MaximumSpeed = 100.0f;
    private const float ScrollSpeedFactor = 1.1f;
    private const float ZeroSpeedRecovery = 0.1f;
    private float _sensitivity = 0.1f;
    private Vector2 _lastMousePosition;
    private bool _firstMouse = true;

    public Matrix4x4 GetViewMatrix()
    {
        var view = Matrix4x4.CreateLookAt(Position, Position + Vector3.Normalize(Front), Up);
        return view * Matrix4x4.CreateScale(-1.0f, 1.0f, 1.0f);
    }

    public Matrix4x4 GetProjectionMatrix(float aspectRatio)
    {
        // Explicitly use Vulkan-normalized clip coordinates (Y downward)
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(
            FieldOfViewDegrees * MathF.PI / 180.0f,
            aspectRatio, 
            NearPlane,
            FarPlane
        );
        proj.M22 *= -1.0f; // Invert Y axis for Vulkan clip-space coordinates
        return proj;
    }

    public void HandleKeyboard(IKeyboard keyboard, float deltaTime)
    {
        float moveVelocity = _speed * deltaTime;
        Vector3 right = Vector3.Normalize(Vector3.Cross(Up, Front));

        if (keyboard.IsKeyPressed(Key.ShiftLeft)) moveVelocity *= 3.0f;

        if (keyboard.IsKeyPressed(Key.W)) Position += Front * moveVelocity;
        if (keyboard.IsKeyPressed(Key.S)) Position -= Front * moveVelocity;
        if (keyboard.IsKeyPressed(Key.A)) Position -= right * moveVelocity;
        if (keyboard.IsKeyPressed(Key.D)) Position += right * moveVelocity;
        if (keyboard.IsKeyPressed(Key.Space)) Position += Up * moveVelocity;
        if (keyboard.IsKeyPressed(Key.ControlLeft)) Position -= Up * moveVelocity;
    }

    public void HandleScroll(float scrollDelta)
    {
        if (scrollDelta == 0.0f)
            return;

        if (_speed == MinimumSpeed && scrollDelta > 0.0f)
            _speed = ZeroSpeedRecovery;

        _speed = Math.Clamp(_speed * MathF.Pow(ScrollSpeedFactor, scrollDelta), MinimumSpeed, MaximumSpeed);
    }

    public void ResetMouseTracking()
    {
        _firstMouse = true;
    }

    public void BeginRideCamera(bool resume)
    {
        _rideCameraActive = true;
        _yaw = resume ? _savedRideYaw : 90.0f;
        _pitch = resume ? _savedRidePitch : 0.0f;
        _rideLookAheadYaw = resume ? _savedRideLookAheadYaw : 0.0f;
        _rideLookAheadPitch = resume ? _savedRideLookAheadPitch : 0.0f;
    }

    public void SetRideRotation(Matrix4x4 vehicleRotation, Matrix4x4 lookAheadRotation, bool applyLookAhead)
    {
        var forward = Vector3.TransformNormal(Vector3.UnitZ, lookAheadRotation);
        var localForward = Vector3.TransformNormal(forward, Matrix4x4.Transpose(vehicleRotation));
        var lookAheadYaw = MathF.Atan2(localForward.X, localForward.Z) * 180.0f / MathF.PI;
        var lookAheadPitch = MathF.Asin(Math.Clamp(localForward.Y, -1.0f, 1.0f)) * 180.0f / MathF.PI;

        if (applyLookAhead) {
            var yawDelta = MathF.IEEERemainder(lookAheadYaw - _rideLookAheadYaw, 360.0f);
            _yaw = Math.Clamp(_yaw - yawDelta, 0.0f, 180.0f);
            _pitch = Math.Clamp(_pitch + lookAheadPitch - _rideLookAheadPitch, -89.0f, 89.0f);
            _rideLookAheadYaw = lookAheadYaw;
            _rideLookAheadPitch = lookAheadPitch;
        }

        _baseRotation = vehicleRotation;
        UpdateOrientation();
    }

    public void SuspendRideCamera()
    {
        _savedRideYaw = _yaw;
        _savedRidePitch = _pitch;
        _savedRideLookAheadYaw = _rideLookAheadYaw;
        _savedRideLookAheadPitch = _rideLookAheadPitch;
        ClearBaseRotationPreservingOrientation();
    }

    public void ClearBaseRotationPreservingOrientation()
    {
        var worldFront = Front;
        _rideCameraActive = false;
        _baseRotation = Matrix4x4.Identity;
        _yaw = MathF.Atan2(worldFront.Z, worldFront.X) * 180.0f / MathF.PI;
        _pitch = MathF.Asin(Math.Clamp(worldFront.Y, -1.0f, 1.0f)) * 180.0f / MathF.PI;
        UpdateOrientation();
    }

    public void HandleMouse(Vector2 mousePosition)
    {
        if (_firstMouse)
        {
            _lastMousePosition = mousePosition;
            _firstMouse = false;
            return;
        }

        float xOffset = (mousePosition.X - _lastMousePosition.X) * _sensitivity;
        float yOffset = (_lastMousePosition.Y - mousePosition.Y) * _sensitivity; // Inverted
        _lastMousePosition = mousePosition;

        _yaw -= xOffset;
        if (_rideCameraActive)
            _yaw = Math.Clamp(_yaw, 0.0f, 180.0f);
        _pitch = Math.Clamp(_pitch + yOffset, -89.0f, 89.0f);

        UpdateOrientation();
    }

    private void UpdateOrientation()
    {
        var yawRadians = _yaw * MathF.PI / 180.0f;
        var pitchRadians = _pitch * MathF.PI / 180.0f;
        var direction = new Vector3(
            MathF.Cos(yawRadians) * MathF.Cos(pitchRadians),
            MathF.Sin(pitchRadians),
            MathF.Sin(yawRadians) * MathF.Cos(pitchRadians));

        Front = Vector3.Normalize(Vector3.TransformNormal(direction, _baseRotation));
        Up = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitY, _baseRotation));
        if (Vector3.Cross(Front, Up).LengthSquared() < 0.000001f)
            Up = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitZ, _baseRotation));
    }
}
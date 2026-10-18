using Silk.NET.Input;
using System.Numerics;

namespace RCT3DViewer
{
    public class FlightCamera
    {
        // Start above and slightly behind the terrain, looking toward terrain center.
        public Vector3 Position = new Vector3(192.0f, 120.0f, -120.0f);
        public Vector3 Front = Vector3.Normalize(new Vector3(0.0f, -0.5f, MathF.Sqrt(3.0f) * 0.5f));
        public Vector3 Up = Vector3.UnitY;

        private float _yaw = 90.0f;
        private float _pitch = -30.0f;
        private float _speed = 40.0f;
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
                (float)(Math.PI / 4.0), // 45 Degrees FOV
                aspectRatio, 
                0.1f, 
                1000.0f
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
            _pitch = Math.Clamp(_pitch + yOffset, -89.0f, 89.0f);

            Vector3 direction;
            direction.X = (float)(Math.Cos(_yaw * Math.PI / 180.0) * Math.Cos(_pitch * Math.PI / 180.0));
            direction.Y = (float)(Math.Sin(_pitch * Math.PI / 180.0));
            direction.Z = (float)(Math.Sin(_yaw * Math.PI / 180.0) * Math.Cos(_pitch * Math.PI / 180.0));
            Front = Vector3.Normalize(direction);
        }
    }
}

using Godot;

namespace Expedition.Game;

/// <summary>
/// Orthographic camera at a true isometric angle (D-006). Pans with WASD or the arrow keys,
/// zooms with the mouse wheel, and turns in 90-degree steps with Q and E.
/// </summary>
public partial class CameraRig : Camera3D
{
    private const float IsometricPitch = -35.264f;
    private const float Distance = 60f;
    private const float MinSize = 6f;
    private const float MaxSize = 45f;

    private float _yaw = 45f;

    /// <summary>World point the camera looks at.</summary>
    public Vector3 Target { get; set; }

    public override void _Ready()
    {
        Projection = ProjectionType.Orthogonal;
        Size = 20f;
        Far = 200f;
        Current = true;
        Apply();
    }

    public override void _Process(double delta)
    {
        var input = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) input.Y -= 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) input.Y += 1;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) input.X -= 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) input.X += 1;
        if (input != Vector2.Zero)
        {
            // Move along the ground relative to the way the camera faces.
            var forward = new Vector3(-Basis.Z.X, 0, -Basis.Z.Z).Normalized();
            var right = new Vector3(Basis.X.X, 0, Basis.X.Z).Normalized();
            Target += (right * input.X - forward * input.Y) * Size * 0.6f * (float)delta;
        }

        Apply();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true } wheel)
        {
            if (wheel.ButtonIndex == MouseButton.WheelUp) Size = Mathf.Max(MinSize, Size * 0.9f);
            if (wheel.ButtonIndex == MouseButton.WheelDown) Size = Mathf.Min(MaxSize, Size * 1.1f);
        }
        else if (@event is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode == Key.Q) _yaw += 90f;
            if (key.Keycode == Key.E) _yaw -= 90f;
        }
    }

    /// <summary>The point on the horizontal plane at height <paramref name="y"/> under the given screen position.</summary>
    public Vector3? GroundPointUnder(Vector2 screenPosition, float y = 0f)
    {
        var origin = ProjectRayOrigin(screenPosition);
        var direction = ProjectRayNormal(screenPosition);
        if (Mathf.IsZeroApprox(direction.Y))
        {
            return null;
        }

        var distance = (y - origin.Y) / direction.Y;
        return distance < 0 ? null : origin + direction * distance;
    }

    private void Apply()
    {
        RotationDegrees = new Vector3(IsometricPitch, _yaw, 0);
        Position = Target + Basis.Z * Distance;
    }
}

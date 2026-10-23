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

    /// <summary>The ground (x, z) the point looked at must stay within; null for anywhere.</summary>
    public Rect2? Bounds { get; set; }

    /// <summary>True once the player has panned by hand since this was last cleared.</summary>
    public bool Panned { get; set; }

    public override void _Ready()
    {
        Projection = ProjectionType.Orthogonal;
        Size = 22f;
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
        // The mouse at the edge of the window moves the view too, if the settings say so.
        if (GameSettings.EdgePan && GetViewport() is { } view && DisplayServer.WindowIsFocused())
        {
            var mouse = view.GetMousePosition();
            var size = view.GetVisibleRect().Size;
            const float Edge = 10f;
            if (mouse.X >= 0 && mouse.Y >= 0 && mouse.X <= size.X && mouse.Y <= size.Y)
            {
                if (mouse.X < Edge) input.X -= 1;
                if (mouse.X > size.X - Edge) input.X += 1;
                if (mouse.Y < Edge) input.Y -= 1;
                if (mouse.Y > size.Y - Edge) input.Y += 1;
            }
        }

        if (input != Vector2.Zero)
        {
            // Move along the ground relative to the way the camera faces.
            var forward = new Vector3(-Basis.Z.X, 0, -Basis.Z.Z).Normalized();
            var right = new Vector3(Basis.X.X, 0, Basis.X.Z).Normalized();
            Target += (right * input.X - forward * input.Y) * Size * 0.6f * (float)delta;
            Panned = true;
        }

        Apply();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Dragging with the middle button (or a two-finger drag that the trackpad sends as one) moves the view.
        if (@event is InputEventMouseMotion drag && (drag.ButtonMask & MouseButtonMask.Middle) != 0)
        {
            var forward = new Vector3(-Basis.Z.X, 0, -Basis.Z.Z).Normalized();
            var right = new Vector3(Basis.X.X, 0, Basis.X.Z).Normalized();
            var perPixel = Size / Mathf.Max(1f, GetViewport().GetVisibleRect().Size.Y);
            Target -= (right * drag.Relative.X - forward * drag.Relative.Y * 1.4f) * perPixel;
            Panned = true;
            return;
        }

        // A pinch on a trackpad zooms.
        if (@event is InputEventMagnifyGesture pinch && pinch.Factor > 0)
        {
            Size = Mathf.Clamp(Size / pinch.Factor, MinSize, MaxSize);
            return;
        }

        if (@event is InputEventPanGesture pan)
        {
            var forward = new Vector3(-Basis.Z.X, 0, -Basis.Z.Z).Normalized();
            var right = new Vector3(Basis.X.X, 0, Basis.X.Z).Normalized();
            Target += (right * pan.Delta.X - forward * pan.Delta.Y) * Size * 0.02f;
            Panned = true;
            return;
        }

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

    /// <summary>Turns the camera to look from this compass direction, in degrees.</summary>
    public void SetYaw(float degrees)
    {
        _yaw = degrees;
        Apply();
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
        if (Bounds is { } bounds)
        {
            Target = new Vector3(Mathf.Clamp(Target.X, bounds.Position.X, bounds.End.X), Target.Y, Mathf.Clamp(Target.Z, bounds.Position.Y, bounds.End.Y));
        }

        RotationDegrees = new Vector3(IsometricPitch, _yaw, 0);
        Position = Target + Basis.Z * Distance;
    }
}

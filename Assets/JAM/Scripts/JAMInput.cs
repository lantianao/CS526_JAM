using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// One place to read the keyboard and mouse, so the player body and a possessed
/// enemy answer to exactly the same controls without duplicating the polling.
///
/// This project runs with Active Input Handling set to "Input System Package (New)",
/// so the legacy UnityEngine.Input API would throw at runtime. Devices are polled
/// directly, which keeps the ability keys working without anyone having to author
/// an .inputactions asset for them.
///
/// Every accessor tolerates a missing device: a machine with no mouse attached
/// reads as "not firing" rather than throwing.
/// </summary>
public static class JAMInput
{
    /// <summary>WASD as a direction, magnitude 0..1.</summary>
    public static Vector2 MoveAxis()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return Vector2.zero;

        Vector2 raw = Vector2.zero;
        if (keyboard.wKey.isPressed) raw.y += 1f;
        if (keyboard.sKey.isPressed) raw.y -= 1f;
        if (keyboard.dKey.isPressed) raw.x += 1f;
        if (keyboard.aKey.isPressed) raw.x -= 1f;

        // Clamp rather than normalize: diagonals stay at speed 1 without turning a
        // future analogue stick's partial tilt into full speed.
        return Vector2.ClampMagnitude(raw, 1f);
    }

    public static bool FireHeld()
    {
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
    }

    public static bool FirePressed()
    {
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    /// <summary>Ability keys 1-3, accepting either the number row or the numpad.</summary>
    public static bool AbilityPressed(int slot)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return false;

        switch (slot)
        {
            case 1: return keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame;
            case 2: return keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame;
            case 3: return keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame;
            default: return false;
        }
    }

    /// <summary>
    /// Cursor position in world space, on the same Z plane as <paramref name="reference"/>.
    /// Falls back to the reference point itself when there is no mouse or camera.
    /// </summary>
    public static Vector2 CursorWorldPosition(Camera camera, Vector3 reference)
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || camera == null) return reference;

        Vector3 screenPosition = mouse.position.ReadValue();
        // Distance from the camera to the plane the actor lives on. Irrelevant for
        // an orthographic camera, required for a perspective one.
        screenPosition.z = Mathf.Abs(camera.transform.position.z - reference.z);
        return camera.ScreenToWorldPoint(screenPosition);
    }

    /// <summary>Aim direction from <paramref name="from"/> toward the cursor.</summary>
    public static Vector2 AimDirection(Camera camera, Vector3 from, Vector2 fallback)
    {
        Vector2 offset = CursorWorldPosition(camera, from) - (Vector2)from;
        return offset.sqrMagnitude > 0.0001f ? offset.normalized : fallback;
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

namespace SmashBots {

// Click/tap input from whatever pointer device is current
public static class PointerInput
{
    public static bool Pressed
      => Pointer.current != null && Pointer.current.press.wasPressedThisFrame;

    public static Vector2 Position
      => Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;
}

} // namespace SmashBots

using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class CameraDragPan2D : MonoBehaviour
{
    public float panSpeed = 1f;

    private Camera cam;
    private Vector3 lastMouseWorld;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Update()
    {
        if (Mouse.current == null)
            return;

        // Middle mouse OR right mouse
        bool pressedThisFrame =
            Mouse.current.middleButton.wasPressedThisFrame ||
            Mouse.current.rightButton.wasPressedThisFrame;

        bool isHeld =
            Mouse.current.middleButton.isPressed ||
            Mouse.current.rightButton.isPressed;

        if (pressedThisFrame)
        {
            Vector2 screenPos = Mouse.current.position.ReadValue();
            lastMouseWorld = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        }

        if (isHeld)
        {
            Vector2 screenPos = Mouse.current.position.ReadValue();
            Vector3 currentMouseWorld = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));

            Vector3 delta = lastMouseWorld - currentMouseWorld;
            transform.position += delta * panSpeed;
        }
    }
}

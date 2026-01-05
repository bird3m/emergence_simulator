using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class CameraDragPan2D : MonoBehaviour
{
    [Header("References")]
    public Terrain terrain;

    [Header("Pan")]
    public float panSpeed = 1f;

    [Header("Zoom (optional)")]
    public bool enableZoom = true;
    public float zoomSpeed = 5f;
    public float minZoom = 2f;
    public float maxZoom = 20f;

    private Camera cam;
    private Vector3 lastMouseWorld;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        if (terrain == null)
            terrain = FindObjectOfType<Terrain>();
    }

    private void LateUpdate()
    {
        if (terrain == null)
            return;

        if (Mouse.current == null)
            return;

        // ----- Zoom -----
        if (enableZoom)
        {
            float scrollY = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) > 0.01f)
            {
                cam.orthographicSize -= scrollY * 0.01f * zoomSpeed;
                cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);
            }
        }

        // ----- Drag Pan (MMB or RMB) -----
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

        // ----- Clamp to terrain bounds -----
        transform.position = ClampCameraToTerrain(transform.position);
    }

    private Vector3 ClampCameraToTerrain(Vector3 camPos)
    {
        // Terrain world bounds in XY (assuming terrain.transform.position is bottom-left)
        Vector3 o = terrain.transform.position;

        float minX = o.x;
        float minY = o.y;
        float maxX = o.x + terrain.width * terrain.cellSize;
        float maxY = o.y + terrain.height * terrain.cellSize;

        // Camera half extents in world units
        float halfH = cam.orthographicSize;
        float halfW = cam.orthographicSize * cam.aspect;

        // Clamp so viewport stays inside bounds
        float clampedX = Mathf.Clamp(camPos.x, minX + halfW, maxX - halfW);
        float clampedY = Mathf.Clamp(camPos.y, minY + halfH, maxY - halfH);

        // If camera is larger than terrain in a dimension, center it
        if ((maxX - minX) < (halfW * 2f))
            clampedX = (minX + maxX) * 0.5f;

        if ((maxY - minY) < (halfH * 2f))
            clampedY = (minY + maxY) * 0.5f;

        camPos.x = clampedX;
        camPos.y = clampedY;

        return camPos;
    }
}

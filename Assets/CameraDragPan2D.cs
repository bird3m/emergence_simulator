using UnityEngine;
using UnityEngine.InputSystem;

/*
 * Camera controller that allows dragging and zooming in 2D space
 * Drag with middle or right mouse button to pan the camera
 */
[RequireComponent(typeof(Camera))] //this component requires a Camera component to work
public class CameraDragPan2D : MonoBehaviour
{
    public Terrain terrain; //reference to the terrain to limit camera movement


    public float panSpeed = 1f; //how fast the camera moves when dragging


    public bool enableZoom = true; //toggle zoom on/off
    public float zoomSpeed = 5f; //how fast the camera zooms
    public float minZoom = 2f; //minimum zoom level (closest)
    public float maxZoom = 20f; //maximum zoom level (farthest)

    private Camera cam; //reference to the Camera component
    private Vector3 lastMouseWorld; //last recorded mouse position in world space

    // Time: O(1)
    // Space: O(1)
    private void Awake()
    {
        cam = GetComponent<Camera>(); //get the Camera component attached to this GameObject

        if (terrain == null)
            terrain = FindObjectOfType<Terrain>(); //find the terrain in the scene if not assigned
    }

    // Time: O(1) 
    //  Space: O(1)
    private void LateUpdate()
    {
        if (terrain == null)
            return;

        if (Mouse.current == null) //check if mouse input is available
            return;


        // Scroll wheel changes the  size (zoom level)
        if (enableZoom)
        {
            float scrollY = Mouse.current.scroll.ReadValue().y; //get scroll wheel value
            if (Mathf.Abs(scrollY) > 0.01f) //if there's actual scrolling
            {
                cam.orthographicSize -= scrollY * 0.01f * zoomSpeed; //change zoom level
                cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom); //keep within limits
            }
        }

        bool pressedThisFrame = Mouse.current.middleButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame;

        bool isHeld = Mouse.current.middleButton.isPressed || Mouse.current.rightButton.isPressed;

        // When button is first pressed, save the starting mouse position
        if (pressedThisFrame)
        {
            Vector2 screenPos = Mouse.current.position.ReadValue(); //get mouse position on screen
            lastMouseWorld = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f)); //convert to world space
        }

        if (isHeld)
        {
            Vector2 screenPos = Mouse.current.position.ReadValue();
            Vector3 currentMouseWorld = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));

            Vector3 delta = lastMouseWorld - currentMouseWorld; //calculate how much the mouse moved
            transform.position += delta * panSpeed; //move the camera
        }

   
        transform.position = ClampCameraToTerrain(transform.position);
    }

    // Time: O(1) 
    //  Space: O(1)
    private Vector3 ClampCameraToTerrain(Vector3 camPos)
    {
        // Calculate terrain boundaries in world space
        // Terrain starts at its transform position (bottom-left corner)
        Vector3 o = terrain.transform.position;

        float minX = o.x; //left edge of terrain
        float minY = o.y; //bottom edge of terrain
        float maxX = o.x + terrain.width * terrain.cellSize; //right edge of terrain
        float maxY = o.y + terrain.height * terrain.cellSize; //top edge of terrain

        // Calculate how much area the camera can see
        // orthographicSize is half the height the camera sees
        float halfH = cam.orthographicSize; //half height of camera view
        float halfW = cam.orthographicSize * cam.aspect; //half width of camera view

        // Clamp camera position so the viewport stays inside terrain bounds
        float clampedX = Mathf.Clamp(camPos.x, minX + halfW, maxX - halfW);
        float clampedY = Mathf.Clamp(camPos.y, minY + halfH, maxY - halfH);

        // This prevents weird behavior when zoomed out too far
        if ((maxX - minX) < (halfW * 2f))
            clampedX = (minX + maxX) * 0.5f;

        if ((maxY - minY) < (halfH * 2f))
            clampedY = (minY + maxY) * 0.5f;

        camPos.x = clampedX;
        camPos.y = clampedY;

        return camPos;
    }
}

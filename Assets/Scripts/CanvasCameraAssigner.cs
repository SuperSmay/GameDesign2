using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class CanvasCameraAssigner : MonoBehaviour
{
    void Awake()
    {
        Canvas canvas = GetComponent<Canvas>();
        
        // If the camera isn't set, find the Main Camera
        if (canvas.worldCamera == null)
        {
            canvas.worldCamera = Camera.main;
        }
    }
}
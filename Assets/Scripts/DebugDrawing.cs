using UnityEngine;

public static class DebugDrawing
{
    public static void DrawDebugCircle(Vector3 center, float radius, Color color, float duration = 0.0f, int segments = 32)
    {
        // Ensure valid inputs
        if (radius <= 0f || segments <= 0) return;

        // Calculate angle step for each segment
        float angleStep = 360f / segments;
        Vector3 lineStart = Vector3.zero;
        Vector3 lineEnd = Vector3.zero;

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * angleStep;
            float x = center.x + radius * Mathf.Cos(Mathf.Deg2Rad * angle);
            float y = center.y + radius * Mathf.Sin(Mathf.Deg2Rad * angle);
            
            // For a 2D circle, Z can be 0 (or some other value depending on your game's plane)
            lineEnd = new Vector3(x, y, center.z); 

            if (i > 0)
            {
                // Draw a line between the previous point (lineStart) and the current point (lineEnd)
                Debug.DrawLine(lineStart, lineEnd, color, duration);
            }
            lineStart = lineEnd;
        }
    }

    public static void DrawDebugCapsule(Vector3 start, Vector3 end, float radius, Color color, float duration = 0.0f)
    {
        // Draw the cylindrical part of the capsule
        Vector3 direction = (end - start).normalized;
        Vector3 perpendicular = Vector3.Cross(direction, Vector3.forward).normalized * radius;

        Debug.DrawLine(start + perpendicular, end + perpendicular, color, duration);
        Debug.DrawLine(start - perpendicular, end - perpendicular, color, duration);

        // Draw the hemispherical ends of the capsule
        DrawDebugCircle(start, radius, color, duration);
        DrawDebugCircle(end, radius, color, duration);
    }
}
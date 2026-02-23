using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;

public class CarPathFollower : MonoBehaviour
{

    float currentPosOnSpline = 0f;
    [SerializeField] public SplineController splineController;
    [SerializeField] SplineController startingSplineController;
    [SerializeField] float raycastDistance = 3f;
    [SerializeField] float maxSpeed = 0.1f;
    [SerializeField] float decelerationRate = 0.1f;
    [SerializeField] float accelerationRate = 0.05f;

    [SerializeField] float speed = 0.1f;

    [SerializeField] Rigidbody2D rb;

    [SerializeField] GameObject explosionEffectPrefab;
    [SerializeField] PlayerInput playerInput;

    bool hasCollided = false;
    float collisionCooldown = 0f; // Time in seconds to ignore collisions after a collision has occurred


    InputAction resetAction;

    private void Awake()
    {
        resetAction = playerInput.actions["Reset"];
    }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        splineController = startingSplineController;
    }

    // Update is called once per frame
    void Update()
    {

        bool reset = resetAction.ReadValue<float>() > 0;
        if (reset)
        {
            // Reset the car to the starting position and spline
            splineController = startingSplineController;
            currentPosOnSpline = 0f;
            hasCollided = false;
            collisionCooldown = 0f;
            transform.position = splineController.splineContainer.EvaluatePosition(0f);
            transform.rotation = Quaternion.identity;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            return; // Skip the rest of the update logic on reset
        }

        collisionCooldown = Mathf.Max(collisionCooldown - Time.deltaTime, 0f);
        if (hasCollided) return; // Stop moving if we've collided with another car

        if (currentPosOnSpline > 1f) currentPosOnSpline -= 1f; // Loop back to start of spline
        Vector3 position = splineController.splineContainer.EvaluatePosition(currentPosOnSpline);
        position.z = 0f; // Keep the car on the 2D plane
        Vector3 tangent = splineController.splineContainer.EvaluateTangent(currentPosOnSpline);
        Debug.DrawRay(position, tangent, Color.green);
        // Look at tangent direction
        if (tangent != Vector3.zero)
        {
            float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle - 90); // Subtract 90 degrees to align the car sprite correctly
        }
        
        transform.position = position;

        // Raycast out to detect if there is a car in front of this one, and if so, slow down
        Vector3 rayDirection = transform.up;

        RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, rayDirection, raycastDistance);
        Debug.DrawRay(transform.position, rayDirection * raycastDistance, Color.red);
        if (hits.Length > 0)
        {
            Debug.Log("Raycast count: " + hits.Length);
            Debug.Log("Raycast hit: " + hits[0].collider.name);
        }
        if (hits.Length > 1)
        {            
            float currentDecelerationRate = decelerationRate * (1/(hits[1].distance/raycastDistance)); // Decelerate more if the car in front is closer
            speed = Mathf.Max(speed - currentDecelerationRate * Time.deltaTime, 0.0f); // Slow down if there's a car in front, but don't stop completely
        }
        else
        {
            speed = Mathf.Min(speed + accelerationRate * Time.deltaTime, maxSpeed); // Normal speed
        }

        currentPosOnSpline += speed * Time.deltaTime;

        if (currentPosOnSpline >= 1f)
        {
            // Reached end of spline, transfer to next spline
            if (splineController != null)
            {
                currentPosOnSpline = 0f;
                SplineController.NodeTransferType transferType = splineController.TransferToNextSpline(this);
                if (transferType == SplineController.NodeTransferType.End)
                {
                    // No more splines to transfer to, reset. Placeholder behavior.
                    splineController = startingSplineController;
                }
            }
        }

    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        Debug.Log("Collided with: " + collision.collider.name);

        if (collisionCooldown == 0) {
            collisionCooldown = 1f; // Set cooldown to 1 second
            Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity); // Spawn explosion effect
        } 
        
        hasCollided = true; // Stop moving if we've collided with another car
    }
}

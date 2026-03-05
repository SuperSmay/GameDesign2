using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

#nullable enable

public class CarPathFollower : MonoBehaviour, IPointerClickHandler
{

    float currentSplineTValue = 0f;
    public IntersectionNode intersectionNode;

    public bool isDeviant = false; // Whether this car is a "deviant" that doesn't follow traffic rules.
    // TODO Have the car control the behavior, rather than having the intersection controller manually modify the attributes.

    public float raycastDistance;
    public float maxSpeed;
    float minSpeed = 0.03f; // Minimum speed to stop the very gradual creeping when trying to stop for a target.
    public float decelerationRate;
    public float accelerationRate;
    public TurnChoice? turnIntention; // The car will take the first available turn that matches this intention when it reaches an intersection.
                                     // If the there is no "continue" option, and the intended turn is not available, the car will take any available turn.

    [SerializeField] float speed;

    public bool canProceedAtStopSign = false; // Whether this car is currently allowed to proceed through a stop sign. This is set by the IntersectionController when it's this car's turn to go.

    [SerializeField] Rigidbody2D rb;

    [SerializeField] GameObject explosionEffectPrefab;
    [SerializeField] GameObject exhaustEffectPrefab;


    bool hasCollided = false;
    GameObject? exhaustEffectInstance;
    float collisionCooldown = 0f; // Time in seconds to ignore collisions after a collision has occurred

    // Braking tracking: remember the first detection distance for the current target so we can
    // compute a smooth, linear target-speed curve from detection to stop point.
    GameObject? trackedTarget = null;
    float initialDetectionDistance = 0f;
    const float kMinDetectionSpan = 0.001f;


    // used to determine whether the tracked target has moved independently
    Vector3 trackedTargetLastPosition;

    IntersectionController intersectionController = IntersectionController.Instance;


    void UpdatePositionAlongSpline()
    {
        // Get spline length for consistent speed across different splines (since the T value is normalized, we need to account for spline length to maintain consistent speed)
        float splineLength = intersectionNode.splineContainer.CalculateLength();

        currentSplineTValue += speed * Time.fixedDeltaTime / splineLength * 10f;  // 10 is a rough estimate of the avg spline length.
        // TODO remove the 10x speed multiplier



        // Move along the current spline
        Vector3 targetPosition = intersectionNode.splineContainer.EvaluatePosition(currentSplineTValue);
        targetPosition.z = 0f; // Keep the car on the 2D plane
        rb.MovePosition(targetPosition); // Move the car to the target position on the spline
    }

    void UpdateRotationAlongSpline()
    {
        // Point along the current spline
        Vector3 tangent = intersectionNode.splineContainer.EvaluateTangent(currentSplineTValue);
        // Look at tangent direction
        if (tangent != Vector3.zero)
        {
            float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
            rb.MoveRotation(Quaternion.Euler(0, 0, angle - 90)); // Subtract 90 degrees to align the car sprite correctly
        }
    }

    void DoRaycastDetection()
    {
        // Raycast out to detect if there is a car in front of this one, and if so, slow down for next update.
        // TODO - Change this to use a target point to stop at, rather than just based on distance.
        // This will allow for more precise stopping at stop signs and better speed matching between cars.
        Vector3 rayDirection = transform.up;

        RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, rayDirection, raycastDistance);
        Debug.DrawRay(transform.position, rayDirection * raycastDistance, Color.red);

        RaycastHit2D? closestTargetHit = null;
        float closestTargetDistance = Mathf.Infinity;
        for (int i = 0; i < hits.Length; i++)
        {
            // Ignore hits that are this car's own collider
            if (hits[i].collider.gameObject == gameObject) continue;

            // Ignore stop lines if we're allowed to go
            if (canProceedAtStopSign && hits[i].collider.gameObject.layer == LayerMask.NameToLayer("Stop Lines")) continue;

            // Ignore interssection leave triggers so we don't get confused when leaving an intersection
            if (hits[i].collider.gameObject.layer == LayerMask.NameToLayer("Intersection Leave")) continue;

            if (hits[i].distance < closestTargetDistance)
            {
                closestTargetDistance = hits[i].distance;
                closestTargetHit = hits[i];
            }


            // Debug.Log("Hit: " + hits[i].collider.name + " at distance: " + hits[i].distance);
        }

        if (closestTargetHit.HasValue)
        {

            bool isCar = closestTargetHit.Value.collider.gameObject.layer == LayerMask.NameToLayer("Cars");
            bool isStopSign = closestTargetHit.Value.collider.gameObject.layer == LayerMask.NameToLayer("Stop Lines");

            // Stop closer to stop lines -- Ignore far away lines
            bool didHitStopSign = false;
            if (isStopSign)
            {
                didHitStopSign = true;
            }

            // always plan to stop half a unit ahead, ignoring stop sign branching
            float desiredStopDistance = didHitStopSign ? 0.3f : 1.5f;
            float distanceToTarget = closestTargetHit.Value.distance;
            float distanceToStop = distanceToTarget - desiredStopDistance;
            // Track the target we detected so the curve is anchored to the original detection point.
            GameObject hitObj = closestTargetHit.Value.collider.gameObject;
            if (trackedTarget == null || trackedTarget != hitObj)
            {
                trackedTarget = hitObj;
                initialDetectionDistance = distanceToTarget;
                trackedTargetLastPosition = hitObj.transform.position;
            }
            else
            {
                // compute how much the target moved since last frame
                Vector3 newPos = hitObj.transform.position;
                Vector3 moveDelta = newPos - trackedTargetLastPosition;
                trackedTargetLastPosition = newPos;

                // project movement onto our ray direction to see if the target has
                // moved closer or farther independently of us.
                float forwardMovement = Vector3.Dot(moveDelta, transform.up);
                if (forwardMovement > 0.0001f)
                {
                    // target moved away
                    initialDetectionDistance = distanceToTarget;
                }
                else if (forwardMovement < -0.0001f)
                {
                    // target moved closer
                    initialDetectionDistance = distanceToTarget;
                }
            }

            // If we're already at or past the desired stop point, brake to zero using the max deceleration.
            if (distanceToStop <= 0f)
            {
                speed = Mathf.Max(speed - decelerationRate * Time.fixedDeltaTime, 0f);
            }
            else
            {
                // Compute a linear target speed between detection and stop point.
                float detectionSpan = Mathf.Max(initialDetectionDistance - desiredStopDistance, kMinDetectionSpan);
                float t = Mathf.Clamp01(distanceToStop / detectionSpan); // 1 at detection, 0 at stop point
                float targetSpeed = Mathf.Min(maxSpeed * t, maxSpeed);

                // Smoothly move current speed toward targetSpeed using acceleration/deceleration caps.
                float delta = (speed > targetSpeed) ? decelerationRate * Time.fixedDeltaTime : accelerationRate * Time.fixedDeltaTime;
                speed = Mathf.MoveTowards(speed, targetSpeed, delta);
                if (speed < minSpeed && targetSpeed > 0f) // If we're trying to move but are below the minimum speed, snap up to the minimum speed to prevent creeping.
                {
                    speed = minSpeed;
                    if (didHitStopSign && !isCar)  // isCar is check for the closest hit. We aren't first in line if the closest hit is a car.
                    {
                        intersectionController.EnqueueStopSign(this);
                    }
                }

            }
        }
        else
        {
            // No target in sight: clear tracking and accelerate to cruise speed.
            trackedTarget = null;
            initialDetectionDistance = 0f;
            speed = Mathf.Min(speed + accelerationRate * Time.fixedDeltaTime, maxSpeed); // Normal speed
        }
    }

    void HandleEndOfSpline()
    {
        // Try to transfer to next spline

        // The idea is that if there is a "continue" node, we take that one. 
        // Otherwise, we look at our turn intention and try to take the corresponding turn if it's available, and if not, we take any available turn. 
        // If there are no available turns, the car is destroyed.

        // TODO create system to specify car intentions at intersections (left turn, right turn, straight) and use that to determine which spline to transfer to
        // For now, just continue until there is no "continueNode", at which point we will try to transfer to a random outgoing spline from the intersection node

        List<TurnChoice> availableTurns = intersectionNode.GetAvailableTurnChoices();
        TurnChoice? chosenTurn = null;

        // First, try to continue
        if (availableTurns.Contains(TurnChoice.Continue))
        {
            chosenTurn = TurnChoice.Continue;
        }
        // Then, try to match the turn intention if there is one
        else if (turnIntention.HasValue && availableTurns.Contains(turnIntention.Value))
        {
            chosenTurn = turnIntention.Value;
        }
        // Finally, if the intended turn isn't available, just pick a random available turn
        else if (availableTurns.Count > 0)
        {
            // Pick a random available turn
            int index = Random.Range(0, availableTurns.Count);
            chosenTurn = availableTurns[index];
        }
        // Note: If there are no available turns, chosenTurn will remain null, and the car will be destroyed below when we fail to transfer to a new node.

        IntersectionNode? nextNode = intersectionNode.TransferCarToNextNode(this, chosenTurn);
        if (nextNode != null) {
            intersectionNode = nextNode;
            currentSplineTValue = 0f;
        }
        else
        {
            DestroyAndDropParticles(); // No more splines to transfer to, destroy the car
            intersectionController.DequeueStopSign(this); // If we're waiting at a stop sign, make sure to dequeue from the stop sign queue when we leave, even if it's because we're destroyed.
            return;
        }
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

        if (!hasCollided && exhaustEffectPrefab != null)
        {
            if (exhaustEffectInstance == null)
            {
                exhaustEffectInstance = Instantiate(exhaustEffectPrefab, transform);
            }
        }
        else
        {
            if (exhaustEffectInstance != null)
            {
                StopExhaust();
            }

        }

        collisionCooldown = Mathf.Max(collisionCooldown - Time.deltaTime, 0f);

    }

    void FixedUpdate()
    {
        // Apply physics-based movement if needed (currently not used since we're directly setting position in Update)

        if (hasCollided) return; // Stop moving if we've collided with another car

        // T value updates        
        if (currentSplineTValue > 1f)
        {
            HandleEndOfSpline();
        }

        
        DoRaycastDetection();
        UpdatePositionAlongSpline();
        UpdateRotationAlongSpline();

    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // If we hit a stop sign trigger, set canProceedAtStopSign to true so we can ignore the stop sign raycast in Update and proceed through the intersection.
        if (other.gameObject.layer == LayerMask.NameToLayer("Intersection Leave"))
        {
            intersectionController.DequeueStopSign(this);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        Debug.Log("Collided with: " + collision.collider.name);
        rb.bodyType = RigidbodyType2D.Dynamic; // Make the car affected by physics after collision

        if (collisionCooldown == 0)
        {
            collisionCooldown = 1f; // Set cooldown to 1 second
            Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity); // Spawn explosion effect
        }

        hasCollided = true; // Stop moving if we've collided with another car
    }

    public void OnPointerClick(PointerEventData e)
    {
        IntersectionController.Instance.DequeueStopSign(this);
        if (isDeviant)
        {
            // If this car is a deviant, clicking it will "report" it and destroy it, giving the player points.
            IntersectionController.Instance.AddScore(1); // TODO add a score system and update the score when a deviant car is reported
            Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity); // Spawn explosion effect
            DestroyAndDropParticles();
        }
        else
        {
            // If this car is not a deviant, clicking it will penalize the player by reducing their score and destroying the car.
            IntersectionController.Instance.AddScore(-1); // TODO add a score system and update the score when a non-deviant car is mistakenly reported
            DestroyAndDropParticles();
        }
    }

    // Called just before OnDestroy()
    void OnDisable()
    {

        StopExhaust();
        // Notify the intersection controller that this car was removed so it can update active counts
        if (IntersectionController.Instance != null)
        {
            IntersectionController.Instance.NotifyCarDestroyed();
        }
    }

    void StopExhaust()
    {
        if (exhaustEffectInstance)
        {
            exhaustEffectInstance.transform.parent = null;  // Detach from parent 
            ParticleSystem particles = exhaustEffectInstance.GetComponent<ParticleSystem>();
            particles.Stop();
            exhaustEffectInstance = null;
        }
    }

    public void DestroyAndDropParticles()
    {
        StopExhaust();
        Destroy(gameObject);
    }
}

public enum TurnChoice
{
    Continue,
    Left,
    NoTurn,
    Right
}
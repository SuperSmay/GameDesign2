using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Splines;

#nullable enable

public class CarPathFollower : MonoBehaviour, IPointerClickHandler
{

    float currentSplineTValue = 0f;
    [System.NonSerialized] public IntersectionNode intersectionNode;

    public bool isDeviant; // Whether this car is a "deviant" that doesn't follow traffic rules.
    // TODO Have the car control the behavior, rather than having the intersection controller manually modify the attributes.

    public float stopSignMinimumQueueDistance; // How far ahead the car can queue for a stop sign. 
    public float maxDistanceToMoveAfterStopping;  // If the car is stopped for a target but that target moves away (e.g. a car in front turns or accelerates), this is the maximum distance the car will stay stopped within. This prevents cars from getting stuck trying to stop for a target that has moved away.
    public float maxSpeed;
    public float minSpeed; // Minimum speed to stop the very gradual creeping when trying to stop for a target.
    public float decelerationRate;
    public float absoluteMaxEmergencyDeceleration; // An absolute max limit to how hard the car will brake when trying to avoid a collision
    public float accelerationRate;
    public float targetStopTime;  // Safe time headway in seconds (tune this for following distance)
    public List<ColliderStopDistanceInfo> stopDistancesList;
   
        
     // List of stop distances for different collider types, used to populate the stopDistances dictionary in Awake()
    Dictionary<ColliderType, float> stopDistances { get
        {
            Dictionary<ColliderType, float> dict = new Dictionary<ColliderType, float>();
            foreach (ColliderStopDistanceInfo info in stopDistancesList)
            {
                dict[info.colliderType] = info.distance;
            }
            return dict;
        } } // How far away the car should try to stop from different types of targets (other cars, stop signs, etc)
    public TurnChoice turnIntention; // The car will take the first available turn that matches this intention when it reaches an intersection.
                                     // If the there is no "continue" option, and the intended turn is not available, the car will take any available turn.

    float speed;
    float despawnTimer = 5f;

    [System.NonSerialized] public bool canProceedAtStopSign = false; // Whether this car is currently allowed to proceed through a stop sign. This is set by the IntersectionController when it's this car's turn to go.

    Rigidbody2D rb;
    SpriteRenderer spriteRenderer;

    [SerializeField] GameObject explosionEffectPrefab;
    [SerializeField] GameObject exhaustEffectPrefab;
    [SerializeField] GameObject tireScreechEffectPrefab;
    [SerializeField] List<GameObject> tireScreechEffectSpawnPoints; // List of points (e.g. empty child game objects) where we can spawn tire screech effects when the car brakes hard.
    List<GameObject>? tireScreechEffectInstances;
    [SerializeField] List<Sprite> sprites; // List of possible sprites to randomly assign to this car for visual variety.

    [Header("Path Scanning")]
    public float lookAheadDistance = 25f; // Max distance to scan
    public float carWidth = 0.5f;           // Used for the SphereCast radius
    public LayerMask obstacleLayer;       // Only hit other cars/stop lines


    bool hasCollided = false;
    bool hasBeenClicked = false; // Whether this car has been clicked on by the player, used to prevent multiple clicks being registered on the same car.
    GameObject? exhaustEffectInstance;
    float collisionCooldown = 0f; // Time in seconds to ignore collisions after a collision has occurred

    // Braking tracking: remember the first detection distance for the current target so we can
    // compute a smooth, linear target-speed curve from detection to stop point.
    GameObject? trackedTarget = null;


    // used to determine whether the tracked target has moved independently
    Vector3 trackedTargetLastPosition;

    IntersectionController intersectionController = IntersectionController.Instance;


    void UpdatePositionAlongSpline()
    {
        // Get spline length for consistent speed across different splines (since the T value is normalized, we need to account for spline length to maintain consistent speed)
        float splineLength = intersectionNode.splineContainer.CalculateLength();

        currentSplineTValue += speed * intersectionController.fixedDeltaTimeSpeedMult / splineLength * 10f;  // 10 is a rough estimate of the avg spline length.
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

        RaycastHit2D? closestTargetHit = ScanPathAhead();

        if (closestTargetHit.HasValue)
        {
            GameObject hitObj = closestTargetHit.Value.collider.gameObject;
            float distanceToTarget = Vector3.Distance(transform.position, closestTargetHit.Value.point);

            // 1. Determine base minimum stopping distance
            float desiredStopDistance = stopDistances[LayerMasksToColliderType.Map[hitObj.layer]] + 0.7f;

            // 2. Calculate Target Velocity
            float targetSpeed = 0f;
            if (trackedTarget != hitObj)
            {
                trackedTarget = hitObj;
                trackedTargetLastPosition = hitObj.transform.position;
                // Assume target is stationary on the exact frame we detect it to avoid delta spikes
            }
            else
            {
                Vector3 newPos = hitObj.transform.position;
                Vector3 moveDelta = newPos - trackedTargetLastPosition;
                trackedTargetLastPosition = newPos;

                // Project movement onto our up axis (assuming 2D top-down) and convert to speed per second
                float forwardMovement = Vector3.Dot(moveDelta, transform.up);
                targetSpeed = forwardMovement / intersectionController.fixedDeltaTimeSpeedMult;
            }

            // 3. Intelligent Driver Model (IDM) Variables
            float s = distanceToTarget;          // Current distance to target
            float s0 = desiredStopDistance;      // Minimum desired gap
            float v = speed;                     // Current speed
            float v0 = maxSpeed;                 // Desired speed (speed limit)
            float deltaV = v - targetSpeed;      // Approach rate (positive if we are faster than target)
            float a = accelerationRate;          // Max acceleration
            float b = decelerationRate;          // Comfortable deceleration

            // 4. Calculate the desired dynamic gap (s*)
            // We use Mathf.Max to ensure that if the target speeds away rapidly, we don't get a negative gap requirement
            float s_star = s0 + Mathf.Max(0f, (v * targetStopTime) + ((v * deltaV) / (2f * Mathf.Sqrt(a * b))));

            // 5. Calculate acceleration using the IDM formula
            float freeRoadTerm = 1f - Mathf.Pow(v / v0, 4f);
            float interactionTerm = Mathf.Pow(s_star / Mathf.Max(s, 0.001f), 2f); // prevent division by zero

            float calculatedAcceleration = a * (freeRoadTerm - interactionTerm);

            // Optional: Apply an absolute hard limit to emergency braking if a car cuts us off instantly
            if (calculatedAcceleration < 0 && calculatedAcceleration < -absoluteMaxEmergencyDeceleration)
            {
                SpawnTireScreechEffect();
            }
            else
            {
                StopTireScreechEffect();
            }
            calculatedAcceleration = Mathf.Max(calculatedAcceleration, -absoluteMaxEmergencyDeceleration);

            // 6. Apply acceleration to speed
            speed += calculatedAcceleration * intersectionController.fixedDeltaTimeSpeedMult;

            // Prevent reversing and cap at true max speed
            speed = Mathf.Clamp(speed, 0f, maxSpeed);

            // 7. Snap to zero if creeping too slowly
            if (speed < minSpeed && calculatedAcceleration <= 0f)
            {
                speed = 0f;
            }

            // 8. Intersection Controller Notification
            if (LayerMasksToColliderType.Map[hitObj.layer] == ColliderType.StopLine &&
                distanceToTarget < stopSignMinimumQueueDistance &&
                speed == 0f)
            {
                intersectionController.EnqueueStopSign(this);
            }
        }
        else
        {
            // Free road behavior: accelerate to max speed
            trackedTarget = null;
            StopTireScreechEffect();
            speed = Mathf.MoveTowards(speed, maxSpeed, accelerationRate * intersectionController.fixedDeltaTimeSpeedMult);
        }
    }

    RaycastHit2D? ScanPathAhead(float maxAngleToScan = 45f)
    {
        float distanceScanned = 0f;
        IntersectionNode? scanNode = intersectionNode;
        float t = currentSplineTValue;

        Vector3 stepStartPos = transform.position;
        float sphereRadius = carWidth / 2f;

        while (distanceScanned < lookAheadDistance && scanNode != null)
        {
            Spline currentSpline = scanNode.splineContainer.Spline;
            float splineLength = currentSpline.GetLength();

            // Step forward by 2 meters at a time (tune this for performance vs curve precision)
            float stepDistance = Mathf.Min(2f, lookAheadDistance - distanceScanned);

            // Convert step distance to T (normalized 0 to 1). 
            // Note: For extreme precision on wildly distorted splines, use SplineUtility, 
            // but simple linear ratio is highly performant and usually perfect for roads.
            float tStep = stepDistance / splineLength;
            float nextT = t + tStep;

            Vector3 stepEndPos;
            bool crossingToNextNode = false;

            Vector3 localEndPos;
            if (nextT > 1f)
            {
                // We reached the end of this spline piece
                localEndPos = currentSpline.EvaluatePosition(1f);
                crossingToNextNode = true;
            }
            else
            {
                localEndPos = currentSpline.EvaluatePosition(nextT);
            }

            // Transform the local spline point into world space coordinates!
            stepEndPos = scanNode.splineContainer.transform.TransformPoint(localEndPos);

            // Do the SphereCast for this specific chunk of the curve
            Vector3 direction = stepEndPos - stepStartPos;
            float segmentLength = direction.magnitude;

            // If the direction is more than maxAngleToScan, we stop here because drivers irl are sometimes bad at looking around corners.
            if (Vector3.Angle(direction, transform.up) > maxAngleToScan)
            {
                break;
            }

            if (segmentLength > 0.001f) // Prevent zero-length cast errors
            {
                // We cast from stepStartPos to stepEndPos
                DebugDrawing.DrawDebugCapsule(stepStartPos, stepEndPos, sphereRadius, Color.red, 0.1f);
                RaycastHit2D[] hits = Physics2D.CircleCastAll(stepStartPos, sphereRadius, direction.normalized, segmentLength);

                RaycastHit2D? closestTargetHit = null;
                float closestTargetDistance = Mathf.Infinity;

                foreach (RaycastHit2D hit in hits)
                {
                    // Ignore hits that are this car's own collider
                    if (hit.collider.gameObject == gameObject) continue;
                    // Ignore hits on the clickbox colliders
                    if (hit.collider.gameObject.layer == ColliderTypeToLayerMasks.Map[ColliderType.ClickBox]) continue;
                    // Ignore stop lines if we're allowed to go
                    if (canProceedAtStopSign && hit.collider.gameObject.layer == ColliderTypeToLayerMasks.Map[ColliderType.StopLine]) continue;
                    // Ignore interssection leave triggers so we don't get confused when leaving an intersection
                    if (hit.collider.gameObject.layer == ColliderTypeToLayerMasks.Map[ColliderType.IntersectionLeave]) continue;


                    float distanceToHit = Vector3.Distance(transform.position, hit.point);
                    // We found our closest target!
                    if (distanceToHit < closestTargetDistance)
                    {
                        closestTargetDistance = distanceToHit;
                        closestTargetHit = hit;
                    }
                }

                if (closestTargetHit.HasValue)
                {
                    // We found a target in this segment, return it
                    return closestTargetHit;
                }
            }

            // Move our start position forward for the next loop iteration
            distanceScanned += segmentLength;
            stepStartPos = stepEndPos;

            if (crossingToNextNode)
            {
                // ⚠️ IMPORTANT: You must use a "Peek" method here, not one that alters the car's state.
                scanNode = scanNode.PeekNextNode(turnIntention);
                t = 0f; // Reset T to the start of the new spline
            }
            else
            {
                t = nextT;
            }
        }

        return null; // Nothing found in our path
    }
    void SpawnTireScreechEffect()
    {
        Debug.Log("Spawning tire screech effect");
        if (tireScreechEffectPrefab != null && tireScreechEffectSpawnPoints != null)
        {
            if (tireScreechEffectInstances == null)
            {
                tireScreechEffectInstances = new List<GameObject>();
                foreach (GameObject spawnPoint in tireScreechEffectSpawnPoints)
                {
                    GameObject effectInstance = InstantiateParticleEffects(tireScreechEffectPrefab, spawnPoint.transform);
                    tireScreechEffectInstances.Add(effectInstance);
                }
            }
        }
    }

    void StopTireScreechEffect()
    {
        Debug.Log("Stopping tire screech effect");
        if (tireScreechEffectInstances != null)
        {
            foreach (GameObject effectInstance in tireScreechEffectInstances)
            {
                ParticleSystem particles = effectInstance.GetComponent<ParticleSystem>();
                particles.Stop();
                effectInstance.transform.parent = null;  // Detach from parent so it doesn't get destroyed immediately with the car
            }
            tireScreechEffectInstances = null;
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
        TurnChoice chosenTurn = TurnChoice.Unspecified;

        // First, try to continue
        if (availableTurns.Contains(TurnChoice.Continue))
        {
            chosenTurn = TurnChoice.Continue;
        }
        // Then, try to match the turn intention if there is one
        else if (turnIntention != TurnChoice.Unspecified && availableTurns.Contains(turnIntention))
        {
            chosenTurn = turnIntention;
        }
        // Finally, if the intended turn isn't available, just pick a random available turn
        else if (availableTurns.Count > 0)
        {
            // Pick a random available turn
            int index = Random.Range(0, availableTurns.Count);
            chosenTurn = availableTurns[index];
        }
        // Note: If there are no available turns, chosenTurn will remain TurnChoice.Unspecified, and the car will be destroyed below when we fail to transfer to a new node.

        IntersectionNode? nextNode = intersectionNode.TransferCarToNextNode(this, chosenTurn);
        if (nextNode != null)
        {
            intersectionNode = nextNode;
            currentSplineTValue = 0f;
        }
        else
        {
            DestroyAndDropParticles(); // No more splines to transfer to, destroy the car
            return;
        }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Randomly assign a sprite from the list for visual variety
        if (sprites != null && sprites.Count > 0)
        {
            int index = Random.Range(0, sprites.Count);
            spriteRenderer.sprite = sprites[index];
        }

    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

        if (hasCollided)
        {
            despawnTimer -= intersectionController.deltaTimeSpeedMult;
            if (despawnTimer <= 0f)
            {
                DestroyAndDropParticles();
            }
        }

        if (!hasCollided && exhaustEffectPrefab != null)
        {
            if (exhaustEffectInstance == null)
            {
                exhaustEffectInstance = InstantiateParticleEffects(exhaustEffectPrefab, transform);
            }
        }
        else
        {
            if (exhaustEffectInstance != null)
            {
                StopExhaust();
            }

        }

        collisionCooldown = Mathf.Max(collisionCooldown - intersectionController.deltaTimeSpeedMult, 0f);

    }

    GameObject InstantiateParticleEffects(GameObject effectPrefab, Transform parent)
    {
        GameObject effectInstance = Instantiate(effectPrefab, parent);
        intersectionController.activeEffects.Add(effectInstance);
        return effectInstance;
    }

    GameObject InstantiateParticleEffects(GameObject effectPrefab, Vector3 position, Quaternion rotation)
    {
        GameObject effectInstance = Instantiate(effectPrefab, position, rotation);
        intersectionController.activeEffects.Add(effectInstance);
        return effectInstance;
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
        if (other.gameObject.layer == ColliderTypeToLayerMasks.Map[ColliderType.IntersectionLeave])
        {
            intersectionController.DequeueStopSign(this);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        rb.bodyType = RigidbodyType2D.Dynamic; // Make the car affected by physics after collision
        IntersectionController.Instance.DequeueStopSign(this);
        StopTireScreechEffect();

        if (collisionCooldown == 0)
        {
            collisionCooldown = 1f; // Set cooldown to 1 second
            InstantiateParticleEffects(explosionEffectPrefab, transform.position, Quaternion.identity); // Spawn explosion effect
        }

        hasCollided = true; // Stop moving if we've collided with another car
        intersectionController.gameOver = true; // Trigger game over in the intersection controller
        intersectionController.gameOverPosition = transform.position; // Set the game over position to the location of the collision for camera focus
    }

    public void OnPointerClick(PointerEventData e)
    {

        if (isDeviant && !hasBeenClicked && !hasCollided)
        {
            // If this car is a deviant, clicking it will "report" it and destroy it, giving the player points.
            GameManager.Instance.Score += 1;
            InstantiateParticleEffects(explosionEffectPrefab, transform.position, Quaternion.identity); // Spawn explosion effect
            DestroyAndDropParticles();
        }

        else if (hasCollided)
        {
            DestroyAndDropParticles();
        }
        else if (!hasBeenClicked)
        {
            // If this car is not a deviant, clicking it will penalize the player by reducing their score and destroying the car.
            GameManager.Instance.Score -= 1;
            gameObject.GetComponent<SpriteRenderer>().color = Color.gray; // Flash gray to indicate mistaken report

        }

        hasBeenClicked = true;
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
        StopTireScreechEffect();
        IntersectionController.Instance.DequeueStopSign(this);
        IntersectionController.Instance.activeCars.Remove(this);
        Destroy(gameObject);
    }
}

public enum ColliderType
{
    Car,
    StopLine,
    IntersectionLeave,
    ClickBox,
}

public static class ColliderTypeToLayerMasks
{
    // Map collider types to layer indices
    public static readonly Dictionary<ColliderType, int> Map = new Dictionary<ColliderType, int>()
    {
        { ColliderType.Car, LayerMask.NameToLayer("Cars") },
        { ColliderType.StopLine, LayerMask.NameToLayer("Stop Lines") },
        { ColliderType.IntersectionLeave, LayerMask.NameToLayer("Intersection Leave") },
        { ColliderType.ClickBox, LayerMask.NameToLayer("Pointer Interaction") },
    };
}

public static class LayerMasksToColliderType
{
    // Map layer indices back to collider types for easy lookup during raycast hit processing
    public static readonly Dictionary<int, ColliderType> Map = new Dictionary<int, ColliderType>()
    {
        { LayerMask.NameToLayer("Cars"), ColliderType.Car },
        { LayerMask.NameToLayer("Stop Lines"), ColliderType.StopLine },
        { LayerMask.NameToLayer("Intersection Leave"), ColliderType.IntersectionLeave },
        { LayerMask.NameToLayer("Pointer Interaction"), ColliderType.ClickBox },
    };
}

[System.Serializable]
public struct ColliderStopDistanceInfo
{
    public ColliderType colliderType;
    public float distance;

    public ColliderStopDistanceInfo(ColliderType type, float dist)
    {
        colliderType = type;
        distance = dist;
    }
}

public enum TurnChoice
{
    Continue,
    Left,
    NoTurn,
    Right,
    Unspecified // Used for cars that don't have a specific turn intention and will just take any available turn
}
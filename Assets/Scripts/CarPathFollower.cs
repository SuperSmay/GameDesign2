using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Splines;

#nullable enable

public class CarPathFollower : MonoBehaviour, IPointerClickHandler
{

    float currentSplineTValue = 0f;
    [System.NonSerialized] public IntersectionNode intersectionNode;

    public DeviantType deviantType; // Whether this car is a "deviant" that doesn't follow traffic rules.
    // TODO Have the car control the behavior, rather than having the intersection controller manually modify the attributes.

    public float stopSignMinimumQueueDistance; // How far ahead the car can queue for a stop sign. 
    public float maxDistanceToMoveAfterStopping;  // If the car is stopped for a target but that target moves away (e.g. a car in front turns or accelerates), this is the maximum distance the car will stay stopped within. This prevents cars from getting stuck trying to stop for a target that has moved away.
    public float maxSpeed;
    public float minSpeed; // Minimum speed to stop the very gradual creeping when trying to stop for a target.
    public float speedVariance; // Random variance added to the base speed for each car for visual variety
    public float centerOffset; // How much the car is offset from the center of the lane.
    public float centerVariance; // Random variance added to the center of the car's path along the spline for visual variety (e.g. to make some cars drive slightly to the left or right within the lane)
    public float decelerationRate;
    public float absoluteMaxEmergencyDeceleration; // An absolute max limit to how hard the car will brake when trying to avoid a collision
    public float accelerationRate;
    public float targetStopTime;  // Safe time headway in seconds (tune this for following distance)
    public List<ColliderStopDistanceInfo> stopDistancesList;

    // The stop lines this car has committed to going through. 
    // This is used to stop the car from breaking for a pedestrian that enters the crosswalk right after the car has been allowed to go through the stop line.
    public HashSet<IntersectionStopLine> committedStopLines = new HashSet<IntersectionStopLine>();


    // List of stop distances for different collider types, used to populate the stopDistances dictionary in Awake()
    Dictionary<ColliderType, float> stopDistances
    {
        get
        {
            Dictionary<ColliderType, float> dict = new Dictionary<ColliderType, float>();
            foreach (ColliderStopDistanceInfo info in stopDistancesList)
            {
                dict[info.colliderType] = info.distance;
            }
            return dict;
        }
    } // How far away the car should try to stop from different types of targets (other cars, stop signs, etc)
    public TurnChoice turnIntention; // The car will take the first available turn that matches this intention when it reaches an intersection.
                                     // If the there is no "continue" option, and the intended turn is not available, the car will take any available turn.

    public float speed;
    float despawnTimer = 5f;

    // [System.NonSerialized] public bool canProceedAtStopSign = false; // Whether this car is currently allowed to proceed through a stop sign. This is set by the IntersectionController when it's this car's turn to go.

    Rigidbody2D rb;
    SpriteRenderer spriteRenderer;
    public Collider2D col;

    [SerializeField] GameObject explosionEffectPrefab;
    [SerializeField] GameObject exhaustEffectPrefab;
    [SerializeField] GameObject tireScreechEffectPrefab;
    [SerializeField] List<GameObject> tireScreechEffectSpawnPoints; // List of points (e.g. empty child game objects) where we can spawn tire screech effects when the car brakes hard.
    List<GameObject>? tireScreechEffectInstances;
    [SerializeField] GameObject turnSignalEffectPrefab;
    [SerializeField] List<GameObject> leftTurnSignalSpawnPoints;
    [SerializeField] List<GameObject> rightTurnSignalSpawnPoints;
    List<GameObject>? turnSignalInstances;
    [SerializeField] List<Sprite> sprites; // List of possible sprites to randomly assign to this car for visual variety.

    [Header("Path Scanning")]
    public float lookAheadDistance = 25f; // Max distance to scan
    public float carWidth = 0.5f;           // Used for the SphereCast radius


    bool hasCollided = false;
    bool hasBeenClicked = false; // Whether this car has been clicked on by the player, used to prevent multiple clicks being registered on the same car.
    bool hasSwitchedToTurnNode = false; // Used to track whether we've switched to the turn node after reaching the split point, so we can update our turn signals accordingly on the next node change.
    int tireMarkTriggersThisFixedUpdate = 0; // How many things are currently triggering the spawning of tire mark effects (e.g. hard braking, sharp turning while speeding, etc.). We use this to determine when to stop the tire mark effects, since multiple conditions can trigger them at the same time.
    GameObject? exhaustEffectInstance;
    float collisionCooldown = 0f; // Time in seconds to ignore collisions after a collision has occurred
    float smoothedTargetSpeed = 0f;
    bool hasEncounteredDeviantBehavior = false; // Whether the car has already encountered the conditions to trigger its deviant behavior. Used to prevent the player from losing points for something they couldn't have known about.

    // Speeding deviant behavior variables
    float speedingTurnAngleThreshold = 20f; // Minimum angle change to trigger tire screech effects when speeding

    // Swerving deviant behavior variables
    float swervingAmplitude = 0.2f; // How far left and right the car swerves from the center of the lane
    float swervingFrequency = 1f; // How fast the car swerves left and right
    float swervingTimer = 0f; // Timer to keep track of swerving oscillation
    float swervingCooldownTimer = 0f; // Timer to determine how long to wait before starting to swerve again after finishing a swerving behavior

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

        // Now 'speed' truly means "World Units per second"
        currentSplineTValue += (speed * GameManager.Instance.fixedDeltaTimeSpeedMult) / splineLength;



        // Move along the current spline
        Vector3 targetPosition = intersectionNode.splineContainer.EvaluatePosition(currentSplineTValue);
        targetPosition.z = 0f; // Keep the car on the 2D plane

        float centerOffsetWithSwerve = centerOffset;

        if (deviantType == DeviantType.swerving)
        {

            if (swervingCooldownTimer > 0f)
            {
                swervingCooldownTimer -= GameManager.Instance.deltaTimeSpeedMult;
            }
            else
            {
                swervingTimer += GameManager.Instance.deltaTimeSpeedMult * (speed / maxSpeed);  // Scale how much we adjust the offset by speed. This stops them sliding around at a stop, and stops the the swerving from jumping around as they slow down.
                if (swervingTimer >= Mathf.PI * 2 / swervingFrequency) // After one full oscillation, reset the timer and start a cooldown
                {
                    swervingTimer = 0f;
                    swervingCooldownTimer = Random.Range(1f, 3f); // Random cooldown between 1 and 3 seconds before swerving again
                }

                float swervingOffset = Mathf.Sin(swervingTimer * swervingFrequency) * swervingAmplitude;
                centerOffsetWithSwerve += swervingOffset;
            }

        }

        // Apply the combined perpendicular offset to the direction of movement (assuming the car's forward direction is along the spline tangent)
        Vector3 tangent = ((Vector3)intersectionNode.splineContainer.EvaluateTangent(currentSplineTValue)).normalized;
        Vector3 perpendicular = new Vector3(-tangent.y, tangent.x, 0f); // Rotate tangent 90 degrees to get perpendicular
        targetPosition += perpendicular * centerOffsetWithSwerve;

        // If we are speeding, also add tire screech marks when turning sharply
        if (deviantType == DeviantType.speeding)
        {
            float angleChange = Vector3.Angle(tangent, transform.up);
            if (angleChange > speedingTurnAngleThreshold / 10 && speed > maxSpeed * 0.7f) // If we're turning more than the threshold and going fast, spawn tire screech effects
            {
                tireMarkTriggersThisFixedUpdate++;
            }
        }

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

        // Vector3 calculatedCurrentVelocity = (transform.position - previousSelfPosition) / GameManager.Instance.fixedDeltaTimeSpeedMult;

        if (GameManager.Instance.paused || GameManager.Instance.fixedDeltaTimeSpeedMult == 0) return; // Don't do raycast detection if the game is paused

        RaycastHit2D? closestTargetHit = ScanPathAhead();

        if (closestTargetHit.HasValue)
        {

            DebugDrawing.DrawDebugCircle(closestTargetHit.Value.point, 0.1f, Color.green);

            GameObject hitObj = closestTargetHit.Value.collider.gameObject;
            float distanceToTarget = Vector3.Distance(transform.position, closestTargetHit.Value.point);

            if (deviantType == DeviantType.tailgating && distanceToTarget < 1f)
            {
                hasEncounteredDeviantBehavior = true; // The car is tailgating, so if they get within 1 meter of the target, we consider that enough for the player to notice
            }

            // 1. Determine base minimum stopping distance
            float desiredStopDistance = stopDistances[LayerMasksToColliderType.Map[hitObj.layer]] + 0.7f;

            // 2. Calculate Target Velocity
            if (trackedTarget != hitObj)
            {
                trackedTarget = hitObj;
                trackedTargetLastPosition = hitObj.transform.position;
                
                // Try to get the real speed if it's a car, otherwise assume 0
                // (Assuming your car script is called CarController, change as needed)
                var targetCar = hitObj.GetComponent<CarPathFollower>();
                smoothedTargetSpeed = targetCar != null ? targetCar.speed * 10f : 0f; 
            }
            else
            {
                Vector3 newPos = hitObj.transform.position;
                Vector3 moveDelta = newPos - trackedTargetLastPosition;
                trackedTargetLastPosition = newPos;

                float forwardMovement = Vector3.Dot(moveDelta, transform.up);
                float rawTargetSpeed = forwardMovement / GameManager.Instance.fixedDeltaTimeSpeedMult;
                
                // SMOOTH THE NOISE: Blend the new raw speed with the previous smoothed speed
                smoothedTargetSpeed = Mathf.Lerp(smoothedTargetSpeed, rawTargetSpeed, Time.fixedDeltaTime * 15f);
            }

            // 3. Intelligent Driver Model (IDM) Variables
            float s = distanceToTarget;          // Current distance to target
            float s0 = desiredStopDistance;      // Minimum desired gap
            float v = speed;                     // Current speed
            float v0 = maxSpeed;                 // Desired speed (speed limit)
            float deltaV = v - smoothedTargetSpeed;      // Approach rate (positive if we are faster than target)
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
                tireMarkTriggersThisFixedUpdate++;
            }
            calculatedAcceleration = Mathf.Max(calculatedAcceleration, -absoluteMaxEmergencyDeceleration);

            // 6. Apply acceleration to speed
            speed += calculatedAcceleration * GameManager.Instance.fixedDeltaTimeSpeedMult;

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
                // If we hit a stop line collider, it will have this component
                IntersectionStopLine stopLine = hitObj.GetComponent<IntersectionStopLine>();
                intersectionController.EnqueueStopSign(stopLine, this);
            }
        }
        else
        {
            // Free road behavior: accelerate to max speed
            trackedTarget = null;
            speed = Mathf.MoveTowards(speed, maxSpeed, accelerationRate * GameManager.Instance.fixedDeltaTimeSpeedMult);
        }
    }

    RaycastHit2D? ScanPathAhead(float maxAngleToScan = 45f)
    {
        // TODO put the spawn point in front of the car not at the origin of the car
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
                DebugDrawing.DrawDebugCapsule(stepStartPos, stepEndPos, sphereRadius, Color.red);
                RaycastHit2D[] hits = Physics2D.CircleCastAll(stepStartPos, sphereRadius, direction.normalized, segmentLength);

                RaycastHit2D? closestTargetHit = null;
                float closestTargetDistance = Mathf.Infinity;

                foreach (RaycastHit2D hit in hits)
                {

                    // Draw debug points for all hits
                    DebugDrawing.DrawDebugCircle(hit.point, 0.1f, Color.blue);

                    // Ignore hits that are this car's own collider
                    if (hit.collider.gameObject == gameObject) continue;
                    // Ignore hits on the clickbox colliders
                    if (hit.collider.gameObject.layer == ColliderTypeToLayerMasks.Map[ColliderType.ClickBox]) continue;
                    // Ignore stop lines if we're going to run them (deviant)
                    if (hit.collider.gameObject.layer == ColliderTypeToLayerMasks.Map[ColliderType.StopLine] && deviantType == DeviantType.runsStop)
                    {
                        hasEncounteredDeviantBehavior = true; // We've decided to ignore a stop line, so the player will be able to spot this.
                        continue;
                    }
                    // Ignore stop lines if we're allowed to go
                    if (hit.collider.gameObject.layer == ColliderTypeToLayerMasks.Map[ColliderType.StopLine])
                    {
                        IntersectionStopLine stopLine = hit.collider.gameObject.GetComponent<IntersectionStopLine>();
                        // Check if we are allowed to proceed through this stop line, and if the movement-blocking colliders for this stop line are clear (e.g. no pedestrians in the crosswalk, etc.)
                        if (stopLine.CanCarProceed(this))
                        {
                            if (stopLine.AreMovementBlockingCollidersClearOfCars(col))
                            {
                                if (stopLine.AreMovementBlockingCollidersClearOfPeds(col))
                                {
                                    // We can go through this stop line, so ignore it in our pathfinding
                                    continue;
                                }
                                else if (deviantType == DeviantType.ignoresPedestrians)
                                {
                                    hasEncounteredDeviantBehavior = true; // We've decided to ignore a stop line with pedestrians in the crosswalk, so the player will be able to spot this.
                                    continue;
                                }
                            }
                        }
                    }
                    // Ignore intersection leave triggers so we don't get confused when leaving an intersection
                    if (hit.collider.gameObject.layer == ColliderTypeToLayerMasks.Map[ColliderType.IntersectionLeave]) continue;
                    // Stop line filtering
                    if (hit.collider.gameObject.layer == ColliderTypeToLayerMasks.Map[ColliderType.StopLine])
                    {
                        IntersectionStopLine stopLine = hit.collider.gameObject.GetComponent<IntersectionStopLine>();

                        // Ignore stop lines that we've already committed to going through, since we won't be stopping for those anymore even if we're still within the max stopping distance.
                        if (committedStopLines.Contains(stopLine))
                        {
                            continue;
                        }

                        // Ignore stop lines that don't match our turn intention, since we won't be stopping for those.
                        // Continue is the case where it applies to all turns, so we don't want to skip those
                        if (stopLine.turnChoiceForThisStopLine != turnIntention && stopLine.turnChoiceForThisStopLine != TurnChoice.Continue)
                        {
                            continue;
                        }
                    }

                    // Note: This is disabled because it means that cars will just run pedestrians over.
                    // Instead, this is implemented by ignoring pedestrian only colliders in the calls to AreMovementBlockingCollidersClear(),
                    // so the car will stop for the stop line but won't stop for pedestrians in the crosswalk. 
                    // If needed, this could be enhanced to have the movement blocking colliders keep a list of which types of objects
                    // are in range, but for now simply ignoring the ped only colliders achieves the desired behavior.

                    // Ignore pedestrians if we're a deviant that ignores pedestrians
                    // if (deviantType == DeviantType.ignoresPedestrians && hit.collider.gameObject.layer == ColliderTypeToLayerMasks.Map[ColliderType.Pedestrian])
                    // {
                    //     continue;
                    // }

                    // If we don't have a LayerMask defined for this collider type, we log a warning and ignore it (treat it as free road)
                    if (!LayerMasksToColliderType.Map.ContainsKey(hit.collider.gameObject.layer))
                    {
                        Debug.LogWarning("No layer mask defined for collider layer " + hit.collider.gameObject.layer + ". Ignoring.");
                        continue;
                    }

                    if (!stopDistances.ContainsKey(LayerMasksToColliderType.Map[hit.collider.gameObject.layer]))
                    {
                        Debug.LogWarning("No stop distance defined for collider type " + LayerMasksToColliderType.Map[hit.collider.gameObject.layer] + ". Ignoring.");
                        continue;
                    }


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
                // Get continue node if it exists, otherwise get the node corresponding to our turn intention
                scanNode = scanNode.continueNode != null ? scanNode.continueNode : scanNode.PeekNextNode(turnIntention);
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

        List<TurnChoice> availableTurns = intersectionNode.GetAvailableTurnChoices();
        TurnChoice? chosenTurn = null;

        // First, try to continue
        if (availableTurns.Contains(TurnChoice.Continue))
        {
            chosenTurn = TurnChoice.Continue;
        }
        // Then, try to match the turn intention if there is one
        // Note: The turn intention will be Continue if the car doesn't have a specific intention
        // We check for that above, so this check will fail in that case and a random turn will be chosen below
        else if (availableTurns.Contains(turnIntention))
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
        // Note: If there are no available turns, chosenTurn will remain null, 
        // and the car will be destroyed below when we fail to transfer to a new node.
        IntersectionNode? nextNode = intersectionNode.TransferCarToNextNode(this, chosenTurn);
        if (nextNode != null)
        {
            intersectionNode = nextNode;
            currentSplineTValue = 0f;

            if (chosenTurn != TurnChoice.Continue)
            {
                hasSwitchedToTurnNode = true;
            }
            else if (hasSwitchedToTurnNode)
            {
                // If we just came off a turn, turn off the turn signals
                DespawnTurnSignals();
            }
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
        col = GetComponent<Collider2D>();

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
            despawnTimer -= GameManager.Instance.deltaTimeSpeedMult;
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

        collisionCooldown = Mathf.Max(collisionCooldown - GameManager.Instance.deltaTimeSpeedMult, 0f);

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

        tireMarkTriggersThisFixedUpdate = 0; // Reset the count of tire mark triggers for this frame, we will increment it in various calls below if the effect is needed.

        // T value updates        
        if (currentSplineTValue > 1f)
        {
            HandleEndOfSpline();
        }


        DoRaycastDetection();
        UpdatePositionAlongSpline();
        UpdateRotationAlongSpline();

        if (tireMarkTriggersThisFixedUpdate == 0)
        {
            StopTireScreechEffect();
        }
        else
        {
            SpawnTireScreechEffect();
        }

    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // // If we hit a stop sign trigger, set canProceedAtStopSign to true so we can ignore the stop sign raycast in Update and proceed through the intersection.
        // if (other.gameObject.layer == ColliderTypeToLayerMasks.Map[ColliderType.IntersectionLeave])
        // {
        //     intersectionController.DequeueStopSign(this);
        // }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {

        // Don't ragdoll on pedestrian collisions, just ignore them (cars can run over pedestrians but it doesn't cause them to crash)
        if (collision.gameObject.layer == ColliderTypeToLayerMasks.Map[ColliderType.Pedestrian])
        {
            return;
        }

        rb.bodyType = RigidbodyType2D.Dynamic; // Make the car affected by physics after collision
        // IntersectionController.Instance.DequeueStopSign(this);

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

        if ((deviantType != DeviantType.none) && !hasBeenClicked && !hasCollided)
        {
            // If this car is a deviant, clicking it will "report" it and destroy it, giving the player points.
            GameManager.Instance.Score += 1;
            InstantiateParticleEffects(explosionEffectPrefab, transform.position, Quaternion.identity); // Spawn explosion effect
            DestroyAndDropParticles(true);
        }

        else if (hasCollided)
        {
            DestroyAndDropParticles();
        }
        else if (!hasBeenClicked)
        {
            // If this car is not a deviant, clicking it will penalize the player
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

    public void DestroyAndDropParticles(bool destroyedByPlayer = false)
    {
        StopExhaust();
        StopTireScreechEffect();
        // IntersectionController.Instance.DequeueStopSign(this);
        IntersectionController.Instance.activeCars.Remove(this);
        if (deviantType != DeviantType.none && !destroyedByPlayer && hasEncounteredDeviantBehavior)
        {
            // GameManager.Instance.Score -= 1; // Penalize for letting a deviant escape
            GameManager.Instance.allowedMistakes -= 1; // Lose a life for letting a deviant escape
            GameManager.Instance.DeviantBehaviorCounts[deviantType] = GameManager.Instance.DeviantBehaviorCounts.ContainsKey(deviantType) ? GameManager.Instance.DeviantBehaviorCounts[deviantType] + 1 : 1; // Increment the count of this deviant type that has escaped
        }
        Destroy(gameObject);
    }

    public void Initialize(CarSpawn carSpawn, IntersectionNode node)
    {
        this.intersectionNode = node;

        // Note: This is done BEFORE setting up the deviant behavior.
        // This is because the speeding cars will be going way too fast to stop nicely if they spawn right behind another car.
        maxSpeed += Random.Range(-speedVariance, speedVariance); // Add some random variance to the speed for visual variety
        speed = maxSpeed; // Start at max speed for a more dynamic start
        SetupDeviantBehavior(carSpawn.deviantBehavior.deviantType);
        SetupTurnIntention(carSpawn.turnChoice);
        SpawnTurnSignals();
        
        centerOffset += Random.Range(-centerVariance, centerVariance); // Add some random variance to the center offset for visual variety
    }

    void SetupTurnIntention(TurnChoice turnChoice)
    {
        // This means unspecified, so we pick a random one from what's available on the path we spawned on.
        if (turnChoice == TurnChoice.Continue)
        {
            TurnChoice[] possibleIntentions = intersectionNode.availableTurnChoicesOnPath;
            turnIntention = possibleIntentions[Random.Range(0, possibleIntentions.Length)];
        }
        else
        {
            turnIntention = turnChoice;
        }

        // Warn for invalid turn intentions that don't match the available turns on this path
        if (!intersectionNode.availableTurnChoicesOnPath.ToArray().Contains(turnIntention))
        {
            Debug.LogWarning("Car spawned with turn intention " + turnIntention + " but that turn is not available on the path it spawned on. Available turns: " + string.Join(", ", intersectionNode.availableTurnChoicesOnPath) + ". Car will not stop at direction specific stop lines.");
        }

    }

    public void SetupDeviantBehavior(DeviantType deviantSpawnType)
    {

        switch (deviantSpawnType)
        {
            case DeviantType.tailgating:
                List<ColliderStopDistanceInfo> newStopDistances = new List<ColliderStopDistanceInfo>();
                foreach (ColliderStopDistanceInfo info in stopDistancesList)
                {
                    if (info.colliderType == ColliderType.StopLine)
                    {
                        newStopDistances.Add(new ColliderStopDistanceInfo(info.colliderType, -1f)); // Stop after the line!
                    }
                    else if (info.colliderType == ColliderType.Car)
                    {
                        newStopDistances.Add(new ColliderStopDistanceInfo(info.colliderType, 0.3f)); // Stop very close to other cars
                    }
                    else
                    {
                        newStopDistances.Add(info);
                    }
                }
                stopDistancesList = newStopDistances;
                targetStopTime /= 10f; // Shorter time headway to target for more aggressive braking behavior
                break;
            case DeviantType.speeding:
                maxSpeed *= 3f; // Higher max speed for speeding behavior
                accelerationRate *= 3f; // Faster acceleration for speeding behavior
                // Also do some tailgating to make it more obvious when the cars are just lined up
                newStopDistances = new List<ColliderStopDistanceInfo>();
                foreach (ColliderStopDistanceInfo info in stopDistancesList)
                {
                    if (info.colliderType == ColliderType.Car)
                    {
                        newStopDistances.Add(new ColliderStopDistanceInfo(info.colliderType, 0.3f)); // Stop very close to other cars
                    }
                    else
                    {
                        newStopDistances.Add(info);
                    }
                }
                stopDistancesList = newStopDistances;
                targetStopTime /= 10f; // Shorter time headway to target for more aggressive braking behavior
                hasEncounteredDeviantBehavior = true; // Speeding is obvious and can be identified immediately
                break;
            case DeviantType.swerving:
                // Most of the important behavior is handled in UpdatePositionAlongSpline() where we apply a swerving offset to the car's position along the spline. 
                // Here, we just set the parameters for the swerving behavior.
                swervingCooldownTimer = Random.Range(0f, 2f); // Random initial cooldown so not all swerving cars start swerving at the same time
                swervingAmplitude = Random.Range(0.3f, 0.5f); // Random amplitude between 0.3 and 0.5
                swervingFrequency = Random.Range(0.5f, 1.5f); // Random frequency between 0.5 and 1.5
                hasEncounteredDeviantBehavior = true; // Swerving is obvious and can be identified immediately
                break;
            case DeviantType.runsStop:
                // Nothing to do here, this is handled elsewhere
                break;
            case DeviantType.ignoresPedestrians:
                // Nothing to do here, this is handled elsewhere
                break;
        }

        deviantType = deviantSpawnType;
    }

    void SpawnTurnSignals()
    {
        if (turnSignalEffectPrefab == null) return;

        List<GameObject>? spawnPoints = null;
        if (turnIntention == TurnChoice.Left)
        {
            spawnPoints = leftTurnSignalSpawnPoints;
        }
        else if (turnIntention == TurnChoice.Right)
        {
            spawnPoints = rightTurnSignalSpawnPoints;
        }

        if (spawnPoints == null || spawnPoints.Count == 0) return;

        // Implementation for spawning turn signals
        if (turnSignalInstances == null)
        {
            turnSignalInstances = new List<GameObject>();
            foreach (GameObject spawnPoint in spawnPoints)
            {
                GameObject effectInstance = InstantiateParticleEffects(turnSignalEffectPrefab, spawnPoint.transform);
                effectInstance.transform.parent = spawnPoint.transform; // Parent to the spawn point so it moves with the car
                turnSignalInstances.Add(effectInstance);
            }
        }
    }

    void DespawnTurnSignals()
    {
        if (turnSignalInstances != null)
        {
            foreach (GameObject effectInstance in turnSignalInstances)
            {
                Destroy(effectInstance);
            }
            turnSignalInstances = null;
        }
    }
}

public enum ColliderType
{
    Car,
    Pedestrian,
    StopLine,
    IntersectionLeave,
    ClickBox,
    Default
}

public static class ColliderTypeToLayerMasks
{
    // Map collider types to layer indices
    public static readonly Dictionary<ColliderType, int> Map = new Dictionary<ColliderType, int>()
    {
        { ColliderType.Car, LayerMask.NameToLayer("Cars") },
        { ColliderType.Pedestrian, LayerMask.NameToLayer("Pedestrians") },
        { ColliderType.StopLine, LayerMask.NameToLayer("Stop Lines") },
        { ColliderType.IntersectionLeave, LayerMask.NameToLayer("Intersection Leave") },
        { ColliderType.ClickBox, LayerMask.NameToLayer("Pointer Interaction") },
        { ColliderType.Default, LayerMask.NameToLayer("Default") },
    };
}

public static class LayerMasksToColliderType
{
    // Map layer indices back to collider types for easy lookup during raycast hit processing
    public static readonly Dictionary<int, ColliderType> Map = new Dictionary<int, ColliderType>()
    {
        { LayerMask.NameToLayer("Cars"), ColliderType.Car },
        { LayerMask.NameToLayer("Pedestrians"), ColliderType.Pedestrian },
        { LayerMask.NameToLayer("Stop Lines"), ColliderType.StopLine },
        { LayerMask.NameToLayer("Intersection Leave"), ColliderType.IntersectionLeave },
        { LayerMask.NameToLayer("Pointer Interaction"), ColliderType.ClickBox },
        { LayerMask.NameToLayer("Default"), ColliderType.Default },
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
    Continue,  // Also used for cars that don't have a specific turn intention and will just take any available turn
    Left,
    NoTurn,
    Right
}
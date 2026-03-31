using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Linq;
using UnityEngine.Rendering.Universal;
using Unity.VisualScripting;

#nullable enable

public class IntersectionController : MonoBehaviour
{

    public GameObject carPrefab;
    public GameObject busPrefab;
    // [SerializeField] PixelPerfectCamera pixelCamera;
    // Vector2 cameraOriginalResolution;
    // Vector2 cameraOriginalPosition;
    // int cameraOriginalPPU;


    [SerializeField] IntersectionNode northSpawnNode;
    [SerializeField] IntersectionNode northSpawnNode2; // Optional second spawn node for the north side if the intersection has multiple lanes on that side
    [SerializeField] IntersectionNode eastSpawnNode;
    [SerializeField] IntersectionNode eastSpawnNode2; // Optional second spawn node for the east side if the intersection has multiple lanes on that side
    [SerializeField] IntersectionNode southSpawnNode;
    [SerializeField] IntersectionNode southSpawnNode2; // Optional second spawn node for the south side if the intersection has multiple lanes on that side
    [SerializeField] IntersectionNode westSpawnNode;
    [SerializeField] IntersectionNode westSpawnNode2; // Optional second spawn node for the west side if the intersection has multiple lanes on that side

    [SerializeField] GameObject northCrosswalkBlocker;
    [SerializeField] GameObject eastCrosswalkBlocker;
    [SerializeField] GameObject southCrosswalkBlocker;
    [SerializeField] GameObject westCrosswalkBlocker;

    [SerializeField] GameObject PedestrianPrefab;
    [SerializeField] Transform[] PedestrianSpawnPoints;

    [SerializeField] PlayerInput playerInput;

    [SerializeField] Light2D globalDayLight;
    [SerializeField] Light2D globalMorningLight;
    [SerializeField] Light2D globalNightLight;

    public static IntersectionController Instance { get; private set; }

    public List<CarPathFollower> activeCars = new List<CarPathFollower>();

    public bool useStoplights = false;
    public TimeOfDay timeOfDay = TimeOfDay.Day;

    public bool useTimer = true;  // Some rounds just run until the the spawn order is complete and all cars have cleared the intersection, so we don't want to use the timer for those rounds since it would be redundant and could cause confusion.
    // Note: This also means that rounds without a spawn order and a timer will be infinite. This is intentional.

    List<StopSignQueueEntry> stopSignQueue = new List<StopSignQueueEntry>();

    [SerializeField] float stopSignWaitTime; // Time to wait at a stop sign before allowing the next car to go
    float stopSignTimer = 0f;

    [SerializeField] List<StoplightPhase> stoplightPhases;
    [SerializeField] List<StoplightPhaseLight> stoplightPhaseLights;
    [SerializeField] Sprite greenLightSprite;
    [SerializeField] Sprite yellowLightSprite;
    [SerializeField] Sprite redLightSprite;

    float stoplightPhaseDuration = 15f; // Time to wait before switching to the next stoplight phase
    float stoplightPhaseTimer = 0f;
    int currentStoplightPhase = 0;

    InputAction resetAction;


    public CarSpawn[]? carSpawns;
    public PedSpawn[]? pedSpawns;
    int carSpawnIndex = 0;
    int pedSpawnIndex = 0;

    Action roundEndCallback = () => { GameManager.Instance.EndRound(false); }; // Called when the round end animation is complete to show the round end screen.
    bool roundEnded = false;
    float roundEndAnimationDuration = 3f; // Duration of the game over animation (e.g. zooming in on the crash site)
    float roundEndAnimationTimer = 0f;
    public Vector2 gameOverPosition; // Position to focus the camera on when the game is over


    public List<GameObject> activeEffects = new List<GameObject>();

    [Header("Spawning")]
    [SerializeField] float spawnInterval; // seconds between spawn attempts
    float spawnTimer = float.PositiveInfinity;
    [SerializeField] float pedestrianSpawnInterval = 5f; // seconds between pedestrian spawn attempts
    float pedestrianSpawnTimer = 0f;
    bool pedestriansEnabled = false;
    DeviantType[] possibleDeviantBehaviors;
    [SerializeField] int maxActiveCars;
    int activeCarCount = 0;
    [SerializeField, Range(0f, 1f)] float deviantProbability;

    public void EnqueueStopSign(IntersectionStopLine stopLine, CarPathFollower car)
    {

        if (useStoplights) return;

        StopSignQueueEntry entry = new StopSignQueueEntry(stopLine, car);

        // Don't add the car to the queue if is already there!
        if (!stopSignQueue.Contains(entry))
        {
            stopSignQueue.Add(entry);
        }
    }

    void AllowThroughStopSign(CarPathFollower car)
    {
        StopSignQueueEntry entry = stopSignQueue.Find(e => e.car == car);
        if (stopSignQueue.Contains(entry))
        {
            stopSignQueue.Remove(entry);
            entry.stopLine.carsAllowedThrough.Add(car);
            car.committedStopLines.Add(entry.stopLine);
            // Reset the timer when a car leaves the stop sign, so the next car has to wait the full time
            stopSignTimer = 0f;
        }
    }

    private void Awake()
    {
        Instance = this;
        resetAction = playerInput.actions["Reset"];
        // gameOverTimer = gameOverDelay; // Initialize game over timer
        // cameraOriginalResolution = new Vector2(pixelCamera.refResolutionX, pixelCamera.refResolutionY);
        // cameraOriginalPPU = pixelCamera.assetsPPU;
        // cameraOriginalPosition = pixelCamera.transform.position;

    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        switch (timeOfDay)
        {
            case TimeOfDay.Morning:
                globalMorningLight.enabled = true;
                globalDayLight.enabled = false;
                globalNightLight.enabled = false;
                break;
            case TimeOfDay.Day:
                globalMorningLight.enabled = false;
                globalDayLight.enabled = true;
                globalNightLight.enabled = false;
                break;
            case TimeOfDay.Night:
                globalMorningLight.enabled = false;
                globalDayLight.enabled = false;
                globalNightLight.enabled = true;
                break;
        }
    }

    // Update is called once per frame
    void Update()
    {

        if (GameManager.Instance.allowedMistakes < 0)
        {
            if (!roundEnded)
            {
                EndRound(false, false); // End the round with a loss and don't play the animation
            }
        }

        if (roundEnded)
        {
            roundEndAnimationTimer += Time.deltaTime;
            if (roundEndAnimationTimer >= roundEndAnimationDuration)
            {
                roundEndCallback.Invoke(); // Call the assigned callback. This will store the status of the round.
            }

            GameManager.Instance.gameSpeedMultiplier = 1f - (roundEndAnimationTimer / roundEndAnimationDuration); // Gradually slow down time


            // Do a vignette effect
            


            // // Scale Resolution
            // pixelCamera.refResolutionX = Mathf.RoundToInt(cameraOriginalResolution.x * gameSpeedMultiplier);
            // pixelCamera.refResolutionY = Mathf.RoundToInt(cameraOriginalResolution.y * gameSpeedMultiplier);

            // // Scale PPU by the SAME gameSpeedMultiplier to maintain relative scale
            // pixelCamera.assetsPPU = Mathf.RoundToInt(cameraOriginalPPU * gameSpeedMultiplier);

            // float zoomPower = 4.0f;
            // float currentZoom = Mathf.Lerp(1f, zoomPower, 1f - gameSpeedMultiplier);

            // // 2. Move the camera toward the spot
            // pixelCamera.transform.position = Vector3.Lerp(cameraOriginalPosition, gameOverPosition, 1f - (gameSpeedMultiplier/10));

            // // 3. Apply the zoom (Note: This is usually a public property 'pixelPerfectZoom')
            // pixelCamera.assetsPPU = (int)(cameraOriginalPPU * currentZoom);

        }

        UpdateParticleSpeeds();

        // If the game timer has run out, end the round
        if (useTimer && GameManager.Instance.gameTimer >= GameManager.Instance.gameDuration) 
        {
            // Game over, show round end scene
            EndRound(true);
            return;
        }

        // If we aren't using the timer, check the spawn order and car count for round end conditions instead
        // Note: If carSpawns is null or empty, then the round will never end without a timer. This is intentional, so no case handling is needed for that scenario.
        if (!useTimer && carSpawns != null && carSpawns.Length > 0 && carSpawnIndex >= carSpawns.Length && activeCarCount == 0)
        {
            // Game over, show round end scene
            EndRound(true);
            return;
        }

        // Spawn cars periodically
        spawnTimer += GameManager.Instance.deltaTimeSpeedMult;
        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f;
            DoNextCarSpawn();
        }

        if (pedestriansEnabled)
        {
            pedestrianSpawnTimer += GameManager.Instance.deltaTimeSpeedMult;
            if (pedestrianSpawnTimer >= pedestrianSpawnInterval)
            {
                pedestrianSpawnTimer = 0f;
                DoNextPedSpawn();
            }
        }
        

        // Update stop sign timer
        if (stopSignQueue.Count > 0 && !useStoplights) // Only update the stop sign timer if there are cars waiting at the stop sign and we're not using stoplights (since stoplights will handle timing separately)
        {
            stopSignTimer += GameManager.Instance.deltaTimeSpeedMult;
            if (stopSignTimer >= stopSignWaitTime)
            {
                // Allow the first car in the queue that can go to proceed, and remove it from the queue
                // If the car in the front of the queue can't go, skip it and try the next, and so on.
                for (int i = 0; i < stopSignQueue.Count; i++)
                {
                    if (
                        stopSignQueue[i].stopLine.AreMovementBlockingCollidersClearOfCars(stopSignQueue[i].car.col) &&
                        (
                        stopSignQueue[i].car.deviantType == DeviantType.ignoresPedestrians ||
                        stopSignQueue[i].stopLine.AreMovementBlockingCollidersClearOfPeds(stopSignQueue[i].car.col)
                        )
                    )
                    {
                        AllowThroughStopSign(stopSignQueue[i].car);
                        stopSignTimer = 0f; // Reset timer after allowing a car through
                        break;
                    }
                }
            }
        }

        if (useStoplights)
        {
            stoplightPhaseTimer += GameManager.Instance.deltaTimeSpeedMult;
            if (stoplightPhaseTimer >= stoplightPhaseDuration)
            {
                // Cache the lights in the previous phase so we can turn them all red before turning the next phase green
                StoplightPhase previousPhaseStopLines = stoplightPhases[currentStoplightPhase];
                StoplightPhaseLight previousPhaseLights = stoplightPhaseLights[currentStoplightPhase];
                
                // Turn all lights red before changing the phase
                // Move to the next stoplight phase
                currentStoplightPhase = (currentStoplightPhase + 1) % stoplightPhases.Count;
                StoplightPhase activeStopLines = stoplightPhases[currentStoplightPhase];
                StoplightPhaseLight activePhaseLights = stoplightPhaseLights[currentStoplightPhase];
                foreach (var stopLine in previousPhaseStopLines.stopLines)
                {
                    stopLine.currentStoplightColor = StoplightColor.Red;
                }
                foreach (var light in previousPhaseLights.stopLights)
                {
                    light.GetComponent<SpriteRenderer>().sprite = redLightSprite;
                }

                foreach (var stopLine in activeStopLines.stopLines)
                {
                    stopLine.currentStoplightColor = StoplightColor.Green;
                }
                foreach (var light in activePhaseLights.stopLights)
                {
                    light.GetComponent<SpriteRenderer>().sprite = greenLightSprite;
                }

                stoplightPhaseTimer = 0f; // Reset timer after changing phases
            }
        }


        bool reset = resetAction.ReadValue<float>() > 0;
        resetAction.Reset(); // Reset the action so it doesn't keep returning true until the button is released

        // Spawn cars at the start nodes when the reset button is pressed
        if (reset)
        {
            DoNextCarSpawn();

        }
    }

    void UpdateParticleSpeeds()
    {

        foreach (GameObject effect in activeEffects)
        {
            // If particle has been destroyed, skip it
            if (effect == null) continue;
            ParticleSystem.MainModule mainModule = effect.GetComponent<ParticleSystem>().main;
            mainModule.simulationSpeed = GameManager.Instance.gameSpeedMultiplier;
        }

        activeEffects.RemoveAll(effect => effect == null); // Remove destroyed effects from the list
    }

    // Called by cars when they are destroyed so the spawner can keep track of active cars
    public void NotifyCarDestroyed()
    {
        activeCarCount = Mathf.Max(0, activeCarCount - 1);
    }

    SpawnResult SpawnCar(CarSpawn spawn)
    {
        IntersectionNode? spawnNode = GetSpawnNode(spawn.spawnLocation);
        if (spawnNode == null) return SpawnResult.invalid; // If there isn't a valid spawn node for the specified location, return invalid to indicate the spawn failed

        // If the deviant behavior type is random, randomly select a deviant behavior from the possible behaviors defined for this round
        else if (spawn.deviantBehavior.deviantType == DeviantType.random)
        {
            DeviantType[] possibleDeviants = possibleDeviantBehaviors.Length > 0 ? possibleDeviantBehaviors : new DeviantType[] { DeviantType.none }; // If no possible deviant behaviors are defined, default to none to prevent errors
            if (possibleDeviants.Length > 0)
            {
                spawn.deviantBehavior.deviantType = possibleDeviants[UnityEngine.Random.Range(0, possibleDeviants.Length)];
            }
            else
            {
                spawn.deviantBehavior.deviantType = DeviantType.none; // If no possible deviant behaviors are defined, set to none
            }
        }

        // If the type is unspecified, use the probability to determine if the car should be deviant.
        // If it should be, then pass through the set behavior. If not, set the behavior to none.
        if (!spawn.deviantBehavior.isSpecified)
        {
            if (UnityEngine.Random.Range(0f, 1f) > deviantProbability)
            {
                spawn.deviantBehavior.deviantType = DeviantType.none;
            }
        }

        return spawnNode.SpawnCarIfNodeEmpty(spawn) ? SpawnResult.success : SpawnResult.blocked; // If the spawn node is occupied, return blocked to indicate the spawn failed. Otherwise, spawn the car and return success
    }

    void DoNextCarSpawn()
    {
        if (carSpawns == null || carSpawns.Length == 0)
        {
            SpawnCar(CarSpawn.Blank); // If no spawn order is defined, just spawn a car at a random start node
            return;
        }

        // If a spawn order is defined, spawn cars according to the order
        if (carSpawnIndex < carSpawns.Length)
        {
            CarSpawn spawn = carSpawns[carSpawnIndex];
            SpawnResult result = SpawnCar(spawn);
            if (result == SpawnResult.success)
            {
                activeCarCount++;
                carSpawnIndex++;
            }
            else if (result == SpawnResult.invalid)
            {
                Debug.LogWarning("Invalid spawn location specified for car spawn at index " + carSpawnIndex);
                carSpawnIndex++; // Skip this spawn and move on to the next one
                DoNextCarSpawn(); // Attempt to spawn the next car immediately since this one was invalid
            }
        }
    }

    void DoNextPedSpawn()
    {

        PedSpawn? spawn = null;

        if (PedestrianSpawnPoints.Length == 1)
        {
            Debug.LogWarning("Only 1 pedestrian spawn point defined!");
            return;
        }

        if (pedSpawns == null || pedSpawns.Length == 0)
        {
            Transform spawnPoint = PedestrianSpawnPoints[UnityEngine.Random.Range(0, PedestrianSpawnPoints.Length)]; // If no spawn order is defined, just spawn a pedestrian at a random spawn point
            Transform targetPoint = PedestrianSpawnPoints[UnityEngine.Random.Range(0, PedestrianSpawnPoints.Length)];
            while (targetPoint == spawnPoint) // Ensure target point is different from spawn point
            {
                targetPoint = PedestrianSpawnPoints[UnityEngine.Random.Range(0, PedestrianSpawnPoints.Length)];
            }
            spawn = new PedSpawn(spawnPoint, targetPoint);
        }

        // If a spawn order is defined, spawn cars according to the order
        else if (pedSpawnIndex >= pedSpawns.Length)
        {
            return; // If we've gone through the whole spawn order, stop spawning pedestrians
        }
        else if (pedSpawnIndex < pedSpawns.Length)
        {
            spawn = pedSpawns[pedSpawnIndex];
            pedSpawnIndex++;
            // Log an error and continue if only one of the locations is null
            if (spawn.Value.spawnLocation == null ^ spawn.Value.targetLocation == null)
            {
                Debug.LogError("Invalid pedestrian spawn: spawn location or target location is null.");
                DoNextPedSpawn(); // Attempt to spawn the next pedestrian immediately since this one was invalid
                return;
            }
            // If the locations aren't defined, but we don't have enough points to pick from, log an error and continue
            else if (spawn.Value.spawnLocation == null && spawn.Value.targetLocation == null && PedestrianSpawnPoints.Length < 2)
            {
                Debug.LogError("Not enough pedestrian spawn points defined to randomly assign spawn and target locations.");
                DoNextPedSpawn(); // Attempt to spawn the next pedestrian immediately since this one was invalid
                return;
            }
            // If both are null, then pick locations randomly
            else if (spawn.Value.spawnLocation == null && spawn.Value.targetLocation == null)
            {
                Transform spawnPoint = PedestrianSpawnPoints[UnityEngine.Random.Range(0, PedestrianSpawnPoints.Length)];
                Transform targetPoint = PedestrianSpawnPoints[UnityEngine.Random.Range(0, PedestrianSpawnPoints.Length)];
                while (targetPoint == spawnPoint) // Ensure target point is different from spawn point
                {
                    targetPoint = PedestrianSpawnPoints[UnityEngine.Random.Range(0, PedestrianSpawnPoints.Length)];
                }
                spawn = new PedSpawn(spawnPoint, targetPoint);
            }

        }

        if (spawn == null)
        {
            Debug.LogWarning("Pedestrian spawn point is null!");
            return;
        }


        GameObject ped = Instantiate(PedestrianPrefab, spawn.Value.spawnLocation.position, Quaternion.identity);
        ped.GetComponent<PedestrianNavAgent>().Initialize(spawn.Value.targetLocation);

    }

    IntersectionNode? GetSpawnNode(SpawnLocation location)
    {
        // Note: even though the spawn nodes aren't marked nullable,
        // they might not be assigned. This means that the current intersection setup doesn't have that spawn location,
        // so we return null. We can still return the same node though, because it will just be null in that case and the spawn function will handle it appropriately.
        switch (location)
        {
            case SpawnLocation.north:
                return northSpawnNode;
            case SpawnLocation.north2:
                return northSpawnNode2;
            case SpawnLocation.east:
                return eastSpawnNode;
            case SpawnLocation.east2:
                return eastSpawnNode2;
            case SpawnLocation.south:
                return southSpawnNode;
            case SpawnLocation.south2:
                return southSpawnNode2;
            case SpawnLocation.west:
                return westSpawnNode;
            case SpawnLocation.west2:
                return westSpawnNode2;
            // If no spawn location is specified, pick a random one
            case SpawnLocation.random:
                IntersectionNode[] startNodes = GetAllSpawnNodes();
                int idx = UnityEngine.Random.Range(0, startNodes.Length);
                return startNodes[idx];
            default:
                throw new ArgumentException("Invalid spawn location: " + location);
        }
    }

    IntersectionNode[] GetAllSpawnNodes()
    {
        List<IntersectionNode> returnList = new List<IntersectionNode> { northSpawnNode, northSpawnNode2, eastSpawnNode, eastSpawnNode2, southSpawnNode, southSpawnNode2, westSpawnNode, westSpawnNode2 };
        returnList.RemoveAll(node => node == null);
        returnList.RemoveAll(node => node.gameObject.activeSelf == false); // Remove any nodes that are inactive, since they shouldn't be used for spawning
        returnList.RemoveAll(node => node.gameObject.activeInHierarchy == false); // Remove any nodes that are inactive, since they shouldn't be used for spawning
        return returnList.ToArray(); // Remove any null nodes from the list
    }
    
    public void Initialize(RoundConfig roundConfig)
    {
        this.carSpawns = roundConfig.spawnOrder;
        this.pedSpawns = roundConfig.pedSpawnOrder;
        this.pedestrianSpawnInterval = roundConfig.pedSpawnDelay;
        this.pedestriansEnabled = roundConfig.pedestriansEnabled;
        this.deviantProbability = roundConfig.deviantSpawnChance;
        this.possibleDeviantBehaviors = roundConfig.possibleDeviantBehaviors;
        this.spawnInterval = roundConfig.spawnDelay;
        this.useTimer = roundConfig.useTimer;
        this.timeOfDay = roundConfig.timeOfDay;
    }

    public void EndRound(bool success, bool playAnimation = false)
    {

        if (roundEnded) return; // If the round has already ended, don't do anything

        if (playAnimation) 
        {
            roundEndAnimationTimer = 0f;
        }
        else
        {
            roundEndAnimationTimer = roundEndAnimationDuration; // Skip the animation and go straight to the end screen
        }

        roundEnded = true;
        roundEndCallback = () => { GameManager.Instance.EndRound(success); }; // Set the callback to end the round with the appropriate success value when the animation is done

    }

}

public enum SpawnResult
{
    success,
    blocked,
    invalid
}

public struct StopSignQueueEntry
{
    public IntersectionStopLine stopLine;
    public CarPathFollower car;

    public StopSignQueueEntry(IntersectionStopLine stopLine, CarPathFollower car)
    {
        this.stopLine = stopLine;
        this.car = car;
    }
}

[Serializable]
public struct StoplightPhase
{
    public List<IntersectionStopLine> stopLines;

    public StoplightPhase(List<IntersectionStopLine> stopLines)
    {
        this.stopLines = stopLines;
    }
}

[Serializable]
public struct StoplightPhaseLight
{
    public List<GameObject> stopLights;

    public StoplightPhaseLight(List<GameObject> stopLights)
    {
        this.stopLights = stopLights;
    }
}

[Serializable]
public enum TimeOfDay
{
    Morning,
    Day,
    Night
}
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

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
    [SerializeField] IntersectionNode eastSpawnNode;
    [SerializeField] IntersectionNode southSpawnNode;
    [SerializeField] IntersectionNode westSpawnNode;

    [SerializeField] GameObject northCrosswalkBlocker;
    [SerializeField] GameObject eastCrosswalkBlocker;
    [SerializeField] GameObject southCrosswalkBlocker;
    [SerializeField] GameObject westCrosswalkBlocker;

    [SerializeField] GameObject PedestrianPrefab;
    [SerializeField] Transform[] PedestrianSpawnPoints;

    [SerializeField] PlayerInput playerInput;

    public static IntersectionController Instance { get; private set; }

    public List<CarPathFollower> activeCars = new List<CarPathFollower>();

    List<StopSignQueueEntry> stopSignQueue = new List<StopSignQueueEntry>();
    List<IntersectionMovementBlockingCollider> stopSigns = new List<IntersectionMovementBlockingCollider>();

    [SerializeField] float stopSignWaitTime; // Time to wait at a stop sign before allowing the next car to go
    float stopSignTimer = 0f;

    InputAction resetAction;


    public CarSpawn[]? carSpawns;
    public PedSpawn[]? pedSpawns;
    int carSpawnIndex = 0;
    int pedSpawnIndex = 0;

    float gameOverDelay = 3f; // Time to wait after the game is over before showing the round end screen
    float gameOverTimer;
    public bool gameOver = false;
    public Vector2 gameOverPosition; // Position to focus the camera on when the game is over

    public List<GameObject> activeEffects = new List<GameObject>();

    [Header("Spawning")]
    [SerializeField] float spawnInterval; // seconds between spawn attempts
    float spawnTimer = 0f;
    [SerializeField]float pedestrianSpawnInterval = 5f; // seconds between pedestrian spawn attempts
    float pedestrianSpawnTimer = 0f;
    bool pedestriansEnabled = false;
    [SerializeField] int maxActiveCars;
    int activeCarCount = 0;
    [SerializeField, Range(0f, 1f)] float deviantProbability;

    public void EnqueueStopSign(IntersectionStopLine stopLine, CarPathFollower car)
    {
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
        gameOverTimer = gameOverDelay; // Initialize game over timer
        // cameraOriginalResolution = new Vector2(pixelCamera.refResolutionX, pixelCamera.refResolutionY);
        // cameraOriginalPPU = pixelCamera.assetsPPU;
        // cameraOriginalPosition = pixelCamera.transform.position;

    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // This is for debugging
        // If there isn't a GameManager in the scene, create one and initialize it with default values
        // if (GameManager.Instance == null)
        // {
        //     GameObject gameManagerObj = new GameObject("GameManager");
        //     gameManagerObj.AddComponent<GameManager>();
        // }

        stopSignWaitTime -= 0.1f * GameManager.Instance.roundNumber; // Decrease stop sign wait time each round to increase difficulty
        stopSignWaitTime = Mathf.Max(0f, stopSignWaitTime); // Set a minimum stop sign wait time to prevent it from becoming negative
    }

    // Update is called once per frame
    void Update()
    {

        if (GameManager.Instance.allowedMistakes < 0)
        {
            gameOver = true;
        }

        if (gameOver)
        {
            gameOverTimer -= Time.deltaTime;
            if (gameOverTimer <= 0f) // Wait for 3 seconds before showing round end screen
            {
                GameManager.Instance.EndRound(false);
            }

            GameManager.Instance.gameSpeedMultiplier = 1f - (gameOverDelay - gameOverTimer) / gameOverDelay; // Gradually slow down time over 3 seconds


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

        if (GameManager.Instance.gameTimer >= GameManager.Instance.gameDuration && activeCars.Count == 0 && GameManager.Instance.allowedMistakes >= 0) // If the game timer has run out and there are no more cars on the screen, end the round with a win
        {
            // Game over, show round end scene
            GameManager.Instance.EndRound(true);
            return;
        }
        // Spawn cars periodically while the game timer is running
        if (GameManager.Instance.gameTimer < GameManager.Instance.gameDuration)
        {
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
        }

        // Update stop sign timer
        if (stopSignQueue.Count > 0)
        {
            stopSignTimer += GameManager.Instance.deltaTimeSpeedMult;
            if (stopSignTimer >= stopSignWaitTime)
            {
                // Allow the first car in the queue that can go to proceed, and remove it from the queue
                // If the car in the front of the queue can't go, skip it and try the next, and so on.
                for (int i = 0; i < stopSignQueue.Count; i++)
                {
                    if (stopSignQueue[i].stopLine.AreMovementBlockingCollidersClear(stopSignQueue[i].car.col))
                    {
                        AllowThroughStopSign(stopSignQueue[i].car);
                        stopSignQueue.RemoveAt(i); // Note: Modifying the list while iterating is usually a bad idea, but in this case we break immediately after, so it won't cause any issues
                        stopSignTimer = 0f; // Reset timer after allowing a car through
                        break;
                    }
                }
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
            case SpawnLocation.east:
                return eastSpawnNode;
            case SpawnLocation.south:
                return southSpawnNode;
            case SpawnLocation.west:
                return westSpawnNode;
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
        List<IntersectionNode> returnList = new List<IntersectionNode> { northSpawnNode, eastSpawnNode, southSpawnNode, westSpawnNode };
        returnList.RemoveAll(node => node == null);
        return returnList.ToArray(); // Remove any null nodes from the list
    }
    public void Initialize(RoundConfig roundConfig)
    {
        this.carSpawns = roundConfig.spawnOrder;
        this.pedSpawns = roundConfig.pedSpawnOrder;
        this.pedestrianSpawnInterval = roundConfig.pedSpawnDelay;
        this.pedestriansEnabled = roundConfig.pedestriansEnabled;
        this.deviantProbability = roundConfig.deviantSpawnChance;
        this.spawnInterval = roundConfig.spawnDelay;
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
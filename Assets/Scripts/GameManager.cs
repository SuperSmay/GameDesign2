using System;

using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{

    public static GameManager Instance { get; private set; }

    public int Score = 0;

    public bool roundSuccessful = false;

    // Current round info
    public int roundNumber = 1;
    public int allowedMistakes = 3;
    public float gameTimer = 0f;
    public float gameDuration = 60f; // Duration of the game in seconds
    public float gameSpeedMultiplier = 1f;
    public bool paused = false;
    public float fixedDeltaTimeSpeedMult { get { return gameSpeedMultiplier * (paused ? 0f : Time.fixedDeltaTime); } }
    public float deltaTimeSpeedMult { get { return gameSpeedMultiplier * (paused ? 0f : Time.deltaTime); } }

    public RoundConfig[] rounds;

    [SerializeField] String intersectionControllerFourWayStopSceneName;
    [SerializeField] String intersectionControllerFourWayStoplightSceneName;
    [SerializeField] String intersectionControllerTIntersectionSceneName;
    [SerializeField] String intersectionControllerHighwayIntersectionSceneName;
    [SerializeField] GameObject uiControllerPrefab;

    IntersectionController currentIntersectionController;
    UIController currentUIController;

    void Update()
    {

        if (currentUIController == null) return; // Don't update the timer if the UIController hasn't been initialized yet (pre-round)

        if (currentUIController.showingPreamble)
        {
            paused = true;
        }
        gameTimer += deltaTimeSpeedMult;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // Ensure only one instance of GameManager exists
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Persist across scene loads
    }

    void OnEnable()
    {
        // Subscribe to the sceneLoaded event with your custom callback method
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // This method is called when the behaviour is disabled or inactive
    void OnDisable()
    {
        // Unsubscribe from the event to prevent memory leaks
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Custom callback method
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log("Scene " + scene.name + " loaded with mode: " + mode);

        // Find the IntersectionController in the loaded scene and initialize it
        currentIntersectionController = FindFirstObjectByType<IntersectionController>();
        if (currentIntersectionController != null)
        {
            Debug.Log("Found IntersectionController in scene: " + scene.name);
            StartRound();
        } 

        // You can add specific logic here, for example, checking the scene name
        if (scene.name == "MainScene")
        {
            // Perform actions specific to "MainScene"
            Debug.Log("Main scene loaded.");
            LoadNextRoundScene();
        }
    }

    public void RoundEndConfirmClicked()
    {
        // Start the game switching scene
        Score = 0; // Reset score
        roundNumber += roundSuccessful ? 1 : 0; // Increment round number
        roundSuccessful = false; // Reset round success for the next round
        SceneManager.LoadScene("MainScene");
    }

    public void LoadNextRoundScene()
    {
        RoundConfig roundConfig = rounds[roundNumber - 1];
        switch (roundConfig.intersectionLayout)
        {
            case IntersectionLayout.fourWayStop:
                // Load scene additively so we don't lose the GameManager or UIController
                SceneManager.LoadScene(intersectionControllerFourWayStopSceneName, LoadSceneMode.Additive);
                break;
            case IntersectionLayout.fourWayStoplight:
                SceneManager.LoadScene(intersectionControllerFourWayStoplightSceneName, LoadSceneMode.Additive);
                break;
            case IntersectionLayout.tIntersection:
                SceneManager.LoadScene(intersectionControllerTIntersectionSceneName, LoadSceneMode.Additive);
                break;
            case IntersectionLayout.highway:
                SceneManager.LoadScene(intersectionControllerHighwayIntersectionSceneName, LoadSceneMode.Additive);
                break;
        }
    }

    public void StartRound()
    {
        RoundConfig roundConfig = rounds[roundNumber - 1];

        gameTimer = 0f;
        gameSpeedMultiplier = 1f;
        gameDuration = roundConfig.timer;
        allowedMistakes = roundConfig.allowedMistakes;

        currentIntersectionController.Initialize(roundConfig);
        currentUIController = Instantiate(uiControllerPrefab).GetComponent<UIController>();
        currentUIController.Initialize(roundConfig);
    }

    public void EndRound(bool success)
    {
        if (currentUIController.showingEndMessage) return; // Don't allow ending the round multiple times
        // TODO refine round end
        roundSuccessful = success;
        paused = true;
        currentUIController.ShowNextEndMessage();
    }

    public void CloseRound()
    {
        SceneManager.LoadScene("RoundEndScene");
    }

}

[Serializable]
public struct RoundConfig
{
    public float timer;
    public float spawnDelay;
    public float pedSpawnDelay;
    public IntersectionLayout intersectionLayout;
    public CarSpawn[] spawnOrder;
    public PedSpawn[] pedSpawnOrder;
    public float deviantSpawnChance;
    public bool isTutorial;
    public bool pedestriansEnabled;
    public string[] preambles;
    public string[] failureMessages;
    public string[] successMessages;
    public int allowedMistakes;

}

[Serializable]
public struct CarSpawn
{
    public SpawnLocation spawnLocation;
    public TurnChoice turnChoice;
    public DeviantBehaviorInfo deviantBehavior;
    public VehicleType vehicleType;

    public CarSpawn(SpawnLocation spawnLocation, TurnChoice turnChoice, DeviantBehaviorInfo deviantBehavior, VehicleType vehicleType)
    {
        this.spawnLocation = spawnLocation;
        this.turnChoice = turnChoice;
        this.deviantBehavior = deviantBehavior;
        this.vehicleType = vehicleType;  // TODO make this do something
    }

    static public CarSpawn Blank
    {
        get
        {
            return new CarSpawn(SpawnLocation.random, TurnChoice.Continue, new DeviantBehaviorInfo { deviantType = DeviantType.random, isSpecified = false }, VehicleType.random);
        }
    }
}

[Serializable]
public struct PedSpawn
{
    public Transform spawnLocation;
    public Transform targetLocation;

    public PedSpawn(Transform spawnLocation, Transform targetLocation)
    {
        this.spawnLocation = spawnLocation;
        this.targetLocation = targetLocation;
    }
}

public enum VehicleType
{
    car,
    bus,
    random
}

public enum SpawnLocation
{
    north,
    east,
    south,
    west,
    random
}

public enum IntersectionLayout
{
    fourWayStop,
    fourWayStoplight,
    tIntersection,
    highway
}

[Serializable]
public struct DeviantBehaviorInfo
{
    public DeviantType deviantType;
    public bool isSpecified; // Whether the deviant type was explicitly specified or if it should be randomly determined based on deviantSpawnChance
}

public enum DeviantType
{
    runsStop,
    illegalTurn,
    tailgating,
    speeding,
    swerving,
    random,
    none
}
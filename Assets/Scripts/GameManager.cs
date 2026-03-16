using System;

using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{

    public static GameManager Instance { get; private set; }

    public int Score = 0;
    public int roundNumber = 1;
    public bool roundSuccessful = false;

    public RoundConfig[] rounds;

    [SerializeField] GameObject intersectionControllerFourWayStopPrefab;
    [SerializeField] GameObject intersectionControllerFourWayStoplightPrefab;
    [SerializeField] GameObject intersectionControllerTIntersectionPrefab;
    [SerializeField] GameObject intersectionControllerHighwayIntersectionPrefab;
    [SerializeField] GameObject uiControllerPrefab;

    IntersectionController currentIntersectionController;
    // UIController currentUIController;



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

        // You can add specific logic here, for example, checking the scene name
        if (scene.name == "MainScene")
        {
            // Perform actions specific to "MainScene"
            Debug.Log("Main scene loaded.");
            StartRound(); // Start the round when the main scene is loaded
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

    public void StartRound()
    {
        RoundConfig roundConfig = rounds[roundNumber - 1];
        switch (roundConfig.intersectionLayout)
        {
            case IntersectionLayout.fourWayStop:
                currentIntersectionController = Instantiate(intersectionControllerFourWayStopPrefab).GetComponent<IntersectionController>();
                break;
            case IntersectionLayout.fourWayStoplight:
                currentIntersectionController = Instantiate(intersectionControllerFourWayStoplightPrefab).GetComponent<IntersectionController>();
                break;
            case IntersectionLayout.tIntersection:
                currentIntersectionController = Instantiate(intersectionControllerTIntersectionPrefab).GetComponent<IntersectionController>();
                break;
            case IntersectionLayout.highway:
                currentIntersectionController = Instantiate(intersectionControllerHighwayIntersectionPrefab).GetComponent<IntersectionController>();
                break;
        }
        currentIntersectionController.Initialize(roundConfig);
        // currentUIController = Instantiate(uiControllerPrefab).GetComponent<UIController>();
        // currentUIController.Initialize(roundConfig);
    }

}

[Serializable]
public struct RoundConfig
{
    public float timer;
    public float spawnDelay;
    public IntersectionLayout intersectionLayout;
    public CarSpawn[] spawnOrder;
    public float deviantSpawnChance;
    public bool isTutorial;
    public bool pedestriansEnabled;
    public string[] preambles;
    public string[] failureMessages;
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
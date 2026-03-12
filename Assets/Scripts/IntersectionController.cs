using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class IntersectionController : MonoBehaviour
{

    public GameObject carPrefab;

    [SerializeField] IntersectionNode[] startNodes;

    [SerializeField] PlayerInput playerInput;

    public static IntersectionController Instance { get; private set; }

    public List<CarPathFollower> activeCars = new List<CarPathFollower>();

    List<CarPathFollower> stopSignQueue = new List<CarPathFollower>();
    [SerializeField] float stopSignWaitTime; // Time to wait at a stop sign before allowing the next car to go
    float stopSignTimer = 0f;

    InputAction resetAction;

    [SerializeField] TMPro.TextMeshProUGUI scoreTextComponent;
    [SerializeField] TMPro.TextMeshProUGUI timerTextComponent;

    [SerializeField] float gameDuration = 60f; // Duration of the game in seconds
    float gameTimer = 0f;

    [Header("Spawning")]
    [SerializeField] float spawnInterval; // seconds between spawn attempts
    float spawnTimer = 0f;
    [SerializeField] int maxActiveCars;
    int activeCarCount = 0;
    [SerializeField, Range(0f, 1f)] float deviantProbability;

    public void EnqueueStopSign(CarPathFollower car)
    {
        if (!stopSignQueue.Contains(car))
        {
            stopSignQueue.Add(car);
        }
    }

    public void DequeueStopSign(CarPathFollower car)
    {
        if (stopSignQueue.Contains(car))
        {
            stopSignQueue.Remove(car);
            // Reset the timer when a car leaves the stop sign, so the next car has to wait the full time
            stopSignTimer = 0f;
        }
    }

    private void Awake()
    {
        Instance = this;
        resetAction = playerInput.actions["Reset"];

    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // This is for debugging
        // If there isn't a GameManager in the scene, create one and initialize it with default values
        if (GameManager.Instance == null)
        {
            GameObject gameManagerObj = new GameObject("GameManager");
            gameManagerObj.AddComponent<GameManager>();
        }

        scoreTextComponent.text = "Score: 0";
        timerTextComponent.text = "Time: " + gameDuration.ToString("F1");

        stopSignWaitTime -= 0.1f * GameManager.Instance.roundNumber; // Decrease stop sign wait time each round to increase difficulty
        stopSignWaitTime = Mathf.Max(0f, stopSignWaitTime); // Set a minimum stop sign wait time to prevent it from becoming negative
    }

    // Update is called once per frame
    void Update()
    {
        gameTimer += Time.deltaTime;
        timerTextComponent.text = "Time: " + Mathf.Max(0, gameDuration - gameTimer).ToString("F1");

        scoreTextComponent.text = "Score: " + GameManager.Instance.Score;

        if (gameTimer >= gameDuration && activeCars.Count == 0)
        {
            // Game over, show round end scene
            UnityEngine.SceneManagement.SceneManager.LoadScene("RoundEndScene");
            return;
        }
        // Spawn cars periodically while the game timer is running
        if (gameTimer < gameDuration && startNodes != null && startNodes.Length > 0)
        {
            // spawnTimer += Time.deltaTime;
            if (spawnTimer >= spawnInterval)
            {
                spawnTimer = 0f;
                if (activeCarCount < maxActiveCars)
                {
                    // pick a random start node and spawn a car
                    int idx = UnityEngine.Random.Range(0, startNodes.Length);
                    bool deviant = UnityEngine.Random.value < deviantProbability;
                    startNodes[idx].SpawnCarIfNodeEmpty(deviant);
                    activeCarCount++;
                }
            }
        }

        // Update stop sign timer
        if (stopSignQueue.Count > 0)
        {
            stopSignTimer += Time.deltaTime;
            if (stopSignTimer >= stopSignWaitTime)
            {
                // Allow the first car in the queue to go
                CarPathFollower carToGo = stopSignQueue[0];
                carToGo.canProceedAtStopSign = true;
            }
        }




        bool reset = resetAction.ReadValue<float>() > 0;
        resetAction.Reset(); // Reset the action so it doesn't keep returning true until the button is released

        // Spawn cars at the start nodes when the reset button is pressed
        if (reset)
        {
            // pick a random start node and spawn a car
            int idx = UnityEngine.Random.Range(0, startNodes.Length);
            bool deviant = UnityEngine.Random.value < deviantProbability;
            startNodes[idx].SpawnCarIfNodeEmpty(deviant);
            activeCarCount++;

        }
    }

    // Called by cars when they are destroyed so the spawner can keep track of active cars
    public void NotifyCarDestroyed()
    {
        activeCarCount = Mathf.Max(0, activeCarCount - 1);
    }


}

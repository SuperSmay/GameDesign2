using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class IntersectionController : MonoBehaviour
{

    public GameObject carPrefab;
    public GameObject busPrefab;
    [SerializeField] PixelPerfectCamera pixelCamera;
    Vector2 cameraOriginalResolution;
     Vector2 cameraOriginalPosition;
    int cameraOriginalPPU;

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
    [SerializeField] Button nextTextButton;
    [SerializeField] TMPro.TextMeshProUGUI rulesText;
    [SerializeField] Image textBackground;

    int textPage = 0;
    string[] rulesPages = new string[]
    {
        "Welcome, fellow road watcher!\nYour job:\n\tWatch the intersection from above\n\tLook for unsafe driving behaviors\n\tClick on the cars that are driving recklessly to stop them",
        "Not every driver is dangerous, so choose carefully! Dangerous drivers may…\n\tDrive too fast\n\tDrive through the stop sign without stopping\n\tStop too close to other vehicles\n\nStop these cars, and help us keep our roads safe!"

    };

    [SerializeField] float gameDuration = 60f; // Duration of the game in seconds
    float gameOverDelay = 3f; // Time to wait after the game is over before showing the round end screen
    float gameTimer = 0f;
    float gameOverTimer;
    public bool gameOver = false;
    public Vector2 gameOverPosition; // Position to focus the camera on when the game is over
    public float gameSpeedMultiplier = 1f;
    public float fixedDeltaTimeSpeedMult { get { return gameSpeedMultiplier * Time.fixedDeltaTime; } }
    public float deltaTimeSpeedMult { get { return gameSpeedMultiplier * Time.deltaTime; } }

    public List<GameObject> activeEffects = new List<GameObject>();

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
        gameOverTimer = gameOverDelay; // Initialize game over timer
        cameraOriginalResolution = new Vector2(pixelCamera.refResolutionX, pixelCamera.refResolutionY);
        cameraOriginalPPU = pixelCamera.assetsPPU;
        cameraOriginalPosition = pixelCamera.transform.position;

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

        if (GameManager.Instance.roundNumber == 1)
        {
            textPage = 0; // Reset the text page
            rulesText.text = rulesPages[textPage];
            gameSpeedMultiplier = 0f; // Pause the game at the start to show the rules
            // Show rules text and hide it when the player clicks the next button
            rulesText.gameObject.SetActive(true);
            textBackground.gameObject.SetActive(true);
            nextTextButton.gameObject.SetActive(true);
            nextTextButton.onClick.AddListener(() =>
            {
                if (textPage < rulesPages.Length - 1)
                {
                    textPage++;
                    rulesText.text = rulesPages[textPage];
                }
                else
                {
                    // If we've reached the end of the rules pages, hide the text and button
                    rulesText.gameObject.SetActive(false);
                    textBackground.gameObject.SetActive(false);
                    nextTextButton.gameObject.SetActive(false);
                    gameSpeedMultiplier = 1f; // Start the game
                }
            });
        } else
        {
            // If it's not the first round, skip the rules and start the game immediately
            rulesText.gameObject.SetActive(false);
            textBackground.gameObject.SetActive(false);
            nextTextButton.gameObject.SetActive(false);
            gameSpeedMultiplier = 1f; // Start the game
        }
    }

    // Update is called once per frame
    void Update()
    {

        if (gameOver)
        {
            gameOverTimer -= Time.deltaTime;
            if (gameOverTimer <= 0f) // Wait for 3 seconds before showing round end screen
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("RoundEndScene");
            }

            gameSpeedMultiplier = 1f - (gameOverDelay - gameOverTimer) / gameOverDelay; // Gradually slow down time over 3 seconds


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

        gameTimer += deltaTimeSpeedMult;
        timerTextComponent.text = "Time: " + Mathf.Max(0, gameDuration - gameTimer).ToString("F1");

        scoreTextComponent.text = "Score: " + GameManager.Instance.Score;

        if (gameTimer >= gameDuration && activeCars.Count == 0)
        {
            // Game over, show round end scene
            GameManager.Instance.roundSuccessful = true; // Mark the round as successful since the player survived until the end
            UnityEngine.SceneManagement.SceneManager.LoadScene("RoundEndScene");
            return;
        }
        // Spawn cars periodically while the game timer is running
        if (gameTimer < gameDuration && startNodes != null && startNodes.Length > 0)
        {
            spawnTimer += deltaTimeSpeedMult;
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
            stopSignTimer += deltaTimeSpeedMult;
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

    void UpdateParticleSpeeds()
    {

        foreach (GameObject effect in activeEffects)
        {
            // If particle has been destroyed, skip it
            if (effect == null) continue;
            ParticleSystem.MainModule mainModule = effect.GetComponent<ParticleSystem>().main;
            mainModule.simulationSpeed = gameSpeedMultiplier;
        }

        activeEffects.RemoveAll(effect => effect == null); // Remove destroyed effects from the list
    }

    // Called by cars when they are destroyed so the spawner can keep track of active cars
    public void NotifyCarDestroyed()
    {
        activeCarCount = Mathf.Max(0, activeCarCount - 1);
    }

    void PlayRoundEndAnimation()
    {

    }


}

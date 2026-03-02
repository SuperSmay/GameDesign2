using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class IntersectionController : MonoBehaviour
{

    [SerializeField] GameObject carPrefab;

    [SerializeField] IntersectionNode[] startNodes;

    [SerializeField] PlayerInput playerInput;

    public static IntersectionController Instance { get; private set; }

    List<CarPathFollower> stopSignQueue = new List<CarPathFollower>();
    float stopSignWaitTime = 1f; // Time to wait at a stop sign before allowing the next car to go
    float stopSignTimer = 0f;

    InputAction resetAction;

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
        
    }

    // Update is called once per frame
    void Update()
    {

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
            foreach (IntersectionNode startNode in startNodes)
            {
                GameObject car = Instantiate(carPrefab, startNode.transform.position, Quaternion.identity);
                CarPathFollower carPathFollower = car.GetComponent<CarPathFollower>();
                carPathFollower.intersectionNode = startNode;
            }
        }
    }
}

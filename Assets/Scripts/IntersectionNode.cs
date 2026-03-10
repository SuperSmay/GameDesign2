using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Splines;

#nullable enable
public class IntersectionNode : MonoBehaviour
{

    [System.NonSerialized] public SplineContainer splineContainer;

    List<CarPathFollower> carPathFollowers = new List<CarPathFollower>();

    [SerializeField] IntersectionNode? continueNode;
    [SerializeField] IntersectionNode? leftTurnNode;
    [SerializeField] IntersectionNode? rightTurnNode;
    [SerializeField] IntersectionNode? noTurnNode;

    public IntersectionNode? TransferCarToNextNode(CarPathFollower car, TurnChoice turnChoice)
    {
        IntersectionNode? nextNode = null;
        switch (turnChoice)
        {
            case TurnChoice.Continue:
                if (continueNode != null)
                    nextNode = continueNode;
                break;
            case TurnChoice.Left:
                nextNode = leftTurnNode;
                break;
            case TurnChoice.Right:
                nextNode = rightTurnNode;
                break;
            case TurnChoice.NoTurn:
                nextNode = noTurnNode;
                break;
        }

        if (nextNode != null)
        {
            nextNode.OnCarEnter(car);
        }
        else if (turnChoice != TurnChoice.Unspecified) // Don't warn if there was no attempt to continue at all.
        {
            Debug.LogWarning($"Car attempted to transfer to a node that doesn't exist for turn choice {turnChoice}");
        }

        OnCarExit(car);

        return nextNode;

    }

    public void OnCarEnter(CarPathFollower car)
    {
        if (!carPathFollowers.Contains(car))
        {
            carPathFollowers.Add(car);
        }
    }

    public void OnCarExit(CarPathFollower car)
    {
        if (carPathFollowers.Contains(car))
        {
            carPathFollowers.Remove(car);
        }
    }

    public List<TurnChoice> GetAvailableTurnChoices()
    {
        List<TurnChoice> choices = new List<TurnChoice>();
        if (continueNode != null) choices.Add(TurnChoice.Continue);
        if (leftTurnNode != null) choices.Add(TurnChoice.Left);
        if (rightTurnNode != null) choices.Add(TurnChoice.Right);
        if (noTurnNode != null) choices.Add(TurnChoice.NoTurn);
        return choices;
    }


    void Awake()
    {
        splineContainer = GetComponent<SplineContainer>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public bool HasCars()
    {
        return carPathFollowers.Count > 0;
    }

    public void SpawnCar(bool deviant)
    {
       Vector3 tangent = splineContainer.EvaluateTangent(0f);
       float angle = 0f;
        // Look at tangent direction
        if (tangent != Vector3.zero)
        {
            angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
        }
        Quaternion rotation = Quaternion.Euler(0, 0, angle - 90);

        GameObject car = Instantiate(IntersectionController.Instance.carPrefab, transform.position, rotation);
        CarPathFollower carPathFollower = car.GetComponent<CarPathFollower>();
        carPathFollower.intersectionNode = this;

        OnCarEnter(carPathFollower);
        IntersectionController.Instance.activeCars.Add(carPathFollower);

        carPathFollower.maxSpeed += GameManager.Instance.roundNumber * 0.05f; // Increase base speed based on round number to make the game more challenging as it goes on
        carPathFollower.raycastDistance += GameManager.Instance.roundNumber * 0.2f;

        if (deviant)  // TODO make this better
        {
            carPathFollower.isDeviant = true;
            // Choose a random behavior to modify
            int behaviorToModify = Random.Range(0, 3);
            switch (behaviorToModify)
            {
                case 0:
                    carPathFollower.maxSpeed *= 3f; // Higher speed for speeding behavior
                    break;
                case 1:
                case 2:
                    carPathFollower.canProceedAtStopSign = true; // Allow proceeding at stop signs for running stop signs behavior
                    break;
            }
        }
    }

    public void SpawnCarIfNodeEmpty(bool deviant)
    {
        if (!HasCars())
        {
            SpawnCar(deviant);
        }
    }

}

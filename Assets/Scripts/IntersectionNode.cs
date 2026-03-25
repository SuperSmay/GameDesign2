using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

#nullable enable
public class IntersectionNode : MonoBehaviour
{

    [System.NonSerialized] public SplineContainer splineContainer;

    List<CarPathFollower> carPathFollowers = new List<CarPathFollower>();

    public IntersectionNode? continueNode;
    public IntersectionNode? leftTurnNode;
    public IntersectionNode? rightTurnNode;
    public IntersectionNode? noTurnNode;

    public TurnChoice[] availableTurnChoicesOnPath;

    public IntersectionNode? TransferCarToNextNode(CarPathFollower car, TurnChoice? turnChoice)
    {
        IntersectionNode? nextNode = null;
        switch (turnChoice)
        {
            case TurnChoice.Continue:
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
        else if (!turnChoice.HasValue) // Don't warn if there was no attempt to continue at all.
        {
            Debug.LogWarning($"Car attempted to transfer to a node that doesn't exist for turn choice {turnChoice}");
        }

        OnCarExit(car);

        return nextNode;

    }

    public IntersectionNode? PeekNextNode(TurnChoice turnChoice)
    {
        switch (turnChoice)
        {
            case TurnChoice.Left:
                return leftTurnNode;
            case TurnChoice.Right:
                return rightTurnNode;
            case TurnChoice.NoTurn:
                return noTurnNode;
            default:
                return continueNode;
        }
    }


    // Note!
    // This function assumes that only ONE path split will happen for the each complete route through the intersection.
    // Thus, it will only build the choices available until it hits a node with no continue path.
    TurnChoice[] BuildAvailableTurnChoices()
    {

        // Base case. 
        // This is guaranteed to happen eventually unless there is a loop.
        if (continueNode == null)
        {
            List<TurnChoice> choices = GetAvailableTurnChoices();
            return choices.ToArray();
        }

        // Recursive case
        return continueNode.BuildAvailableTurnChoices();

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
        availableTurnChoicesOnPath = BuildAvailableTurnChoices();
    }

    public bool HasCars()
    {
        return carPathFollowers.Count > 0;
    }

    public void SpawnCar(CarSpawn carSpawn)
    {
       Vector3 tangent = splineContainer.EvaluateTangent(0f);
       float angle = 0f;
        // Look at tangent direction
        if (tangent != Vector3.zero)
        {
            angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
        }
        Quaternion rotation = Quaternion.Euler(0, 0, angle - 90);

        // Sometimes spawn a bus
        float spawnSeed = UnityEngine.Random.Range(0f, 1f);

        GameObject? prefabToSpawn;

        switch (carSpawn.vehicleType)
        {
            case VehicleType.bus:
                prefabToSpawn = IntersectionController.Instance.busPrefab;
                break;
            case VehicleType.car:
                prefabToSpawn = IntersectionController.Instance.carPrefab;
                break;
            case VehicleType.random:
                if (spawnSeed < 0.2f)
                {
                    prefabToSpawn = IntersectionController.Instance.busPrefab;
                }
                else
                {
                    prefabToSpawn = IntersectionController.Instance.carPrefab;
                }
                break;
            default:
                prefabToSpawn = IntersectionController.Instance.carPrefab;
                break;
        }

        GameObject car = Instantiate(prefabToSpawn, transform.position, rotation);
        CarPathFollower carPathFollower = car.GetComponent<CarPathFollower>();
        carPathFollower.Initialize(carSpawn, this);

        OnCarEnter(carPathFollower);
        IntersectionController.Instance.activeCars.Add(carPathFollower);

        carPathFollower.maxSpeed += GameManager.Instance.roundNumber * 0.05f; // Increase base speed based on round number to make the game more challenging as it goes on

    }

    public bool SpawnCarIfNodeEmpty(CarSpawn carSpawn)
    {
        if (!HasCars())
        {
            SpawnCar(carSpawn);
            return true;
        }
        return false;
    }

    

}

using System.Collections.Generic;
using UnityEngine;

public class IntersectionStopLine : MonoBehaviour
{

    [SerializeField] IntersectionMovementBlockingCollider[] collidersToCheckBeforeMoving;
    public HashSet<CarPathFollower> carsAllowedThrough = new HashSet<CarPathFollower>();
    public bool allCarsAllowedThrough = false;
    public StoplightColor currentStoplightColor = StoplightColor.Red;
    public TurnChoice turnChoiceForThisStopLine;

    public bool CanCarProceed(CarPathFollower car)
    {
        if (allCarsAllowedThrough) return true;
        return carsAllowedThrough.Contains(car);
    }

    public bool AreMovementBlockingCollidersClearOfCars(Collider2D colliderToIgnore)
    {
        foreach (var collider in collidersToCheckBeforeMoving)
        {
            if (colliderToIgnore != null && collider.CarsInZone.Count == 1 && collider.CarsInZone.Contains(colliderToIgnore))
            {
                continue;
            }
            
            if (collider.CarsInZone.Count > 0)
            {
                return false;
            }
        }

        return true;
    }

    public bool AreMovementBlockingCollidersClearOfPeds(Collider2D colliderToIgnore)
    {
        foreach (var collider in collidersToCheckBeforeMoving)
        {
            if (colliderToIgnore != null && collider.PedsInZone.Count == 1 && collider.PedsInZone.Contains(colliderToIgnore))
            {
                continue;
            }
            
            if (collider.PedsInZone.Count > 0)
            {
                return false;
            }
        }

        return true;
    }
}


public enum StoplightColor
{
    Red,
    Yellow,
    Green
}

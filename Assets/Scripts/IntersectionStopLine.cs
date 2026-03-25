using System.Collections.Generic;
using UnityEngine;

public class IntersectionStopLine : MonoBehaviour
{

    [SerializeField] IntersectionMovementBlockingCollider[] collidersToCheckBeforeMoving;
    public HashSet<CarPathFollower> carsAllowedThrough = new HashSet<CarPathFollower>();
    public bool allCarsAllowedThrough = false;
    public TurnChoice turnChoiceForThisStopLine;

    public bool CanCarProceed(CarPathFollower car)
    {
        if (allCarsAllowedThrough) return true;
        return carsAllowedThrough.Contains(car);
    }

    public bool AreMovementBlockingCollidersClear(Collider2D colliderToIgnore)
    {
        foreach (var collider in collidersToCheckBeforeMoving)
        {
            if (colliderToIgnore != null && collider.ObjectsInRange.Count == 1 && collider.ObjectsInRange.Contains(colliderToIgnore))
            {
                continue;
            }
            else if (collider.ObjectsInRange.Count > 0)
            {
                return false;
            }
        }

        return true;
    }
}

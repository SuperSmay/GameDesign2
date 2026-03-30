using System.Collections.Generic;
using UnityEngine;

public class CrosswalkEntrance : MonoBehaviour
{

    [SerializeField] IntersectionMovementBlockingCollider intersectionCenterCollider;

    List<PedestrianNavAgent> waitingPedestrians = new List<PedestrianNavAgent>();

    private void OnTriggerEnter2D(Collider2D collision)
    {

        // Filter to only pedestrians
        if (collision.gameObject.layer != ColliderTypeToLayerMasks.Map[ColliderType.Pedestrian]) return;

        PedestrianNavAgent pedestrian = collision.GetComponent<PedestrianNavAgent>();

        if (pedestrian == null)
        {
            Debug.LogError("A pedestrian entered the crosswalk trigger but doesn't have a PedestrianNavAgent component!");
            return;
        }

        // 1. Get the direction the pedestrian is currently moving
        Vector3 movingDirection = pedestrian.navMeshAgent.velocity.normalized;

        // 2. Get the direction this trigger is facing 
        // (Assuming the green Y-axis arrow points into the crosswalk)
        Vector3 streetDirection = transform.up;

        // 3. Compare them using the Dot Product
        float directionCheck = Vector3.Dot(movingDirection, streetDirection);

        // 4. If the number is less than 0, they are walking away from the street!
        // (We use <= 0.2f to give a little wiggle room for diagonal walking)
        if (directionCheck <= 0.2f)
        {
            Debug.Log("Pedestrian is leaving the crosswalk. Let them pass!");
            return;
        }

        // 5. Check the relevant collider
        if (intersectionCenterCollider != null && intersectionCenterCollider.CarsInZone.Count == 0)
        {
            Debug.Log("Intersection is clear, let the pedestrian pass!");
            return;
        }

        pedestrian.PauseWalking();
        waitingPedestrians.Add(pedestrian);

    }

    void Update()
    {
        // Clear out any nulls in the waiting pedestrians list (in case they got hit by a car and destroyed, etc.)
        waitingPedestrians.RemoveAll(item => item == null);

        // If the intersection is clear, let all waiting pedestrians go
        if (waitingPedestrians.Count > 0 && intersectionCenterCollider != null && intersectionCenterCollider.CarsInZone.Count == 0)
        {
            OnIntersectionCleared();
        }
    }

    public void OnIntersectionCleared()
    {
        foreach (PedestrianNavAgent ped in waitingPedestrians)
        {
            if (ped != null) ped.ResumeWalking();
        }
        waitingPedestrians.Clear();
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        PedestrianNavAgent pedestrian = collision.GetComponent<PedestrianNavAgent>();
        if (pedestrian != null && waitingPedestrians.Contains(pedestrian))
        {
            waitingPedestrians.Remove(pedestrian);
        }
    }
}

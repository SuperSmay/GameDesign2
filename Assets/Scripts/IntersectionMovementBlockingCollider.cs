using System.Collections.Generic;
using UnityEngine;

public class IntersectionMovementBlockingCollider : MonoBehaviour
{

    BoxCollider2D boxCollider;
    
    // A HashSet ensures we don't count the same object twice
    public HashSet<Collider2D> CarsInZone = new HashSet<Collider2D>();
    public HashSet<Collider2D> PedsInZone = new HashSet<Collider2D>();
    public bool detectCars = true;
    public bool detectPeds = true;

    void Awake()
    {
        boxCollider = GetComponent<BoxCollider2D>();
    }

    void Update()
    {
        // Clear out nulls
        CarsInZone.RemoveWhere(item => item == null);
        PedsInZone.RemoveWhere(item => item == null);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {

        if (other.gameObject.layer == ColliderTypeToLayerMasks.Map[ColliderType.ClickBox]) return; // Ignore click boxes, since they don't actually block movement and would just cause confusion

        if (other.gameObject.layer == ColliderTypeToLayerMasks.Map[ColliderType.Car] && !detectCars) return; // Ignore cars if detectCars is false
        if (other.gameObject.layer == ColliderTypeToLayerMasks.Map[ColliderType.Pedestrian] && !detectPeds) return; // Ignore pedestrians if detectPeds is false

        if (other.gameObject.layer == ColliderTypeToLayerMasks.Map[ColliderType.Car])
        {
            if (!CarsInZone.Contains(other))
            {
                CarsInZone.Add(other);
            }
        }
        else if (other.gameObject.layer == ColliderTypeToLayerMasks.Map[ColliderType.Pedestrian])
        {
            if (!PedsInZone.Contains(other))
            {
                PedsInZone.Add(other);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // Remove the object when it leaves
        if (other.gameObject.layer == ColliderTypeToLayerMasks.Map[ColliderType.Car])
        {
            CarsInZone.Remove(other);
        }
        else if (other.gameObject.layer == ColliderTypeToLayerMasks.Map[ColliderType.Pedestrian])
        {
            PedsInZone.Remove(other);
        }
    }
}

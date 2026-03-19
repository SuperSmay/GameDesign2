using System.Collections.Generic;
using UnityEngine;

public class IntersectionMovementBlockingCollider : MonoBehaviour
{

    BoxCollider2D boxCollider;
    
    // A HashSet ensures we don't count the same object twice
    public HashSet<Collider2D> ObjectsInRange = new HashSet<Collider2D>();

    void Awake()
    {
        boxCollider = GetComponent<BoxCollider2D>();
    }

    void Update()
    {
        // Clear out nulls
        ObjectsInRange.RemoveWhere(item => item == null);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {

        if (other.gameObject.layer == ColliderTypeToLayerMasks.Map[ColliderType.ClickBox]) return; // Ignore click boxes, since they don't actually block movement and would just cause confusion

        // Add the object when it enters the zone
        if (!ObjectsInRange.Contains(other))
        {
            ObjectsInRange.Add(other);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // Remove the object when it leaves
        if (ObjectsInRange.Contains(other))
        {
            ObjectsInRange.Remove(other);
        }
    }
}

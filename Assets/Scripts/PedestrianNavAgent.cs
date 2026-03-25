using UnityEngine;
using UnityEngine.AI;

public class PedestrianNavAgent : MonoBehaviour
{

    NavMeshAgent navMeshAgent;
    [SerializeField] Transform target;

    float speed = 0.5f;
    float despawnDistance = 0.2f;

    void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        navMeshAgent.updateRotation = false; 
        navMeshAgent.updateUpAxis = false;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (Vector3.Distance(transform.position, target.transform.position) < despawnDistance)
        {
            Destroy(gameObject);
        }

        navMeshAgent.speed = GameManager.Instance.paused ? 0 : speed * GameManager.Instance.gameSpeedMultiplier;

    }

    public void Initialize(Transform target)
    {
        this.target = target;
        navMeshAgent.SetDestination(target.position);
    }

    public void OnCollisionEnter2D(Collision2D collision)
    {
        // When we collide with a car, ragdoll and stop moving
        if (collision.gameObject.layer == ColliderTypeToLayerMasks.Map[ColliderType.Car])
        {
            // However, if the car isn't moving, we don't ragdoll since it won't actually cause the pedestrian to fall over
            if (collision.gameObject.GetComponent<CarPathFollower>().speed < 0.1f)
            {
                return;
            }
            navMeshAgent.enabled = false; // Disable NavMeshAgent to stop movement
            GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic; // Make the pedestrian affected by physics
        }
    }
}

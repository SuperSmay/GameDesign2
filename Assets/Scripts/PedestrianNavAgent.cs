using UnityEngine;
using UnityEngine.AI;

public class PedestrianNavAgent : MonoBehaviour
{

    NavMeshAgent navMeshAgent;
    Animator animator;

    [SerializeField] AnimatorOverrideController[] costumes;
    [SerializeField] Transform target;

    float baseSpeed = 0.5f;
    float despawnDistance = 0.2f;

    void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        navMeshAgent.updateRotation = false; 
        navMeshAgent.updateUpAxis = false;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Randomly select a costume for the pedestrian
        if (costumes.Length > 0)
        {
            int randomIndex = Random.Range(0, costumes.Length);
            animator.runtimeAnimatorController = costumes[randomIndex];
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Vector3.Distance(transform.position, target.transform.position) < despawnDistance)
        {
            Destroy(gameObject);
        }

        navMeshAgent.speed = GameManager.Instance.paused ? 0 : baseSpeed * GameManager.Instance.gameSpeedMultiplier;
        if (navMeshAgent.velocity.sqrMagnitude > Mathf.Epsilon)
        {
            animator.speed = navMeshAgent.velocity.magnitude / baseSpeed; // Set animation speed based on how fast we're moving
        } else
        {
            // Reset to standing
            animator.speed = 0f;
            animator.Play("Walk", -1, 0f);
        }
        UpdateRotation();
    }

    void UpdateRotation()
    {
        // 1. Check if the agent is actually moving (velocity isn't zero)
        if (navMeshAgent.velocity.sqrMagnitude > Mathf.Epsilon)
        {
            // 2. Get the direction we are moving
            Vector3 direction = navMeshAgent.velocity.normalized;

            // 3. Calculate the angle in degrees
            // Atan2 takes (y, x) and calculates the angle of that vector
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

            // 4. Apply the rotation to the Z axis
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
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

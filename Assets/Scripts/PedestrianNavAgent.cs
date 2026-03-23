using UnityEngine;
using UnityEngine.AI;

public class PedestrianNavAgent : MonoBehaviour
{

    NavMeshAgent navMeshAgent;
    [SerializeField] Transform target;

    float speed = 1f;
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
}

using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class car : MonoBehaviour
{

    float currentSpeed = 0f;

    [SerializeField] Rigidbody2D rb;

    [SerializeField]float accelerationRate = 5f;
    [SerializeField] float decelerationRate = 3f;
    [SerializeField] float rotationRate = 200f;

    [SerializeField] PlayerInput playerInput;
    
    InputAction moveAction;

    private void Awake()
    {
        moveAction = playerInput.actions["Move"];
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

        Vector2 movement = moveAction.ReadValue<Vector2>();
        if (movement.y == 0)
        {

            // Decelerate when no vertical input
            // currentSpeed = Mathf.MoveTowards(currentSpeed, 0, decelerationRate * Time.deltaTime);
        }
        else
        {
            rb.AddForce(transform.up * movement.y * accelerationRate); // Accelerate based on vertical input
            // Accelerate based on vertical input
            // currentSpeed += movement.y * accelerationRate * Time.deltaTime;
        }

        if (movement.x != 0)
        {
            rb.AddTorque(-movement.x * rotationRate); // Rotate based on horizontal input
            // Rotate based on horizontal input
            // transform.Rotate(Vector3.forward, movement.x * -1 * rotationRate * Time.deltaTime);
        }


        transform.Translate(Vector3.up * currentSpeed * Time.deltaTime);
    }

}

using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintMultiplier = 1.5f;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool isSprinting;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
    float speed = isSprinting ? moveSpeed * sprintMultiplier : moveSpeed;
    rb.linearVelocity = moveInput * speed;
    }

    public void Move(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void Sprint(InputAction.CallbackContext context)
    {
        if (context.started) isSprinting = true;
        if (context.canceled) isSprinting = false;
    }
}
using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Components")]
    private Rigidbody2D rb;
    private BoxCollider2D coll;

    [Header("Layer Masks")]
    [SerializeField] private LayerMask groundLayer;

    [Header("Movement")]
    [SerializeField] private float maxSpeed = 9f;
    [SerializeField] private float acceleration = 90f;
    [SerializeField] private float deceleration = 60f;
    private Vector2 moveInput;

    [Header("Jump Mechanics")]
    [SerializeField] private float jumpForce = 15f;
    [SerializeField] private float gravityScale = 4f;
    [SerializeField] private float fallMultiplier = 2.5f;
    [SerializeField] private float lowJumpMultiplier = 2f;
    [SerializeField] private float coyoteTime = 0.15f;
    [SerializeField] private float jumpBufferTime = 0.1f;
    private float coyoteCounter;
    private float jumpBufferCounter;

    [Header("Dash Mechanics")]
    [SerializeField] private float dashSpeed = 24f;
    [SerializeField] private float dashTime = 0.15f;
    [SerializeField] private float dashCooldown = 0.2f;
    private bool canDash = true;
    private bool isDashing;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        coll = GetComponent<BoxCollider2D>();
        rb.gravityScale = gravityScale;
    }

    void Update()
    {
        // 1. Gather Directional Inputs
        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.y = Input.GetAxisRaw("Vertical");

        // 2. Coyote Time Management
        if (IsGrounded())
        {
            coyoteCounter = coyoteTime;
            canDash = true; // Refresh dash on ground
        }
        else
        {
            coyoteCounter -= Time.deltaTime;
        }

        // 3. Jump Buffering Management
        if (Input.GetButtonDown("Jump"))
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        // 4. Trigger Jump
        if (jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            Jump();
        }

        // 5. Trigger Dash
        if (Input.GetButtonDown("Dash") && canDash && !isDashing)
        {
            StartCoroutine(PerformDash());
        }
    }

    void FixedUpdate()
    {
        if (isDashing) return;

        Run();
        ModifyPhysics();
    }

    private void Run()
    {
        // Calculate targeted velocity along x-axis
        float targetSpeed = moveInput.x * maxSpeed;
        float speedDif = targetSpeed - rb.linearVelocity.x;

        // Choose between acceleration and deceleration rates
        float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? acceleration : deceleration;
        float movement = speedDif * accelRate;

        rb.AddForce(movement * Vector2.right, ForceMode2D.Force);
    }

    private void ModifyPhysics()
    {
        // Variable jump height logic (Celeste's custom gravity feel)
        if (rb.linearVelocity.y < 0)
        {
            rb.gravityScale = gravityScale * fallMultiplier;
        }
        else if (rb.linearVelocity.y > 0 && !Input.GetButton("Jump"))
        {
            rb.gravityScale = gravityScale * lowJumpMultiplier;
        }
        else
        {
            rb.gravityScale = gravityScale;
        }
    }

    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f); // Reset vertical speed first
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
    }

    private IEnumerator PerformDash()
    {
        canDash = false;
        isDashing = true;
        
        // Retain gravity settings
        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;

        // Determine 8-way directional vector or default forward if zero input
        Vector2 dashDir = moveInput.normalized;
        if (dashDir == Vector2.zero) 
        {
            dashDir = new Vector2(transform.localScale.x > 0 ? 1 : -1, 0);
        }

        // Apply raw static speed velocity over the duration of the dash
        rb.linearVelocity = dashDir * dashSpeed;
        yield return new WaitForSeconds(dashTime);

        rb.gravityScale = originalGravity;
        isDashing = false;

        yield return new WaitForSeconds(dashCooldown);
    }

    private bool IsGrounded()
    {
        // Simple raycast/boxcast under the player to detect ground layers
        float extraHeight = 0.1f;
        RaycastHit2D raycastHit = Physics2D.BoxCast(coll.bounds.center, coll.bounds.size - new Vector3(0.1f, 0f, 0f), 0f, Vector2.down, extraHeight, groundLayer);
        return raycastHit.collider != null;
    }
}

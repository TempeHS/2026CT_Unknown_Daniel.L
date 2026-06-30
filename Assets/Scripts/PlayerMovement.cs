using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float maxSpeed    = 9f;
    [SerializeField] private float runAccel    = 100f;  // accel toward max speed
    [SerializeField] private float runReduce   = 40f;   // gentle bleed when over max (preserves dash momentum)
    [SerializeField] private float airMult     = 0.65f;

    [Header("Jump")]
    [SerializeField] private float jumpSpeed       = 18f;
    [SerializeField] private float jumpHBoost      = 4f;
    [SerializeField] private float varJumpTime     = 0.2f;
    [SerializeField] private float coyoteTime      = 0.1f;
    [SerializeField] private float jumpBufferTime  = 0.1f;
    [SerializeField] private float fallMultiplier  = 2.5f;
    [SerializeField] private float lowJumpMult     = 2f;
    [SerializeField] private float gravityScale    = 4f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed    = 24f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 0.15f;

    [Header("Climb")]
    [SerializeField] private float climbUpSpeed   = 5f;
    [SerializeField] private float climbDownSpeed = 8f;

    [Header("Detection")]
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody2D   rb;
    private BoxCollider2D coll;
    private SpriteRenderer sprite;

    private Vector2 moveInput;
    private float coyoteCounter, jumpBufferCounter, varJumpCounter, varJumpSpeed;
    private bool  jumpHeld, canDash = true, isDashing, dashOnCooldown, climbHeld;
    private int   facingDir = 1;

    void Awake()
    {
        rb     = GetComponent<Rigidbody2D>();
        coll   = GetComponent<BoxCollider2D>();
        sprite = GetComponent<SpriteRenderer>();
        rb.gravityScale = gravityScale;
    }

    void Update()
    {
        if (IsGrounded()) { coyoteCounter = coyoteTime; if (!dashOnCooldown) canDash = true; }
        else              coyoteCounter -= Time.deltaTime;

        jumpBufferCounter -= Time.deltaTime;
        varJumpCounter    -= Time.deltaTime;

        // Cancel variable jump window immediately on button release (Celeste behaviour)
        if (!jumpHeld) varJumpCounter = 0f;

        bool climbing = climbHeld && IsTouchingWall();
        if (jumpBufferCounter > 0f && coyoteCounter > 0f && !climbing) Jump();

        if (moveInput.x != 0)
        {
            facingDir = moveInput.x > 0 ? 1 : -1;
            if (sprite != null) sprite.flipX = facingDir < 0;
        }
    }

    void FixedUpdate()
    {
        if (isDashing) return;

        bool grounded = IsGrounded();
        bool climbing = climbHeld && IsTouchingWall();

        if (climbing)
        {
            rb.gravityScale = 0f;
            // moveInput.y > 0 = up key = move up (positive Y in Unity)
            float vy = moveInput.y > 0 ? climbUpSpeed : (moveInput.y < 0 ? -climbDownSpeed : 0f);
            rb.linearVelocity = new Vector2(0f, vy);
        }
        else
        {
            // Horizontal — Celeste-style: bleed excess speed slowly, accelerate to max quickly
            float mult   = grounded ? 1f : airMult;
            float target = moveInput.x * maxSpeed;
            float vx     = rb.linearVelocity.x;
            bool  overMax = Mathf.Abs(vx) > maxSpeed && Mathf.Sign(vx) == Mathf.Sign(moveInput.x);
            float rate    = overMax ? runReduce : runAccel;
            // Use AddForce so tilemap collision responses aren't overridden
            float forceFactor = (Mathf.MoveTowards(vx, target, rate * mult * Time.fixedDeltaTime) - vx) / Time.fixedDeltaTime;
            rb.AddForce(Vector2.right * forceFactor, ForceMode2D.Force);

            float vy = rb.linearVelocity.y;

            // Variable jump: clamp upward speed to initial jump speed while holding (Celeste-style)
            if (varJumpCounter > 0f && jumpHeld)
                vy = Mathf.Max(vy, varJumpSpeed);

            // Gravity tweaks
            if (vy < 0)
                rb.gravityScale = gravityScale * fallMultiplier;
            else if (vy > 0 && varJumpCounter <= 0f)
                rb.gravityScale = gravityScale * lowJumpMult;
            else
                rb.gravityScale = gravityScale;

            if (varJumpCounter > 0f && jumpHeld)
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, vy);
        }
    }

    // Input callbacks
    public void OnMove(InputAction.CallbackContext ctx) => moveInput = ctx.ReadValue<Vector2>();

    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (ctx.started)  { jumpBufferCounter = jumpBufferTime; jumpHeld = true; }
        if (ctx.canceled) jumpHeld = false;
    }

    public void OnDash(InputAction.CallbackContext ctx)
    {
        if (ctx.started && canDash && !isDashing) StartCoroutine(Dash());
    }

    public void OnClimb(InputAction.CallbackContext ctx)
    {
        if (ctx.started)  climbHeld = true;
        if (ctx.canceled) climbHeld = false;
    }

    private void Jump()
    {
        varJumpSpeed      = jumpSpeed;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x + moveInput.x * jumpHBoost, jumpSpeed);
        varJumpCounter    = varJumpTime;
        coyoteCounter     = 0f;
        jumpBufferCounter = 0f;
    }

    private IEnumerator Dash()
    {
        canDash        = false;
        isDashing      = true;
        dashOnCooldown = true;

        float savedGravity    = rb.gravityScale;
        rb.gravityScale       = 0f;
        Vector2 dir           = moveInput.normalized;
        if (dir == Vector2.zero) dir = new Vector2(facingDir, 0f);
        rb.linearVelocity     = dir * dashSpeed;

        yield return new WaitForSeconds(dashDuration);

        // Preserve horizontal momentum, cancel upward speed (like Celeste's EndDash)
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Min(rb.linearVelocity.y, 0f));
        rb.gravityScale   = savedGravity;
        isDashing         = false;

        yield return new WaitForSeconds(dashCooldown);
        dashOnCooldown = false;
        if (IsGrounded()) canDash = true;
    }

    private bool IsGrounded() =>
        Physics2D.BoxCast(coll.bounds.center, new Vector2(coll.bounds.size.x - 0.1f, 0.05f),
            0f, Vector2.down, coll.bounds.extents.y + 0.1f, groundLayer).collider != null;

    private bool IsTouchingWall()
    {
        // Cast from center outward — avoids starting inside tilemap colliders
        float dist = coll.bounds.extents.x + 0.1f;
        float cy   = coll.bounds.center.y;
        float cx   = coll.bounds.center.x;
        float h    = coll.bounds.extents.y * 0.45f;
        Vector2 dir = new Vector2(facingDir, 0f);
        return Physics2D.Raycast(new Vector2(cx, cy + h), dir, dist, groundLayer)
            || Physics2D.Raycast(new Vector2(cx, cy - h), dir, dist, groundLayer);
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Run")]
    [SerializeField] private float maxSpeed  = 9f;
    [SerializeField] private float runAccel  = 100f;
    [SerializeField] private float runReduce = 40f;    // gentle bleed when over max (keeps dash/walljump momentum)
    [SerializeField] private float airMult   = 0.65f;

    [Header("Jump")]
    [SerializeField] private float jumpForce      = 18f;
    [SerializeField] private float jumpHBoost     = 4f;
    [SerializeField] private float varJumpTime    = 0.2f;   // how long holding jump keeps you rising
    [SerializeField] private float coyoteTime     = 0.1f;
    [SerializeField] private float jumpBufferTime = 0.1f;
    [SerializeField] private float gravityScale   = 4f;
    [SerializeField] private float fallMultiplier = 2.5f;
    [SerializeField] private float maxFallSpeed   = 20f;
    [SerializeField] private float fastFallSpeed  = 30f;

    [Header("Wall Jump")]
    [SerializeField] private float wallJumpHSpeed  = 13f;
    [SerializeField] private float wallJumpForceT  = 0.16f; // input locked away from wall this long

    [Header("Climb")]
    [SerializeField] private float climbUpSpeed   = 5f;
    [SerializeField] private float climbDownSpeed = 8f;
    [SerializeField] private float climbSlipSpeed = 3f;     // slide down when out of stamina
    [SerializeField] private float wallSlideSpeed = 2f;     // slow fall when pressed to wall
    [SerializeField] private float maxStamina     = 110f;
    [SerializeField] private float climbUpCost     = 45f;   // stamina/sec climbing up
    [SerializeField] private float climbStillCost  = 10f;   // stamina/sec hanging still
    [SerializeField] private float climbJumpCost   = 27f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed    = 24f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashEndSpeed = 16f;      // speed retained after dash
    [SerializeField] private float dashCooldown = 0.2f;

    // Components
    private Rigidbody2D    rb;
    private Collider2D     coll;
    private SpriteRenderer sprite;
    private readonly Collider2D[] _hits = new Collider2D[8];

    // Input
    private Vector2 moveInput;
    private bool    jumpHeld, climbHeld;

    // Jump state
    private float coyoteCounter, jumpBufferCounter, varJumpCounter, varJumpSpeed;
    private int   forceMoveX;
    private float forceMoveXTimer;
    private bool  jumpedThisFrame;

    // Dash state
    private bool  canDash = true, isDashing, dashOnCooldown;

    // Climb
    private float stamina;
    private bool  isClimbing;

    private int facingDir = 1;

    void Awake()
    {
        rb        = GetComponent<Rigidbody2D>();
        coll      = GetComponent<Collider2D>();
        if (coll == null) coll = GetComponentInChildren<Collider2D>();   // collider may live on a child
        sprite    = GetComponent<SpriteRenderer>();
        if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
        stamina   = maxStamina;
        rb.gravityScale = gravityScale;

        if (coll == null)
        {
            Debug.LogError("PlayerMovement: no Collider2D found on " + name + " or its children. Add a Collider2D.", this);
            enabled = false;
            return;
        }

        var mat = new PhysicsMaterial2D { friction = 0f, bounciness = 0f };
        coll.sharedMaterial = mat;
    }

    void Update()
    {
        jumpedThisFrame = false;
        bool grounded = IsGrounded();

        // Refresh on ground
        if (grounded)
        {
            coyoteCounter = coyoteTime;
            if (!dashOnCooldown) canDash = true;
            stamina = maxStamina;
        }
        else coyoteCounter -= Time.deltaTime;

        // Timers
        jumpBufferCounter -= Time.deltaTime;
        varJumpCounter    -= Time.deltaTime;
        if (forceMoveXTimer > 0f) forceMoveXTimer -= Time.deltaTime;
        if (!jumpHeld) varJumpCounter = 0f;   // release cancels rise

        // Jump priority: wall jump when airborne against a wall, else ground/coyote jump
        if (jumpBufferCounter > 0f)
        {
            if (coyoteCounter > 0f && !isClimbing) Jump();
            else if (!grounded && TouchingWall(out int wallDir)) WallJump(wallDir);
            else if (isClimbing) ClimbJump();
        }

        // Facing
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

        // ── Climb ──────────────────────────────────────────────
        isClimbing = climbHeld && !grounded && TouchingWall(out int cDir) && cDir == facingDir;
        if (isClimbing)
        {
            rb.gravityScale = 0f;
            float vy;
            if (stamina <= 0f)             { vy = -climbSlipSpeed; }                                       // out of stamina: slip down
            else if (moveInput.y > 0)      { vy = climbUpSpeed;   stamina -= climbUpCost   * Time.fixedDeltaTime; }
            else if (moveInput.y < 0)      { vy = -climbDownSpeed; }
            else                           { vy = 0f;             stamina -= climbStillCost * Time.fixedDeltaTime; }
            rb.linearVelocity = new Vector2(0f, vy);
            return;
        }

        // ── Horizontal (momentum preserving) ───────────────────
        float inputX = moveInput.x;
        if (forceMoveXTimer > 0f) inputX = forceMoveX;   // locked away from wall after a wall jump

        float mult   = grounded ? 1f : airMult;
        float target = inputX * maxSpeed;
        float vx     = rb.linearVelocity.x;
        bool  over   = Mathf.Abs(vx) > maxSpeed && Mathf.Sign(vx) == Mathf.Sign(target);
        vx = Mathf.MoveTowards(vx, target, (over ? runReduce : runAccel) * mult * Time.fixedDeltaTime);

        // ── Vertical / gravity ─────────────────────────────────
        float vyOut = rb.linearVelocity.y;

        if (grounded)
        {
            rb.gravityScale = gravityScale;
        }
        else
        {
            // Variable jump: keep rising while holding
            if (varJumpCounter > 0f && jumpHeld)
                vyOut = Mathf.Max(vyOut, varJumpSpeed);

            if (vyOut < 0f) rb.gravityScale = gravityScale * fallMultiplier;
            else            rb.gravityScale = gravityScale;

            // Wall slide: slow the fall when pressing into a wall
            if (vyOut < 0f && climbHeld && TouchingWall(out int wd) && wd == facingDir)
                vyOut = Mathf.Max(vyOut, -wallSlideSpeed);

            // Clamp fall speed (fast-fall when holding down)
            float capFall = (moveInput.y < 0) ? fastFallSpeed : maxFallSpeed;
            if (vyOut < -capFall) vyOut = -capFall;
        }

        rb.linearVelocity = new Vector2(vx, vyOut);
    }

    // ── Detection — no layer setup needed, self-collider excluded by reference ──
    private bool OverlapSolid(Vector2 pos, Vector2 size)
    {
        int count = Physics2D.OverlapBoxNonAlloc(pos, size, 0f, _hits);
        for (int i = 0; i < count; i++)
            if (_hits[i] != null && _hits[i] != coll && _hits[i].transform.root != transform.root)
                return true;
        return false;
    }

    private bool IsGrounded()
    {
        if (jumpedThisFrame) return false;
        var b = coll.bounds;
        return OverlapSolid(new Vector2(b.center.x, b.min.y), new Vector2(b.size.x - 0.04f, 0.1f));
    }

    private bool TouchingWall(out int dir)
    {
        var b = coll.bounds;
        Vector2 size = new Vector2(0.1f, b.size.y - 0.1f);
        if (OverlapSolid(new Vector2(b.max.x + 0.02f, b.center.y), size)) { dir =  1; return true; }
        if (OverlapSolid(new Vector2(b.min.x - 0.02f, b.center.y), size)) { dir = -1; return true; }
        dir = 0; return false;
    }

    // ── Input callbacks ──
    public void OnMove(InputAction.CallbackContext ctx)  => moveInput = ctx.ReadValue<Vector2>();

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

    // ── Actions ──
    private void Jump()
    {
        jumpedThisFrame   = true;
        coyoteCounter     = 0f;
        jumpBufferCounter = 0f;
        varJumpCounter    = varJumpTime;
        varJumpSpeed      = jumpForce;
        rb.gravityScale   = gravityScale;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x + moveInput.x * jumpHBoost, jumpForce);
    }

    private void WallJump(int wallDir)
    {
        jumpBufferCounter = 0f;
        varJumpCounter    = varJumpTime;
        varJumpSpeed      = jumpForce;
        // push away from the wall and lock input briefly so you actually leave it
        forceMoveX      = -wallDir;
        forceMoveXTimer = wallJumpForceT;
        rb.gravityScale   = gravityScale;
        rb.linearVelocity = new Vector2(-wallDir * wallJumpHSpeed, jumpForce);
    }

    private void ClimbJump()
    {
        if (stamina <= 0f) return;
        stamina -= climbJumpCost;
        isClimbing = false;
        int wallDir = facingDir;
        forceMoveX      = -wallDir;
        forceMoveXTimer = wallJumpForceT;
        jumpBufferCounter = 0f;
        varJumpCounter    = varJumpTime;
        varJumpSpeed      = jumpForce;
        rb.gravityScale   = gravityScale;
        rb.linearVelocity = new Vector2(-wallDir * wallJumpHSpeed, jumpForce);
    }

    private IEnumerator Dash()
    {
        canDash = false; isDashing = true; dashOnCooldown = true;
        varJumpCounter = 0f;

        float savedGravity = rb.gravityScale;
        rb.gravityScale    = 0f;

        Vector2 dir = moveInput.normalized;
        if (dir == Vector2.zero) dir = new Vector2(facingDir, 0f);
        rb.linearVelocity = dir * dashSpeed;

        yield return new WaitForSeconds(dashDuration);

        // Retain a portion of speed in the dash direction; kill upward so you fall naturally
        Vector2 end = dir * dashEndSpeed;
        if (end.y > 0f) end.y = 0f;
        rb.linearVelocity = end;
        rb.gravityScale   = savedGravity;
        isDashing         = false;

        yield return new WaitForSeconds(dashCooldown);
        dashOnCooldown = false;
        if (IsGrounded()) canDash = true;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CharacterController))]
public class EnemyController : MonoBehaviour
{
    public static EnemyController ActivePossessedEnemy;

    public enum WeaponType { Melee, Ranged }

    [Header("Camera Tracking")]
    public Transform headBoneTarget;
    public Vector3 cameraOffset = new Vector3(0f, 0.25f, 0.05f);
    public float cameraFollowSpeed = 30f;

    [Header("Procedural Head Bob")]
    public float bobSpeed = 14f;
    public float walkBobAmount = 0.04f;
    public float sprintBobAmount = 0.07f;
    public float crouchBobAmount = 0.02f;

    [Header("Base Movement Speeds")]
    public float walkSpeed = 3.5f;
    public float sprintSpeed = 6.0f;
    public float crouchSpeed = 1.5f;
    public float movementSmoothTime = 0.15f;

    [Header("Look Parameters")]
    public float mouseSensitivity = 2.0f;
    public float upDownRange = 80.0f;
    public float lookSmoothTime = 0.05f;

    [Header("Jumping & Gravity")]
    public float gravity = -9.81f;
    public float maxJumpHeight = 1.69f;
    public float jumpChargeRate = 1.5f;

    [Header("UI & FOV")]
    public Image jumpFillImage;
    public CanvasGroup jumpCanvasGroup;
    public float uiFadeSpeed = 5f;
    public float defaultFOV = 60f;
    public float zoomFOV = 35f;
    public float zoomSpeed = 10f;

    [Header("Crouching")]
    public float crouchHeight = 1.0f;
    public float standingHeight = 2.0f;
    public float crouchTransitionSpeed = 10f;

    [Header("Possession Timer & Stun")]
    public float possessionTime = 10f;
    public KeyCode earlyExitKey = KeyCode.E;
    public float stunDuration = 4.0f;
    public Image timerFillImage;
    public CanvasGroup timerCanvasGroup;
    public CanvasGroup vignetteFadeGroup;

    [Header("Degradation Settings")]
    public float startSwayAmount = 0.0f;
    public float endSwayAmount = 0.05f;
    public float additiveStaggerAmount = 0.8f;
    public float startLookMultiplier = 1.0f;
    public float endLookMultiplier = 0.8f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip[] stepSounds;
    public AudioClip jumpSound;
    public AudioClip deathSound;
    public AudioClip punchSound;
    public AudioClip shootSound;
    public float baseStepInterval = 0.5f;

    [Header("Combat Stats")]
    public WeaponType weaponType = WeaponType.Melee;
    public float health = 100f;
    public float attackDamage = 25f;

    [Header("References")]
    public Animator animator;

    [HideInInspector] public Vector2 aiInput = Vector2.zero;
    [HideInInspector] public bool aiIsChasing = false;
    [HideInInspector] public bool isPossessed = false;
    [HideInInspector] public bool isStunned = false;
    [HideInInspector] public bool isDead = false;
    [HideInInspector] public bool isAiming = false;

    private Camera playerCamera;
    private CharacterController characterController;
    private Bounce bounceReference;

    private Vector3 currentMoveVelocity;
    private Vector3 moveDampVelocity;
    private Vector2 currentMouseLook;
    private Vector2 smoothMouseVelocity;
    private float verticalRotation;
    private float horizontalRotation;
    private float currentZTilt = 0f;
    private Vector3 velocity;

    private float jumpCharge = 0f;
    private bool isChargingJump = false;
    private bool isCrouching = false;
    private bool isSprinting = false;

    private float currentPossessionTimer;
    private float stepTimer;
    private float nextAttackTime = 0f;
    private float aimReleaseTime = 0f;

    private float bobTimer = 0f;
    private float currentBobX = 0f;
    private float currentBobY = 0f;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (jumpFillImage != null) jumpFillImage.fillAmount = 0;
        if (jumpCanvasGroup != null) jumpCanvasGroup.alpha = 0;
        if (timerCanvasGroup != null) timerCanvasGroup.alpha = 0;
        if (vignetteFadeGroup != null) vignetteFadeGroup.alpha = 0;
    }

    public void StartPossession(Camera cam, Bounce bounceScript)
    {
        if (ActivePossessedEnemy != null && ActivePossessedEnemy != this)
        {
            ActivePossessedEnemy.ForceEject();
        }
        ActivePossessedEnemy = this;

        EnemySight meleeBrain = GetComponent<EnemySight>();
        if (meleeBrain != null) meleeBrain.enabled = false;

        EnemyGunnerSight gunnerBrain = GetComponent<EnemyGunnerSight>();
        if (gunnerBrain != null) gunnerBrain.enabled = false;

        playerCamera = cam;
        bounceReference = bounceScript;

        playerCamera.fieldOfView = defaultFOV;
        verticalRotation = playerCamera.transform.localEulerAngles.x;
        if (verticalRotation > 180f) verticalRotation -= 360f;
        horizontalRotation = 0f;
        currentZTilt = 0f;

        if (headBoneTarget != null && playerCamera != null)
        {
            Vector3 targetPos = headBoneTarget.position + transform.TransformDirection(cameraOffset);
            playerCamera.transform.position = targetPos;
            playerCamera.transform.rotation = Quaternion.Euler(verticalRotation, transform.eulerAngles.y, currentZTilt);
        }

        currentPossessionTimer = possessionTime;
        isPossessed = true;
        isStunned = false;
        isAiming = false;
        aimReleaseTime = 0f;

        if (timerCanvasGroup != null) timerCanvasGroup.alpha = 1f;
        if (animator != null) animator.enabled = true;
    }

    void Update()
    {
        if (isDead) return;

        if (isPossessed)
        {
            if (Input.GetKeyDown(earlyExitKey))
            {
                ExitPossessionEarly();
                return;
            }

            HandlePossessionTimer();
            HandleMouseLook();
            HandleCrouch();
            HandleZoom();
            HandleJumpCharge();
            HandleCameraFollow();
            HandlePossessedCombat();
        }

        HandleMovement();
        UpdateAnimations();
    }

    private void HandlePossessedCombat()
    {
        if (weaponType == WeaponType.Melee)
        {
            if (Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + 1.2f;
                if (animator != null) animator.SetTrigger(Random.value > 0.5f ? "PunchLeft" : "PunchRight");
                PlayPunchSound();
                StartCoroutine(PossessedMeleeHit());
            }
        }
        else if (weaponType == WeaponType.Ranged)
        {
            if (Input.GetMouseButton(1))
            {
                aimReleaseTime = Time.time + 1.5f;
            }

            isAiming = Time.time < aimReleaseTime;

            if (Input.GetMouseButton(0) && Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + 0.5f;
                if (animator != null) animator.SetTrigger("Shoot");
                PlayShootSound();
                PossessedShootHit();
            }
        }
    }

    public void PlayPunchSound()
    {
        if (audioSource != null && punchSound != null) audioSource.PlayOneShot(punchSound);
    }

    public void PlayShootSound()
    {
        if (audioSource != null && shootSound != null) audioSource.PlayOneShot(shootSound);
    }

    private IEnumerator PossessedMeleeHit()
    {
        yield return new WaitForSeconds(0.3f);
        ExecuteHitscan(3f);
    }

    private void PossessedShootHit()
    {
        ExecuteHitscan(100f);
    }

    private void ExecuteHitscan(float distance)
    {
        RaycastHit[] hits = Physics.RaycastAll(playerCamera.transform.position, playerCamera.transform.forward, distance);

        float closestDist = Mathf.Infinity;
        RaycastHit closestHit = new RaycastHit();
        bool hitSomething = false;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.root == transform.root) continue;

            if (hit.distance < closestDist)
            {
                closestDist = hit.distance;
                closestHit = hit;
                hitSomething = true;
            }
        }

        if (hitSomething)
        {
            EnemyController enemy = closestHit.collider.GetComponentInParent<EnemyController>();
            if (enemy != null) enemy.TakeDamage(attackDamage, transform);
        }
    }

    public void TakeDamage(float amount, Transform attacker)
    {
        if (isDead) return;
        health -= amount;

        if (health <= 0)
        {
            Die();
        }
        else if (attacker != null)
        {
            EnemySight meleeBrain = GetComponent<EnemySight>();
            if (meleeBrain != null)
            {
                meleeBrain.playerTarget = attacker;
                meleeBrain.SuspectAndInvestigate(attacker.position);
            }

            EnemyGunnerSight gunnerBrain = GetComponent<EnemyGunnerSight>();
            if (gunnerBrain != null)
            {
                gunnerBrain.playerTarget = attacker;
                gunnerBrain.SuspectAndInvestigate(attacker.position);
            }
        }
    }

    private void UpdateAnimations()
    {
        if (animator == null || !animator.enabled || isStunned) return;

        Vector3 localVelocity = transform.InverseTransformDirection(currentMoveVelocity);
        animator.SetFloat("ForwardSpeed", localVelocity.z, 0.1f, Time.deltaTime);
        animator.SetBool("IsCrouching", isCrouching);
        animator.SetBool("IsGrounded", characterController.isGrounded);
        animator.SetBool("IsAiming", isAiming);
    }

    private void HandlePossessionTimer()
    {
        currentPossessionTimer -= Time.deltaTime;

        if (timerFillImage != null) timerFillImage.fillAmount = currentPossessionTimer / possessionTime;

        if (currentPossessionTimer <= 3f && vignetteFadeGroup != null)
        {
            float fadeProgress = Mathf.InverseLerp(3f, 0f, currentPossessionTimer);
            float eyeBlinkPulse = Mathf.Abs(Mathf.Sin(currentPossessionTimer * Mathf.PI * 2f));
            vignetteFadeGroup.alpha = Mathf.Lerp(fadeProgress, fadeProgress * 0.5f, eyeBlinkPulse);
        }

        if (currentPossessionTimer <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;
        isAiming = false;

        if (audioSource != null && deathSound != null) audioSource.PlayOneShot(deathSound);
        if (animator != null) animator.SetTrigger("Die");
        if (characterController != null) characterController.enabled = false;

        if (isPossessed)
        {
            if (ActivePossessedEnemy == this) ActivePossessedEnemy = null;
            if (vignetteFadeGroup != null) vignetteFadeGroup.alpha = 0f;
            if (timerCanvasGroup != null) timerCanvasGroup.alpha = 0f;

            bounceReference.InitiateReturn();
            isPossessed = false;
        }

        this.enabled = false;
    }

    private void HandleMouseLook()
    {
        float degradation = currentPossessionTimer > 3f ? Mathf.InverseLerp(possessionTime, 3f, currentPossessionTimer) : 1f;
        float currentLookSmooth = Mathf.Lerp(lookSmoothTime, 0.5f, degradation);
        float currentSens = mouseSensitivity * Mathf.Lerp(startLookMultiplier, endLookMultiplier, degradation);
        float currentSway = Mathf.Lerp(startSwayAmount, endSwayAmount, degradation);

        float mouseX = Input.GetAxisRaw("Mouse X") * currentSens;
        float mouseY = Input.GetAxisRaw("Mouse Y") * currentSens;

        float swayX = Mathf.Sin(Time.time * 2f) * currentSway;
        float swayY = Mathf.Cos(Time.time * 1.5f) * currentSway;

        Vector2 targetMouseLook = new Vector2(mouseX + swayX, mouseY + swayY);
        currentMouseLook = Vector2.SmoothDamp(currentMouseLook, targetMouseLook, ref smoothMouseVelocity, currentLookSmooth);

        verticalRotation -= currentMouseLook.y;
        verticalRotation = Mathf.Clamp(verticalRotation, -upDownRange, upDownRange);

        currentZTilt = 0f;
        if (currentPossessionTimer <= 3f)
        {
            float collapseP = 1f - (currentPossessionTimer / 3f);
            verticalRotation += collapseP * 40f * Time.deltaTime;
            currentZTilt = Mathf.Lerp(0f, 75f, collapseP);
        }

        transform.rotation *= Quaternion.Euler(0, currentMouseLook.x, 0);
    }

    private void HandleMovement()
    {
        if (isStunned) return;

        float degradation = isPossessed ? Mathf.InverseLerp(possessionTime, 3f, currentPossessionTimer) : 1f;

        if (characterController.isGrounded && velocity.y < 0) velocity.y = -2f;

        float targetSpeed = 0f;
        float moveHorizontal = 0f;
        float moveVertical = 0f;

        if (isPossessed)
        {
            isSprinting = Input.GetKey(KeyCode.LeftShift) && !isCrouching && !isChargingJump && Input.GetAxis("Vertical") > 0;
            targetSpeed = isCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : walkSpeed);
            if (isChargingJump) targetSpeed *= 0.5f;

            if (currentPossessionTimer <= 3f)
            {
                float collapseP = 1f - (currentPossessionTimer / 3f);
                targetSpeed = Mathf.Lerp(targetSpeed, 0f, collapseP);
            }

            moveHorizontal = Input.GetAxisRaw("Horizontal");
            moveVertical = Input.GetAxisRaw("Vertical");
        }
        else if (aiIsChasing)
        {
            targetSpeed = walkSpeed;
            moveHorizontal = aiInput.x;
            moveVertical = aiInput.y;
        }

        Vector3 inputDir = (transform.right * moveHorizontal + transform.forward * moveVertical).normalized;
        float swayMove = 0f;
        if (isPossessed) swayMove = Mathf.Sin(Time.time * 3f) * (degradation * additiveStaggerAmount);

        inputDir += transform.right * swayMove;

        Vector3 targetMoveVelocity = inputDir.normalized * targetSpeed;
        currentMoveVelocity = Vector3.SmoothDamp(currentMoveVelocity, targetMoveVelocity, ref moveDampVelocity, movementSmoothTime);

        characterController.Move(currentMoveVelocity * Time.deltaTime);
        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);

        HandleFootsteps();
    }

    private void HandleFootsteps()
    {
        Vector3 horizontalVelocity = new Vector3(currentMoveVelocity.x, 0, currentMoveVelocity.z);

        if (characterController.isGrounded && horizontalVelocity.sqrMagnitude > 0.1f)
        {
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f)
            {
                if (stepSounds != null && stepSounds.Length > 0 && audioSource != null)
                {
                    audioSource.pitch = isSprinting ? 1.4f : 1.0f;
                    audioSource.PlayOneShot(stepSounds[Random.Range(0, stepSounds.Length)]);
                }
                stepTimer = isSprinting ? baseStepInterval * 0.7f : baseStepInterval;
            }
        }
        else if (horizontalVelocity.sqrMagnitude < 0.1f)
        {
            stepTimer = 0f;
        }
    }

    private void HandleCrouch()
    {
        if (isStunned) return;
        if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C)) isCrouching = !isCrouching;
        float targetHeight = isCrouching ? crouchHeight : standingHeight;
        characterController.height = Mathf.Lerp(characterController.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
    }

    private void HandleZoom()
    {
        bool isZooming = Input.GetMouseButton(1) && weaponType == WeaponType.Melee;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, isZooming ? zoomFOV : defaultFOV, Time.deltaTime * zoomSpeed);
    }

    private void HandleJumpCharge()
    {
        if (isStunned || !characterController.isGrounded) return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            isChargingJump = true;
            jumpCharge = 0f;
            StopAllCoroutines();
            if (jumpCanvasGroup != null) jumpCanvasGroup.alpha = 1f;
        }

        if (isChargingJump && Input.GetKey(KeyCode.Space))
        {
            jumpCharge += Time.deltaTime * jumpChargeRate;
            if (jumpFillImage != null) jumpFillImage.fillAmount = jumpCharge;
            if (jumpCharge >= 1f) ExecuteJump();
        }

        if (isChargingJump && Input.GetKeyUp(KeyCode.Space)) ExecuteJump();
    }

    private void ExecuteJump()
    {
        isChargingJump = false;
        float targetHeight = Mathf.Lerp(0f, maxJumpHeight, jumpCharge);
        if (targetHeight > 0.1f)
        {
            velocity.y = Mathf.Sqrt(targetHeight * -2f * gravity);
            if (animator != null) animator.SetTrigger("Jump");
            if (audioSource != null && jumpSound != null) audioSource.PlayOneShot(jumpSound);
        }
        jumpCharge = 0f;
        StartCoroutine(FadeOutJumpUI());
    }

    private void HandleCameraFollow()
    {
        if (headBoneTarget == null || playerCamera == null) return;

        Vector3 targetPos = headBoneTarget.position + transform.TransformDirection(cameraOffset);

        if (characterController.isGrounded && currentMoveVelocity.sqrMagnitude > 0.1f)
        {
            float speedMult = isSprinting ? 1.3f : (isCrouching ? 0.8f : 1f);
            float bobAmt = isSprinting ? sprintBobAmount : (isCrouching ? crouchBobAmount : walkBobAmount);

            bobTimer += Time.deltaTime * bobSpeed * speedMult;
            currentBobX = Mathf.Cos(bobTimer * 0.5f) * (bobAmt * 0.5f);
            currentBobY = Mathf.Sin(bobTimer) * bobAmt;
        }
        else
        {
            currentBobX = Mathf.Lerp(currentBobX, 0f, Time.deltaTime * 10f);
            currentBobY = Mathf.Lerp(currentBobY, 0f, Time.deltaTime * 10f);
            if (Mathf.Abs(currentBobX) < 0.001f && Mathf.Abs(currentBobY) < 0.001f) bobTimer = 0f;
        }

        targetPos += transform.right * currentBobX + transform.up * currentBobY;

        playerCamera.transform.position = Vector3.Lerp(playerCamera.transform.position, targetPos, Time.deltaTime * cameraFollowSpeed);

        Quaternion targetRotation = Quaternion.Euler(verticalRotation, transform.eulerAngles.y, currentZTilt);
        playerCamera.transform.rotation = Quaternion.Slerp(playerCamera.transform.rotation, targetRotation, Time.deltaTime * cameraFollowSpeed);
    }

    private IEnumerator FadeOutJumpUI()
    {
        if (jumpCanvasGroup == null) yield break;
        while (jumpCanvasGroup.alpha > 0)
        {
            jumpCanvasGroup.alpha -= Time.deltaTime * uiFadeSpeed;
            yield return null;
        }
        if (jumpFillImage != null) jumpFillImage.fillAmount = 0;
    }

    private void ExitPossessionEarly()
    {
        if (ActivePossessedEnemy == this) ActivePossessedEnemy = null;

        bounceReference.InitiateReturn();
        ForceEject();
    }

    public void ForceEject()
    {
        if (vignetteFadeGroup != null) vignetteFadeGroup.alpha = 0f;
        if (timerCanvasGroup != null) timerCanvasGroup.alpha = 0f;

        isPossessed = false;
        isAiming = false;

        EnemySight meleeBrain = GetComponent<EnemySight>();
        if (meleeBrain != null) meleeBrain.enabled = true;

        EnemyGunnerSight gunnerBrain = GetComponent<EnemyGunnerSight>();
        if (gunnerBrain != null) gunnerBrain.enabled = true;

        StartCoroutine(StunRoutine());
    }

    private IEnumerator StunRoutine()
    {
        isStunned = true;
        if (animator != null) animator.SetTrigger("Stun");

        yield return new WaitForSeconds(stunDuration);

        isStunned = false;
    }
}
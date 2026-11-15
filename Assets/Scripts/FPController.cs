using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CharacterController))]
public class FPController : MonoBehaviour
{
    [Header("Camera Tracking & Visuals")]
    public Transform headBoneTarget;
    public Vector3 cameraOffset = new Vector3(0f, 0.25f, 0.05f);
    public string invisibleLayerName = "Body";
    public GameObject[] bodyMeshesToHide;
    public float cameraFollowSpeed = 30f;

    [Header("Procedural Head Bob")]
    public float bobSpeed = 14f;
    public float walkBobAmount = 0.04f;
    public float sprintBobAmount = 0.07f;
    public float crouchBobAmount = 0.02f;

    [Header("Movement Speeds")]
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

    [Header("Jump UI")]
    public Image jumpFillImage;
    public CanvasGroup jumpCanvasGroup;
    public float uiFadeSpeed = 5f;

    [Header("Crouching")]
    public float crouchHeight = 1.0f;
    public float standingHeight = 2.0f;
    public float crouchTransitionSpeed = 10f;

    [Header("Zooming (ADS)")]
    public float defaultFOV = 60f;
    public float zoomFOV = 35f;
    public float zoomSpeed = 10f;

    [Header("References")]
    public Camera playerCamera;
    public Animator animator;

    private CharacterController characterController;
    private Vector3 currentMoveVelocity;
    private Vector3 moveDampVelocity;
    private Vector2 currentMouseLook;
    private Vector2 smoothMouseVelocity;
    private float verticalRotation;
    private Vector3 velocity;

    private float jumpCharge = 0f;
    private bool isChargingJump = false;
    private bool isCrouching = false;
    private bool isSprinting = false;

    private float bobTimer = 0f;
    private float currentBobX = 0f;
    private float currentBobY = 0f;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (jumpFillImage != null) jumpFillImage.fillAmount = 0;
        if (jumpCanvasGroup != null) jumpCanvasGroup.alpha = 0;

        int hiddenLayer = LayerMask.NameToLayer(invisibleLayerName);
        if (hiddenLayer == -1) hiddenLayer = 2;

        foreach (GameObject meshObj in bodyMeshesToHide)
        {
            if (meshObj != null)
            {
                meshObj.layer = hiddenLayer;
            }
        }
    }

    void Update()
    {
        HandleMouseLook();
        HandleMovement();
        HandleCrouch();
        HandleZoom();
        HandleJumpCharge();
        HandleCameraFollow();
        UpdateAnimations();
    }

    private void UpdateAnimations()
    {
        if (animator == null) return;

        Vector3 localVelocity = transform.InverseTransformDirection(currentMoveVelocity);
        animator.SetFloat("ForwardSpeed", localVelocity.z, 0.1f, Time.deltaTime);
        animator.SetBool("IsCrouching", isCrouching);
        animator.SetBool("IsGrounded", characterController.isGrounded);
    }

    private void HandleMouseLook()
    {
        float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        Vector2 targetMouseLook = new Vector2(mouseX, mouseY);
        currentMouseLook = Vector2.SmoothDamp(currentMouseLook, targetMouseLook, ref smoothMouseVelocity, lookSmoothTime);

        verticalRotation -= currentMouseLook.y;
        verticalRotation = Mathf.Clamp(verticalRotation, -upDownRange, upDownRange);

        transform.rotation *= Quaternion.Euler(0, currentMouseLook.x, 0);
    }

    private void HandleMovement()
    {
        bool isGrounded = characterController.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        isSprinting = Input.GetKey(KeyCode.LeftShift) && !isCrouching && !isChargingJump && Input.GetAxis("Vertical") > 0;
        float targetSpeed = isCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : walkSpeed);

        if (isChargingJump) targetSpeed *= 0.5f;

        float moveHorizontal = Input.GetAxisRaw("Horizontal");
        float moveVertical = Input.GetAxisRaw("Vertical");

        Vector3 inputDir = (transform.right * moveHorizontal + transform.forward * moveVertical).normalized;
        Vector3 targetMoveVelocity = inputDir * targetSpeed;

        currentMoveVelocity = Vector3.SmoothDamp(currentMoveVelocity, targetMoveVelocity, ref moveDampVelocity, movementSmoothTime);
        characterController.Move(currentMoveVelocity * Time.deltaTime);

        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }

    private void HandleCrouch()
    {
        if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C))
            isCrouching = !isCrouching;

        float targetHeight = isCrouching ? crouchHeight : standingHeight;
        characterController.height = Mathf.Lerp(characterController.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
    }

    private void HandleZoom()
    {
        bool isZooming = Input.GetMouseButton(1);
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, isZooming ? zoomFOV : defaultFOV, Time.deltaTime * zoomSpeed);
    }

    private void HandleJumpCharge()
    {
        if (!characterController.isGrounded) return;

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

        if (isChargingJump && Input.GetKeyUp(KeyCode.Space))
        {
            ExecuteJump();
        }
    }

    private void ExecuteJump()
    {
        isChargingJump = false;

        float targetHeight = Mathf.Lerp(0f, maxJumpHeight, jumpCharge);
        if (targetHeight > 0.1f)
        {
            velocity.y = Mathf.Sqrt(targetHeight * -2f * gravity);
            if (animator != null) animator.SetTrigger("Jump");
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

        Quaternion targetRotation = Quaternion.Euler(verticalRotation, transform.eulerAngles.y, 0);
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
}
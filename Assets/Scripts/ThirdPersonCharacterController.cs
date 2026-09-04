using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCharacterController : MonoBehaviour
{
    private CharacterController characterController;
    private InputSystem_Actions inputActions;
    [SerializeField] private GameObject mainCamera;
    [SerializeField] private Animator animator;
    private StateMachine stateMachine;

    public float WalkSpeed = 5.0f;
    public float RunSpeed = 10.0f;
    public float FallMoveSpeed = 2.0f;
    public float RotationSmoothTime = 0.12f;
    public float Gravity = -15.0f;
    public bool IsGrounded = true;
    public float GroundedOffset = -0.14f;
    public float GroundedRadius = 0.28f;
    public float GroundCheckDistance = 0.5f;
    private RaycastHit groundHit;
    public LayerMask GroundLayers;

    public GameObject CinemachineCameraTarget; //follow target
    public float TopClamp = 70f;
    public float BottomClamp = -30f;
    private float cinemachineTargetYaw;
    private float cinemachineTargetPitch;
    [SerializeField] float sensitivity = 0.05f;

    private Vector2 moveInput;
    private float targetRotation = 0.0f;
    private float rotationVelocity; //for smooth damp
    private float verticalVelocity = 0.0f;
    [SerializeField] private float terminalVelocity = 53.0f;

    public bool IsMovementPressed;

    // animation IDs
    private int animIDWalk;
    private int animIDRun;
    private int animIDMovementPressed;

    private void Awake()
    {
        if (mainCamera == null)
        {
            mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
        }

        inputActions = new InputSystem_Actions();
        characterController = GetComponent<CharacterController>();
        //animator = GetComponent<Animator>();

        inputActions.Player.Move.started += OnMoveInput;
        inputActions.Player.Move.canceled += OnMoveInput;
        inputActions.Player.Move.performed += OnMoveInput;

        stateMachine = new StateMachine();
        Movement movementRootState = new Movement(stateMachine, null, this);
        stateMachine.Init(movementRootState);
    }
    private Vector3 startPosition;
    private void Start()
    {
        cinemachineTargetYaw = CinemachineCameraTarget.transform.eulerAngles.y;
        AssignAnimationIDs();
        
        startPosition = transform.position;

    }

    private void OnMoveInput(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>();
        IsMovementPressed = moveInput != Vector2.zero;
        animator.SetBool(animIDMovementPressed, IsMovementPressed);
    }

    private void OnEnable()
    {
        inputActions.Player.Enable();
    }

    private void Update()
    {
        HandleGravity();
        GroundedCheck();
        HandleRotation();

        stateMachine.Tick(Time.deltaTime);

        if (Keyboard.current.shiftKey.wasPressedThisFrame)
        {
            PlayAnimation("Dash", false);
        }
        
        
        float t = Mathf.Sin(Time.time) * 0.5f + 0.5f;
    
        Vector3 endPosition = new Vector3(
            startPosition.x + 20f,
            startPosition.y,
            startPosition.z
        );
    
        Vector3 move = Vector3.Lerp(
            startPosition,
            endPosition,
            t
        );

        characterController.Move(move);
    }
    
    public bool raww;
    public bool sloope;
    public bool rescaale;
    // private void OnAnimatorMove()
    // {
    //     //if (!IsMovementPressed) return;
    //
    //     Vector3 raw = animator.deltaPosition; // cache ONCE
    //     Vector3 slope = SlopeCorrection(raw);
    //     Vector3 rescale = slope.normalized * raw.magnitude;
    //
    //     //Vector3 velocity = animator.deltaPosition;
    //
    //     Vector3 postGravRaw = raw;
    //     postGravRaw.y += verticalVelocity * Time.deltaTime; //apply gravity and vertical velocites like jump etc
    //
    //     Vector3 postGravSlope = slope;
    //     postGravSlope.y += verticalVelocity * Time.deltaTime; //apply gravity and vertical velocites like jump etc
    //
    //     Vector3 postGravRescale = rescale;
    //     postGravRescale.y += verticalVelocity * Time.deltaTime; //apply gravity and vertical velocites like jump etc
    //
    //     if (raww) characterController.Move(postGravRaw);
    //     else if (sloope) characterController.Move(postGravSlope);
    //             else if (rescaale) characterController.Move(postGravRescale);
    //     
    //
    //     //Debug.Log($"raw={raw.magnitude:F4} | slope={slope.magnitude:F4} | rescaled={rescale.magnitude:F4} | " +
    //         //$"postGravRaw={postGravRaw.magnitude:F4} | postGravSlope={postGravSlope.magnitude:F4} | postGravRescale={postGravRescale.magnitude:F4}");
    //
    //
    // }

    private void LateUpdate()
    {
        CameraRotation(); 
    }

    public void PlayAnimation(string targetAnimation, bool rootMotion = true, float transitionTime = 0.25f)
    {
        animator.applyRootMotion = rootMotion;
        animator.CrossFade(targetAnimation, transitionTime);
    }

    private void AssignAnimationIDs()
    {
        animIDWalk = Animator.StringToHash("isWalking");
        animIDRun = Animator.StringToHash("isRunning");
        animIDMovementPressed = Animator.StringToHash("MovementPressed");
    }

    private void HandleAnimation()
    {
        bool isWalking = animator.GetBool(animIDWalk);
        bool isRunning = animator.GetBool(animIDRun);

        if (IsMovementPressed && !isWalking)
        {
            animator.SetBool(animIDWalk, true);
        }
        else if (!IsMovementPressed && isWalking)
        {
            animator.SetBool(animIDWalk, false);
        }
    }

    private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        // wrap angles back
        if (lfAngle < -360f) lfAngle += 360f;
        if (lfAngle > 360f) lfAngle -= 360f;

        // clamp
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }

    private void CameraRotation()
    {
        cinemachineTargetYaw += inputActions.Player.Look.ReadValue<Vector2>().x * sensitivity;
        cinemachineTargetPitch -= inputActions.Player.Look.ReadValue<Vector2>().y * sensitivity;

        // clamp our rotations so our values are limited 360 degrees
        cinemachineTargetYaw = ClampAngle(cinemachineTargetYaw, float.MinValue, float.MaxValue);
        cinemachineTargetPitch = ClampAngle(cinemachineTargetPitch, BottomClamp, TopClamp);

        // Cinemachine will follow this target
        CinemachineCameraTarget.transform.rotation = Quaternion.Euler(cinemachineTargetPitch, cinemachineTargetYaw, 0.0f);
    }

    public void Move(float moveSpeed)
    {
        // take forward and rotate it by targetRotation to get the direction the player should move
        Vector3 targetDirection = Quaternion.Euler(0.0f, targetRotation, 0.0f) * Vector3.forward;

        // move the player
        //Vector3 horizontalMovement = targetDirection.normalized * (MoveSpeed * Time.deltaTime);
        //Vector3 verticalMovement = new Vector3(0.0f, velocity.y, 0.0f) * Time.deltaTime;
        //characterController.Move(horizontalMovement + verticalMovement);

        //characterController.Move(targetDirection.normalized * (MoveSpeed * Time.deltaTime)); //no gravity

        //velocity = new Vector3(
        //    targetDirection.x * moveSpeed,
        //    velocity.y,
        //    targetDirection.z * moveSpeed
        //    );
    }

    private void HandleRotation()
    {
        // normalise input direction unity input system alr does it but whatever
        Vector3 inputDirection = new Vector3(moveInput.x, 0.0f, moveInput.y).normalized;

        if (inputDirection != Vector3.zero)
        {
            // change input direction to angle then offset by the camera y rot 
            targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + mainCamera.transform.eulerAngles.y;
        }

        float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetRotation, ref rotationVelocity, RotationSmoothTime);

        // rotate to face input direction relative to camera position
        transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);

        //Vector3 inputDirection = new Vector3(moveInput.x, 0.0f, moveInput.y).normalized;

        //if (inputDirection != Vector3.zero)
        //{
        //    Vector3 targetRotationDirection =
        //        mainCamera.transform.forward * inputDirection.z +
        //        mainCamera.transform.right * inputDirection.x;

        //    targetRotationDirection.Normalize();
        //    targetRotationDirection.y = 0;

        //    Quaternion newRotation = Quaternion.LookRotation(targetRotationDirection);

        //    Quaternion targetRot = Quaternion.Slerp(
        //        transform.rotation,
        //        newRotation,
        //        RotationSmoothTime * Time.deltaTime
        //    );

        //    transform.rotation = targetRot;
        //}
    }

    private void GroundedCheck()
    {
        // set sphere position, with offset
        //Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
        //IsGrounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);

        Vector3 spherePos = transform.position + Vector3.down * GroundedOffset;

        IsGrounded = Physics.SphereCast(spherePos, GroundedRadius, Vector3.down, out groundHit, GroundCheckDistance, GroundLayers, QueryTriggerInteraction.Ignore);
    }

    private Vector3 SlopeCorrection(Vector3 characterVelocity)
    {
        if (IsGrounded)
        {
            float dot = Vector3.Dot(Vector3.up, groundHit.normal);
            if (dot != -1 && verticalVelocity <= 0)
            {
                return Vector3.ProjectOnPlane(characterVelocity, groundHit.normal);
            }
        }

        return characterVelocity;
    }

    private void HandleGravity()
    {
        // stop velocity from dropping infinitely while grounded
        if (IsGrounded && verticalVelocity < 0.0f)
        {
            verticalVelocity = -2f; //keeps character grounded by slightly pushing down, but not too much to cause issues with slopes
        }

        // apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
        if (verticalVelocity > -terminalVelocity)
        {
            verticalVelocity += Gravity * Time.deltaTime;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 spherePos = transform.position + Vector3.down * GroundedOffset;

        // sphere at cast origin
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(spherePos, GroundedRadius);

        // sphere at the end of the cast (max distance traveled)
        Gizmos.color = IsGrounded? Color.green: Color.red;
        Gizmos.DrawWireSphere(spherePos + Vector3.down * GroundCheckDistance, GroundedRadius);

        Debug.DrawLine(spherePos, spherePos + Vector3.down * GroundCheckDistance, Color.cyan);
    }
    


}

using UnityEngine;
using UnityEngine.InputSystem;


public class AnimationAndMovementController : MonoBehaviour
{
    InputSystem_Actions playerInput;
    CharacterController characterController;
    Animator animator;

    Vector2 currentMoveInput = Vector2.zero;
    Vector3 currentMovement = Vector3.zero;
    bool isMovementPressed;

    [SerializeField] float moveSpeed = 2.0f;
    [SerializeField] float rotationSpeed = 5.0f;

    private void Awake()
    {
        playerInput = new InputSystem_Actions();
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        playerInput.Player.Move.started += OnMoveInput;
        playerInput.Player.Move.canceled += OnMoveInput;
        playerInput.Player.Move.performed += OnMoveInput;
    }

    private void OnEnable()
    {
        playerInput.Player.Enable();
    }

    private void OnDisable()
    {
        playerInput.Player.Disable();
    }

    private void Look()
    {

    }
   
    private void Update()
    {
        characterController.Move(currentMovement * moveSpeed * Time.deltaTime);
        HandleAnimation();
        HandleRotation();
    }

    private void OnMoveInput(InputAction.CallbackContext ctx)
    {
        currentMoveInput = ctx.ReadValue<Vector2>();
        currentMovement.x = currentMoveInput.x;
        currentMovement.z = currentMoveInput.y;
        isMovementPressed = currentMovement.x != 0 || currentMovement.z != 0;
    }

    private void HandleAnimation()
    {
        bool isWalking = animator.GetBool("isWalking");
        bool isRunning = animator.GetBool("isRunning");

        if (isMovementPressed && !isWalking)
        {
            animator.SetBool("isWalking", true);
        }
        else if (!isMovementPressed && isWalking)
        {
            animator.SetBool("isWalking", false);
        }
    }

    private void HandleRotation()
    {
        Vector3 positionToLookAt;
        positionToLookAt.x = currentMovement.x;
        positionToLookAt.y = 0.0f;
        positionToLookAt.z = currentMovement.z;
        Quaternion currentRotation = transform.rotation;
        if (isMovementPressed)
        {
            Quaternion targetRotation = Quaternion.LookRotation(positionToLookAt);
            transform.rotation = Quaternion.Slerp(currentRotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }
}

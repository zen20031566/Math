using UnityEngine;
using UnityEngine.InputSystem;

public class Airborne : State
{
    private ThirdPersonCharacterController ctx;

    public Airborne(StateMachine stateMachine, State parent, ThirdPersonCharacterController ctx) : base(stateMachine, parent)
    {
        this.ctx = ctx;
    }

    protected override State GetInitialState() => null;

    protected override State GetTransition()
    {
        if (ctx.IsGrounded) return ((Movement)Parent).Grounded;

        return null;
    }


    protected override void OnTick(float deltaTime)
    {
        ctx.Move(ctx.FallMoveSpeed);
    }
}

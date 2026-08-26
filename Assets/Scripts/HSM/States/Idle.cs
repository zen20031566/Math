using UnityEngine;
using UnityEngine.InputSystem;

public class Idle : State
{
    private ThirdPersonCharacterController ctx;

    public Idle(StateMachine stateMachine, State parent, ThirdPersonCharacterController ctx) : base(stateMachine, parent)
    {
        this.ctx = ctx;
    }

    protected override State GetInitialState() => null;

    protected override State GetTransition()
    {
        if (ctx.IsMovementPressed) return ((Grounded)Parent).Walk;

        return null;
    }

    protected override void OnEnter()
    {
        ctx.PlayAnimation("Idle", true);
    }

    protected override void OnExit() { }

    protected override void OnTick(float deltaTime) { }
}

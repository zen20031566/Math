using UnityEngine;

public class Walk : State
{
    private ThirdPersonCharacterController ctx;

    public Walk(StateMachine stateMachine, State parent, ThirdPersonCharacterController ctx) : base(stateMachine, parent)
    {
        this.ctx = ctx;
    }

    protected override State GetInitialState() => null;

    protected override State GetTransition()
    {
        if (!ctx.IsMovementPressed) return ((Grounded)Parent).Idle;

        return null;
    }

    protected override void OnEnter() 
    {
        ctx.PlayAnimation("Walk", true, 0.0f);
    }

    protected override void OnExit() { }

    protected override void OnTick(float deltaTime)
    {

    }
}

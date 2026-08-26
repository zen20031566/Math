using UnityEngine;

public class Grounded : State
{
    public readonly Idle Idle;
    public readonly Walk Walk;
    private ThirdPersonCharacterController ctx;

    public Grounded(StateMachine stateMachine, State parent, ThirdPersonCharacterController ctx) : base(stateMachine, parent)
    {
        this.ctx = ctx;
        Idle = new Idle(stateMachine, this, ctx);
        Walk = new Walk(stateMachine, this, ctx);
    }

    protected override State GetInitialState() => Idle;

    protected override State GetTransition()
    {
        if (!ctx.IsGrounded) return ((Movement)Parent).Airborne;

        return null;
    }


    protected override void OnTick(float deltaTime) { }
}

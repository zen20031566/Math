using UnityEngine;

public class Movement : State
{
    public readonly Grounded Grounded;
    public readonly Airborne Airborne;
    private ThirdPersonCharacterController ctx;

    public Movement(StateMachine stateMachine, State parent, ThirdPersonCharacterController ctx) : base(stateMachine, parent)
    {
        this.ctx = ctx;
        Grounded = new Grounded(stateMachine, this, ctx);
        Airborne = new Airborne(stateMachine, this, ctx);
    }

    protected override State GetInitialState() => Grounded;

    protected override void OnEnter() { }
    protected override void OnExit() { }
    protected override void OnTick(float deltaTime) { }
}

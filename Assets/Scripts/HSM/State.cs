using UnityEngine;

public abstract class State
{
    public readonly StateMachine StateMachine;
    public readonly State Parent;
    public State ActiveChild;

    public State(StateMachine stateMachine, State parent = null)
    {
        StateMachine = stateMachine;
        Parent = parent;
    }

    protected virtual State GetInitialState() => null; //initial child to enter when this state starts (null = this is the leaf)
    protected virtual State GetTransition() => null; //target state to switch to this frame (null = stay in current state)

    protected virtual void OnEnter()
    {
        Debug.Log(this + "Ennter");
    }

    protected virtual void OnExit()
    {
        Debug.Log(this + "Exit");
    }
    protected virtual void OnTick(float deltaTime) { }

    public void Enter()
    {
        //set parent 
        if (Parent != null) Parent.ActiveChild = this;

        //run enter logic
        OnEnter();

        //check for initial child state and enter
        State initialState = GetInitialState();
        if (initialState != null)
        {
            initialState.Enter();
        }
    }

    public void Exit()
    {
        //clear child
        if (ActiveChild != null)
            //ActiveChild.Exit();
            ActiveChild = null;

        //run exit logic
        OnExit();
    }

    public void Tick(float deltaTime)
    {
        //check for transitions
        State transitionState = GetTransition();
        if (transitionState != null)
        {
            StateMachine.ChangeState(this, transitionState);
            return;
        }

        //tick child
        if (ActiveChild != null) ActiveChild.Tick(deltaTime);

        //run tick logic
        OnTick(deltaTime);
    }

    //better maybe
    //protected virtual State GetTransition(float value)
    //{
    //    foreach (TransitionPair transition in transitions)
    //    {
    //        if (transition.predicate(value))
    //            return transition.nextState;
    //    }

    //    return null;
    //}
}

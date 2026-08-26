using UnityEngine;

public class TransitionSequencer : MonoBehaviour
{
    public readonly StateMachine StateMachine;

    public TransitionSequencer(StateMachine stateMachine)
    {
        StateMachine = stateMachine;
    }

    //request a transition from one state to another
    public void RequestTransition(State from, State to)
    {
        StateMachine.ChangeState(from, to);
    }
}

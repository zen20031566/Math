using System.Collections.Generic;
using UnityEngine;

public class StateMachine
{

    public State Root {  get; private set; }
    public TransitionSequencer TransitionSequencer {  get; private set; }
    private bool started;

    public void Init(State root)
    {
        Root = root;
        Root.Enter();
        started = true;
    }

    public void Tick(float deltaTime)
    {
        if (started) Root.Tick(deltaTime);
    }

    public void ChangeState(State from, State to)
    {
        if (from == to || from == null || to == null) return;

        State lca = Lca(from, to);

        // Exit current branch up to (but not including) LCA
        for (State s = from; s != lca; s = s.Parent) s.Exit();

        // Enter target branch from LCA down to target
        var stack = new Stack<State>();
        for (State s = to; s != lca; s = s.Parent) stack.Push(s);
        while (stack.Count > 0) stack.Pop().Enter();

        // Print hierarchy after the transition
        PrintActiveHierarchy();
    }

    // Compute the Lowest Common Ancestor of two states.
    public static State Lca(State a, State b)
    {
        // Create a set of all parents of 'a'
        var ap = new HashSet<State>();
        for (var s = a; s != null; s = s.Parent) ap.Add(s);

        // Find the first parent of 'b' that is also a parent of 'a'
        for (var s = b; s != null; s = s.Parent)
            if (ap.Contains(s)) return s;

        // If no common ancestor found, return null
        return null;
    }

    private void PrintActiveHierarchy()
    {
        State current = Root;

        string hierarchy = current.GetType().Name;

        while (current.ActiveChild != null)
        {
            current = current.ActiveChild;
            hierarchy += " -> " + current.GetType().Name;
        }

        Debug.Log($"State Hierarchy: {hierarchy}");
    }
}




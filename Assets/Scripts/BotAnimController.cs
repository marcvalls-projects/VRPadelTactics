using System;
using System.Collections;
using System.Collections.Generic;
using Palmmedia.ReportGenerator.Core.Parser.Analysis;
using UnityEngine;

public class BotAnimController : MonoBehaviour
{
    public Animator animator;

    public enum State
    {
        Idle,
        RunningForward,
        Hitting,
        RunningBackward
    }
    
    private State state = State.Idle;

    private static readonly Dictionary<State, int> stateIdentifier =
        new Dictionary<State, int>() { { State.Idle, 0 }, { State.RunningForward, 1 }, { State.Hitting, 2 }, {State.RunningBackward, 3} };

    private void SwitchToState(State newState)
    {
        state = newState;
        animator.SetInteger("animState", stateIdentifier[newState]);
    }

    public void StayIdle()
    {
        SwitchToState(State.Idle);
    }
    
    private void LookAt(Transform mannequin, Vector3 target)
    {
        Vector3 direction = target - mannequin.position;
        direction.y = 0;
        
        if (direction.magnitude > 0)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            mannequin.rotation = lookRotation;
        }
    }

    private IEnumerator SlideTo(Vector3 position)
    {
        float elapsedTime = 0;
        float velocity = 1.2f;
        float time = Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(position.x, position.z))/velocity;
        
        while (elapsedTime < time)
        {
            yield return null;
            transform.position += new Vector3(position.x - transform.position.x, 0, position.z - transform.position.z).normalized * velocity * Time.deltaTime;
            elapsedTime += Time.deltaTime;
        }
        
        LookAt(transform, new Vector3(transform.position.x, 0, 0));
        SwitchToState(State.Idle);
    }

    public void RunTo(Vector3 position)
    {
        if (Mathf.Abs(position.z) <= Mathf.Abs(transform.position.z))
        {
            LookAt(transform, position);
            SwitchToState(State.RunningForward);
        }
        else
        {
            LookAt(transform, -position);
            SwitchToState(State.RunningBackward);
        }
        StartCoroutine(SlideTo(position));
    }

    private IEnumerator HitSequence()
    {
        SwitchToState(State.Hitting);
        yield return new WaitForSeconds(105f/30f);
        SwitchToState(State.Idle);
    }

    public void Hit()
    {
        StartCoroutine(HitSequence());
    }
    
    void Start()
    {

    }
    
    void Update()
    {
        
    }
}

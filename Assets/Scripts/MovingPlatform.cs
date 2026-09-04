using System;
using UnityEngine;
using System.Collections.Generic;
public class MovingPlatform : MonoBehaviour
{
    [SerializeField] private Transform point1;
    private Vector3 startPosition;
    private void Start()
    {
        startPosition = transform.position;
        lastPosition = transform.position;
    }

    private void Update()
    {
        float t = Mathf.Sin(Time.time) * 0.5f + 0.5f;
    
        Vector3 endPosition = new Vector3(
            startPosition.x + 20f,
            startPosition.y,
            startPosition.z
        );
    
        transform.position = Vector3.Lerp(
            startPosition,
            endPosition,
            t
        );
    }
    
    private Vector3 lastPosition;
    private List<CharacterController> riders = new List<CharacterController>();
    

    void OnTriggerEnter(Collider other)
    {
        CharacterController cc = other.GetComponent<CharacterController>();
        if (cc != null) riders.Add(cc);
    }

    void OnTriggerExit(Collider other)
    {
        CharacterController cc = other.GetComponent<CharacterController>();
        if (cc != null) riders.Remove(cc);
    }

    void FixedUpdate()
    {
        Vector3 delta = transform.position - lastPosition;
        if (delta != Vector3.zero)
        {
            foreach (var rider in riders)
                rider.Move(delta);
        }
        lastPosition = transform.position;
    }
    
}

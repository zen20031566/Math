using UnityEngine;

public class ProjectingVectorOntoPlane : MonoBehaviour
{
    [SerializeField] Transform a;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float dot = Vector3.Dot(a.position - transform.position, transform.up);
        Vector3 projected = (a.position - transform.position) - dot * transform.up;
        a.transform.forward = projected.normalized;
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawLine(transform.position, transform.position + transform.up);

        float dot = Vector3.Dot(a.position - transform.position, transform.up);
        Vector3 projected = (a.position - transform.position) - dot * transform.up;
        Gizmos.DrawLine(transform.position, transform.position + projected);
    }
}

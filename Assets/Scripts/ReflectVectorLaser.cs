using UnityEditor;
using UnityEngine;

public class ReflectVectorLaser : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Vector3 headPos = transform.position;
        Vector3 lookDir = transform.forward;

        Handles.color = Color.red;
        Handles.DrawAAPolyLine(headPos, headPos + lookDir * 1f);

        if (Physics.Raycast(headPos, lookDir, out RaycastHit hit))
        {
            Handles.color = Color.green;
            Handles.DrawAAPolyLine(headPos, hit.point);

            //Reflect vector this is vector3 reflect but manual formula
            //w = v - 2 * (v dot n) * n
            Vector3 reflected = lookDir - 2 * (Vector3.Dot(lookDir, hit.normal)) * hit.normal;

            Handles.color = Color.cyan;
            Handles.DrawAAPolyLine(hit.point, hit.point + reflected * 1f);
        }
    }
}

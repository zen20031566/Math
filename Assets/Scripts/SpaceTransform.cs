using System.Collections.ObjectModel;
using UnityEngine;

public class SpaceTransform : MonoBehaviour
{
    public Vector2 localSpacePoint;
    public Vector2 worldSpacePoint;
    public Transform localSpaceObj;

    private void OnDrawGizmos()
    {
        Vector3 objPos = transform.position;    
        Vector3 right = transform.right;
        Vector3 up = transform.forward;

        DrawBasisVectors(objPos, right, up);
        DrawBasisVectors(Vector3.zero, Vector3.right, Vector3.forward);

        Gizmos.color = Color.cyan;

        Vector3 spherePos = right * localSpacePoint.x + up * localSpacePoint.y;
        spherePos += objPos;


        Gizmos.DrawSphere(new Vector3(spherePos.x, 0, spherePos.z), 0.2f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(new Vector3(worldSpacePoint.x, 0, worldSpacePoint.y), 0.2f);

        Vector2 relPoint = worldSpacePoint - new Vector2(objPos.x, objPos.z);
        float x = Vector2.Dot(relPoint, new Vector2(right.x, right.z));
        float y = Vector2.Dot(relPoint, new Vector2(up.x, up.z));

        localSpaceObj.localPosition = new Vector3(x, 0, localSpaceObj.localPosition.z);

        //Local to world
        //transform.TransformPoint

        //World to local
        //transform.InverseTransformPoint


    }

    private void DrawBasisVectors(Vector3 pos, Vector3 right, Vector3 up)
    {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(pos, right); 

        Gizmos.color = Color.green;
        Gizmos.DrawRay(pos, up);
        Gizmos.color = Color.white;


    }

}

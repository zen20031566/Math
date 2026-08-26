using UnityEditor;
using UnityEngine;

public class CrossProductThing : MonoBehaviour
{
    public Transform turret;
    public float turretRotation = 0f;
    void DrawRay(Vector3 pos, Vector3 dir, Color color)
    {
        Handles.color = color;
        Handles.DrawAAPolyLine(pos, pos + dir);
    }

    private void OnDrawGizmos()
    {
        Vector3 headPos = transform.position;
        Vector3 lookDir = transform.forward;

        if (Physics.Raycast(headPos, transform.forward, out RaycastHit hit))
        {
            Vector3 up = hit.normal;
            Vector3 right = Vector3.Cross(lookDir, up).normalized; //Cross look dir with normal to get right or x axis
            Vector3 forward = Vector3.Cross(up, right).normalized; //Cross right with normal to get forward or z axis

            //Quaternion yaw = Quaternion.AngleAxis(turretRotation, up);
            //right = yaw * right;
            //forward = yaw * forward;

            DrawRay(headPos, hit.point - headPos, Color.white);

            //Rotation correctly on surface like example when placing turrent on uneven surface
            DrawRay(hit.point, up, Color.green);
            DrawRay(hit.point, right, Color.red);
            DrawRay(hit.point, forward, Color.blue);

            
            //turret.up = up;
            //turret.right = right;
            //turret.forward = forward;

            Quaternion turretRot = Quaternion.LookRotation(forward, up);

            turret.rotation = turretRot;
            turret.position = hit.point;

            //Use matrix for mathematical representation rather than using the transform component directly
            Matrix4x4 turretToWorld = Matrix4x4.TRS(hit.point, turretRot, Vector3.one * 0.25f);
            Matrix4x4 worldToTurret = turretToWorld.inverse;

            //Points
            Vector3[] pts = new Vector3[] 
            {
            new Vector3( 1, 0, 1 ),  // bottom 4 positions
            new Vector3(-1, 0, 1 ),
            new Vector3(-1, 0, -1 ),
            new Vector3( 1, 0, -1 ),

            new Vector3( 1, 2, 1 ),  // top 4 positions
            new Vector3(-1, 2, 1 ),
            new Vector3(-1, 2, -1 ),
            new Vector3( 1, 2, -1 )
            };

            Gizmos.color = Color.red;
            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 worldPoint = turretToWorld.MultiplyPoint3x4(pts[i]);
                Gizmos.DrawSphere(worldPoint, 0.03f);
            }

            //You can make it so if the lookdir and normal is parallel then instead use up vector of player instead of forward vector
        }
        else
        {
            DrawRay(headPos, lookDir, Color.red);
        }
    }
}

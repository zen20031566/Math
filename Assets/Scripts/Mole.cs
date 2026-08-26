using UnityEditor;
using UnityEngine;

public class Mole : MonoBehaviour
{
    public float maxBoreDistance;
    public float maxGap;

    private void OnDrawGizmos()
    {
        Vector3 headPos = transform.position;
        Vector3 lookDir = transform.forward;

        //If we find valid surface that can spawn start portal
        if (Physics.Raycast(headPos, lookDir, out RaycastHit hit))
        {
            Vector3 startPoint = hit.point;
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(startPoint, 0.2f);

            Vector3 maxPoint = startPoint + (lookDir * maxBoreDistance);

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(maxPoint, 0.2f);

            float hitLength = Vector3.Distance(maxPoint, startPoint);

            RaycastHit[] frontHits = Physics.RaycastAll(startPoint, lookDir, hitLength);
            System.Array.Sort(frontHits, (a, b) => a.distance.CompareTo(b.distance)); //sort accending by distance to ray origin

            RaycastHit[] backHits = Physics.RaycastAll(maxPoint, -lookDir, hitLength);
            System.Array.Sort(backHits, (a, b) => b.distance.CompareTo(a.distance)); //sort decending by distance to ray origin

            if (backHits.Length == 0 || frontHits.Length == 0) return;

            //Compare the hits from front and back to find the largest gap between them
            Vector3 validGap = Vector3.zero;
            for (int i = 0; i < backHits.Length - 1; i++)
            {
                //For visualization purposes, draw the hits from front and back
                

                float dis = Vector3.Distance(backHits[i].point, frontHits[i].point);

                if (dis > maxGap)
                {
                    validGap = backHits[i].point;
                    break;
  
                }

                Gizmos.color = Color.red;
                Gizmos.DrawSphere(backHits[i].point, 0.2f);

                Gizmos.color = Color.green;
                Gizmos.DrawSphere(frontHits[i].point, 0.2f);
            }

            if(validGap == Vector3.zero)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(backHits[backHits.Length-1].point, 0.25f);
            }
            else
                            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(validGap, 0.25f);
            }

        }
    }
}

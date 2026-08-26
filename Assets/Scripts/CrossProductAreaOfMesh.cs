using UnityEngine;

public class CrossProductAreaOfMesh : MonoBehaviour
{
    public Mesh mesh;
    public float area = 0f;

    private void OnValidate()
    {
        //Cache these accessing is expensive
        Vector3[] vertices = mesh.vertices;

        //Show each vertices
        int[] triangles = mesh.triangles;

        area = 0f;
        for (int i = 0; i < triangles.Length; i += 3) //loop per triple cause triangle
        {
            Vector3 a = vertices[triangles[i]];
            Vector3 b = vertices[triangles[i + 1]];
            Vector3 c = vertices[triangles[i + 2]];

            area += Vector3.Cross(b - a, c - a).magnitude;
        }
        area *= 0.5f; //Area of triangle is half the cross product magnitude
        Debug.Log($"Area of mesh {mesh.name} is {area}");
    }
}

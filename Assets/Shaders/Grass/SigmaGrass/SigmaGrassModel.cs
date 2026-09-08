using UnityEngine;

[CreateAssetMenu(menuName = "SigmaGraphics/SigmaGrassModel")]
public class SigmaGrassModel : ScriptableObject
{
    public Mesh Mesh;
    public Material  Material;
    public Mesh LODMesh;
    public Vector2 Scale = Vector2.one;
    public Vector2 ScaleVariationRange = new Vector2(0.6f, 1.0f);
}

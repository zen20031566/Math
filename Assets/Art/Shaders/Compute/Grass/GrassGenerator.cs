using System;
using UnityEngine;

struct GrassData 
{
    Vector4 position;
    Vector2 uv;
    public float displacement;
};

public class GrassGenerator : MonoBehaviour
{
    [SerializeField] private ComputeShader computeShader;
    [SerializeField] private int resolution = 10;
    [SerializeField] private Material material;
    [SerializeField] private Mesh mesh;
    [SerializeField] private float heightOffset = -0.2f;
    
    private ComputeBuffer grassDataBuffer;
    private ComputeBuffer argsBuffer;

    [SerializeField] private int alphaMapIndex;
    
    private Terrain terrain; 
    private bool updateGrass = false;
    
    Bounds bounds;  
    
    void OnEnable()
    {
        terrain =  Terrain.activeTerrain;
        grassDataBuffer = new ComputeBuffer(resolution * resolution, sizeof(float) * 7); //number of floats: position(float4) uv(float2) displacement(float) 4 + 2 + 1 = 7
        argsBuffer = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);
        
        UpdateGrassBuffer();
    }
    
    void OnDisable () 
    {
        grassDataBuffer.Release();
        grassDataBuffer = null;
    }

    void UpdateGrassBuffer()
    {
        computeShader.SetInt("_Resolution", resolution);
        computeShader.SetBuffer(0, "_GrassDataBuffer", grassDataBuffer);
        
        Vector3 terrainPosition = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size; 
        Texture heightMap = terrain.terrainData.heightmapTexture;
        Texture2D alphaMap = terrain.terrainData.GetAlphamapTexture(alphaMapIndex);
        
        computeShader.SetVector("_TerrainPosition", terrainPosition);
        computeShader.SetVector("_TerrainSize", terrainSize);
        computeShader.SetTexture(0, "_HeightMap", heightMap);
        computeShader.SetTexture(0, "_AlphaMap", alphaMap);
        computeShader.SetFloat("_HeightOffset", heightOffset);
        
        int groups = Mathf.CeilToInt(resolution / 8f);
        computeShader.Dispatch(0, groups, groups, 1);
        
        GrassData[] debugData = new GrassData[grassDataBuffer.count];
grassDataBuffer.GetData(debugData);

float minVal = float.MaxValue;
float maxVal = float.MinValue;
foreach (var d in debugData)
{
    minVal = Mathf.Min(minVal, d.displacement);
    maxVal = Mathf.Max(maxVal, d.displacement);
}
Debug.Log($"Raw heightmap min: {minVal}, max: {maxVal}");

        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        // Arguments for drawing mesh.
        args[0] = (uint)mesh.GetIndexCount(0); //number of triangle indices
        args[1] = (uint)grassDataBuffer.count; //instance count
        args[2] = (uint)mesh.GetIndexStart(0);
        args[3] = (uint)mesh.GetBaseVertex(0);
        argsBuffer.SetData(args);

        material.SetBuffer("_GrassDataBuffer", grassDataBuffer);
        
        bounds = new Bounds(terrainPosition + terrainSize * 0.5f, terrainSize);
    }

    private void Update()
    {
        if (updateGrass) UpdateGrassBuffer();   
        
        Graphics.DrawMeshInstancedIndirect(mesh, 0, material, bounds, argsBuffer);
    }
}

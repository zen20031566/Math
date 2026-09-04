using System;
using UnityEngine;

public class GrassGenerator : MonoBehaviour
{
    [SerializeField] private ComputeShader computeShader;
    [SerializeField, Range(0, 1000)] private int resolution = 100;
    [SerializeField] private Material material;
    [SerializeField] private Mesh mesh;
    
    [SerializeField] private Vector2 scale = Vector2.one;
    [SerializeField] private Vector2 scaleVariationRange = new Vector2(0.6f, 1.0f);
    
    private ComputeBuffer grassDataBuffer;
    private ComputeBuffer argsBuffer;

    private int kernel;
    
    [SerializeField] private int alphaMapIndex;
    
    private Terrain terrain; 
    [SerializeField] private bool updateGrass = false;
    
    Bounds bounds;  
    
    void OnEnable()
    {
        kernel = computeShader.FindKernel("InitializeGrass");
        terrain =  Terrain.activeTerrain;
        grassDataBuffer = new ComputeBuffer(resolution * resolution, sizeof(float) * 12); //number of floats: position(float4) uv(float2) displacement(float) 4 + 2 + 1 = 7
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
        if (grassDataBuffer == null || grassDataBuffer.count != resolution * resolution)
        {
            grassDataBuffer?.Release();
            grassDataBuffer = new ComputeBuffer(resolution * resolution, sizeof(float) * 12);
        }

        computeShader.SetInt("_Resolution", resolution);
        computeShader.SetBuffer(kernel, "_GrassDataBuffer", grassDataBuffer);
        
        Vector3 terrainPosition = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size; 
        Texture heightMap = terrain.terrainData.heightmapTexture;
        Texture2D alphaMap = terrain.terrainData.GetAlphamapTexture(alphaMapIndex);
        Texture normalMap = terrain.normalmapTexture;   
        
        computeShader.SetVector("_TerrainPosition", terrainPosition);
        computeShader.SetVector("_TerrainSize", terrainSize);
        
        computeShader.SetTexture(kernel, "_HeightMap", heightMap);
        //computeShader.SetTexture(0, "_AlphaMap", alphaMap);
        computeShader.SetTexture(kernel, "_NormalMap", normalMap);
        
        computeShader.SetVector("_Scale", scale);
        computeShader.SetVector("_ScaleVariationRange", scaleVariationRange);
        
        int groups = Mathf.CeilToInt(resolution / 8f);
        computeShader.Dispatch(kernel, groups, groups, 1);

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

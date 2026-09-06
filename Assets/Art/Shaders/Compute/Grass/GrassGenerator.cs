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

    private int initGrassKernel;
    
    [SerializeField] private int alphaMapIndex;
    
    private Terrain terrain; 
    [SerializeField] private bool updateGrass = false;
    
    Bounds bounds;  
    
    //Cull grass
    [SerializeField] private ComputeShader cullComputeShader;
    private ComputeBuffer voteBuffer;
    private int voteKernel;
    
    private int numThreadGroups;
    private int numVoteThreadGroups;
    private int numGroupScanThreadGroups;
    
    void OnEnable()
    {
        initGrassKernel = computeShader.FindKernel("InitializeGrass");
        terrain =  Terrain.activeTerrain;
        grassDataBuffer = new ComputeBuffer(resolution * resolution, sizeof(float) * 12); //number of floats: position(float4) uv(float2) displacement(float) 4 + 2 + 1 = 7
        argsBuffer = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);
        
        voteKernel = computeShader.FindKernel("Vote");
        voteBuffer = new ComputeBuffer(resolution * resolution, sizeof(uint));
        
        
        numThreadGroups = Mathf.CeilToInt(resolution * resolution/ 128.0f);
        //we want power 2 numbers because the group scan algo needs it? i dont really understand but yes
        if (numThreadGroups > 128) 
        {
            int powerOfTwo = 128;
            while (powerOfTwo < numThreadGroups)
                powerOfTwo *= 2;
            
            numThreadGroups = powerOfTwo;
        } 
        else 
        {
            while (128 % numThreadGroups != 0)
                numThreadGroups++;
        }
        
        numVoteThreadGroups = Mathf.CeilToInt(resolution * resolution / 128.0f);
        numGroupScanThreadGroups = Mathf.CeilToInt(resolution * resolution / 1024.0f);
        
        UpdateGrassBuffer();
    }
    
    void OnDisable () 
    {
        grassDataBuffer.Release();
        grassDataBuffer = null;
        
        voteBuffer.Release();
        voteBuffer = null;
    }

    void CullGrass()
    {
        
    }
    
    void UpdateGrassBuffer()
    {
        if (grassDataBuffer == null || grassDataBuffer.count != resolution * resolution)
        {
            grassDataBuffer?.Release();
            grassDataBuffer = new ComputeBuffer(resolution * resolution, sizeof(float) * 12);
        }

        computeShader.SetInt("_Resolution", resolution);
        computeShader.SetBuffer(initGrassKernel, "_GrassDataBuffer", grassDataBuffer);
        
        Vector3 terrainPosition = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size; 
        Texture heightMap = terrain.terrainData.heightmapTexture;
        Texture2D alphaMap = terrain.terrainData.GetAlphamapTexture(alphaMapIndex);
        Texture normalMap = terrain.normalmapTexture;   
        
        computeShader.SetVector("_TerrainPosition", terrainPosition);
        computeShader.SetVector("_TerrainSize", terrainSize);
        
        computeShader.SetTexture(initGrassKernel, "_HeightMap", heightMap);
        //computeShader.SetTexture(0, "_AlphaMap", alphaMap);
        computeShader.SetTexture(initGrassKernel, "_NormalMap", normalMap);
        
        computeShader.SetVector("_Scale", scale);
        computeShader.SetVector("_ScaleVariationRange", scaleVariationRange);
        
        int threadGroups = Mathf.CeilToInt(resolution / 8f);
        computeShader.Dispatch(initGrassKernel, threadGroups, threadGroups , 1);

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

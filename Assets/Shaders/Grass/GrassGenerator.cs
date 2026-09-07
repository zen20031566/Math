using System;
using UnityEngine;
using UnityEngine.Serialization;

public class GrassGenerator : MonoBehaviour
{
    [SerializeField, Range(0, 512)] private int resolution = 500;
    [SerializeField] private Material material;
    [SerializeField] private Mesh mesh;
    [SerializeField] private Vector2 scale = Vector2.one;
    [SerializeField] private Vector2 scaleVariationRange = new Vector2(0.6f, 1.0f);
    
    [SerializeField] private int alphaMapIndex;
    private Terrain terrain; 
    
    [Range(0, 100.0f)] public float maxDrawDistance = 100.0f;
    
    [SerializeField] private bool updateGrass = false;
    
    [SerializeField] ComputeShader initGrassShader;
    [SerializeField] ComputeShader cullGrassShader;
    
    private int initGrassKernel, voteKernel, scanKernel, scanGroupSumKernel, compactKernel;
    
    private ComputeBuffer grassDataBuffer, voteBuffer, scanBuffer, groupSumArrayBuffer, scannedGroupSumBuffer,
        culledGrassBuffer, argsBuffer;

    private int numGrassInitThreadGroups, numThreadGroups, numVoteThreadGroups, numGroupScanThreadGroups; //scan and compact uses numThreadGroups
    
    private Bounds bounds;  
    private uint[] args;

    public class GrassChunk
    {
        
    }
    
    void OnEnable()
    {
        terrain =  Terrain.activeTerrain;
            
        if (initGrassShader == null || cullGrassShader == null) Debug.LogError("Compute shaders not set");
        
        initGrassKernel = initGrassShader.FindKernel("InitializeGrass");
        voteKernel = cullGrassShader.FindKernel("Vote");
        scanKernel = cullGrassShader.FindKernel("Scan");
        scanGroupSumKernel = cullGrassShader.FindKernel("ScanGroupSums");
        compactKernel = cullGrassShader.FindKernel("Compact"); 
        
        grassDataBuffer = new ComputeBuffer(resolution * resolution, sizeof(float) * 12); //grass data size
        voteBuffer = new ComputeBuffer(resolution * resolution, sizeof(uint));
        scanBuffer = new ComputeBuffer(resolution * resolution, sizeof(uint));
        groupSumArrayBuffer = new ComputeBuffer(resolution * resolution, sizeof(uint));
        scannedGroupSumBuffer = new ComputeBuffer(resolution * resolution, sizeof(uint));
        culledGrassBuffer = new ComputeBuffer(resolution * resolution, sizeof(float) * 12);
        argsBuffer = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);
        
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
        //numGroupScanThreadGroups = Mathf.CeilToInt(resolution * resolution / 2048.0f);
        
        bounds = new Bounds(terrain.transform.position + terrain.terrainData.size * 0.5f, terrain.terrainData.size);
        
        args = new uint[5] { 0, 0, 0, 0, 0 };
        // Arguments for drawing mesh.
        args[0] = (uint)mesh.GetIndexCount(0); //number of triangle indices
        args[1] = (uint)0; //instance count
        args[2] = (uint)mesh.GetIndexStart(0);
        args[3] = (uint)mesh.GetBaseVertex(0);
        argsBuffer.SetData(args);
        
        UpdateGrassBuffer();
    }
    
    void OnDisable () 
    {
        grassDataBuffer?.Release();
        voteBuffer?.Release();
        scanBuffer?.Release();
        groupSumArrayBuffer?.Release();
        scannedGroupSumBuffer?.Release();
        culledGrassBuffer?.Release();
        argsBuffer?.Release();

        grassDataBuffer = null;
        voteBuffer = null;
        scanBuffer = null;
        groupSumArrayBuffer = null;
        scannedGroupSumBuffer = null;
        culledGrassBuffer = null;
        argsBuffer = null;
    }

    void CullGrass(Matrix4x4 VP)
    {
        argsBuffer.SetData(args);
        
        //Vote
        cullGrassShader.SetMatrix("MATRIX_VP", VP);
        cullGrassShader.SetBuffer(voteKernel, "_GrassDataBuffer", grassDataBuffer);
        cullGrassShader.SetBuffer(voteKernel, "_VoteBuffer", voteBuffer);
        cullGrassShader.SetVector("_CameraPosition", Camera.main.transform.position);
        cullGrassShader.SetFloat("_MaxDrawDistance", maxDrawDistance);
        cullGrassShader.Dispatch(voteKernel, numVoteThreadGroups, 1, 1);
        
        //Scan Instances
        cullGrassShader.SetBuffer(scanKernel, "_VoteBuffer", voteBuffer);
        cullGrassShader.SetBuffer(scanKernel, "_ScanBuffer", scanBuffer);
        cullGrassShader.SetBuffer(scanKernel, "_ScanGroupSumArray", groupSumArrayBuffer);
        cullGrassShader.Dispatch(scanKernel, numThreadGroups, 1, 1);

        //Scan Groups
        cullGrassShader.SetInt("_ScanNumOfGroups", numThreadGroups);
        cullGrassShader.SetBuffer(scanGroupSumKernel, "_ScanGroupSumArrayIn", groupSumArrayBuffer);
        cullGrassShader.SetBuffer(scanGroupSumKernel, "_ScanGroupSumArrayOut", scannedGroupSumBuffer);
        cullGrassShader.Dispatch(scanGroupSumKernel, 1, 1, 1);

        //Compact
        cullGrassShader.SetBuffer(compactKernel, "_GrassDataBuffer", grassDataBuffer);
        cullGrassShader.SetBuffer(compactKernel, "_VoteBuffer", voteBuffer);
        cullGrassShader.SetBuffer(compactKernel, "_ScanBuffer", scanBuffer);
        cullGrassShader.SetBuffer(compactKernel, "_ArgsBuffer", argsBuffer);
        cullGrassShader.SetBuffer(compactKernel, "_CulledGrassOutputBuffer", culledGrassBuffer);
        cullGrassShader.SetBuffer(compactKernel, "_ScanGroupSumArray", scannedGroupSumBuffer);
        cullGrassShader.Dispatch(compactKernel, numThreadGroups, 1, 1);
    }
    
    void UpdateGrassBuffer()
    {
        if (grassDataBuffer == null || grassDataBuffer.count != resolution * resolution)
        {
            grassDataBuffer?.Release();
            grassDataBuffer = new ComputeBuffer(resolution * resolution, sizeof(float) * 12);
        }

        initGrassShader.SetInt("_Resolution", resolution);
        initGrassShader.SetBuffer(initGrassKernel, "_GrassDataBuffer", grassDataBuffer);
        
        Vector3 terrainPosition = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size; 
        Texture heightMap = terrain.terrainData.heightmapTexture;
        Texture2D alphaMap = terrain.terrainData.GetAlphamapTexture(alphaMapIndex);
        Texture normalMap = terrain.normalmapTexture;   
        
        initGrassShader.SetVector("_TerrainPosition", terrainPosition);
        initGrassShader.SetVector("_TerrainSize", terrainSize);
        
        initGrassShader.SetTexture(initGrassKernel, "_HeightMap", heightMap);
        //computeShader.SetTexture(0, "_AlphaMap", alphaMap);
        initGrassShader.SetTexture(initGrassKernel, "_NormalMap", normalMap);
        
        initGrassShader.SetVector("_Scale", scale);
        initGrassShader.SetVector("_ScaleVariationRange", scaleVariationRange);
        
        numGrassInitThreadGroups = Mathf.CeilToInt(resolution / 8f);
        initGrassShader.Dispatch(initGrassKernel, numGrassInitThreadGroups, numGrassInitThreadGroups, 1);

        material.SetBuffer("_GrassDataBuffer", culledGrassBuffer);
        
    }

    private void Update()
    {
        if (updateGrass)
        {
            UpdateGrassBuffer();
        }   
        
        Matrix4x4 P = Camera.main.projectionMatrix;
        Matrix4x4 V = Camera.main.worldToCameraMatrix;
        Matrix4x4 VP = P * V;
        
        CullGrass(VP);
        
        Graphics.DrawMeshInstancedIndirect(mesh, 0, material, bounds, argsBuffer);
    }
}

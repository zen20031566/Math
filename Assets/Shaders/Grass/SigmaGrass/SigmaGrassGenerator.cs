using System;
using UnityEngine;
using UnityEngine.Serialization;
using static System.Runtime.InteropServices.Marshal;
using System.Collections.Generic;

public enum SigmaGrassTerrainSize
{
    Low = 128,
    Mid = 256,
    High = 512,
}

public struct SigmaGrassData 
{
    public Vector4 Position;
    public Vector3 Up;
    public Vector3 Forward;
    public Vector2 Scale;
};

public struct SigmaGrassChunkData
{
    public int Resolution;
    public Terrain Terrain;
    public int NumChunkPerEdge;
    public int ChunkSize;
    public int NumThreadsPerChunk;
    public ComputeShader InitGrassShader;
    public ComputeShader CullGrassShader;
    public int InitGrassKernel;
}

public class SigmaGrassGenerator : MonoBehaviour
{
    [SerializeField] private List<SigmaGrassModel> models = new List<SigmaGrassModel>();
    
    [SerializeField] private SigmaGrassTerrainSize mapSize = SigmaGrassTerrainSize.Low;
    private int resolution;
    
    [Range(0, 100.0f)] public float maxDrawDistance = 100.0f;
    [Range(0, 100.0f)] public float lodCutoff = 100.0f;
    private float sqrLodCutoff;
    
    [SerializeField] ComputeShader initGrassShader;
    [SerializeField] ComputeShader cullGrassShader;

    private Terrain terrain; 
    
    private int initGrassKernel;
    private int voteKernel;
    private int scanKernel;
    private int scanGroupSumKernel;
    private int compactKernel;

    private ComputeBuffer voteBuffer;
    private ComputeBuffer scanBuffer;
    private ComputeBuffer groupSumArrayBuffer;
    private ComputeBuffer scannedGroupSumBuffer;

    private int numGrassInitThreadGroups;
    private int numThreadGroups; //scan and compact uses numThreadGroups
    private int numVoteThreadGroups;
    private int numGroupScanThreadGroups; 
    
    [SerializeField] int numChunkPerEdge = 4;
    private int totalNumChunks;
    private int chunkSize;
    private List<SigmaGrassChunk> chunks;
    private int numThreadsPerChunk;

    private Camera camera;
    
    void OnEnable()
    {
        UpdateData();
        TerrainCallbacks.heightmapChanged += OnHeightmapChanged;
        TerrainCallbacks.textureChanged   += OnTextureChanged;
    }
    
    void OnDisable()
    {
        ClearData();
        TerrainCallbacks.heightmapChanged -= OnHeightmapChanged;
        TerrainCallbacks.textureChanged   -= OnTextureChanged;
    }

    void OnHeightmapChanged(Terrain t, RectInt region, bool synched)
    {
        if (t != terrain) return;
        ClearData();
        UpdateData();
    }

    void OnTextureChanged(Terrain t, string name, RectInt region, bool synched)
    {
        if (t != terrain) return;
        ClearData();
        UpdateData();
    }
    
    private void UpdateData()
    {
        camera = Camera.main;
        sqrLodCutoff = lodCutoff * lodCutoff;
        terrain =  Terrain.activeTerrain;
        resolution = (int)mapSize;
        terrain.terrainData.size = Vector3.one * resolution;
            
        if (initGrassShader == null || cullGrassShader == null) Debug.LogError("Compute shaders not set");
        
        initGrassKernel = initGrassShader.FindKernel("InitGrass");
        voteKernel = cullGrassShader.FindKernel("Vote");
        scanKernel = cullGrassShader.FindKernel("Scan");
        scanGroupSumKernel = cullGrassShader.FindKernel("ScanGroupSums");
        compactKernel = cullGrassShader.FindKernel("Compact"); 
        
        chunkSize = resolution / numChunkPerEdge;
        numThreadsPerChunk = chunkSize * chunkSize;
        totalNumChunks = numChunkPerEdge * numChunkPerEdge;
        
        voteBuffer = new ComputeBuffer(numThreadsPerChunk, sizeof(uint));
        scanBuffer = new ComputeBuffer(numThreadsPerChunk, sizeof(uint));
        groupSumArrayBuffer = new ComputeBuffer(numThreadsPerChunk, sizeof(uint));
        scannedGroupSumBuffer = new ComputeBuffer(numThreadsPerChunk, sizeof(uint));

        numThreadGroups = Mathf.CeilToInt(numThreadsPerChunk / 128.0f);
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
        
        numVoteThreadGroups = Mathf.CeilToInt(numThreadsPerChunk / 128.0f);
        //numGroupScanThreadGroups = Mathf.CeilToInt(resolution * resolution / 2048.0f);
        
        InitChunks();
    }

    private void ClearData()
    {
        voteBuffer?.Release();
        scanBuffer?.Release();
        groupSumArrayBuffer?.Release();
        scannedGroupSumBuffer?.Release();
        
        voteBuffer = null;
        scanBuffer = null;
        groupSumArrayBuffer = null;
        scannedGroupSumBuffer = null;

        if (chunks != null)
        {
            foreach (var chunk in chunks)
                chunk.ClearBuffers();
            chunks = null;
        }
    }
    
    void InitChunks()
    {
        SigmaGrassChunkData chunkData = new SigmaGrassChunkData();
        chunkData.Resolution = resolution;
        chunkData.Terrain = terrain;
        chunkData.NumChunkPerEdge = numChunkPerEdge;
        chunkData.ChunkSize = chunkSize;
        chunkData.NumThreadsPerChunk = numThreadsPerChunk;
        chunkData.InitGrassShader = initGrassShader;
        chunkData.CullGrassShader = cullGrassShader;
        chunkData.InitGrassKernel = initGrassKernel;
            
        chunks = new List<SigmaGrassChunk>();

        foreach (var model in models)
        {
            for (int x = 0; x < numChunkPerEdge; ++x) 
            {
                for (int y = 0; y < numChunkPerEdge; ++y) 
                {
                    var chunk = new SigmaGrassChunk();
                    chunk.Init(model, chunkData, x, y);
                    chunks.Add(chunk);
                }
            }
        }
    }
    
    void CullGrass(Matrix4x4 VP, SigmaGrassChunk chunk, bool noLOD)
    {
        if (noLOD)
            chunk.ArgsBuffer.SetData(chunk.Args);
        else
            chunk.ArgsBufferLOD.SetData(chunk.ArgsLOD);
        
        //Vote
        cullGrassShader.SetMatrix("MATRIX_VP", VP);
        cullGrassShader.SetBuffer(voteKernel, "_GrassDataBuffer", chunk.GrassDataBuffer);
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
        cullGrassShader.SetBuffer(compactKernel, "_GrassDataBuffer", chunk.GrassDataBuffer);
        cullGrassShader.SetBuffer(compactKernel, "_VoteBuffer", voteBuffer);
        cullGrassShader.SetBuffer(compactKernel, "_ScanBuffer", scanBuffer);
        cullGrassShader.SetBuffer(compactKernel, "_ArgsBuffer", noLOD ? chunk.ArgsBuffer : chunk.ArgsBufferLOD);
        cullGrassShader.SetBuffer(compactKernel, "_CulledGrassOutputBuffer", chunk.CulledGrassBuffer);
        cullGrassShader.SetBuffer(compactKernel, "_ScanGroupSumArray", scannedGroupSumBuffer);
        cullGrassShader.Dispatch(compactKernel, numThreadGroups, 1, 1);
    }
    
    private void Update()
    {
        if (chunks == null || camera == null || models == null) return;
        
        Matrix4x4 P = camera.projectionMatrix;
        Matrix4x4 V = camera.worldToCameraMatrix;
        Matrix4x4 VP = P * V;
        
        foreach (var chunk in chunks)
        {
            // float dist = Vector3.Distance(camera.transform.position, chunk.Bounds.center);
            // bool noLOD = dist < lodCutoff;
            
            float sqrDist = chunk.Bounds.SqrDistance(camera.transform.position);
            sqrLodCutoff = lodCutoff * lodCutoff;
            bool noLOD = sqrDist < sqrLodCutoff;
            
            CullGrass(VP, chunk, noLOD);
            
            if (noLOD)
                Graphics.DrawMeshInstancedIndirect(chunk.Mesh, 0, chunk.Material, chunk.Bounds, chunk.ArgsBuffer);
            else
                Graphics.DrawMeshInstancedIndirect(chunk.LODMesh, 0, chunk.Material, chunk.Bounds, chunk.ArgsBufferLOD);
        }
       
    }
    
}

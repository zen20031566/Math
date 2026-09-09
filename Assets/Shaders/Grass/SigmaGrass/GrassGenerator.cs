using System;
using UnityEngine;
using UnityEngine.Serialization;
using static System.Runtime.InteropServices.Marshal;
using System.Collections.Generic;

public enum GrassTerrainSize
{
    Low = 128,
    Mid = 256,
    High = 512,
}

public struct GrassData 
{
    public Vector4 Position;
    public Vector3 Up;
    public Vector3 Forward;
    public Vector2 Scale;
};

public struct GrassChunkData
{
    public int Resolution;
    public Terrain Terrain;
    public int NumChunkPerEdge;
    public int ChunkSize;
    public int NumThreadsPerChunk;
    public ComputeShader InitGrassShader;
    public ComputeShader CullGrassShader;
    public int InitGrassKernel;
    public Texture2D[] DetailMaps;
    public int DetailMapIndex;
    public Vector4 DetailSplatMapChannels;
}

public enum ChunkState { Culled, FullRes, LOD }

[RequireComponent(typeof(GrassPaintController))]
public class GrassGenerator : MonoBehaviour
{
    [SerializeField] private SigmaGrassModel[] models = new SigmaGrassModel[8];
    
    [SerializeField] private GrassTerrainSize mapSize = GrassTerrainSize.Low;
    private int resolution;
    
    [Range(0, 100.0f)] public float maxDrawDistance = 48.0f;
    [Range(0, 100.0f)] public float lodCutoff = 5.0f;
    private float sqrLodCutoff;
    private float[] distanceBands;
    
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
    private int chunkSize;
    private GrassChunk[] chunks; 
    private int numThreadsPerChunk;

    private Camera camera;
    private CullingGroup cullingGroup;

    private ChunkState[] chunkStates;
    private bool[] chunkBufferAllocateRequests;
    private bool[] chunkBufferClearRequests;
    
    private GrassPaintController grassPaintController;
    private Texture2D[] detailMaps;
    
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
        terrain = GetComponent<Terrain>();
        if (terrain == null) Debug.LogError("Place this script on terrain!");
        
        camera = Camera.main;
        
        grassPaintController = GetComponent<GrassPaintController>();
        detailMaps = grassPaintController.SourceTextures;
        
        sqrLodCutoff = lodCutoff * lodCutoff;
        if (lodCutoff >= maxDrawDistance)
        {
            lodCutoff = maxDrawDistance - 1f;
        }
        distanceBands = new float[] { lodCutoff, maxDrawDistance };
        
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
        
        cullingGroup?.Dispose();
        cullingGroup = null;
    }
    
    public Vector4 GetDetailSplatMapChannels(int ID)
    {
        ID = ID % 4;
            
        var rChannel = ID == 0 ? 1f : 0f;
        var gChannel = ID == 1 ? 1f : 0f;
        var bChannel = ID == 2 ? 1f : 0f;
        var aChannel = ID == 3 ? 1f : 0f;

        var splatChannels = new Vector4(rChannel, gChannel, bChannel, aChannel);
        return splatChannels;
    }
    
    void InitChunks()
    {
        GrassChunkData chunkData = new GrassChunkData();
        chunkData.Resolution = resolution;
        chunkData.Terrain = terrain;
        chunkData.NumChunkPerEdge = numChunkPerEdge;
        chunkData.ChunkSize = chunkSize;
        chunkData.NumThreadsPerChunk = numThreadsPerChunk;
        chunkData.InitGrassShader = initGrassShader;
        chunkData.CullGrassShader = cullGrassShader;
        chunkData.InitGrassKernel = initGrassKernel;

        int totalChunks = models.Length * numChunkPerEdge * numChunkPerEdge;
        chunks = new GrassChunk[totalChunks];
        var chunkBoundingSpheres = new BoundingSphere[totalChunks]; //bounding sphere index needs to match chunk index exactly
        chunkStates = new ChunkState[totalChunks];
        chunkBufferAllocateRequests = new bool[totalChunks];
        chunkBufferClearRequests = new bool[totalChunks];
        
        for (int i = 0; i < models.Length; i++)
        {
            var model = models[i];
            chunkData.DetailMapIndex = (i > 3) ? 1 : 0;
            chunkData.DetailSplatMapChannels = GetDetailSplatMapChannels(i);
            
            for (int x = 0; x < numChunkPerEdge; ++x) 
            {
                for (int y = 0; y < numChunkPerEdge; ++y) 
                {
                    var chunk = new GrassChunk();
                    chunk.Init(model, chunkData, x, y);
                    chunks[i] = chunk;
                    chunkBoundingSpheres[i] = new BoundingSphere(chunk.Bounds.center, chunk.Bounds.extents.magnitude);
                }
            }
        }
        
        cullingGroup = new CullingGroup();
        cullingGroup.targetCamera = camera;
        cullingGroup.SetBoundingSpheres(chunkBoundingSpheres);
        cullingGroup.SetDistanceReferencePoint(camera.transform);
        cullingGroup.SetBoundingDistances(distanceBands);
        cullingGroup.onStateChanged = OnCullingStateChange;
    }

    private void OnCullingStateChange(CullingGroupEvent e)
    {
        ChunkState state;
        if (!e.isVisible || e.currentDistance >= 2)
        {
            state = ChunkState.Culled;
        }
        else if (e.currentDistance == 1) //at lod threshold
        {
            state = ChunkState.LOD;
        }
        else
        {
            state = ChunkState.FullRes;
        }

        chunkBufferAllocateRequests[e.index] = state != ChunkState.Culled && !chunks[e.index].HasBuffers;
        chunkBufferClearRequests[e.index] = state == ChunkState.Culled && chunks[e.index].HasBuffers && e.currentDistance >= 2;
        chunkStates[e.index] = state;
    }

    void CullGrass(Matrix4x4 VP, GrassChunk chunk, bool noLOD)
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
        
        for (int i = 0; i < chunks.Length; i++)
        { 
            GrassChunk chunk = chunks[i];
            
            //Allocate buffer
            if (chunkBufferAllocateRequests[i])
            {
                chunk.AllocateBuffers();
                chunkBufferAllocateRequests[i] = false;
            }
            
            //Clear buffer
            if (chunkBufferClearRequests[i])
            {
                chunk.ClearBuffers();
                chunkBufferClearRequests[i] = false;
            }
            
            //Draw
            if (chunkStates[i] != ChunkState.Culled)
            {
                bool noLOD = chunkStates[i] != ChunkState.LOD;
                CullGrass(VP, chunk, noLOD);
                
                if (noLOD)
                    Graphics.DrawMeshInstancedIndirect(chunk.Mesh, 0, chunk.Material, chunk.Bounds, chunk.ArgsBuffer);
                else
                    Graphics.DrawMeshInstancedIndirect(chunk.LODMesh, 0, chunk.Material, chunk.Bounds, chunk.ArgsBufferLOD);
            }
        }
    }
}

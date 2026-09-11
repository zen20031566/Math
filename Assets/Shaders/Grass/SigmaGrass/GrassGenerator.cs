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
    public Vector2 TerrainUV; 
    public float DensityThreshold;
};

public struct GrassChunkData
{
    public int MapSize;
    public int Resolution;
    public Terrain Terrain;
    public int NumChunkPerEdge;
    public int ChunkSize;
    public int ChunkResolution;
    public int NumThreadsPerChunk;
    public ComputeShader InitGrassShader;
    public ComputeShader CullGrassShader;
    public int InitGrassKernel;
    public Texture2D DetailMap;
    public Vector4 DetailSplatMapChannels;
    public int GrassDensity;
    public int ChunkIndex;
}

public enum ChunkState { Culled, FullRes, LOD }

[RequireComponent(typeof(GrassPaintController))]
public class GrassGenerator : MonoBehaviour
{
    [SerializeField] private List<SigmaGrassModel> models = new List<SigmaGrassModel>();
    
    [SerializeField] private GrassTerrainSize mapSize = GrassTerrainSize.Low;
    private int resolution;

    [SerializeField, Range(1, 15)] private int grassDensity = 10;
    
    [SerializeField, Range(0, 100.0f)] public float maxDrawDistance = 48.0f;
    [SerializeField, Range(0, 100.0f)] public float lodCutoff = 5.0f;
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
    
    private int numThreadGroups; //scan and compact uses numThreadGroups
    private int numVoteThreadGroups;
    private int numGroupScanThreadGroups; 
    
    private int numChunkPerEdge = 4;
    private int chunkSize;
    private int chunkResolution;
    private GrassChunk[] chunks; 
    private int numThreadsPerChunk;

    private Camera camera;
    private CullingGroup cullingGroup;

    private ChunkState[] chunkStates;
    private bool[] chunkBufferAllocateRequests;
    private bool[] chunkBufferClearRequests;
    
    private GrassPaintController grassPaintController;
    private Texture2D[] detailMaps;
    
    private static readonly int MatrixVPID = Shader.PropertyToID("MATRIX_VP");
    private static readonly int GrassDataBufferID = Shader.PropertyToID("_GrassDataBuffer");
    private static readonly int VoteBufferID = Shader.PropertyToID("_VoteBuffer");
    private static readonly int CameraPositionID = Shader.PropertyToID("_CameraPosition");
    private static readonly int MaxDrawDistanceID = Shader.PropertyToID("_MaxDrawDistance");
    private static readonly int DetailMapID = Shader.PropertyToID("_DetailMap");
    private static readonly int DetailSplatMapChannelsID = Shader.PropertyToID("_DetailSplatMapChannels");
    private static readonly int ScanBufferID = Shader.PropertyToID("_ScanBuffer");
    private static readonly int ScanGroupSumArrayID = Shader.PropertyToID("_ScanGroupSumArray");
    private static readonly int ScanNumOfGroupsID = Shader.PropertyToID("_ScanNumOfGroups");
    private static readonly int ScanGroupSumArrayInID = Shader.PropertyToID("_ScanGroupSumArrayIn");
    private static readonly int ScanGroupSumArrayOutID = Shader.PropertyToID("_ScanGroupSumArrayOut");
    private static readonly int ArgsBufferID = Shader.PropertyToID("_ArgsBuffer");
    private static readonly int CulledGrassOutputBufferID = Shader.PropertyToID("_CulledGrassOutputBuffer");
    
    void OnEnable()
    {
        UpdateData();
        
        #if UNITY_EDITOR
        TerrainCallbacks.heightmapChanged += OnHeightmapChanged;
        TerrainCallbacks.textureChanged   += OnTextureChanged;
        UnityEditor.EditorApplication.focusChanged += OnEditorFocusChanged;
        #endif
    }
    
    void OnDisable()
    {
        ClearData();
        
        #if UNITY_EDITOR
        TerrainCallbacks.heightmapChanged -= OnHeightmapChanged;
        TerrainCallbacks.textureChanged   -= OnTextureChanged;
        UnityEditor.EditorApplication.focusChanged -= OnEditorFocusChanged;
        #endif
    }
    
    #if UNITY_EDITOR
    private void OnEditorFocusChanged(bool hasFocus) => UpdateData();
    void OnHeightmapChanged(Terrain t, RectInt region, bool synched) => UpdateData();
    void OnTextureChanged(Terrain t, string name, RectInt region, bool synched) => UpdateData();
    #endif
    
    private void UpdateData()
    {
        ClearData();
        
        terrain = GetComponent<Terrain>();
        if (terrain == null) Debug.LogError("Place this script on terrain!");
        
        #if UNITY_EDITOR
        if (!Application.isPlaying && UnityEditor.SceneView.lastActiveSceneView != null)
            camera = UnityEditor.SceneView.lastActiveSceneView.camera;
        else
        {
        #endif
            camera = Camera.main;
        }
        
        grassPaintController = GetComponent<GrassPaintController>();
        detailMaps = grassPaintController.SourceTextures;
        
        sqrLodCutoff = lodCutoff * lodCutoff;
        if (lodCutoff >= maxDrawDistance)
        {
            lodCutoff = maxDrawDistance - 1f;
        }
        distanceBands = new float[] { lodCutoff, maxDrawDistance };
        
        numChunkPerEdge = (int)mapSize / 32;
        terrain.terrainData.size = Vector3.one * (int)mapSize;
        chunkSize = (int)mapSize / numChunkPerEdge;
        resolution = (int)mapSize * grassDensity;
        chunkResolution = resolution / numChunkPerEdge;
        numThreadsPerChunk = chunkResolution * chunkResolution;
        
        if (initGrassShader == null || cullGrassShader == null) Debug.LogError("Compute shaders not set");
        
        initGrassKernel = initGrassShader.FindKernel("InitGrass");
        voteKernel = cullGrassShader.FindKernel("Vote");
        scanKernel = cullGrassShader.FindKernel("Scan");
        scanGroupSumKernel = cullGrassShader.FindKernel("ScanGroupSums");
        compactKernel = cullGrassShader.FindKernel("Compact"); 
        
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
        chunkData.MapSize = (int)mapSize;
        chunkData.Resolution = resolution;
        chunkData.Terrain = terrain;
        chunkData.NumChunkPerEdge = numChunkPerEdge;
        chunkData.ChunkSize = chunkSize;
        chunkData.ChunkResolution = chunkResolution;
        chunkData.NumThreadsPerChunk = numThreadsPerChunk;
        chunkData.InitGrassShader = initGrassShader;
        chunkData.CullGrassShader = cullGrassShader;
        chunkData.InitGrassKernel = initGrassKernel;
        chunkData.GrassDensity = grassDensity;
        
        int totalChunks = models.Count * numChunkPerEdge * numChunkPerEdge;
        chunks = new GrassChunk[totalChunks];
        var chunkBoundingSpheres = new BoundingSphere[totalChunks]; //bounding sphere index needs to match chunk index exactly
        chunkStates = new ChunkState[totalChunks];
        chunkBufferAllocateRequests = new bool[totalChunks];
        chunkBufferClearRequests = new bool[totalChunks];
        
        int chunkIndex = 0;
        for (int i = 0; i < models.Count; i++)
        {
            var model = models[i];
            int detailMapIndex = (i > 3) ? 1 : 0;
            chunkData.DetailMap = detailMaps[detailMapIndex];
            chunkData.DetailSplatMapChannels = GetDetailSplatMapChannels(i);
            
            for (int x = 0; x < numChunkPerEdge; ++x) 
            {
                for (int y = 0; y < numChunkPerEdge; ++y) 
                {
                    var chunk = new GrassChunk();
                    chunkData.ChunkIndex = chunkIndex;
                    chunk.Init(model, chunkData, x, y);
                    chunks[chunkIndex] = chunk;
                    chunkBoundingSpheres[chunkIndex] = new BoundingSphere(chunk.Bounds.center, chunk.Bounds.extents.magnitude);
                    chunkIndex++;
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


    void InitCull(Matrix4x4 VP)
    {
        cullGrassShader.SetMatrix(MatrixVPID, VP);
        cullGrassShader.SetVector(CameraPositionID, camera.transform.position);
        cullGrassShader.SetFloat(MaxDrawDistanceID, maxDrawDistance);
        cullGrassShader.SetBuffer(voteKernel, VoteBufferID, voteBuffer);
        
        cullGrassShader.SetBuffer(scanKernel, VoteBufferID, voteBuffer);
        cullGrassShader.SetBuffer(scanKernel, ScanBufferID, scanBuffer);
        cullGrassShader.SetBuffer(scanKernel, ScanGroupSumArrayID, groupSumArrayBuffer);
        
        cullGrassShader.SetInt(ScanNumOfGroupsID, numThreadGroups);
        cullGrassShader.SetBuffer(scanGroupSumKernel, ScanGroupSumArrayInID, groupSumArrayBuffer);
        cullGrassShader.SetBuffer(scanGroupSumKernel, ScanGroupSumArrayOutID, scannedGroupSumBuffer);
        
        cullGrassShader.SetBuffer(compactKernel, VoteBufferID, voteBuffer);
        cullGrassShader.SetBuffer(compactKernel, ScanBufferID, scanBuffer);
        cullGrassShader.SetBuffer(compactKernel, ScanGroupSumArrayID, scannedGroupSumBuffer);
    }
    
    void CullGrass(Matrix4x4 VP, GrassChunk chunk, bool noLOD)
    {
        if (noLOD)
            chunk.ArgsBuffer.SetData(chunk.Args);
        else
            chunk.ArgsBufferLOD.SetData(chunk.ArgsLOD);
        
        //Vote
        cullGrassShader.SetBuffer(voteKernel, GrassDataBufferID, chunk.GrassDataBuffer);
        cullGrassShader.SetTexture(voteKernel, DetailMapID, chunk.DetailMap);
        cullGrassShader.SetVector(DetailSplatMapChannelsID, chunk.DetailSplatMapChannels);
        cullGrassShader.Dispatch(voteKernel, numVoteThreadGroups, 1, 1);
        
        //Scan Instances
        cullGrassShader.Dispatch(scanKernel, numThreadGroups, 1, 1);
    
        //Scan Groups
        cullGrassShader.Dispatch(scanGroupSumKernel, 1, 1, 1);
    
        //Compact
        cullGrassShader.SetBuffer(compactKernel, GrassDataBufferID, chunk.GrassDataBuffer);
        cullGrassShader.SetBuffer(compactKernel, ArgsBufferID, noLOD ? chunk.ArgsBuffer : chunk.ArgsBufferLOD);
        cullGrassShader.SetBuffer(compactKernel, CulledGrassOutputBufferID, chunk.CulledGrassBuffer);
        cullGrassShader.Dispatch(compactKernel, numThreadGroups, 1, 1);
    }
    
    private void Update()
    {
        if (chunks == null || camera == null || models == null) return;
        
        Matrix4x4 P = camera.projectionMatrix;
        Matrix4x4 V = camera.worldToCameraMatrix;
        Matrix4x4 VP = P * V;
        InitCull(VP);
        
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
                    Graphics.DrawMeshInstancedIndirect(chunk.Mesh, 0, chunk.Material, chunk.Bounds, chunk.ArgsBuffer, 0, chunk.PropertyBlock);
                else
                    Graphics.DrawMeshInstancedIndirect(chunk.LODMesh, 0, chunk.Material, chunk.Bounds, chunk.ArgsBufferLOD, 0, chunk.PropertyBlock);
            }
        }
    }
    private void OnValidate()
    {
        if (!enabled || !gameObject.activeInHierarchy) return;

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return; // object may have been destroyed by the time this runs
            ClearData();
            UpdateData();
        };
        #else
        ClearData();
        UpdateData();
        #endif
    }
}

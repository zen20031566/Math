using UnityEngine;
using static System.Runtime.InteropServices.Marshal;
public class GrassChunk 
{
    public SigmaGrassModel Model { get; private set; }
    public Mesh Mesh { get; private set; }
    public Mesh LODMesh { get; private set; }
    public Material Material { get; private set; }
    
    public ComputeBuffer GrassDataBuffer { get; private set; }
    public ComputeBuffer CulledGrassBuffer { get; private set; }
    public ComputeBuffer ArgsBuffer { get; private set; }
    public ComputeBuffer ArgsBufferLOD { get; private set; }
    public Bounds Bounds { get; private set; }
    
    public uint[] Args { get; private set; }
    public uint[] ArgsLOD { get; private set; }

    public int ChunkX { get; private set; }
    public int ChunkY { get; private set; }

    public bool HasBuffers => GrassDataBuffer != null && GrassDataBuffer.IsValid();
    
    private Vector2 scale;
    private Vector2 scaleVariationRange;
    private int resolution;
    private Terrain terrain;
    private int numChunkPerEdge;
    private int chunkSize;
    private int numThreadsPerChunk;
    private ComputeShader initGrassShader;
    private ComputeShader cullGrassShader;
    private int initGrassKernel;
    private Texture2D[] detailMaps;
    private int detailMapIndex;
    private Vector4 detailSplatMapChannels;
    
    public void Init(SigmaGrassModel model, GrassChunkData chunkData, int chunkX, int chunkY)
    {
        Model = model;
        Mesh = model.Mesh;
        LODMesh = model.LODMesh;
        Material = new Material(model.Material);
        scale = model.Scale;
        scaleVariationRange = model.ScaleVariationRange;

        resolution = chunkData.Resolution;
        terrain = chunkData.Terrain;
        numChunkPerEdge = chunkData.NumChunkPerEdge;
        chunkSize = chunkData.ChunkSize;
        numThreadsPerChunk = chunkData.NumThreadsPerChunk;
        initGrassShader = chunkData.InitGrassShader;
        cullGrassShader = chunkData.CullGrassShader;
        initGrassKernel = chunkData.InitGrassKernel;
        detailMaps = chunkData.DetailMaps;
        detailMapIndex = chunkData.DetailMapIndex;
        detailSplatMapChannels = chunkData.DetailSplatMapChannels;
        ChunkX = chunkX;
        ChunkY = chunkY;
        
        Vector3 terrainPosition = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size; 
        
        Vector3 center = Vector3.zero;
        center.x = (chunkX + 0.5f) * (terrainSize.x / numChunkPerEdge);
        center.z = (chunkY + 0.5f) * (terrainSize.z / numChunkPerEdge);
        center.y = terrainSize.y * 0.5f;
        center = terrainPosition + center;
        
        Vector3 size = new Vector3
        (
            terrainSize.x / numChunkPerEdge,
            terrainSize.y,
            terrainSize.z / numChunkPerEdge
        );
        
        Bounds = new Bounds(center, size);
        
        Args = new uint[5] { 0, 0, 0, 0, 0 };
        Args[0] = (uint)Mesh.GetIndexCount(0); //number of triangle indices
        Args[1] = (uint)0; //instance count
        Args[2] = (uint)Mesh.GetIndexStart(0);
        Args[3] = (uint)Mesh.GetBaseVertex(0);
        
        ArgsLOD = new uint[5] { 0, 0, 0, 0, 0 };
        ArgsLOD[0] = (uint)LODMesh.GetIndexCount(0);
        ArgsLOD[1] = (uint)0;
        ArgsLOD[2] = (uint)LODMesh.GetIndexStart(0);
        ArgsLOD[3] = (uint)LODMesh.GetBaseVertex(0);
        
        Color randomColor = new Color(
            UnityEngine.Random.Range(0f, 1f),
            UnityEngine.Random.Range(0f, 1f),
            UnityEngine.Random.Range(0f, 1f)
        );
        
        Material.SetColor("_TopColor", randomColor);
    }

    public void AllocateBuffers()
    {
        if (HasBuffers) return;
        
        Vector3 terrainPosition = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size; 
        Texture heightMap = terrain.terrainData.heightmapTexture;
        // Texture2D alphaMap = terrain.terrainData.GetAlphamapTexture(alphaMapIndex);
        Texture normalMap = terrain.normalmapTexture;  
        
        ArgsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        ArgsBufferLOD = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        ArgsBuffer.SetData(Args);
        ArgsBufferLOD.SetData(ArgsLOD);
        
        GrassDataBuffer = new ComputeBuffer(numThreadsPerChunk, SizeOf(typeof(GrassData)));
        CulledGrassBuffer = new ComputeBuffer(numThreadsPerChunk, SizeOf(typeof(GrassData)));
        
        initGrassShader.SetInt("_Resolution", resolution);
        initGrassShader.SetInt("_ChunkSize", chunkSize);
        initGrassShader.SetVector("_ChunkID", new Vector4(ChunkX, ChunkY, 0,0)); 
        initGrassShader.SetBuffer(initGrassKernel, "_GrassDataBuffer", GrassDataBuffer);
        
        initGrassShader.SetVector("_TerrainPosition", terrainPosition);
        initGrassShader.SetVector("_TerrainSize", terrainSize);
        
        initGrassShader.SetTexture(initGrassKernel, "_HeightMap", heightMap);
        //computeShader.SetTexture(0, "_AlphaMap", alphaMap);
        initGrassShader.SetTexture(initGrassKernel, "_NormalMap", normalMap);
        
        initGrassShader.SetVector("_Scale", scale);
        initGrassShader.SetVector("_ScaleVariationRange", scaleVariationRange);
        
        int groups = Mathf.CeilToInt(chunkSize / 8f);
        initGrassShader.Dispatch(initGrassKernel, groups, groups, 1);
        
        Material.SetBuffer("_GrassDataBuffer", CulledGrassBuffer);
        
        UnityEngine.Debug.Log($"Model {Model.name} Chunk ({ChunkX}, {ChunkY}) has been allocated");
    }
    
    public void ClearBuffers() 
    {
        GrassDataBuffer?.Release();
        CulledGrassBuffer?.Release();
        ArgsBuffer?.Release();
        ArgsBufferLOD?.Release();
        
        GrassDataBuffer = null;
        CulledGrassBuffer = null;
        ArgsBuffer = null;
        ArgsBufferLOD = null;
        
        UnityEngine.Debug.Log($"Model {Model.name} Chunk ({ChunkX}, {ChunkY}) has been cleared");
    }
}

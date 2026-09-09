using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

using System.Linq;
using System.Collections.Generic;

[ExecuteInEditMode]
public class GrassPaintController : MonoBehaviour
{
    private const int RT_SIZE = 512;
    private const string SPLAT_MAP_PREFIX = "SigmaGrass_Detail_Splat_Map";
    private const int MAX_SPLAT_MAP_COUNT = 2;
    [SerializeField] private Texture2D[] sourceTextures;
    [SerializeField] private RenderTexture[] renderTextures;

    public Texture2D[] SourceTextures => sourceTextures;
    public RenderTexture[] RenderTextures => renderTextures;
    public int CurrentSplatMapIndex { get; private set; } = 0;

    private Terrain terrain;
    
    //paint happens on render texture then is stored into texture2d 
    private void OnEnable()
    {
        terrain = GetComponent<Terrain>();
        
        #if UNITY_EDITOR
        CreateSourceTextures(); 
        InitRenderTextures();
        #endif
    }

    private void OnDestroy()
    {
        if (renderTextures != null && renderTextures.Length > 0)
        {
            foreach (var rt in renderTextures)
            {
                if (rt != null && rt.IsCreated())
                    rt.Release();
            }
        }
    }
    
    public void SetCurrentSplatMap(int index)
    {
        CurrentSplatMapIndex = index;
    }
    
    public void InitRenderTextures()
    {
        renderTextures = new RenderTexture[sourceTextures.Length];
        
        for (int i = 0; i < sourceTextures.Length; i++)
        {
            renderTextures[i] = new RenderTexture(RT_SIZE, RT_SIZE, 0, RenderTextureFormat.ARGB32);
            
            //Copy source texture into render texture
            RenderTexture.active = renderTextures[i];
            Graphics.Blit(sourceTextures[i], renderTextures[i]);
            RenderTexture.active = null;
        }
    }
    
    public void WriteRtToTexture2D(RenderTexture rt, Texture2D texture2d)
    {
        RenderTexture.active = rt;
        texture2d.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        texture2d.Apply();
        RenderTexture.active = null;
    }
    
#if UNITY_EDITOR
    private void CreateSourceTextures()
    {
        for (int i = 0; i < MAX_SPLAT_MAP_COUNT; i++)
        {
            var terrainDataAssets =  AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(terrain.terrainData));
            bool hasSourceTexture = terrainDataAssets.Any(x => x.name == SPLAT_MAP_PREFIX + "_" + i);
            
            if (!hasSourceTexture)
            {
                //Create texture2D and store into terrain data
                var t2d = new Texture2D(RT_SIZE, RT_SIZE, TextureFormat.ARGB32, false);
                var pixels = new Color[RT_SIZE * RT_SIZE];
                t2d.SetPixels(0, 0, t2d.width, t2d.width, pixels); //init texture with all pixels rgba to 0
                t2d.Apply();
                t2d.name = SPLAT_MAP_PREFIX + "_" + i;
                t2d.wrapMode = TextureWrapMode.Clamp;
                AssetDatabase.AddObjectToAsset(t2d, terrain.terrainData); //add to current terrain data
                AssetDatabase.SaveAssets();
            }
        }
        
        LoadSourceTextures();
    }
    
    private void LoadSourceTextures()
    {
        //load into cached array
        var terrainDataAssets =  AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(terrain.terrainData));
        var textures = terrainDataAssets.Where(x => x.name.Contains(SPLAT_MAP_PREFIX)).OrderBy(x => x.name).ToList();
        sourceTextures = new Texture2D[2];
        
        for (int i = 0; i < textures.Count(); i++)
        {
            sourceTextures[i] = textures[i] as Texture2D;
        }
    }
    
    private void OnValidate()
    {
        if (sourceTextures == null || sourceTextures.Length == 0 || sourceTextures[CurrentSplatMapIndex] == null)
        {
            if (terrain == null) terrain = GetComponent<Terrain>();
            var terrainDataAssets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(terrain.terrainData));
            var textures = terrainDataAssets.Where(x => x.name.Contains(SPLAT_MAP_PREFIX)).ToList();
            if (textures.Count > 0)
            {
                LoadSourceTextures();
            }
            else
            {
                CreateSourceTextures();
            }
        }
    }
        
    [ContextMenu("Deletes and Reset All Detail Maps")]
    public void ClearnRelatedAssets()
    {
        var terrainDataAssets= AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(terrain.terrainData));
        var assetList = new List<UnityEngine.Object>();
        
        foreach (var asset in terrainDataAssets)
        {
            if (asset.name.Contains("Detail Map") || asset.name.Contains(SPLAT_MAP_PREFIX))
            {
                assetList.Add(asset);
            }
        }

        foreach (var asset in assetList)
        {
            DestroyImmediate(asset, true);
        }

        CreateSourceTextures();
    }
#endif
}

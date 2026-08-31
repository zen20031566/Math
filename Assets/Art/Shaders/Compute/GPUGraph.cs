using System;
using UnityEngine;

public class GPUGraph : MonoBehaviour
{
    [SerializeField] int resolution = 10;
    [SerializeField] FunctionLibrary.FunctionName function;
    [SerializeField] ComputeShader computeShader;
    [SerializeField, Min(0f)] float functionDuration = 1f;
    float duration;
    ComputeBuffer positionsBuffer;
    [SerializeField] Material material;
    [SerializeField] Mesh mesh;

    static readonly int positionsId = Shader.PropertyToID("_Positions");
    static readonly int resolutionId = Shader.PropertyToID("_Resolution");
    static readonly int stepId = Shader.PropertyToID("_Step");
    static readonly int timeId = Shader.PropertyToID("_GraphTime");
    
    void OnEnable()
    {
	    int stride = sizeof(float) * 3;
	    positionsBuffer = new ComputeBuffer(resolution * resolution, stride);
    }
    
    void OnDisable () 
    {
	    positionsBuffer.Release();
	    positionsBuffer = null;
    }

    private void Update()
    {
	    UpdateFunctionOnGPU();
    }

    void UpdateFunctionOnGPU () 
    {
	    float step = 2f / resolution;
	    computeShader.SetInt(resolutionId, resolution);
	    computeShader.SetFloat(stepId, step);
	    computeShader.SetFloat(timeId, Time.time);
	    computeShader.SetBuffer(0, positionsId, positionsBuffer);
	    
	    
	    int groups = Mathf.CeilToInt(resolution / 8f);
	    computeShader.Dispatch(0, groups, groups, 1);
	    
	    material.SetBuffer(positionsId, positionsBuffer);
	    material.SetFloat(stepId, step);
	    
	    var bounds = new Bounds(Vector3.zero, Vector3.one * (2f + 2f / resolution));
	    Graphics.DrawMeshInstancedProcedural(
		    mesh, 0, material, bounds, positionsBuffer.count
	    );
	    
	    Debug.Log($"Instanced objects: {positionsBuffer.count}");
    }
    
    
}

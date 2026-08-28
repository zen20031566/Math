using System;
using UnityEngine;

public class SkyManager : MonoBehaviour
{
    public Light directionalLight;
    private static readonly int DirLightLToW = Shader.PropertyToID("_DirLightLToW");
    [SerializeField, Range(0f, 24f)]private float timeOfDay = 0;
    [SerializeField] float secondsPerGameHour = 600f; //10 min = 1 in game time
    [SerializeField] private Color dayColor = Color.white;
    [SerializeField] private Color nightColor = Color.darkBlue;

    private void Update()
    {
        timeOfDay = (timeOfDay + Time.deltaTime * 1 / secondsPerGameHour) % 24f;

        HandleDirLight();

        Shader.SetGlobalMatrix(DirLightLToW,
            directionalLight.transform.localToWorldMatrix); //sends dir light transform matrix into shader
    }

    private void OnValidate()
    {
        HandleDirLight();
    }

    private void HandleDirLight()
    {
        float angle = ((timeOfDay - 6) / 24f) * 360f; //minus 6 cause 6am is 0degree
        directionalLight.transform.rotation = Quaternion.Euler(angle, 0f, 0f);
        
        float t = Mathf.InverseLerp(-0.3f, 0.25f, -directionalLight.transform.forward.y);
        float dayNightCycle = Mathf.SmoothStep(0f, 1f, t);
        
        directionalLight.color = Color.Lerp(nightColor, dayColor, dayNightCycle);
        
        // Debug.Log("Cycle:" + dayNightCycle);
        // Debug.Log("Dir:" + -directionalLight.transform.forward.y);
    }
}

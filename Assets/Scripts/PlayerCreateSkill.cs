using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCreateSkill : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private GameObject skillPrefab;
    // [SerializeField] private PlayerInputManager inputManager;
    [SerializeField] private LayerMask summonLayer;

    [SerializeField] private bool isDebug = false;
    private float maxSkillRange = 10f;
    private Camera camera;

    private void Awake()
    {
        camera = Camera.main;
    }

    private void Update()
    {
        if (isDebug == true)
        {
            DrawCameraRay();
        }
        
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TrySummon();
        }
    }

    // See what is camera aiming (idk why drawray doesnt show in game view)
    private void DrawCameraRay()
    {
        Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        lineRenderer.positionCount = 2;

        lineRenderer.SetPosition(0, new Vector3(ray.origin.x, ray.origin.y - 0.1f, ray.origin.z));
        lineRenderer.SetPosition(1, ray.origin + ray.direction * 100f);

    }

    // Temporary to see skill range
    private void OnDrawGizmos()
    {
        if (isDebug == true)
        {
            Handles.color = Color.green;
            Handles.DrawWireDisc(transform.position, Vector3.up, maxSkillRange);
        }

    }


    private void TrySummon()
    {
        Debug.Log("Summon Pressed");
        //middle of camera
        Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, summonLayer))
        {
            float distance = Vector3.Distance(transform.position, hit.point);

            if (distance <= maxSkillRange)
            {
                Debug.Log("Summoned");
                Instantiate(skillPrefab, new Vector3(hit.point.x,(hit.point.y-4.5f),hit.point.z), Quaternion.identity);
            }

        }
    }

    private void OnEnable()
    {
       
    }

    private void OnDisable()
    {

    }
}

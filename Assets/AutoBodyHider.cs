using UnityEngine;
using System.Collections.Generic;

public class AutoBodyHider : MonoBehaviour
{
    [Header("Meshes to Hide")]
    public GameObject[] bodyMeshesToHide;
    public string invisibleLayerName = "Body";

    [Header("Camera Proximity")]
    public Transform headBoneTarget;
    public float hideDistance = 1.0f;

    private bool isHidden = false;
    private Dictionary<GameObject, int> originalLayers = new Dictionary<GameObject, int>();
    private Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;

        foreach (var mesh in bodyMeshesToHide)
        {
            if (mesh != null)
            {
                originalLayers[mesh] = mesh.layer;
            }
        }
    }

    void Update()
    {
        if (mainCam == null || headBoneTarget == null) return;

        float distance = Vector3.Distance(mainCam.transform.position, headBoneTarget.position);

        if (distance <= hideDistance && !isHidden)
        {
            HideMeshes();
        }
        else if (distance > hideDistance && isHidden)
        {
            ShowMeshes();
        }
    }

    private void HideMeshes()
    {
        isHidden = true;
        int hiddenLayer = LayerMask.NameToLayer(invisibleLayerName);
        if (hiddenLayer == -1) hiddenLayer = 2;

        foreach (var mesh in bodyMeshesToHide)
        {
            if (mesh != null) mesh.layer = hiddenLayer;
        }
    }

    private void ShowMeshes()
    {
        isHidden = false;
        foreach (var mesh in bodyMeshesToHide)
        {
            if (mesh != null && originalLayers.ContainsKey(mesh))
            {
                mesh.layer = originalLayers[mesh];
            }
        }
    }
}
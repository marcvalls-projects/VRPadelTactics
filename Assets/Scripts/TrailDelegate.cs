using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.AffordanceSystem.Receiver.Rendering;

public class TrailDelegate
{
    private TrailRenderer trailRenderer;
    private LineRenderer[] lineRenderers;
    private Material trailMaterial;
    private Material alliedPathMaterial;
    private Material enemyPathMaterial;
    private Material expertPathMaterial;
    
    public TrailDelegate(TrailRenderer trailRenderer, LineRenderer redLineRenderer, LineRenderer blueLineRenderer, Material trailMaterial, Material alliedPathMaterial, Material enemyPathMaterial, Material expertPathMaterial)
    {
        this.trailRenderer = trailRenderer;
        this.lineRenderers = new LineRenderer[]{redLineRenderer, blueLineRenderer};
        this.trailMaterial = trailMaterial;
        this.alliedPathMaterial = alliedPathMaterial;
        this.enemyPathMaterial = enemyPathMaterial;
        this.expertPathMaterial = expertPathMaterial;
        
        trailRenderer.enabled = false;
        lineRenderers[0].enabled = false;
        lineRenderers[1].enabled = false;
    }

    public void Clear()
    {
        bool wasEnabled = trailRenderer.enabled;
        
        trailRenderer.enabled = false;
        trailRenderer.Clear();
        trailRenderer.enabled = wasEnabled;
        
        lineRenderers[0].positionCount = 0;
        lineRenderers[1].positionCount = 0;
    }

    public void ActivateTrail()
    {
        trailRenderer.material = trailMaterial;
        trailRenderer.startWidth = 0.1f;
        trailRenderer.endWidth = 0.0f;
        trailRenderer.time = 0.5f;
        
        trailRenderer.enabled = true;
    }
    
    public void StopTrail()
    {
        trailRenderer.enabled = false;
    }
    
    public void ShowPath(Vector3[] path, Color colour)
    {
        LineRenderer lineRenderer = lineRenderers[0];
        if (colour != Color.red)
        {
            lineRenderer = lineRenderers[1];
        }

        lineRenderer.enabled = true;
        lineRenderer.material = colour == Color.blue ? alliedPathMaterial : (colour == Color.red ? enemyPathMaterial : expertPathMaterial);
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;
        lineRenderer.startColor = lineRenderer.endColor = colour;
        
        lineRenderer.positionCount = path.Length;
        lineRenderer.SetPositions(path);
    }

    public void ClearPaths(Color colour)
    {
        if (colour == Color.red)
        {
            lineRenderers[0].positionCount = 0;
        }
        else if (colour == Color.blue || colour == Color.green)
        {
            lineRenderers[1].positionCount = 0;
        }
    }
    
}

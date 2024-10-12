using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class InputDelegate
{
    private XRRayInteractor interactor;
    private InputActionAsset inputActionAsset;
    private BallController ballController;
    private GameObject racket;
    private GameObject rightControllerVisual;
    private NearFarInteractor nearFarInteractor;

    private bool hasNewInput = false;
    private Vector3 newInput;

    // Height Interactor Data
    private Vector3 origin;
    private Vector3 target;
    private float gravity;
    private Vector3 previousVelocityVector = Vector3.zero;
    
    private static readonly float maxVerticalVelocity = 15f;

    public enum InputType
    {
        NearFar,
        TeleportInteractor,
        TargetInteractor,
        HeightInteractor,
        Racket
    }
    
    InputType currentInputType = InputType.NearFar;
    
    public InputDelegate(XRRayInteractor interactor, NearFarInteractor nearFarInteractor, InputActionAsset inputActionAsset, BallController ballController, GameObject rightControllerVisual)
    {
        this.interactor = interactor;
        this.nearFarInteractor = nearFarInteractor;
        this.inputActionAsset = inputActionAsset;
        this.ballController = ballController;
        this.rightControllerVisual = rightControllerVisual;
        
        SetInputType(InputType.NearFar);
        
        this.inputActionAsset.FindActionMap("XRI Right Interaction").FindAction("UI Press").performed += OnPress;
    }

    private Vector3 GetStartingVelocity()
    {
        float vy = rightControllerVisual.transform.forward.normalized.y * maxVerticalVelocity; 
        // 0 = (y0 - yf) + (vy)t + (0.5a)t^2
        float t = (-vy + Mathf.Sqrt(vy*vy -4f * (origin.y - target.y) * (0.5f*gravity)))/(2 * (0.5f*gravity));
        
        return new Vector3((target.x - origin.x)/t, vy, (target.z - origin.z)/t);
    }

    private Vector3[] GetHeightSetPath()
    {
        float vy = rightControllerVisual.transform.forward.normalized.y * maxVerticalVelocity; 

        // 0 = (y0 - yf) + (vy)t + (0.5a)t^2
        float t = (-vy + Mathf.Sqrt(vy*vy -4f * (origin.y - target.y) * (0.5f*gravity)))/(2 * (0.5f*gravity));

        Vector3[] result = new Vector3[51];
        for (int i = 0; i <= 50; ++i)
        {
            result[i] = new Vector3(origin.x + (target.x - origin.x) * i / 50f, origin.y + vy * (i*t/50) + 0.5f * gravity * (i*t/50) * (i*t/50), origin.z + (target.z - origin.z) * i / 50f);
        }
        
        return result;
    }

    private Vector3 GetTargetSelected()
    {
        Vector3[] path = GetTargetSetPath();
        return path[path.Length - 1];
    }
    
    private Vector3[] GetTargetSetPath()
    {
        Vector3 velocity = rightControllerVisual.transform.forward.normalized * maxVerticalVelocity;
        float vx = velocity.x;
        float vy = velocity.y;
        float vz = velocity.z;
        
        // 0 = (y0 - 0) + (vy)t + (0.5a)t^2
        float t = (-vy + Mathf.Sqrt(vy*vy -4f * (origin.y) * (0.5f*gravity)))/(2 * (0.5f*gravity));

        Vector3[] result = new Vector3[51];
        for (int i = 0; i <= 50; ++i)
        {
            result[i] = new Vector3(origin.x + vx * (i*t/50), origin.y + vy * (i*t/50) + 0.5f * gravity * (i*t/50) * (i*t/50), origin.z + vz * (i*t/50));
        }
        
        return result;
    }

    public void Update()
    {
        if (InputIsValid())
        {
            if (currentInputType == InputType.TargetInteractor)
            {
                ballController.DrawPath(GetTargetSetPath(), Color.blue);
            }
            else if (currentInputType == InputType.HeightInteractor)
            {
                ballController.DrawPath(GetHeightSetPath(), Color.blue);
            }
        }
        else
        {
            ballController.ClearPaths(Color.blue);
        }
    }

    private bool InputIsValid()
    {
        switch (currentInputType)
        {
            case InputType.TargetInteractor:
                Vector3 currentTarget = GetTargetSelected();
                return origin.z * currentTarget.z < 0 && Math.Abs(currentTarget.z) < 10f && Math.Abs(currentTarget.x) < 5f;
            
            case InputType.HeightInteractor:
                Vector3[] path = GetHeightSetPath();

                int i;
                for (i = 1; i < path.Length; ++i) if (path[i - 1].z * path[i].z < 0) break;

                return path[i - 1].y >= 1f || path[i].y >= 1f;
            
            default:
                return true;
        }
    }

    void OnPress(InputAction.CallbackContext context)
    {
        if (interactor.gameObject.activeSelf && interactor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            newInput = hit.point;
            hasNewInput = true;
        }
        else if (InputIsValid())
        {
            if (currentInputType == InputType.TargetInteractor)
            {
                newInput = GetTargetSelected();
                hasNewInput = true;
            }
            else if (currentInputType == InputType.HeightInteractor)
            {
                newInput = GetStartingVelocity();
                hasNewInput = true;
            }
        }
    }

    public bool HasNewInput()
    {
        return hasNewInput;
    }

    public Vector3 GetNewInput()
    {
        hasNewInput = false;
        return newInput;
    }

    public void SetRacket(Transform racketTransform)
    {
        racket = racketTransform.gameObject;
        racket.SetActive(currentInputType == InputType.Racket);
    }

    public void SetTargetInteractorData(Vector3 origin, float gravity)
    {
        this.origin = origin;
        this.gravity = -gravity;
    }

    public void SetHeightInteractorData(Vector3 origin, Vector3 target, float gravity)
    {
        this.origin = origin;
        this.target = target;
        this.gravity = -gravity;
    }

    public void SetInputType(InputType inputType)
    {
        currentInputType = inputType;
        hasNewInput = false;

        if (racket)
        {
            racket.SetActive(inputType == InputType.Racket);
        }
        rightControllerVisual.SetActive(inputType != InputType.Racket);
        
        nearFarInteractor.gameObject.SetActive(inputType == InputType.NearFar);
        interactor.gameObject.SetActive(inputType == InputType.TeleportInteractor);
    }
}

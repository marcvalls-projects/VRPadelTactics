using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{   
    public Canvas finishUI;
    public TextMeshProUGUI finishText;
    public BallController ballController;
    public Image overlay;
    
    private const float overlayFadeTime = 0.75f;
    private bool watchExpertRequested = false;
    
    void Start()
    {
        SetOverlayAlpha(0);
        ShowStartingUI();
    }
    
    public void StartRandomGame()
    {
        HideUI();
        StartCoroutine(RunGameSequence());
    }

    public IEnumerator ExecuteFadeAction(Action action)
    {
        FadeToBlack();
        yield return new WaitForSeconds(overlayFadeTime);

        action();
        
        FadeFromBlack();
        yield return new WaitForSeconds(overlayFadeTime);
    }

    private Color GetColorForPercentage(int percentage)
    {
        percentage = Math.Max(percentage, 40);
        percentage = Math.Min(percentage, 60);
        float p = (percentage - 40) / 20f;
        return new Color(1 - p, p, 0, 1);
    }

    private IEnumerator ExecuteScoreAnim(int percentage)
    {
        int currentPercentage = 0;
        finishText.text = "0%";
        finishText.color = GetColorForPercentage(0);

        while (currentPercentage < percentage)
        {
            currentPercentage = Math.Min(percentage, currentPercentage + UnityEngine.Random.Range(1, 3));
            finishText.text = currentPercentage.ToString() + "%";
            finishText.color = GetColorForPercentage(currentPercentage);
            yield return new WaitForSeconds(0.05f);
        }
    }

    public void LaunchFinishUI(Vector3 selfPosition)
    {
        finishUI.transform.position = new Vector3(Mathf.Sign(selfPosition.x) * Mathf.Min(3.5f, Mathf.Abs(selfPosition.x)), finishUI.transform.position.y, Mathf.Sign(selfPosition.z) * Mathf.Max(1, Mathf.Abs(selfPosition.z) - 4));
        finishUI.transform.rotation = Quaternion.Euler(transform.rotation.x, selfPosition.z < 0f ? 0 : 180, 0);
        finishUI.gameObject.SetActive(true);
    }

    public void LaunchFinishUI(Vector3 selfPosition, int percentageScore)
    {
        LaunchFinishUI(selfPosition);
        StartCoroutine(ExecuteScoreAnim(percentageScore));
    }

    public void SetWatchExpertRequested(bool requested)
    {
        watchExpertRequested = requested;
    }

    public bool HasWatchExpertBeenRequested()
    {
        return watchExpertRequested;
    }
    
    private IEnumerator RunGameSequence()
    {
        ballController.StopAllCoroutines();
        
        FadeToBlack();
        yield return new WaitForSeconds(overlayFadeTime);
        
        ballController.SetupRandomGame();
        
        FadeFromBlack();
        yield return new WaitForSeconds(overlayFadeTime);
        
        ballController.StartLiveGame();
    }
    
    private void FadeToBlack()
    {
        StartCoroutine(FadeTo(1));
    }

    private void FadeFromBlack()
    {
        StartCoroutine(FadeTo(0));
    }

    private IEnumerator FadeTo(float targetAlpha)
    {
        float startAlpha = overlay.color.a;
        float time = 0;

        while (time < overlayFadeTime)
        {
            time += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, targetAlpha, time / overlayFadeTime);
            SetOverlayAlpha(alpha);
            yield return null;
        }

        SetOverlayAlpha(targetAlpha);
    }

    private void SetOverlayAlpha(float alpha)
    {
        Color color = overlay.color;
        color.a = alpha;
        overlay.color = color;
    }

    public void HideUI()
    {
        Light[] lights = FindObjectsOfType<Light>();
        
        foreach (Light light in lights)
        {
            light.intensity = 2;
        }
        
        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(false);
        }
        
        finishUI.gameObject.SetActive(false);
    }

    public void ShowStartingUI()
    {
        Light[] lights = FindObjectsOfType<Light>();
        
        foreach (Light light in lights)
        {
            light.intensity = 0;
        }
        
        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(true);
        }
    }

    public void Exit()
    {  
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

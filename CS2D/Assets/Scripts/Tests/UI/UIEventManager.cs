using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIEventManager : MonoBehaviour
{
    public TMP_Text textComponent;
    private CanvasGroup canvasGroup;

    private const float EventMessageTimeout = 3.5f;
    private string clientName;
    private const float FadeDuration = 0.5f;

    private void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        clientName = GameObject.FindWithTag("ClientEntity").GetComponent<ClientEntity>().ClientName;
    }

    public void ShowKillEvent(string shooterName, string victimName) // NEW EVENTS STEP OVER CURRENT ONES...
    {
        if (shooterName == clientName)
        {
            textComponent.text = $"YOU KILLED {victimName}";
        } else if (victimName == clientName)
        {
            textComponent.text = $"YOU WERE KILLED BY {shooterName}";
        }
        else
        {
            textComponent.text = $"{shooterName} KILLED {victimName}";
        }
        
        StartCoroutine(RunEventFadeCycle());
    }

    private IEnumerator RunEventFadeCycle()
    {
        var counter = 0f;
        while (counter < FadeDuration)
        {
            counter += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0, 1, counter / FadeDuration);
            yield return null;
        }
        
        yield return new WaitForSeconds(EventMessageTimeout);
        
        counter = 0f;
        while (counter < FadeDuration)
        {
            counter += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1, 0, counter / FadeDuration);
            yield return null;
        }
    }
}

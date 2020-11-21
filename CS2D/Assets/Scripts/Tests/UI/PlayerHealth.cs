using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    public Slider healthSlider;

    public void SetPlayerHealth(float value)
    {
        healthSlider.value = value;
    }

    public void TogglePlayerHealth()
    {
        healthSlider.gameObject.SetActive(!healthSlider.gameObject.activeSelf);
    }
}

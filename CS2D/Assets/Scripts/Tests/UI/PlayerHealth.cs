using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    public Healthbar healthbar;

    public void SetPlayerHealth(int value)
    {
        healthbar.SetHealth(value);
    }

    public void TogglePlayerHealth()
    {
        healthbar.gameObject.SetActive(!healthbar.gameObject.activeSelf);
    }

    public void InitializePlayerHealth(int health)
    {
        healthbar.maximumHealth = health;
        SetPlayerHealth(health);
    }
}

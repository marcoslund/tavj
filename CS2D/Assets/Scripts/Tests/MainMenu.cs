using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class MainMenu : MonoBehaviour
{
    public GameObject title;
    public GameObject mainMenu;
    public GameObject playMenu;
    
    public void OpenPlayMenu()
    {
        title.SetActive(false);
        mainMenu.SetActive(false);
        playMenu.SetActive(true);   
    }
    
    public void QuitGame()
    {
        Application.Quit();
    }
}

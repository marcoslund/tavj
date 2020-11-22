using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayMenu : MonoBehaviour
{
    public GameObject title;
    public GameObject mainMenu;
    public GameObject playMenu;
    public ConnectionManager connectionManager;

    public Button[] actionButtons;

    public TMP_InputField playerNameInput;
    public TMP_InputField gameIpInput;
    
    public void ReturnToMainMenu()
    {
        playMenu.SetActive(false);
        title.SetActive(true);
        mainMenu.SetActive(true);
    }
    
    public void HostGame()
    {
        SavePlayerPrefs();
        SceneManager.LoadScene("Server Game");
    }

    public void JoinGame()
    {
        SavePlayerPrefs();
        BlockActions();
        connectionManager.InitializePlayerConnection();
    }

    private void BlockActions()
    {
        foreach (var btn in actionButtons)
        {
            btn.interactable = false;
        }
    }

    private void SavePlayerPrefs()
    {
        var playerName = playerNameInput.text;
        var ip = gameIpInput.text;
        PlayerPrefs.SetString("PlayerName", string.IsNullOrWhiteSpace(playerName)? "PLAYER" : playerName);
        PlayerPrefs.SetString("ServerIp", string.IsNullOrWhiteSpace(ip)? "127.0.0.1" : ip);
        PlayerPrefs.Save();
    }
}

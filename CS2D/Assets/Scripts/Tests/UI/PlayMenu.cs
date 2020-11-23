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
        connectionManager.StopPlayerConnection();
        BlockActions(false);
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
        BlockActions(true);
        connectionManager.InitializePlayerConnection();
    }

    private void BlockActions(bool value)
    {
        foreach (var btn in actionButtons)
        {
            btn.interactable = !value;
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

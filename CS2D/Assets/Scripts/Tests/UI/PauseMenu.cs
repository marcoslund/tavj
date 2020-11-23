using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public GameObject pauseMenu;
    private ClientEntity clientEntity;
    private FirstPersonView firstPersonView;

    // Start is called before the first frame update
    private void Start()
    {
        var clientEntityGO = GameObject.FindWithTag("ClientEntity");
        clientEntity = clientEntityGO.GetComponent<ClientEntity>();
        firstPersonView = clientEntityGO.GetComponent<FirstPersonView>();
    }

    // Update is called once per frame
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            var cursorState = Cursor.lockState;
            Cursor.lockState = cursorState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
            firstPersonView.enabled = !firstPersonView.enabled;
            var gunManager = GameObject.FindWithTag("Weapon").GetComponent<GunManager>();
            gunManager.enabled = !gunManager.enabled;
            pauseMenu.SetActive(!pauseMenu.activeSelf);
        }
    }
    
    public void PauseMenuResume()
    {
        Cursor.lockState = CursorLockMode.Locked;
        firstPersonView.enabled = true;
        GameObject.FindWithTag("Weapon").GetComponent<GunManager>().enabled = true;
        pauseMenu.SetActive(false);
    }
    
    public void GoToMainMenu()
    {
        clientEntity.DisconnectFromPauseMenu = true;
        clientEntity.SendPlayerDisconnect();
        pauseMenu.SetActive(false);
        SceneManager.LoadScene(0);
    }
}

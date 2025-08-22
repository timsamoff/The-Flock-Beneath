using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Interactions : MonoBehaviour
{

    public void PlayNext()
    {
        SceneManager.LoadScene("Game");
    }

    public void About()
    {
        SceneManager.LoadScene("About");
    }

    public void Credits()
    {
        SceneManager.LoadScene("Credits");
    }
    
    public void Menu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
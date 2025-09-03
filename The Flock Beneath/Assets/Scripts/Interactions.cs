using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Interactions : MonoBehaviour
{

    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip clickSound;

    private AudioSource audioSource;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

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

    public void RestartMusic()
    {
        AudioFader audioFader = FindFirstObjectByType<AudioFader>();
        if (audioFader != null)
        {
            audioFader.RestartAndFadeIn();
        }
    }

    public void PlayHover()
    {
        audioSource.PlayOneShot(hoverSound, 0.25f);
    }

    public void PlayClick()
    {
        audioSource.PlayOneShot(clickSound, 0.5f);
    }
}
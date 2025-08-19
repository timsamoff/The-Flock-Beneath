using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Interactions : MonoBehaviour {

    public void PlayNext()
    {
        SceneManager.LoadScene("Game");
    }
}
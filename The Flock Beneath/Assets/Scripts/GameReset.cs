using UnityEngine;

public class GameReset : MonoBehaviour
{
    void Start()
    {
        PlayerPrefs.DeleteAll();
        Debug.Log("PlayerPrefs reset on MainMenu load.");
    }
}
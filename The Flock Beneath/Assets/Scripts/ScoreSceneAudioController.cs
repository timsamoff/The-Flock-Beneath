using UnityEngine;

public class ScoreSceneAudioController : MonoBehaviour
{
    void Start()
    {
        // Fade out music when scene loads
        AudioFader audioFader = FindFirstObjectByType<AudioFader>();
        if (audioFader != null)
        {
            audioFader.FadeOut();
        }
    }
}
using UnityEngine;

public class ScoreSceneAudioController : MonoBehaviour
{
    void Start()
    {
        // Fade out the music when this scene loads
        AudioFader audioFader = FindFirstObjectByType<AudioFader>();
        if (audioFader != null)
        {
            audioFader.FadeOut();
        }
    }
}
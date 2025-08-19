using UnityEngine;
using TMPro;

public class ScoreDisplay : MonoBehaviour
{
    public TextMeshProUGUI scoreText;

    void Start()
    {
        Cursor.visible = true;
        int previousLevel = PlayerPrefs.GetInt("NextLevel") - 1;
        float stars = GetLevelScore(previousLevel);
        
        Debug.Log($"ScoreDisplay: Previous level = {previousLevel}, Stars = {stars}");
        
        string scoreString = $"Level {previousLevel} Score: {stars:F1} stars";
        scoreText.text = scoreString;
    }

    float GetLevelScore(int level)
    {
        return PlayerPrefs.GetFloat($"Level{level}Stars", 0f);
    }
}
using System.Collections;
using UnityEngine;

public class SheepBleats : MonoBehaviour
{
    [Header("Bleat Sound Effects")]
    [SerializeField] private AudioClip[] bleatSounds;
    
    [Header("Timing Settings")]
    [SerializeField] private Vector2 timeBetweenBleatsRange = new Vector2(3f, 8f);
    
    [Header("Audio Settings")]
    [SerializeField] private float volume = 1f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.8f, 1.2f);
    
    private Coroutine bleatCoroutine;
    
    void Start()
    {
        if (bleatSounds == null || bleatSounds.Length == 0)
        {
            Debug.LogWarning($"No bleat sounds assigned to {gameObject.name}!");
            return;
        }
        
        bleatCoroutine = StartCoroutine(BleatRoutine());
    }
    
    void OnDestroy()
    {
        if (bleatCoroutine != null)
        {
            StopCoroutine(bleatCoroutine);
        }
    }
    
    IEnumerator BleatRoutine()
    {
        while (true)
        {
            float waitTime = Random.Range(timeBetweenBleatsRange.x, timeBetweenBleatsRange.y);
            yield return new WaitForSeconds(waitTime);
            
            PlayRandomBleat();
        }
    }
    
    void PlayRandomBleat()
    {
        if (bleatSounds == null || bleatSounds.Length == 0) return;
        
        AudioClip randomBleat = bleatSounds[Random.Range(0, bleatSounds.Length)];
        
        if (randomBleat == null)
        {
            Debug.LogWarning($"Null audio clip found in bleat sounds array on {gameObject.name}!");
            return;
        }
        
        GameObject audioObject = new GameObject("SheepBleatAudio");
        audioObject.transform.position = transform.position;
        
        AudioSource audioSource = audioObject.AddComponent<AudioSource>();
        
        audioSource.clip = randomBleat;
        audioSource.volume = volume;
        audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        audioSource.spatialBlend = 1f;
        audioSource.playOnAwake = false;
        
        audioSource.Play();
        

        StartCoroutine(DestroyAudioObject(audioObject, randomBleat.length));
    }
    
    IEnumerator DestroyAudioObject(GameObject audioObj, float delay)
    {
        yield return new WaitForSeconds(delay + 0.1f);
        
        if (audioObj != null)
        {
            Destroy(audioObj);
        }
    }
    
    public void TriggerBleat()
    {
        PlayRandomBleat();
    }
    
    public void StopBleating()
    {
        if (bleatCoroutine != null)
        {
            StopCoroutine(bleatCoroutine);
            bleatCoroutine = null;
        }
    }
    
    public void StartBleating()
    {
        if (bleatCoroutine == null)
        {
            bleatCoroutine = StartCoroutine(BleatRoutine());
        }
    }
    
    public void SetTimingRange(float minTime, float maxTime)
    {
        timeBetweenBleatsRange = new Vector2(minTime, maxTime);
    }
}
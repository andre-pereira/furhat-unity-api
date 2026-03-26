using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [SerializeField] private AudioClip moveSound;
    [SerializeField] private AudioClip captureSound;
    [SerializeField] private AudioClip selectSound;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
    }

    public void PlayMove()
    {
        if (moveSound != null)
            audioSource.PlayOneShot(moveSound);
    }

    public void PlayCapture()
    {
        if (captureSound != null)
            audioSource.PlayOneShot(captureSound);
    }

    public void PlaySelect()
    {
        if (selectSound != null)
            audioSource.PlayOneShot(selectSound);
    }
}
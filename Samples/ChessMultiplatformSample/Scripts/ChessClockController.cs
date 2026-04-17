using System;
using TMPro;
using UnityEngine;

public class ChessClockController : MonoBehaviour
{
    [SerializeField] private TMP_Text whiteTimerText;
    [SerializeField] private TMP_Text blackTimerText;

    [Header("Colors")]
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color inactiveColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color lowTimeColor = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField] private float lowTimeThresholdSeconds = 30f;

    public event Action<PieceSide> OnTimeExpired;

    private float whiteTimeRemaining;
    private float blackTimeRemaining;
    private PieceSide activeSide;
    private bool isRunning;

    private void Update()
    {
        if (!isRunning)
            return;

        if (activeSide == PieceSide.White)
        {
            whiteTimeRemaining -= Time.deltaTime;
            if (whiteTimeRemaining <= 0f)
            {
                whiteTimeRemaining = 0f;
                isRunning = false;
                UpdateDisplay();
                OnTimeExpired?.Invoke(PieceSide.White);
                return;
            }
        }
        else
        {
            blackTimeRemaining -= Time.deltaTime;
            if (blackTimeRemaining <= 0f)
            {
                blackTimeRemaining = 0f;
                isRunning = false;
                UpdateDisplay();
                OnTimeExpired?.Invoke(PieceSide.Black);
                return;
            }
        }

        UpdateDisplay();
    }

    public void Initialize(float totalSeconds)
    {
        whiteTimeRemaining = totalSeconds;
        blackTimeRemaining = totalSeconds;
        isRunning = false;
        UpdateDisplay();
    }

    public void StartTicking(PieceSide side)
    {
        activeSide = side;
        isRunning = true;
        UpdateDisplay();
    }

    public void SwitchTo(PieceSide side)
    {
        activeSide = side;
        UpdateDisplay();
    }

    public void StopAll()
    {
        isRunning = false;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        UpdateTimer(whiteTimerText, whiteTimeRemaining, activeSide == PieceSide.White && isRunning);
        UpdateTimer(blackTimerText, blackTimeRemaining, activeSide == PieceSide.Black && isRunning);
    }

    private void UpdateTimer(TMP_Text text, float seconds, bool isActive)
    {
        if (text == null)
            return;

        text.text = FormatTime(seconds);

        if (seconds <= lowTimeThresholdSeconds && seconds > 0f && isActive)
            text.color = lowTimeColor;
        else if (isActive)
            text.color = activeColor;
        else
            text.color = inactiveColor;
    }

    private static string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return $"{m:00}:{s:00}";
    }
}

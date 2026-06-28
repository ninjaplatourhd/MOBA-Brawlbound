using TMPro;
using UnityEngine;

public class FPSCounter : MonoBehaviour
{
    [SerializeField] private TMP_Text fpsText;
    [SerializeField] private float updateInterval = 0.25f;

    private float timer;
    private int frameCount;
    private float accumulatedTime;

    private void Awake()
    {
        if (fpsText == null)
            fpsText = GetComponent<TMP_Text>();
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;

        timer += deltaTime;
        accumulatedTime += deltaTime;
        frameCount++;

        if (timer < updateInterval)
            return;

        float fps = frameCount / accumulatedTime;
        float frameMs = accumulatedTime / frameCount * 1000f;

        if (fpsText != null)
        {
            fpsText.text = $"FPS: {fps:0}\nMS: {frameMs:0.0}";
        }

        timer = 0f;
        frameCount = 0;
        accumulatedTime = 0f;
    }
}
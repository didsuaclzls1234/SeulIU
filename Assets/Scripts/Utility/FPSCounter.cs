using UnityEngine;
using TMPro;

public class FPSCounter : MonoBehaviour
{
    public TextMeshProUGUI fpsText; // 빈 텍스트 UI 하나 연결
    private float deltaTime = 0.0f;

    void Update()
    {
        // 프레임이 너무 튀지 않게 보정해서 계산
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        float fps = 1.0f / deltaTime;

        // 소수점 버리고 깔끔하게 표시
        fpsText.text = $"FPS: {Mathf.Ceil(fps)}";
    }
}
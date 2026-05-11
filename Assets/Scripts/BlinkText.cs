using TMPro;
using System.Collections;
using UnityEngine;

public class BlinkText : MonoBehaviour
{
    public TextMeshProUGUI blinkText;

    [Header("깜빡임 설정")]
    public float blinkInterval = 0.5f; // 인스펙터에서 속도 조절

    void Start()
    {
        StartCoroutine(BlinkRoutine());
    }

    private IEnumerator BlinkRoutine()
    {
        while (true)
        {
            blinkText.alpha = 1f;
            yield return new WaitForSeconds(blinkInterval);
            blinkText.alpha = 0f;
            yield return new WaitForSeconds(blinkInterval);
        }
    }
}
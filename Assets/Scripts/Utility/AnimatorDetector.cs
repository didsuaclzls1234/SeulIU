using UnityEngine;

public class AnimatorDetector : MonoBehaviour
{
    void Start()
    {
        // 1초 뒤에 현재 씬에 켜져있는 모든 애니메이터를 수색합니다.
        Invoke("FindCulprits", 1.0f);
    }

    void FindCulprits()
    {
        Animator[] allAnimators = FindObjectsByType<Animator>(FindObjectsSortMode.None);
        int activeCount = 0;

        foreach (Animator anim in allAnimators)
        {
            // 애니메이터 컴포넌트가 켜져있고, 게임 오브젝트도 활성화된 놈들만 잡아냄
            if (anim.enabled && anim.gameObject.activeInHierarchy)
            {
                Debug.Log($"🚨 범인 검거: [{anim.gameObject.name}] - 놈이 애니메이터를 돌리고 있습니다!");
                activeCount++;
            }
        }

        Debug.Log($"🔥 총 켜져있는 애니메이터 개수: {activeCount}개");
    }
}
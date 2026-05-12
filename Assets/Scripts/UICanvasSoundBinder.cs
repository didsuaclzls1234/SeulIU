using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UICanvasSoundBinder : MonoBehaviour
{
    [Header("설정된 이름으로 사운드 매니저에서 재생함")]
    public string hoverSoundName = "Tick";
    public string clickSoundName = "Tick";

    void Start()
    {
        // 1. 현재 오브젝트를 포함한 하위의 모든 Button 컴포넌트를 찾음
        Button[] buttons = GetComponentsInChildren<Button>(true);

        foreach (Button btn in buttons)
        {
            // 2. 각 버튼에 EventTrigger가 없다면 추가
            EventTrigger trigger = btn.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = btn.gameObject.AddComponent<EventTrigger>();

            // 3. 마우스 진입(Hover) 이벤트 바인딩
            AddEvent(trigger, EventTriggerType.PointerEnter, () => {
                // 사운드 매니저 인스턴스를 통해 이름으로 재생
                if(SoundManager.Instance != null)
                    SoundManager.Instance.PlaySFX(hoverSoundName);
            });

            // 4. 클릭(Click) 이벤트는 Button의 onClick을 사용해도 됨
            btn.onClick.AddListener(() => {
                if(SoundManager.Instance != null)
                    SoundManager.Instance.PlaySFX(clickSoundName);
            });
        }
    }

    private void AddEvent(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = type;
        entry.callback.AddListener((data) => action());
        trigger.triggers.Add(entry);
    }
}
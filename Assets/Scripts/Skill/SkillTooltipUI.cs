using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 스킬 툴팁 패널을 관리하는 싱글턴.
/// Canvas 하위에 툴팁 패널 오브젝트를 만들고 이 컴포넌트를 붙여주세요.
///
/// [필요한 자식 오브젝트 구조 예시]
/// TooltipPanel
///   ├─ SkillNameText       (TMP)
///   ├─ SkillTypeText       (TMP)
///   ├─ SPCostText          (TMP)
///   ├─ CooldownText        (TMP)
///   ├─ DurationText        (TMP)   ← 지속 턴이 0이면 비활성화
///   └─ DescriptionText     (TMP)
/// </summary>
/// 

public class SkillTooltipUI : MonoBehaviour
{
    public static SkillTooltipUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject tooltipPanel;

    [Header("Texts")]
    public TextMeshProUGUI skillNameText;
    public TextMeshProUGUI skillTypeText;
    public TextMeshProUGUI spCostText;
    public TextMeshProUGUI cooldownText;
    public TextMeshProUGUI durationText;     // 지속 턴 (없으면 숨김)
    public TextMeshProUGUI descriptionText;

    [Header("Follow Settings")]
    public Vector2 offset = new Vector2(15f, -15f); // 마우스 커서 기준 오프셋
    public RectTransform canvasRect;                 // 부모 Canvas의 RectTransform

    private RectTransform tooltipRect;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        tooltipRect = tooltipPanel.GetComponent<RectTransform>();
        tooltipPanel.SetActive(false);
    }

    private void Update()
    {
        if (!tooltipPanel.activeSelf) return;
        FollowMouse();
    }

    // ──────────────────────────────────────────────
    // 외부 호출 API
    // ──────────────────────────────────────────────

    /// <summary>SkillData로 툴팁 표시 (스킬 선택 화면 / 인게임 버튼)</summary>
    public void Show(SkillData data)
    {
        skillNameText.text  = data.skillName;
        skillTypeText.text  = $"[{data.type}]";
        spCostText.text     = data.spCost == 0 ? "SP : -" : $"SP : {data.spCost}";
        cooldownText.text   = data.cooldown == 0 ? "쿨타임 : 없음" : $"쿨타임 : {data.cooldown}턴";

        bool hasDuration = data.durationTurn > 0;
        if (durationText != null)
        {
            durationText.gameObject.SetActive(hasDuration);
            if (hasDuration) durationText.text = $"지속 : {data.durationTurn}턴";
        }

        descriptionText.text = data.description;
        tooltipPanel.SetActive(true);
    }

    /// <summary>ActiveEffect로 툴팁 표시 (버프/디버프 아이콘)</summary>
    public void Show(ActiveEffect effect)
    {
        skillNameText.text  = effect.effectName;
        skillTypeText.text  = effect.isBuff ? "[버프]" : "[디버프]";
        spCostText.text     = "";
        cooldownText.text   = "";

        if (durationText != null)
        {
            durationText.gameObject.SetActive(true);
            durationText.text = $"남은 턴 : {effect.remainingTurns}";
        }

        descriptionText.text = effect.description;
        tooltipPanel.SetActive(true);
    }

    public void Hide()
    {
        tooltipPanel.SetActive(false);
    }

    // ──────────────────────────────────────────────
    // 내부 로직
    // ──────────────────────────────────────────────

    private void FollowMouse()
    {
        // 스크린 좌표 → Canvas 로컬 좌표 변환
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            Input.mousePosition,
            null, // Screen Space Overlay 캔버스는 카메라 null
            out Vector2 localPoint
        );

        // 커서가 화면 중심 기준 어느 쪽인지 판단
        bool isRight  = Input.mousePosition.x > Screen.width  / 2f;
        bool isBottom = Input.mousePosition.y < Screen.height / 2f;

        // 방향에 따라 Pivot 변경 → 툴팁이 커서 반대편으로 펼쳐짐
        tooltipRect.pivot = new Vector2(
            isRight  ? 1f : 0f,
            isBottom ? 0f : 1f
        );

        // 오프셋도 방향에 맞게 부호 반전
        Vector2 appliedOffset = new Vector2(
            offset.x * (isRight  ? -1f : 1f),
            offset.y * (isBottom ? -1f : 1f)
        );

        tooltipRect.localPosition = localPoint + appliedOffset;
    }
}
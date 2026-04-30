using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 스킬 툴팁을 띄울 UI 요소(버튼/아이콘)에 붙이는 트리거.
/// SetData() 또는 SetEffect()로 표시할 데이터를 주입하면 됩니다.
///
/// [사용법]
/// 1. 스킬 선택 버튼, 인게임 스킬 버튼, 버프 아이콘 프리팹에 이 컴포넌트 추가
/// 2. 코드에서 해당 데이터를 주입 (아래 주석 참고)
/// </summary>
public class SkillTooltipTrigger : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    // ─── 두 가지 데이터 모드 중 하나만 세팅하면 됩니다 ───
    private SkillData?    _skillData   = null;
    private ActiveEffect  _activeEffect = null;
    private bool          _useEffect   = false;

    /// <summary>SkillData 주입 (스킬 선택 화면 / 인게임 버튼)</summary>
    public void SetData(SkillData data)
    {
        _skillData  = data;
        _useEffect  = false;
    }

    /// <summary>ActiveEffect 주입 (버프/디버프 아이콘)</summary>
    public void SetEffect(ActiveEffect effect)
    {
        _activeEffect = effect;
        _useEffect    = true;
    }

    // ──────────────────────────────────────────────
    // IPointerEnterHandler / IPointerExitHandler
    // ──────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {   
        Debug.Log($"[Tooltip] OnPointerEnter 호출됨 / skillData: {_skillData?.skillName}");
    
        if (SkillTooltipUI.Instance == null) return;

        if (_useEffect && _activeEffect != null)
            SkillTooltipUI.Instance.Show(_activeEffect);
        else if (_skillData.HasValue)
            SkillTooltipUI.Instance.Show(_skillData.Value);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (SkillTooltipUI.Instance == null) return;
        SkillTooltipUI.Instance.Hide();
    }
}
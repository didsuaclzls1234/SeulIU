using UnityEngine;
using System.Collections.Generic;
using System;

[System.Serializable]
public class ActiveEffect
{
    public int skillId;           // 스킬 정보 (CSV 데이터 조회용)
    public int remainingTurns;    // 남은 턴 수
    public bool isBuff;           // true면 내 버프(파란 테두리), false면 디버프(빨간 테두리)
    public string effectName;     // 툴팁용 이름
    public string description;    // 툴팁용 설명

}

public class SkillManager : MonoBehaviour
{
    public GameManager gameManager;

    [Header("Skill Selection State")]
    public bool isLocalPlayerReady = false;
    public bool isRemotePlayerReady = false;

    [Header("Skill Points")]
    public int mySP = 0;
    public int oppSP = 0;
    public const int MAX_SP = 10; // SP 최대치 10

    [Header("Skill Decks (Instances)")]
    // ID를 바탕으로 생성될 '실제 작동하는 스킬 객체' 리스트 (내부 로직용)
    private List<SkillBase> mySkills = new List<SkillBase>();
    private List<SkillBase> oppSkills = new List<SkillBase>();

    [Header("Skill Decks (Network IDs)")]
    // ** 네트워크 담당자가 방 입장 시 세팅해 줄 '스킬 번호' 배열 (데이터용)
    public int[] mySkillsID = new int[3];
    public int[] oppSkillsID = new int[3];

    // -------------------------------------------------------
    // 현재 적용된 스킬(내가 적용한 & 남이 적용한) 리스트 -> 턴이 끝날 때마다 이 리스트를 순회하며 remainingTurns를 1씩 깎고, 0이 되면 리스트에서 제거한 뒤 UI에 "아이콘 지워!"라고 알려줍니다.
    public List<ActiveEffect> activeEffects = new List<ActiveEffect>();

    // 상대방이 안티매직을 썼는지 여부
    public bool isMySkillSealed = false;

    // UI가 구독할 이벤트들 (데이터만 던져줌)
    public event Action<int> OnSPChanged; // SP가 변했을 때 (내 SP 던져줌)
    public event Action<List<ActiveEffect>> OnActiveEffectsChanged; // 버프/디버프 리스트가 갱신됐을 때
    public event Action<int, bool> OnSkillButtonStateChanged; // 특정 스킬 버튼의 활성화/비활성화 상태가 변했을 때


    /// <summary>
    /// 네트워크 담당자님이 스킬 선택 정보를 동기화했을 때 호출할 함수입니다.
    /// </summary>
    public void InitializeSkillDeck(bool isLocalPlayer, int[] selectedIDs)
    {
        if (isLocalPlayer)
        {
            mySkillsID = selectedIDs;
            Debug.Log($"[Skill] 내 스킬 덱 설정 완료: {string.Join(", ", mySkillsID)}");
        }
        else
        {
            oppSkillsID = selectedIDs;
            Debug.Log($"[Skill] 상대 스킬 덱 설정 완료: {string.Join(", ", oppSkillsID)}");
        }

        // TODO: (시온 작업) 여기서 CSV 데이터를 찾아 실제 Skill 인스턴스를 생성할 예정입니다.
    }


    // 착수 시 SP 증가 (GameManager에서 턴 넘어갈 때 호출해줄 함수)
    public void AddSPOnTurnEnd(StoneColor placedColor)
    {
        if (placedColor == gameManager.localPlayerColor)
        {
            mySP = Mathf.Min(mySP + 1, MAX_SP);
        }
        else
        {
            oppSP = Mathf.Min(oppSP + 1, MAX_SP);
        }

        // 쿨타임 & 지속 턴 감소 로직
        UpdateSkillDurations(placedColor);

        // SP가 바뀌었으니 HUD에 즉시 양쪽 SP 최신화
        if (gameManager.gameHUD != null)
        {
            gameManager.gameHUD.UpdateSPUI(mySP, oppSP);
        }
    }

    private void UpdateSkillDurations(StoneColor turnColor)
    {
        // 턴이 지날 때마다 스킬들의 OnTurnPassed()를 호출하여 쿨다운 감소
        if (turnColor == gameManager.localPlayerColor)
        {
            foreach (var skill in mySkills) skill.OnTurnPassed();
        }
        else
        {
            foreach (var skill in oppSkills) skill.OnTurnPassed();
        }
    }

    // (네트워크 수신용) 상대방이 스킬을 썼을 때
    public void ReceiveOpponentSkill(int skillId, int[] xs, int[] ys)
    {
        // TODO: skillId에 맞는 스킬 찾아서 Execute 실행 및 oppSP 차감 로직
        Debug.Log($"상대가 스킬 {skillId} 사용!");
    }

    // 스킬 선택창 관련 --------------------------------------------------
    // 1. 상화님이 UI에서 스킬을 고를 때마다 호출할 함수
    public void OnSkillSelected(int slotIndex, int skillId)
    {
        // slotIndex: 0~2번 자리, skillId: 선택한 스킬 번호
        mySkillsID[slotIndex] = skillId;
        Debug.Log($"{slotIndex}번 슬롯에 {skillId}번 스킬 장착");
    }

    // 2. 상화님이 '준비 완료' 버튼 누르거나 패킷 받았을 때 호출
    public void SetPlayerReady(bool isLocal)
    {
        if (isLocal) isLocalPlayerReady = true;
        else isRemotePlayerReady = true;

        // 둘 다 준비되면 게임 시작 상태로 변경 (GameManager 제어)
        if (isLocalPlayerReady && isRemotePlayerReady)
        {
            gameManager.StartGameAfterSelection();
        }
    }
}
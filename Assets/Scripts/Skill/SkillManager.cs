using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.UI;
using UnityEngine.Rendering;

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

    [Header("Core & Network")]
    public GameManager gameManager;
    public GameSession gameSession;
    public TimerManager timerManager;

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
    public int[] mySkillsID = new int[] { -1, -1, -1 };
    public int[] oppSkillsID = new int[] { -1, -1, -1 };

    [Header("Targeting State")]
    public int selectedSkillSlot = -1; // 현재 타겟팅 중인 스킬 슬롯

    // -------------------------------------------------------
    // 현재 적용된 스킬(내가 적용한 & 남이 적용한) 리스트 -> 턴이 끝날 때마다 이 리스트를 순회하며 remainingTurns를 1씩 깎고, 0이 되면 리스트에서 제거한 뒤 UI에 "아이콘 지워!"라고 알려줍니다.
    public List<ActiveEffect> activeEffects = new List<ActiveEffect>();

    // 상대방이 안티매직을 썼는지 여부
    public bool isMySkillSealed = false;

    // UI가 구독할 이벤트들 (데이터만 던져줌)
    public event Action<int> OnSPChanged; // SP가 변했을 때 (내 SP 던져줌)
    public event Action<List<ActiveEffect>> OnActiveEffectsChanged; // 버프/디버프 리스트가 갱신됐을 때
    public event Action<int, bool> OnSkillButtonStateChanged; // 특정 스킬 버튼의 활성화/비활성화 상태가 변했을 때


    // 2. 팩토리 메서드 (OCP 원칙: 새로운 스킬이 생기면 여기 case만 추가하면 됨)
    private SkillBase CreateSkillByID(int id)
    {
        // 임시 데이터 뼈대 (나중엔 CSV나 ScriptableObject에서 불러옴)
        SkillData data = new SkillData { skillId = id };

        switch (id)
        {
            case 1:
                data.spCost = 2;
                data.cooldown = 3;
                data.skillName = "돌 이동 (Stone Shift)";
                data.type = "특수형";
                data.targetType = "my";
                return new Skill_1_StoneShift(data); // 
            case 5:
                data.spCost = 4;
                data.cooldown = 2;
                data.skillName = "제거 (Erase)";
                data.type = "공격형";
                data.targetType = "enemy";
                return new SkillErase(data); // 👈 3단계에서 만들 클래스

            // 나중에 다른 스킬들도 여기에 case 추가
            default:
                Debug.Log($"[SkillManager] ID {id} 스킬은 아직 미구현! 빈 껍데기 반환.");
                data.skillName = "미구현 스킬";
                return null; // 임시
        }
    }

    // ---------------------------------------------
    // 1. 초기화 시 타임아웃 이벤트 구독 (GameManager가 상태를 바꿀 때 타이머를 켜준다고 가정)
    private void Start()
    {
        if (timerManager != null)
        {
            timerManager.OnTimeOut += OnSkillSelectionTimeOut;
        }
    }


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
        // 1. 어떤 스킬인지 찾음 (ID 기반)
        SkillBase skillObj = CreateSkillByID(skillId);
        if (skillObj == null) return;

        // 2. 상대방 SP 차감 및 쿨타임(시각적) 표시 로직 
        oppSP -= skillObj.data.spCost;
        if (gameManager.gameHUD != null) gameManager.gameHUD.UpdateSPUI(mySP, oppSP);

        // 3. 실제 효과 실행 (Execute 내부에서 보드 데이터 지우기 수행)
        // xs, ys 배열을 돌면서 들어있는 모든 좌표를 지움 (최대 2개)
        for (int i = 0; i < xs.Length; i++)
        {
            int tx = xs[i];
            int ty = ys[i];

            // 유효한 좌표(-1이 아님)이고 바둑판에 돌이 있다면 삭제
            if (tx != -1 && ty != -1)
            {
                gameManager.board.grid[tx, ty] = 0;
                gameManager.board.RemoveStoneObjectAt(tx, ty);
            }
        }

        Debug.Log($"[Network] 상대방이 {skillObj.data.skillName}을 사용했습니다.");
    }

    // 스킬 선택창 관련 --------------------------------------------------
    // 1. UI에서 스킬을 고를 때마다 호출할 함수
    public void OnSkillSelected(int slotIndex, int skillId)
    {
        // slotIndex: 0~2번 자리, skillId: 선택한 스킬 번호
        mySkillsID[slotIndex] = skillId;
        Debug.Log($"{slotIndex}번 슬롯에 {skillId}번 스킬 장착");
    }

    // 2. '준비 완료' 버튼 누르거나 패킷 받았을 때 호출
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

    // 네트워크 동기화가 끝나고 게임 시작 직전(StartGameAfterSelection)에 호출하면 됩니다.
    public void GenerateSkillInstances()
    {
        mySkills.Clear();

        for (int i = 0; i < 3; i++)
        {
            int skillId = mySkillsID[i];
            SkillBase newSkill = CreateSkillByID(skillId);

            if (newSkill != null)
            {
                mySkills.Add(newSkill);

                // UI에 코스트(SP) 텍스트 반영
                if (gameManager.gameHUD != null && gameManager.gameHUD.skillCostTexts.Length > i)
                {
                    gameManager.gameHUD.skillCostTexts[i].text = newSkill.data.spCost.ToString();
                }
            }
        }
        Debug.Log($"[SkillManager] 내 스킬 인스턴스 3개 생성 완료!");

        // 생성 끝났으니 하단 버튼 이벤트 연결
        BindSkillButtons();
    }

    // 2. 스킬 선택 시간 종료 시 로직
    private void OnSkillSelectionTimeOut()
    {
        // 스킬 선택 중인 상태가 아니면 무시
        if (gameManager.currentState != GameState.WaitingForSkillSelect) return;

        Debug.Log("[SkillManager] 스킬 선택 시간 초과! 남은 슬롯을 랜덤으로 채웁니다.");

        AutoSelectRandomSkills(); // 랜덤 선택 함수 호출
    }

    // TimerManager에서 '스킬 선택 시간 초과' 시 이 함수를 호출하도록 연결
    public void AutoSelectRandomSkills()
    {
        // 1. 선택 가능한 전체 스킬 풀(Pool) 리스트
        List<int> availableSkills = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        // 2. 이미 선택된 스킬은 목록에서 제거
        foreach (int selectedId in mySkillsID)
        {
            if (selectedId > 0) availableSkills.Remove(selectedId);
        }

        // 3. 비어있는 슬롯(-1)을 찾아서 남은 스킬 중 랜덤으로 넣기
        for (int i = 0; i < 3; i++)
        {
            if (mySkillsID[i] == -1) // 비어있다면?
            {
                int randomIndex = UnityEngine.Random.Range(0, availableSkills.Count);
                int pickedId = availableSkills[randomIndex];

                mySkillsID[i] = pickedId; // 슬롯에 장착
                availableSkills.RemoveAt(randomIndex); // 중복 방지를 위해 리스트에서 제거

                // UI에 반영 
                OnSkillSelected(i, pickedId);
            }
        }

        Debug.Log($"[SkillManager] 타임아웃! 스킬 자동 선택 완료: {mySkillsID[0]}, {mySkillsID[1]}, {mySkillsID[2]}");

        // 예외 처리 및 분기
        if (gameSession != null)
        {
            // 1. 멀티플레이 모드: GameSession이 존재하면 패킷 쏘고 상대방 기다림
            gameSession.SendSyncPlayerInfo();
        }
        else
        {
            // 2. 솔로/AI 모드: GameSession이 없으면 상대방 기다릴 필요 없이 즉시 게임 시작
            if (gameManager != null)
            {
                gameManager.StartGameAfterSelection();
            }
        }
    }

    //3. 인게임 하단 버튼 연동(SRP: UI 클릭 이벤트를 SkillManager가 전담)
    public void BindSkillButtons()
    {
        if (gameManager.gameHUD == null) return;

        for (int i = 0; i < 3; i++)
        {
            int slotIndex = i; // 클로저(Closure) 문제 방지용 지역 변수
            Button btn = gameManager.gameHUD.activeSkillButtons[slotIndex];

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnActiveSkillButtonClicked(slotIndex));
        }
    }

    // 인게임 스킬 버튼을 눌렀을 때
    private void OnActiveSkillButtonClicked(int slotIndex)
    {
        // 방어 로직: 내 턴이 아니거나, 게임 중이 아니면 무시
        if (gameManager.currentState != GameState.Playing) return;
        if (gameManager.currentTurnColor != gameManager.localPlayerColor)
        {
            Debug.LogWarning("내 차례에만 스킬을 사용할 수 있습니다!");
            return;
        }

        if (slotIndex >= mySkills.Count) return;

        SkillBase selectedSkill = mySkills[slotIndex];

        // 스킬 사용 가능 여부 체크 (SP, 쿨타임 등)
        if (!selectedSkill.CanUse(mySP, isMySkillSealed, gameManager.board, gameManager.localPlayerColor))
        {
            Debug.LogWarning($"[{selectedSkill.data.skillName}] 사용 불가! (SP 부족 또는 쿨타임)");
            return;
        }

        // 상태를 '스킬 타겟팅'으로 변경 (이제 InputManager가 클릭하면 TryPlaceStone 대신 SkillManager한테 좌표를 넘기게 될 겁니다)
        selectedSkillSlot = slotIndex;
        gameManager.currentState = GameState.SkillTargeting;

        // 비주얼 효과 시작: 보드의 모든 자신/상대 돌에 초록색 테두리 표시
        // targetType 기준으로 하이라이트 분기
        if (selectedSkill.data.targetType == "my")
            gameManager.board.ShowSkillTargetMarkers_My(gameManager.localPlayerColor);
        else if (selectedSkill.data.targetType == "enemy")
            gameManager.board.ShowSkillTargetMarkers(gameManager.localPlayerColor);
        //gameManager.board.ShowSkillTargetMarkers(gameManager.localPlayerColor);

        Debug.Log($"==== [{selectedSkill.data.skillName}] 타겟팅 모드 진입! ====");
    }

    // InputManager에서 바둑판을 클릭했을 때 이 함수를 호출합니다.
    public void ExecuteSkillAt(int x, int y)
    {
        if (selectedSkillSlot < 0 || selectedSkillSlot >= mySkills.Count) return;

        SkillBase skillToUse = mySkills[selectedSkillSlot];

        // 1. 배열로 좌표 전달 (다중 타겟 스킬도 있으니 배열로 감싸서 보냄) (0:선택좌표, 1:랜덤좌표)
        int[] targetX = new int[] { x, -1 };
        int[] targetY = new int[] { y, -1 };

        // 2. 스킬 발동! (구체적인 효과는 각 스킬 클래스 내부에서 처리)
        // Execute 내부에서 targetX[1], targetY[1]의 값을 랜덤 좌표로 채워줍니다.
        if (skillToUse.Execute(targetX, targetY, gameManager, gameManager.board))
        {
            mySP -= skillToUse.data.spCost;
            skillToUse.currentCooldown = skillToUse.data.cooldown;

            if (gameManager.gameHUD != null) gameManager.gameHUD.UpdateSPUI(mySP, oppSP);

            // 네트워크 담당자에게 전달할 패킷 (좌표 2개가 담긴 배열 전송)
            if (gameManager.currentMode == PlayMode.Multiplayer && gameSession != null)
            {
                gameSession.SendUseSkill(skillToUse.data.skillId, targetX, targetY);
            }

            selectedSkillSlot = -1;
            gameManager.currentState = GameState.Playing;
        }
        else
        {
            selectedSkillSlot = -1;
            gameManager.currentState = GameState.Playing;
        }

        // 스킬이 성공하든 실패하든 마커는 지워야 함
        gameManager.board.HideSkillTargetMarkers();
    }
}
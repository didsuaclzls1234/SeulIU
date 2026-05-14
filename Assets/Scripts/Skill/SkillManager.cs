using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.UI;
using UnityEngine.Rendering;
using TMPro;

[System.Serializable]
public class ActiveEffect
{
    public int skillId;           // 스킬 정보 (CSV 데이터 조회용)
    public int remainingTurns;    // 남은 턴 수
    public bool isBuff;           // true면 내 버프(파란 테두리), false면 디버프(빨간 테두리)
    public string effectName;     // 툴팁용 이름
    public string description;    // 툴팁용 설명
    public StoneColor casterColor; // 효과를 건 플레이어의 색깔 (피드백용)
}

public class SkillManager : MonoBehaviour
{
    // 모든 스킬의 기본 정보를 담아둘 딕셔너리
    public Dictionary<int, SkillData> skillDatabase = new Dictionary<int, SkillData>();

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
    // 방 입장 시 세팅해 줄 '스킬 번호' 배열 (데이터용) - 네트워크 관련
    public int[] mySkillsID = new int[] { -1, -1, -1 };
    public int[] oppSkillsID = new int[] { -1, -1, -1 };

    [Header("Targeting State")]
    public int selectedSkillSlot = -1; // 현재 타겟팅 중인 스킬 슬롯

    [Header("Invisibility State")]
    public int myInvisibilityTurns = 0;
    public int oppInvisibilityTurns = 0;

    // -------------------------------------------------------
    // 현재 적용된 스킬(내가 적용한 & 남이 적용한) 리스트 -> 턴이 끝날 때마다 이 리스트를 순회하며 remainingTurns를 1씩 깎고, 0이 되면 리스트에서 제거한 뒤 UI에 "아이콘 지워!"라고 알려줍니다.
    public List<ActiveEffect> activeEffects = new List<ActiveEffect>();

    // 상대방이 안티매직을 썼는지 여부(남은 턴수)
    public int sealedTurnsRemaining = 0;

    // 턴 종료 시 파괴할 제거 스킬 타겟 좌표 (기본값 -1, -1)
    public Vector2Int pendingRemoveTarget = new Vector2Int(-1, -1); 

    // 2. 팩토리 메서드 (OCP 원칙: 새로운 스킬이 생기면 여기 case만 추가하면 됨)
    private SkillBase CreateSkillByID(int id)
    {
        // CSV에서 미리 로드한 데이터를 가져옴
        if (!skillDatabase.TryGetValue(id, out SkillData data)) return null;

        switch (id)
        {
            case 1:
                return new Skill_1_StoneShift(data); 
            case 2:
                return new Skill_2_Seal(data);
            case 3:
                return new Skill_3_DoubleDown(data); 
            case 4:
                return new Skill_4_AntiMagic(data);
            case 5:
                return new Skill_5_Erase(data);
            case 6:
                return new Skill_6_Bladefall(data);
            case 7:
                return new Skill_7_Invisibility(data);
            case 8:
                return new Skill_8_SevenSins(data);   
            case 9:
                return new Skill_9_GodBless(data);
            case 10:
                return new Skill_10_Destruction(data);
            case 11:
                return new Skill_11_Consecration(data);
            // 나중에 다른 스킬들도 여기에 case 추가
            default:
                Debug.Log($"[SkillManager] ID {id} 스킬은 아직 미구현! 빈 껍데기 반환.");
                data.skillName = "미구현 스킬";
                return null; // 임시
        }
    }

    // ---------------------------------------------

    private void Awake() // Start보다 먼저 실행되게
    {
        LoadSkillDataFromCSV();
    }

    // 1. 초기화 시 타임아웃 이벤트 구독 (GameManager가 상태를 바꿀 때 타이머를 켜준다고 가정)
    private void Start()
    {
        if (timerManager != null)
        {
            timerManager.OnTimeOut += OnSkillSelectionTimeOut;
        }
    }

    private void LoadSkillDataFromCSV()
    {
        // Resources 폴더 안의 SkillData.csv 읽기
        TextAsset csvFile = Resources.Load<TextAsset>("SkillData");
        if (csvFile == null)
        {
            Debug.LogError("Resources 폴더에 SkillData.csv 파일이 없습니다!");
            return;
        }

        string[] lines = csvFile.text.Split('\n');
        for (int i = 1; i < lines.Length; i++) // 첫 줄 헤더 스킵
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            // 쉼표로 데이터 쪼개기 (내용에 쉼표가 들어가면 배열이 꼬이니 주의!)
            string[] columns = lines[i].Split(',');

            // 엑셀 빈 칸이나 줄바꿈 처리 방어
            if (columns.Length < 8) continue;

            SkillData data = new SkillData();
            data.skillId = int.Parse(columns[0]);
            data.skillName = columns[1];
            data.type = columns[2];
            data.spCost = int.Parse(columns[3]);
            data.cooldown = int.Parse(columns[4]);
            data.durationTurn = int.Parse(columns[5]); // 지속 턴
            data.targetType = columns[6]; // 타겟 타입
            data.description = columns[7].Trim(); // 설명 (끝에 붙은 쓸데없는 공백/줄바꿈 제거)

            skillDatabase.Add(data.skillId, data);
        }
        Debug.Log($"[SkillManager] 스킬 데이터 {skillDatabase.Count}개 로드 완료!");
    }
    // =========================================================
    // [추가] OnStonePlaced — 착수 시 SP/쿨타임/지속 턴/봉인 일괄 처리
    //        PlayerManual일 때만 감소, SkillInduced는 무시
    // =========================================================
    public void OnStonePlaced(StoneColor placedColor, PlacementType type)
    {
        if (type == PlacementType.SkillInduced) return;

        // 일반 착수를 완료했을 때, 삭제 예약된 돌이 있다면 쾅 터뜨림!
        if (pendingRemoveTarget.x != -1 && pendingRemoveTarget.y != -1)
        {
            int rx = pendingRemoveTarget.x;
            int ry = pendingRemoveTarget.y;

            GameObject deadObj = gameManager.board.GetStoneObjectAt(rx, ry);
            StoneColor deadStoneColor = (StoneColor)gameManager.board.grid[rx, ry];

            gameManager.board.grid[rx, ry] = 0; // 데이터 먼저 삭제 (승리판정 영향 없게)
            gameManager.board.RemoveStoneObjectAt(rx, ry); // 보드 관리 리스트에서 제외 (이때 SetActive(false)됨)
            
            if (deadObj != null) 
            {
                deadObj.SetActive(true); // 🚨 연출을 위해 강제로 다시 켬!
                SkillVFXManager.Instance.PlayGlitchDeath(deadObj, deadStoneColor); // 이펙트 매니저에게 파괴를 위임
            }

            // 기존에 있던 BlinkEmptySpaceEffect 호출 줄은 싹 삭제합니다!
            pendingRemoveTarget = new Vector2Int(-1, -1);
            Debug.Log($"[SkillManager] 예약되었던 돌({rx},{ry}) 파괴 완료!");
        }

        // 1. 쿨타임 감소 — 착수한 플레이어 쪽 스킬들
        List<SkillBase> targetSkills = (placedColor == gameManager.localPlayerColor) ? mySkills : oppSkills;
        foreach (var skill in targetSkills) skill.OnTurnPassed();
 
        // 2. AntiMagic 턴 감소 — 내가 착수할 때 내 봉인 감소
        if (placedColor == gameManager.localPlayerColor && sealedTurnsRemaining > 0)
        {
            sealedTurnsRemaining--;
            Debug.Log($"[SkillManager] AntiMagic 남은 턴: {sealedTurnsRemaining}");
            if (sealedTurnsRemaining == 0 && gameManager.gameHUD != null)
            {
                gameManager.gameHUD.SetOpponentSilencedUI(false);
                gameManager.gameHUD.ShowSystemMessage("안티매직 효과가 풀렸습니다!");

                // 🚨 피격자 화면 회색 오버레이 끄기!
                RefreshAntiMagicOverlayUI();
            }
        }

        // 3. 투명화 턴 감소
        if (placedColor == gameManager.localPlayerColor && myInvisibilityTurns > 0)
        {
            myInvisibilityTurns--;
            if (myInvisibilityTurns == 0)
            {
                // 🚨 투명화 끝날 때 하얗게 깜빡!
                SkillVFXManager.Instance.PlayScreenFlash(Color.white, 0.2f);
                SkillVFXManager.Instance.ToggleOverlay(SkillVFXManager.Instance.invisibilityOverlayImage, false);
                gameManager.board.SetStoneInvisibility(gameManager.localPlayerColor, true, true);
            }
        }
        else if (placedColor != gameManager.localPlayerColor && oppInvisibilityTurns > 0)
        {
            oppInvisibilityTurns--;
            if (oppInvisibilityTurns == 0)
            {
                StoneColor oppColor = gameManager.localPlayerColor.Opponent();
                gameManager.board.SetStoneInvisibility(oppColor, true, false);
                gameManager.gameHUD?.ShowSystemMessage("상대방의 투명화 효과가 해제되었습니다.");
            }
        }
 
        // 4. 봉인(sealedGrid) 턴 감소
        for (int i = 0; i < gameManager.board.boardSize; i++)
        {
            for (int j = 0; j < gameManager.board.boardSize; j++)
            {
                if (gameManager.board.sealedGrid[i, j].turns > 0
                    && gameManager.board.sealedGrid[i, j].owner != placedColor)
                {
                    gameManager.board.sealedGrid[i, j].turns--;
                    if (gameManager.board.sealedGrid[i, j].turns == 0)
                    {
                        gameManager.board.RemoveSealEffect(i, j);
                        Debug.Log($"({i},{j}) 봉인 해제");
                    }
                }
            }
        }
 
        // 5. SP 증가
        if (placedColor == gameManager.localPlayerColor)
            mySP = Mathf.Min(mySP + 1, MAX_SP);
        else
            oppSP = Mathf.Min(oppSP + 1, MAX_SP);

        // 6. activeEffects 감소
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            // 안티매직(4번)은 '상대방'이 돌을 둘 때 지속시간을 깎음! (그래야 나-상-나-상 꽉 채워서 유지됨)
            if (activeEffects[i].skillId == 4)
            {
                if (placedColor != activeEffects[i].casterColor)
                {
                    activeEffects[i].remainingTurns--;
                    if (activeEffects[i].remainingTurns <= 0) 
                    { 
                        activeEffects.RemoveAt(i);

                        // 시전자 화면 하늘색 오버레이 끄기!
                        RefreshAntiMagicOverlayUI();
                    }
                }
            }
            else
            {
                // 다른 일반 버프(투명화 등)는 '시전자(나)'가 돌을 둘 때 깎음
                if (placedColor == activeEffects[i].casterColor)
                {
                    activeEffects[i].remainingTurns--;
                    if (activeEffects[i].remainingTurns <= 0) activeEffects.RemoveAt(i);
                }
            }
        }

        // 7. UI 갱신
        if (gameManager.gameHUD != null)
        {
            gameManager.gameHUD.UpdateSPUI(mySP, oppSP);
            gameManager.gameHUD.RefreshBuffIcons(activeEffects, gameManager.localPlayerColor);
        }
 
        RefreshSkillButtonStates();

        // (새로 놓은 돌에 하늘색 입히고(안티매직), 버프 끝나면 지우기 위해 무조건 갱신)
        gameManager.board.RefreshAllStonesVisuals();
    }

     // =========================================================
    // 스킬 버튼 상태 갱신 (public — GameHUD에서도 호출)
    // =========================================================
    public void RefreshSkillButtonStates()
    {
        if (gameManager.gameHUD == null) return;
 
        for (int i = 0; i < mySkills.Count; i++)
        {
            Button btn    = gameManager.gameHUD.activeSkillButtons[i];
            TMP_Text cdTxt = gameManager.gameHUD.cooldownTexts[i];
 
            bool isPassive    = mySkills[i].data.type == "전용";
            bool isSilenced   = sealedTurnsRemaining > 0;
            bool onCooldown   = mySkills[i].currentCooldown > 0;
            bool notEnoughSP  = mySP < mySkills[i].data.spCost;

            // 칠죄종(8번) 1회 영구 적용 방어 로직: 이미 승리 조건이 7이면 버튼 영구 잠금
            bool isUsedSevenSins = (mySkills[i].data.skillId == 8 && gameManager.board.ruleManager.GetWinCondition((int)gameManager.localPlayerColor.Opponent()) == 7);

            btn.interactable = !isPassive && !onCooldown && !notEnoughSP && !isSilenced && !isUsedSevenSins;
            cdTxt.text = (!isPassive && onCooldown) ? mySkills[i].currentCooldown.ToString() : "";
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
             // ↓ 추가
            gameManager.gameHUD?.RefreshOppDeckSlots(oppSkillsID, skillDatabase);
            Debug.Log($"[Skill] 상대 스킬 덱 설정 완료: {string.Join(", ", oppSkillsID)}");
        }

        // TODO: (시온 작업) 여기서 CSV 데이터를 찾아 실제 Skill 인스턴스를 생성할 예정입니다.
    }

    // AI 모드 전용: AI가 스킬을 사용할지 판단하고 실행하는 함수
    // =========================================================
    // AI 전용 스킬 로직
    // =========================================================
    public void AI_TryUseSkill()
    {
        // 내(AI) 턴이 아니거나, 이미 이번 턴에 스킬을 썼거나, 안티매직에 걸렸으면 종료
        if (gameManager.currentMode != PlayMode.AI || gameManager.hasUsedSkillThisTurn || sealedTurnsRemaining > 0) return;

        foreach (var skill in oppSkills)
        {
            // 패시브(9번, 10번)는 제외
            if (skill.data.type == "전용") continue;

            // 1. SP와 쿨타임 검사
            if (oppSP >= skill.data.spCost && skill.currentCooldown <= 0)
            {
                // 2. 스킬별 타겟팅 좌표 가져오기 (AI의 판단)
                int[] tx = { -1, -1 };
                int[] ty = { -1, -1 };

                bool shouldUse = AI_DetermineSkillTarget(skill.data.skillId, tx, ty);

                // 3. 사용하기로 결정했다면 Execute 실행
                if (shouldUse)
                {
                    // AI도 우리가 만들어둔 Skill_3_DoubleDown.cs 등의 Execute를 똑같이 탑니다
                    if (skill.Execute(tx, ty, gameManager, gameManager.board))
                    {
                        OnAI_SkillSuccess(skill, tx, ty);
                        return; // 한 턴에 하나만 쓰므로 즉시 종료
                    }
                }
            }
        }
    }

    // AI가 무슨 스킬을, 어디에 쓸지 결정하는 "두뇌" 함수 (확장성 100%)
    private bool AI_DetermineSkillTarget(int skillId, int[] tx, int[] ty)
    {
        switch (skillId)
        {
            case 3: // 이중 착수 (Double Down)
                // 발동 확률 50%
                if (UnityEngine.Random.value > 0.5f) return false;

                // 이중착수는 타겟팅 없는 전역 스킬(none)이므로 좌표가 필요 없음 (-1, -1 그대로)
                Debug.Log("[AI] 이중착수(3번) 스킬을 시전합니다!");
                return true;

            // 나중에 여기에 case 5(제거), case 2(봉인) 등을 추가하시면 됩니다!
            // case 5: 
            //     tx[0] = 지울_x좌표; ty[0] = 지울_y좌표;
            //     return true;

            default:
                // 구현되지 않은 스킬은 사용 안 함
                return false;
        }
    }

    // AI가 스킬을 성공적으로 썼을 때의 후처리 (SP 차감, UI 갱신 등)
    private void OnAI_SkillSuccess(SkillBase skill, int[] tx, int[] ty)
    {
        oppSP -= skill.data.spCost;
        skill.currentCooldown = skill.data.cooldown;
        gameManager.hasUsedSkillThisTurn = true;
        //gameManager.gameHUD?.AddSkillLog("AI", skill.data.skillName, gameManager.CurrentMoveCount);

        // 버프 효과 등록
        if (skill.data.durationTurn > 0)
        {
            activeEffects.Add(new ActiveEffect
            {
                skillId = skill.data.skillId,
                casterColor = gameManager.localPlayerColor.Opponent(),
                remainingTurns = skill.data.durationTurn,
                isBuff = false, // 플레이어 입장에선 AI의 버프가 디버프(적대적)로 보이게 false 처리
                effectName = skill.data.skillName,
                description = skill.data.description
            });
            gameManager.gameHUD?.RefreshBuffIcons(activeEffects, gameManager.localPlayerColor);
        }
        
        if (gameManager.gameHUD != null) gameManager.gameHUD.UpdateSPUI(mySP, oppSP);

        // B타입 스킬(이중착수, 칼날비)의 지연 발동을 위해 pendingSkillId 세팅 (멀티플레이 코드와 동일)
        bool isBType = (skill.data.skillId == 3 || skill.data.skillId == 6);
        if (isBType)
        {
            // Execute 내부에서 이미 pendingSkillId를 3으로 설정했으므로 여기서 냅둬도 됨
            Debug.Log($"[AI] B타입 스킬 {skill.data.skillName} 예약 완료. 일반 착수 후 발동 대기.");
        }
        gameManager.gameHUD?.RecordSkillLog(gameManager.CurrentMoveCount + 1, "AI", skill.data.skillName);
    }

    public void AI_AutoSelectSkills()
    {
        if (gameManager.currentMode != PlayMode.AI) return;

        // 기획자님 의도대로 AI는 3번(이중착수)과 본인 전용 패시브만 확정으로 들고 갑니다.
        // 플레이어가 흑(1)이면 AI는 백이므로 11번(신성화), 플레이어가 백(2)이면 AI는 흑이므로 10번(파괴)
        //int aiPassiveId = (gameManager.localPlayerColor == StoneColor.Black) ? 11 : 10;

        oppSkillsID[0] = 3;           // 이중착수 확정
        oppSkillsID[1] = -1; //aiPassiveId; // 패시브 확정
        oppSkillsID[2] = -1;          // 나머지 한 칸은 비워둠 (혹은 다른 스킬 넣어도 됨)

        // AI는 스킬 고르자마자 바로 준비 완료 처리
        isRemotePlayerReady = true;
        Debug.Log($"[AI] 스킬 선택 및 준비 완료: {oppSkillsID[0]}, {oppSkillsID[1]}, 빈칸");
        gameManager.gameHUD?.RefreshOppDeckSlots(oppSkillsID, skillDatabase);
        // 만약 플레이어가 이미 준비 완료 상태라면 곧바로 게임 시작
        if (isLocalPlayerReady)
        {
            gameManager.gameHUD?.HideSkillSelectPanel();
            gameManager.StartGameAfterSelection();
        }

    }

    // -----------------------------------------------------------------

    // (네트워크 수신용) 상대방이 스킬을 썼을 때
    public void ReceiveOpponentSkill(int skillId, int[] xs, int[] ys,int turnCount)
    {   
        SkillData data = skillDatabase[skillId];
        string opponentName = "상대";
        // 1. 공통: 일단 데이터 예약
        gameManager.gameHUD?.RecordSkillLog(0, opponentName,data.skillName);
        if (skillId == 3 || skillId == 6)
        {
            // 돌 놓기 패킷이 오기 전에 로그창에 "상대: [스킬명]"을 먼저 띄움
            gameManager.gameHUD.ForceCommitPendingLog(turnCount);
        }
        
         // ─── B타입 첫 번째 패킷 (xs[0] == -1) : 스킬 선언 → Pending 저장 ───
        if ((skillId == 3 || skillId == 6) && xs[0] == -1)
        {
            // 🚨 상대가 이중착수 썼을 때 내 화면도 번쩍!
            if (skillId == 3) SkillVFXManager.Instance.PlayScreenFlash(Color.white, 0.2f);

            if (skillDatabase.TryGetValue(skillId, out SkillData declData))
            {
                oppSP -= declData.spCost;
                gameManager.gameHUD?.UpdateSPUI(mySP, oppSP);
                gameManager.gameHUD?.RecordSkillLog(turnCount, "상대방", declData.skillName);
            }
            return;
        }

        // ─── B타입 두 번째 패킷 (xs[0] != -1) : 보드 효과만 적용, 로그는 이미 처리됨 ───
        if (skillId == 3 && xs[0] != -1)
        {
            ReceiveSkill_DoubleDown(xs, ys);
            return;
        }
        if (skillId == 6 && xs[0] != -1)
        {
            ReceiveSkill_Bladefall(xs, ys);
            return;
        }

         // 1. 어떤 스킬인지 찾음 (ID 기반)
        //SkillBase skillObj = CreateSkillByID(skillId);완전한 객체 생성 불필요. 정보만 가져오면 됨.
        //if (skillObj == null) return;
        if (!skillDatabase.TryGetValue(skillId, out SkillData skillData)) return;
        // 2. 상대방 SP 차감 및 쿨타임(시각적) 표시 로직 
        oppSP -= skillData.spCost;
        if (gameManager.gameHUD != null) gameManager.gameHUD.UpdateSPUI(mySP, oppSP);


         //  스킬 ID별 분기
        switch (skillId)
        {
            case 1: ReceiveSkill_StoneShift(xs, ys); break;
            case 2: ReceiveSkill_Seal(xs, ys);       break;
            case 3: ReceiveSkill_DoubleDown(xs, ys); break;
            case 4: ReceiveSkill_AntiMagic(xs, ys); break;
            case 5: ReceiveSkill_Erase(xs, ys);      break;
            case 6: ReceiveSkill_Bladefall(xs, ys); break;
            case 7: ReceiveSkill_Invisibility(xs, ys); break;
            case 8: ReceiveSkill_SevenSins(); break; //ReceiveSkill_GodBless(xs, ys); break;
            case 9: ReceiveSkill_GodBless(xs, ys); break; 
            case 10: ReceiveSkill_Destruction(); break;
            case 11: ReceiveSkill_Consecration(); break;
            default:
                Debug.LogWarning($"[Network] 스킬 ID {skillId} 수신 처리 미구현");
                break;
        }
        
        if (skillData.durationTurn > 0)
        {
            activeEffects.Add(new ActiveEffect
            {
                skillId = skillId,
                remainingTurns = skillData.durationTurn,
                isBuff = false,
                effectName = skillData.skillName,
                description = skillData.description,
                casterColor = gameManager.localPlayerColor.Opponent()
            });
            gameManager.gameHUD.RefreshBuffIcons(activeEffects, gameManager.localPlayerColor);
        }

        // * 상대방이 안티매직을 썼을 때도 갱신이 필요할 수 있으므로 (혹은 턴 만료 시)
        gameManager.board.RefreshAllStonesVisuals();

        Debug.Log($"[Network] 상대방이 {skillData.skillName}을 사용했습니다.");
        gameManager.gameHUD?.RecordSkillLog(turnCount, "상대방", skillData.skillName);
        //gameManager.gameHUD?.UpdateGameLog();
        //gameManager.gameHUD?.AddSkillLog("상대방", skillData.skillName, turnCount);

        gameManager.board.RefreshAllStonesVisuals(); // 버프가 켜졌으니 바둑판 렌더링 즉시 새로고침 (안티매직 하늘색 표시용)

        // 🚨 [여기에 추가!] 상대방 버프/디버프 등록 끝났으니 오버레이 상태 최종 갱신!
        RefreshAntiMagicOverlayUI();
    }
    //  1번스킬 추가
    private void ReceiveSkill_StoneShift(int[] xs, int[] ys)
    {
        int tx = xs[0];
        int ty = ys[0];
        int destX = xs[1];
        int destY = ys[1];

        // 1. 이동하기 전에 원래 자리에 방패(신의 가호)가 있었는지 확인!
        bool hadShield = gameManager.board.shieldGrid[tx, ty];

        // 🚨 시작 위치 기억
        Vector3 startPos = gameManager.board.GetWorldPosition(tx, ty);

        // 2. 이동 전 좌표 제거 (돌 + 방패)
        gameManager.board.grid[tx, ty] = 0;
        gameManager.board.RemoveStoneObjectAt(tx, ty);
        if (hadShield) gameManager.board.RemoveShield(tx, ty);

        // 3. 이동 후 좌표 배치 (돌 + 방패)
        StoneColor opponentColor = gameManager.localPlayerColor.Opponent();
        GameObject newStone = gameManager.board.PlaceStone(destX, destY, opponentColor);
        if (hadShield) gameManager.board.ApplyShield(destX, destY);

        // 🚨 텔레포트 이펙트 발동!
        Vector3 endPos = gameManager.board.GetWorldPosition(destX, destY);
        SkillVFXManager.Instance.PlayTeleportVFX(newStone, startPos, endPos);

        // 4. 상대 투명화 시전 중일 때는 코루틴으로 깜빡인 후 숨김!
        if (oppInvisibilityTurns > 0 && newStone != null)
        {
            StartCoroutine(gameManager.board.BlinkAndHideRoutine(newStone, opponentColor, false));
        }
        else if (newStone != null)
        {
            //  투명화가 아니면 무조건 새빨간색으로 강렬하게 3번 점멸!
            StartCoroutine(gameManager.board.HighlightExtraStoneRoutine(newStone, gameManager.board.visualSettings.extraPlaceBlinkColor));
        }
    }
    //  2번스킬 추가
    private void ReceiveSkill_Seal(int[] xs, int[] ys)
    {
         StoneColor opponentColor = gameManager.localPlayerColor.Opponent();
        int duration = skillDatabase[2].durationTurn;
        gameManager.board.ApplySeal(xs[0], ys[0], duration, opponentColor);

        // 🚨 봉인 이펙트 발동!
        Vector3 pos = gameManager.board.GetWorldPosition(xs[0], ys[0]);
        SkillVFXManager.Instance.PlayPrimitiveShield(pos, Color.yellow);
    }
     // [수정] DoubleDown 수신 — pendingExtraPlacement 대신 수신 측 처리
    //        상대방이 보낸 패킷의 xs[1],ys[1]에 랜덤 착수 좌표가 들어있으면 그대로 반영
    //  3번스킬 추가
    private void ReceiveSkill_DoubleDown(int[] xs, int[] ys)
    {
        // 첫 번째 패킷: xs[0] == -1 → 스킬 선언만
        // 두 번째 패킷: xs[0] != -1 → 랜덤 착수 좌표 수신
        if (xs[0] != -1)
        {
            StoneColor oppColor = gameManager.localPlayerColor.Opponent();
            gameManager.ExecutePlaceStonePublic(xs[0], ys[0], oppColor, PlacementType.SkillInduced);

            // 🚨 상대방이 추가 돌을 놓을 때도 바닥에 소환진 쾅! (플래시 삭제)
            Vector3 spawnPos = gameManager.board.GetWorldPosition(xs[0], ys[0]);
            SkillVFXManager.Instance.PlayPrimitiveShield(spawnPos, Color.yellow);

            // 상대방이 투명화 상태라면 깜빡인 후 숨김 처리
            if (oppInvisibilityTurns > 0)
            {
                GameObject extraStone = gameManager.board.GetStoneObjectAt(xs[0], ys[0]);
                if (extraStone != null) StartCoroutine(gameManager.board.BlinkAndHideRoutine(extraStone, oppColor, false));
            }
            else
            {
                //  약한 점멸 대신 새빨간색 점멸 코루틴 호출!
                GameObject extraStone = gameManager.board.GetStoneObjectAt(xs[0], ys[0]);
                if (extraStone != null) StartCoroutine(gameManager.board.HighlightExtraStoneRoutine(extraStone, gameManager.board.visualSettings.extraPlaceBlinkColor));
            }

            Debug.Log($"[Network] DoubleDown 추가 착수 수신: ({xs[0]},{ys[0]})");
            return;
        }
        else
        {
            Debug.Log("[Network] DoubleDown 수신 — B타입 스킬, 착수 패킷 대기");
        }
    }
    //  4번스킬 추가
    
    private void ReceiveSkill_AntiMagic(int[] xs, int[] ys)
    {
        sealedTurnsRemaining = skillDatabase.TryGetValue(4, out SkillData data) ? data.durationTurn : 2; // CSV에서 지속 턴 가져오기 (기본값 2턴)
        Debug.Log("[Network] AntiMagic 수신 — 상대 스킬 " + sealedTurnsRemaining + "턴간 봉인");

        RefreshAntiMagicOverlayUI();

        // 내 화면의 상대방 프로필 쪽에 자물쇠 아이콘 켜기 (직관적 피드백)
        if (gameManager.gameHUD != null)
        {
            gameManager.gameHUD.SetOpponentSilencedUI(true);
            gameManager.gameHUD.ShowSystemMessage("상대방이 안티매직을 사용했습니다. " + sealedTurnsRemaining + "턴간 스킬 사용 불가!");
        }
    }

    // 5번스킬 분리
    private void ReceiveSkill_Erase(int[] xs, int[] ys)
    {
        if (xs[0] != -1 && ys[0] != -1)
        {
            // 상대방이 제거 스킬을 쓰면 즉시 지우지 않고 예약 상태로 만듦
            pendingRemoveTarget = new Vector2Int(xs[0], ys[0]);

            // 🚨 상대가 내 돌을 지우려고 찍었을 때, 내 화면에서도 그 돌을 빨갛게 물들여줌 (시인성!)
            GameObject targetObj = gameManager.board.GetStoneObjectAt(xs[0], ys[0]);
            if (targetObj != null)
            {
                targetObj.GetComponent<StoneVisualController>()?.SetOverlay(Color.red, 0.8f);
            }

            Debug.Log($"[Network] 제거 스킬 예약 수신: ({xs[0]}, {ys[0]}) - 상대가 착수 시 파괴됨");
        }
    }

    // 6번스킬
    private void ReceiveSkill_Bladefall(int[] xs, int[] ys)
    {
        StoneColor opponentColor = gameManager.localPlayerColor.Opponent();

        for (int i = 0; i < xs.Length; i++)
        {
            if (xs[i] != -1 && ys[i] != -1)
            {
                gameManager.board.ApplySealWithKnife(xs[i], ys[i], skillDatabase[6].durationTurn, opponentColor);
            }
        }

        // 🚨 칼날비 카메라 셰이킹
        SkillVFXManager.Instance.PlayBladefallShake();

        if (gameManager.gameHUD != null)
            gameManager.gameHUD.ShowSystemMessage("상대방이 칼날비를 사용했습니다. 빈 교차점이 봉인됩니다!");

        Debug.Log("[Network] 칼날비 수신 — 봉인 적용 완료!");
    }

    // 7번 스킬
    private void ReceiveSkill_Invisibility(int[] xs, int[] ys)
    {
        oppInvisibilityTurns = skillDatabase[7].durationTurn; // 7턴
        StoneColor oppColor = gameManager.localPlayerColor.Opponent();

        // 상대방이 투명화 썼으니, 내 화면의 상대 돌을 100% 투명하게(isVisible=false, isMyStone=false) 변경
        gameManager.board.SetStoneInvisibility(oppColor, false, false);

        // 🚨 상대(시온님) 돌들을 내 화면에서 완전 투명하게 처리
        gameManager.board.SetStoneInvisibility(oppColor, false, false);

        // 🚨 카메라 펀치 삭제! 오직 하얗게 0.2초 번쩍(눈 깜빡)만 남김!
        SkillVFXManager.Instance.PlayScreenFlash(Color.white, 0.2f);

        if (gameManager.gameHUD != null)
            gameManager.gameHUD.ShowSystemMessage("상대방이 투명화를 사용했습니다!");
    }

    // 8번 스킬
    private void ReceiveSkill_SevenSins()
    {
        // 상대방이 썼으니, 내(localPlayerColor) 승리 조건이 7로 바뀜!
        RuleSettings myRules = (gameManager.localPlayerColor == StoneColor.Black) ? gameManager.board.ruleManager.blackRules : gameManager.board.ruleManager.whiteRules;
        myRules.winCondition = 7;

        if (gameManager.gameHUD != null)
            gameManager.gameHUD.ShowSystemMessage("상대방이 칠죄종을 사용했습니다. 나의 승리 조건이 7목으로 변경됩니다!");

        // 🚨 피격자(상대방이 쓴 걸 받은 나) 화면에는 지진 + "필터 영구 유지"
        SkillVFXManager.Instance.PlayCameraShake(0.8f, 0.3f);

        // 🚨 불투명도 100%에 가까운 검붉은색 (0.95f 정도로 해야 바둑판이 살짝 보입니다)
        SkillVFXManager.Instance.SetPersistentOverlay(
            SkillVFXManager.Instance.sevenSinsOverlayImage,
            true,
            new Color(0.35f, 0.05f, 0.1f, 0.95f)
        );

        Debug.Log("[Network] 칠죄종 수신 — 내 승리 조건 7목 강제 적용!");
    }

    // 9번 스킬
    private void ReceiveSkill_GodBless(int[] xs, int[] ys)
    {
        // xs, ys 배열에는 선택한 돌과 랜덤으로 선택된 돌의 좌표가 들어있습니다.
        for (int i = 0; i < xs.Length; i++)
        {
            if (xs[i] != -1 && ys[i] != -1)
            {
                // 상대방 화면(내 화면 기준)에 보호막 시각 효과 및 데이터 적용
                gameManager.board.ApplyShield(xs[i], ys[i]);

                // 🚨 상대가 쓴 신의 가호도 내 화면에서 샤라랄라 재생!
                Vector3 pos = gameManager.board.GetWorldPosition(xs[i], ys[i]);
                SkillVFXManager.Instance.PlayPrimitiveShield(pos, Color.cyan);

                // 시각적 피드백: 보호막이 씌워지는 돌을 하늘색으로 깜빡이게 해보죠!
                GameObject stone = gameManager.board.GetStoneObjectAt(xs[i], ys[i]);
                if (stone != null)
                {
                    MeshRenderer mr = stone.GetComponent<MeshRenderer>();

                    // 내 화면에서 이 돌이 보일 때만 하늘색으로 깜빡임! (투명화 스킬 관련 예외처리)
                    if (mr != null && mr.enabled)
                    {
                        gameManager.board.BlinkStoneEffect(stone, gameManager.board.visualSettings.godBlessBlinkColor);
                    }
                }
            }
        }
    }

    // 10번 스킬
    private void ReceiveSkill_Destruction()
    {
        // 상대방(흑돌)이 룰 파괴를 썼으니, 내 화면의 RuleManager에도 동일하게 적용
        gameManager.board.ruleManager.DisableRenjuRules(1);

        // 금수 마커 즉시 갱신
        gameManager.board.UpdateForbiddenMarks(gameManager.currentTurnColor);

        // 🚨 렌주룰 파괴 이펙트 (시뻘건 화면 + 대지진 + 펀치) -> 신성화와 1.5초 간격으로 터짐
        SkillVFXManager.Instance.PlayScreenFlash(new Color(1f, 0, 0, 0.4f), 0.3f);
        SkillVFXManager.Instance.PlayFOVPunch(0.3f, 20f);
        SkillVFXManager.Instance.PlayCameraShake(0.6f, 0.25f);

        if (gameManager.gameHUD != null)
            gameManager.gameHUD.ShowSystemMessage("상대방이 룰 파괴를 사용했습니다. 흑돌의 금수가 해제됩니다.");

        Debug.Log("[Network] 룰 파괴 수신 — 흑돌 금수 해제 적용!");
    }

    // 11번 스킬
    private void ReceiveSkill_Consecration()
    {
        // 상대방(백돌)이 10번 패시브를 가지고 있으면 내 화면(흑돌)의 보드도 신성화 모드로 변경
        gameManager.board.ActivateConsecration();

        // 🚨 신성화 이펙트 (하얗게 전체화면 점멸)
        SkillVFXManager.Instance.PlayScreenFlash(Color.white, 1f);

        if (gameManager.gameHUD != null)
            gameManager.gameHUD.ShowSystemMessage("상대방이 신성화를 장착했습니다. 모든 돌이 백돌로 보입니다!");

        Debug.Log("[Network] 신성화 수신 — 렌더링 방해 적용!");
    }

    // 스킬 선택창 관련 --------------------------------------------------
    // 1. UI에서 스킬을 고를 때마다 호출할 함수
    public void OnSkillSelected(int skillId)
    {
        // slotIndex: 0~2번 자리, skillId: 선택한 스킬 번호
        // mySkillsID[slotIndex] = skillId;
        // Debug.Log($"{slotIndex}번 슬롯에 {skillId}번 스킬 장착");
        // [추가] 이미 선택된 스킬인지 확인
        // ↓ 추가: 준비 완료 후 선택 차단
        if (isLocalPlayerReady)
        {
            gameManager.gameHUD?.ShowSkillSelectMessage("준비 완료 후 변경할 수 없습니다.");
            return;
        }

        for (int i = 0; i < mySkillsID.Length; i++)
        {
            if (mySkillsID[i] == skillId)
            {
                gameManager.gameHUD?.ShowSkillSelectMessage("이미 선택된 스킬입니다.");
                return;
            }
        }
        // 빈 슬롯 찾아서 자동 배정
        for (int i = 0; i < mySkillsID.Length; i++)
        {
            if (mySkillsID[i] == -1)
            {
                mySkillsID[i] = skillId;
                SortDeckAndRefreshUI();
                return;
            }
        }
        // 꽉 찬 경우
        gameManager.gameHUD?.ShowSkillSelectMessage("슬롯이 가득 찼습니다.");
    }

     private void SortDeckAndRefreshUI()
    {
        // ID 오름차순 정렬 (-1은 뒤로)
        System.Array.Sort(mySkillsID, (a, b) =>
        {
            if (a == -1) return 1;
            if (b == -1) return -1;
            return a.CompareTo(b);
        });
 
        // 덱 슬롯 UI 갱신
        gameManager.gameHUD?.RefreshDeckSlots(mySkillsID, skillDatabase);
 
        // 확정 버튼 활성화 — 3슬롯 모두 찼을 때만
        bool isFull = System.Array.TrueForAll(mySkillsID, id => id != -1);
        gameManager.gameHUD?.SetReadyButtonInteractable(isFull);
    }

    // 덱 슬롯 버튼 클릭 시 호출 — 인스펙터에서 각 슬롯 버튼에 연결
    // DeckSlot_0 → OnDeckSlotClicked(0)
    // DeckSlot_1 → OnDeckSlotClicked(1)
    // DeckSlot_2 → OnDeckSlotClicked(2)
    public void OnDeckSlotClicked(int slotIndex)
    {
        // ↓ 추가: 준비 완료 후 취소 차단
        if (isLocalPlayerReady)
        {
            gameManager.gameHUD?.ShowSkillSelectMessage("준비 완료 후 변경할 수 없습니다.");
            return;
        }
        if (slotIndex < 0 || slotIndex >= mySkillsID.Length) return;
        if (mySkillsID[slotIndex] == -1) return; // 이미 비어있음
 
        int removedId = mySkillsID[slotIndex];
        mySkillsID[slotIndex] = -1;
        SortDeckAndRefreshUI();
        Debug.Log($"[SkillSelect] 슬롯{slotIndex} 스킬 {removedId}번 제거");
    }

    // 2. '준비 완료' 버튼 누르거나 패킷 받았을 때 호출
    public void SetPlayerReady(bool isLocal)
    {
        if (isLocal) isLocalPlayerReady = true;
        else isRemotePlayerReady = true;

        // 둘 다 준비되면 게임 시작 상태로 변경 (GameManager 제어)
        if (isLocalPlayerReady && isRemotePlayerReady)
        {
            gameManager.gameHUD?.HideSkillSelectPanel();
            gameManager.StartGameAfterSelection();
        }
    }

    // =========================================================
    // 🚨 안티매직 오버레이 우선순위 교통정리 (무조건 흰색 패널티 우선!)
    // =========================================================
    public void RefreshAntiMagicOverlayUI()
    {
        if (sealedTurnsRemaining > 0)
        {
            // 내가 침묵 당함: 흰색 (투명도 높여서 0.3f)
            SkillVFXManager.Instance.SetPersistentOverlay(SkillVFXManager.Instance.antiMagicOverlayImage, true, new Color(1f, 1f, 1f, 0.3f));
        }
        else
        {
            bool isOpponentSilenced = activeEffects.Exists(e => e.skillId == 4 && e.casterColor == gameManager.localPlayerColor);
            if (isOpponentSilenced)
            {
                // 내가 시전함: 하늘색
                SkillVFXManager.Instance.SetPersistentOverlay(SkillVFXManager.Instance.antiMagicOverlayImage, true, new Color(0f, 0.8f, 1f, 0.6f));
            }
            else
            {
                SkillVFXManager.Instance.SetPersistentOverlay(SkillVFXManager.Instance.antiMagicOverlayImage, false);
            }
        }
    }

    public void ResetForRematch()
    {
        // 인스턴스/덱 초기화
        mySkills.Clear();
        oppSkills.Clear();
        mySkillsID  = new int[] { -1, -1, -1 };
        oppSkillsID = new int[] { -1, -1, -1 };

        // SP 초기화
        mySP  = 0;
        oppSP = 0;

        // 상태 초기화
        sealedTurnsRemaining  = 0;
        myInvisibilityTurns   = 0;
        oppInvisibilityTurns  = 0;
        selectedSkillSlot     = -1;
        activeEffects.Clear();

        // 재시작 시
        RefreshAntiMagicOverlayUI();

        //  제거 스킬 예약 좌표 초기화! (이거 안 하면 다음 판에 엉뚱한 돌이 터집니다)
        pendingRemoveTarget = new Vector2Int(-1, -1);

        // 준비 상태 초기화
        isLocalPlayerReady  = false;
        isRemotePlayerReady = false;

        // UI 갱신
        if (gameManager.gameHUD != null)
        {
            gameManager.gameHUD.UpdateSPUI(0, 0);
            gameManager.gameHUD.RefreshBuffIcons(activeEffects, gameManager.localPlayerColor);
            gameManager.gameHUD.SetOpponentSilencedUI(false);
            gameManager.gameHUD.ResetSkillLog();
            gameManager.gameHUD?.RefreshDeckSlots(mySkillsID, skillDatabase);
            gameManager.gameHUD?.RefreshOppDeckSlots(new int[] { -1, -1, -1 }, skillDatabase);
        }
        Debug.Log("[SkillManager] ResetForRematch 완료");
    }
    // 네트워크 동기화가 끝나고 게임 시작 직전(StartGameAfterSelection)에 호출하면 됩니다.
    public void GenerateSkillInstances()
    {
        mySkills.Clear();
        oppSkills.Clear(); //  (AI 모드일 경우: AI 스킬 리스트도 싹 비우고 시작)

        for (int i = 0; i < 3; i++)
        {
            // 1. 내 스킬 생성 로직 (기존 유지)
            int skillId = mySkillsID[i];
            if (skillId != -1)
            {
                SkillBase newSkill = CreateSkillByID(skillId);

                if (newSkill != null)
                {
                    mySkills.Add(newSkill);

                    if (gameManager.gameHUD != null)
                    {
                        bool isPassive = newSkill.data.type == "전용";

                        if (gameManager.gameHUD.skillCostTexts.Length > i)
                            gameManager.gameHUD.skillCostTexts[i].text = newSkill.data.spCost.ToString(); // UI에 코스트(SP) 텍스트 반영

                        if (gameManager.gameHUD.skillNameTexts != null && gameManager.gameHUD.skillNameTexts.Length > i)
                            gameManager.gameHUD.skillNameTexts[i].text = newSkill.data.skillName; // UI에 스킬 이름 텍스트 반영

                        // 패시브 스킬은 클릭 불가
                        gameManager.gameHUD.activeSkillButtons[i].interactable = !isPassive;

                        Button btn = gameManager.gameHUD.activeSkillButtons[i];
                        SkillTooltipTrigger trigger = btn.GetComponent<SkillTooltipTrigger>();
                        if (trigger == null) trigger = btn.gameObject.AddComponent<SkillTooltipTrigger>();
                       trigger.SetData(newSkill.data, gameManager.gameHUD.GetSkillIcon(newSkill.data.skillId));
                        // 인게임 버튼에 아이콘 전달
                        gameManager.gameHUD?.ApplySkillIconToActiveButton(i, mySkillsID[i]);
                    }
                }
            }

            // 2. 상대방(AI) 스킬 인스턴스 생성
            int oppSkillId = oppSkillsID[i];
            if (oppSkillId != -1)
            {
                SkillBase aiSkill = CreateSkillByID(oppSkillId);
                if (aiSkill != null)
                {
                    oppSkills.Add(aiSkill);
                }
            }
        }

        Debug.Log($"[SkillManager] 내 스킬 {mySkills.Count}개, 상대방(AI) 스킬 {oppSkills.Count}개 생성 완료!");

        // 생성 끝났으니 하단 버튼 이벤트 연결
        BindSkillButtons();

        // 게임 시작 직후, 현재 SP와 쿨타임에 맞춰 버튼 상태를 즉시 업데이트
        RefreshSkillButtonStates();
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
        List<int> availableSkills = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9};//전용 스킬 9,10 은 별도로.

        // 10번: 렌주룰 파괴(흑), 11번: 신성화(백)
        if (gameManager.localPlayerColor == StoneColor.Black)
            availableSkills.Add(10);
        else
            availableSkills.Add(11);

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

                // mySkillsID[i] = pickedId; // 슬롯에 장착
                availableSkills.RemoveAt(randomIndex); // 중복 방지를 위해 리스트에서 제거

                // UI에 반영 
                OnSkillSelected(pickedId);
            }
        }

        Debug.Log($"[SkillManager] 타임아웃! 스킬 자동 선택 완료: {mySkillsID[0]}, {mySkillsID[1]}, {mySkillsID[2]}");

        // 예외 처리 및 분기
        if (gameManager.currentMode == PlayMode.Multiplayer && gameSession != null)
        {
            // 1. 멀티플레이 모드: GameSession이 존재하면 패킷 쏘고 상대방 기다림
            gameSession.SendSyncPlayerInfo();
        }
        else
        {
            // 2. 솔로/AI 모드: 내 스킬 덱을 장착하고 즉시 준비 완료 처리
            InitializeSkillDeck(true, mySkillsID);
            SetPlayerReady(true);
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

    // =========================================================
    // 스킬 버튼 클릭 시 타겟팅/프리뷰 상태로 진입
    // =========================================================
    private void OnActiveSkillButtonClicked(int slotIndex)
    {
        // 이미 이번 턴에 스킬을 썼다면 막기
        if (gameManager.hasUsedSkillThisTurn)
        {
            gameManager.gameHUD?.ShowSystemMessage("이번 턴에는 이미 스킬을 사용했습니다!");
            return;
        }

        // 방어 로직: 내 턴이 아니거나, 게임 중이 아니면 무시
        // 스킬 사용 후 선택중일때도 무시.
        //if (gameManager.currentState != GameState.Playing) return;
        if (gameManager.currentState != GameState.Playing && gameManager.currentState != GameState.SkillPreview) return;
        if (gameManager.currentTurnColor != gameManager.localPlayerColor)
        {
            Debug.LogWarning("내 차례에만 스킬을 사용할 수 있습니다!");
            return;
        }

        if (slotIndex >= mySkills.Count) return;

        SkillBase selectedSkill = mySkills[slotIndex];
        SkillUseResult result = selectedSkill.CanUse(mySP, sealedTurnsRemaining > 0, gameManager.board, gameManager.localPlayerColor);
        
        // 스킬 사용 가능 여부 체크 (SP, 쿨타임 등)
        if (result != SkillUseResult.Success)
        {
            string errorMessage = "";
            switch (result)
            {
                case SkillUseResult.AntiMagicBlocked:
                    errorMessage = "안티매직에 걸려 있습니다!";
                    break;
                case SkillUseResult.NotEnoughSP:
                    errorMessage = "SP가 부족합니다!";
                    break;
                case SkillUseResult.OnCooldown:
                    errorMessage = "쿨타임 중입니다!";
                    break;
                case SkillUseResult.NoValidTarget:
                    errorMessage = "유효한 타겟이 없습니다!";
                    break;  
            }
            Debug.LogWarning($"[{selectedSkill.data.skillName}] 사용 불가! ({errorMessage})");
            return;
        }

        // [수정] 즉시 Execute 대신 SkillPreview 상태 진입
        selectedSkillSlot = slotIndex;
        gameManager.currentState = GameState.SkillPreview;
        // 타겟 유형에 따라 안내 메시지 및 하이라이트
        switch (selectedSkill.data.targetType)
        {
            case "my":
                gameManager.board.HideSkillTargetMarkers();
                gameManager.gameHUD?.ShowSystemMessage(
                    $"{selectedSkill.data.skillName} 사용! 대상이 될 내 돌을 클릭하세요. (우클릭: 취소)");
                break;
            case "enemy":
                if (selectedSkill.data.skillId == 5)
                {
                    gameManager.board.HideSkillTargetMarkers();
                    gameManager.gameHUD?.ShowSystemMessage("제거 스킬 사용! 삭제할 상대방 돌을 클릭하세요. (우클릭: 취소)");
                }
                else
                {
                    gameManager.board.ShowSkillTargetMarkers(gameManager.localPlayerColor);
                }
                break;
            case "cell":
                gameManager.board.HideSkillTargetMarkers();
                gameManager.gameHUD?.ShowSystemMessage(
                    $"{selectedSkill.data.skillName} 사용! 대상 칸을 클릭하세요. (우클릭: 취소)");
                break;
            default: // "none" — 즉발형 (A타입)
                gameManager.board.HideSkillTargetMarkers();
                gameManager.gameHUD?.ShowSystemMessage(
                    $"{selectedSkill.data.skillName} 사용! 클릭으로 확정, 우클릭으로 취소.");
                break;
        }
 
        Debug.Log($"==== [{selectedSkill.data.skillName}] SkillPreview 진입! ====");
    }

    // =========================================================
    // [추가] ConfirmSkill — SkillPreview에서 좌클릭 시 호출
    // =========================================================
    public void ConfirmSkill(int x, int y)
    {
        Debug.Log($"[ConfirmSkill] 호출됨 x:{x} y:{y} slot:{selectedSkillSlot}");
        if (selectedSkillSlot < 0 || selectedSkillSlot >= mySkills.Count) return;
 
        SkillBase skill = mySkills[selectedSkillSlot];
        bool isBType    = (skill.data.skillId == 3 || skill.data.skillId == 6);
 
        int arraySize = (skill.data.skillId == 6) ? 20 : 2;
        int[] targetX = new int[arraySize];
        int[] targetY = new int[arraySize];
        for (int j = 0; j < arraySize; j++) { targetX[j] = -1; targetY[j] = -1; }
        // targetX[0] = x; targetY[0] = y; 대신
        if (skill.data.targetType == "none")
        {
            targetX[0] = -1;
            targetY[0] = -1;
        }
        else
        {
            targetX[0] = x;
            targetY[0] = y;
        }

        // 스킬 발동 시도
        if (skill.Execute(targetX, targetY, gameManager, gameManager.board))
        {
            // 성공 시 공통 로직 호출
            OnSkillExecutionSuccess(skill, targetX, targetY);
        }
        else
        {
            // 실패 시 취소(Playing 상태로 돌아감) 시키지 않음! 우클릭할 때까지 무한 대기!
            gameManager.gameHUD?.ShowSystemMessage("유효하지 않은 타겟입니다. 다시 선택하거나 우클릭으로 취소하세요.");
        }
    }

    // =========================================================
    //  공통 스킬 성공 처리 헬퍼 함수
    // =========================================================
    private void OnSkillExecutionSuccess(SkillBase skill, int[] targetX, int[] targetY)
    {
        mySP -= skill.data.spCost;
        skill.currentCooldown = skill.data.cooldown;
        gameManager.hasUsedSkillThisTurn = true;
        //gameManager.gameHUD?.AddSkillLog("나", skill.data.skillName, gameManager.CurrentMoveCount);

        // 🚨 [추가] 내가 스킬을 사용 확정했을 때 내 화면만 약하게 상하좌우 진동 (피드백)
        SkillVFXManager.Instance.PlayMultiDirectionShake(0.15f, 0.05f);

        // B타입은 ExecutePendingSkill에서 로그 기록
        bool isBType = (skill.data.skillId == 3 || skill.data.skillId == 6);
        if (!isBType)
            {
                gameManager.gameHUD?.ShowSkillEffect(skill.data.skillId); // ← 함께 묶음
                gameManager.gameHUD?.RecordSkillLog(gameManager.CurrentMoveCount, "나", skill.data.skillName);
            }   
        // 버프 리스트 등록
        if (skill.data.durationTurn > 0)
        {
            activeEffects.Add(new ActiveEffect
            {
                skillId = skill.data.skillId,
                remainingTurns = skill.data.durationTurn,
                isBuff = true,
                effectName = skill.data.skillName,
                description = skill.data.description,
                casterColor = gameManager.localPlayerColor
            });
            gameManager.gameHUD?.RefreshBuffIcons(activeEffects, gameManager.localPlayerColor);
        }

        if (gameManager.gameHUD != null)
            gameManager.gameHUD.UpdateSPUI(mySP, oppSP);

        if (gameManager.currentMode == PlayMode.Multiplayer && gameSession != null)
        {
            if (isBType)
                gameSession.SendUseSkill(skill.data.skillId, new int[] { -1 }, new int[] { -1 }, gameManager.CurrentMoveCount);
            else
                gameSession.SendUseSkill(skill.data.skillId, targetX, targetY, gameManager.CurrentMoveCount);
        }

        // ** 안티매직 시전 즉시 모든 내 돌에 하늘색 오버레이를 입히기 위해 호출!
        gameManager.board.RefreshAllStonesVisuals();

        // 스킬 사용이 끝났으므로 상태 초기화
        gameManager.currentState = GameState.Playing;
        selectedSkillSlot = -1;
        gameManager.board.HideSkillTargetMarkers();
        gameManager.board.UpdateForbiddenMarks(gameManager.currentTurnColor);
        RefreshSkillButtonStates();

        gameManager.board.RefreshAllStonesVisuals(); // 버프가 켜졌으니 바둑판 렌더링 즉시 새로고침 (안티매직 하늘색 표시용)

        // 🚨 [여기에 추가!] 버프 리스트 등록까지 다 끝났으니, 오버레이 상태 최종 갱신!
        RefreshAntiMagicOverlayUI();
    }

    // =========================================================
    // [추가] ExecutePendingSkill — B타입 착수 후 효과 처리
    //        placedX, placedY = 방금 착수한 좌표 (봉인/랜덤 착수 제외 대상)
    // =========================================================
    public void ExecutePendingSkill(int skillId, int placedX, int placedY)
    {
        switch (skillId)
        {
            case 3: // DoubleDown — placedX,Y 제외 랜덤 착수
            {   
                int logTurn = gameManager.CurrentMoveCount;
                List<Vector2Int> candidates = new List<Vector2Int>();
                int radius = 2; // 반경 2칸 이내 제한 

                for (int x = 0; x < gameManager.board.boardSize; x++)
                {
                    for (int y = 0; y < gameManager.board.boardSize; y++)
                    {
                        if (!(x == placedX && y == placedY) && gameManager.board.IsValidMove(x, y, gameManager.currentTurnColor, silent: true))
                        {
                            // 주변에 다른 돌이 있을 때만 후보에 넣음
                            if (gameManager.board.HasNeighborInRadius(x, y, radius))
                                candidates.Add(new Vector2Int(x, y));
                        }
                    }
                }

                // 만약 초반이라 주변 자리가 없으면 전체 보드에서 찾음 (안전장치)
                if (candidates.Count == 0)
                {
                    for (int x = 0; x < gameManager.board.boardSize; x++)
                        for (int y = 0; y < gameManager.board.boardSize; y++)
                            if (!(x == placedX && y == placedY) && gameManager.board.IsValidMove(x, y, gameManager.currentTurnColor, silent: true))
                                candidates.Add(new Vector2Int(x, y));
                }

                if (candidates.Count > 0)
                {
                    Vector2Int rand = candidates[UnityEngine.Random.Range(0, candidates.Count)];

                    // 🚨 이중착수 돌이 튀어나올 때 그 바닥에 쉴드 이펙트를 소환진처럼 터뜨림!
                    Vector3 spawnPos = gameManager.board.GetWorldPosition(rand.x, rand.y);
                    SkillVFXManager.Instance.PlayPrimitiveShield(spawnPos, Color.yellow);

                    gameManager.ExecutePlaceStonePublic(
                    rand.x, rand.y,
                    gameManager.currentTurnColor,
                    PlacementType.SkillInduced);

                    // 투명화 상태라면 방금 놓은 랜덤 돌도 내 화면에서 깜빡인 후 숨김
                    if (myInvisibilityTurns > 0)
                    {
                        GameObject extraStone = gameManager.board.GetStoneObjectAt(rand.x, rand.y);
                        if (extraStone != null) StartCoroutine(gameManager.board.BlinkAndHideRoutine(extraStone, gameManager.localPlayerColor, true));
                    }
                    else
                    {
                        // 내 화면에서도 돌이 튀어나올 때 새빨갛게 점멸!
                        GameObject extraStone = gameManager.board.GetStoneObjectAt(rand.x, rand.y);
                        if (extraStone != null) StartCoroutine(gameManager.board.HighlightExtraStoneRoutine(extraStone, gameManager.board.visualSettings.extraPlaceBlinkColor));
                        }
                    // 방금 강제로 튀어나온 랜덤 돌에도 안티매직 등 버프 색상을 즉시 묻혀줌!
                    gameManager.board.RefreshAllStonesVisuals();
                    
                    // ↓ 추가
                    SkillBase doubleDown = mySkills.Find(s => s.data.skillId == 3);
                     gameManager.gameHUD?.ShowSkillEffect(3); 
                    if (doubleDown != null)
                    {
                        //gameManager.gameHUD?.AddSkillLog("나", doubleDown.data.skillName, gameManager.CurrentMoveCount);
                        gameManager.gameHUD?.RecordSkillLog(logTurn, "나", doubleDown.data.skillName);
                    }
                    // 일반 착수 패킷이 먼저 날아가도록 프레임 끝까지 지연 전송 (이슈 6 해결)
                    StartCoroutine(SendDeferredSkillPacket(3, new int[] { rand.x, -1 }, new int[] { rand.y, -1 }, logTurn));
                    Debug.Log($"[DoubleDown] 추가 착수: ({rand.x},{rand.y})");
                }
                break;
            }
            case 6: // Bladefall — placedX,Y 제외 봉인 20칸
            {
                int logTurn = gameManager.CurrentMoveCount;
                SkillBase bladefall = mySkills.Find(s => s.data.skillId == 6);
                if (bladefall == null) break;
 
                // Skill_6_Bladefall의 SelectSealTargets를 통해 봉인 적용
                // placedX,Y를 제외 좌표로 넘겨 Execute 재호출
                int[] bx = new int[20]; int[] by = new int[20];
                for (int i = 0; i < 20; i++) { bx[i] = -1; by[i] = -1; }
                bx[0] = placedX; by[0] = placedY; // [0]에 제외 좌표 전달
 
                bladefall.Execute(bx, by, gameManager, gameManager.board);

                 // ↓ 추가
                //gameManager.gameHUD?.AddSkillLog("나", bladefall.data.skillName, gameManager.CurrentMoveCount);
                gameManager.gameHUD?.ShowSkillEffect(6);
                gameManager.gameHUD?.RecordSkillLog(logTurn, "나", bladefall.data.skillName);
                // 일반 착수 패킷 먼저 날아가도록 지연 전송
                StartCoroutine(SendDeferredSkillPacket(6, bx, by, logTurn));

                // 🚨 칼날비 전용 카메라 셰이킹
                SkillVFXManager.Instance.PlayBladefallShake();

                Debug.Log("[Bladefall] 착수 후 봉인 발동 완료");
                break;
            }
        }
    }

    // 수동 착수 패킷을 추월하지 않도록 막아주는 지연 전송 코루틴
    private System.Collections.IEnumerator SendDeferredSkillPacket(int skillId, int[] xs, int[] ys, int turnCount)
    {
        // GameManager의 수동 착수 처리가 완전히 끝날 때까지 프레임 끝에서 대기합니다.
        yield return new WaitForEndOfFrame();

        if (gameManager.currentMode == PlayMode.Multiplayer && gameSession != null)
        {
            gameSession.SendUseSkill(skillId, xs, ys, turnCount);
        }
    }

    public void AutoActivatePassiveSkills()
    {
        StartCoroutine(PassiveSequenceRoutine()); // 코루틴으로 실행
    }

    private System.Collections.IEnumerator PassiveSequenceRoutine()
    {
        // 1. 연출 중 클릭 원천 차단
        gameManager.currentState = GameState.Wait;
        int[] noTarget = System.Array.Empty<int>();

        // 🚨 누가 패시브를 썼는지 기억할 스위치
        bool didBlackUsePassive = false;
        bool didWhiteUsePassive = false;

        // =========================================================
        // [1단계] 흑돌(선공) 패시브 검사 및 발동
        // =========================================================
        if (gameManager.localPlayerColor == StoneColor.Black)
        {
            // 내가 흑돌일 때 내 스킬 검사
            SkillBase myPassive = mySkills.Find(s => s.data.type == "전용");
            if (myPassive != null && myPassive.CanUse(mySP, false, gameManager.board, gameManager.localPlayerColor) == SkillUseResult.Success)
            {
                myPassive.Execute(noTarget, noTarget, gameManager, gameManager.board);
                if (gameManager.currentMode == PlayMode.Multiplayer && gameSession != null)
                    gameSession.SendUseSkill(myPassive.data.skillId, new int[] { -1 }, new int[] { -1 }, gameManager.CurrentMoveCount);

                didBlackUsePassive = true;
            }
        }
        else
        {
            // 상대(AI 포함)가 흑돌일 때 상대 스킬 검사
            SkillBase oppPassive = oppSkills.Find(s => s.data.type == "전용");
            if (oppPassive != null)
            {
                // AI 모드라면 기존에 쓰던 방식 그대로 AI 스킬 강제 발동!
                if (gameManager.currentMode == PlayMode.AI)
                {
                    oppPassive.Execute(noTarget, noTarget, gameManager, gameManager.board);
                }
                didBlackUsePassive = true;
            }
        }

        // 🚨 흑돌이 진짜 패시브를 발동했을 때만 2초 기다려줌! (없으면 0초 패스)
        if (didBlackUsePassive) yield return new WaitForSeconds(2.0f);


        // =========================================================
        // [2단계] 백돌(후공) 패시브 이어서 검사 및 발동
        // =========================================================
        if (gameManager.localPlayerColor == StoneColor.White)
        {
            // 내가 백돌일 때 내 스킬 검사
            SkillBase myPassive = mySkills.Find(s => s.data.type == "전용");
            if (myPassive != null && myPassive.CanUse(mySP, false, gameManager.board, gameManager.localPlayerColor) == SkillUseResult.Success)
            {
                myPassive.Execute(noTarget, noTarget, gameManager, gameManager.board);
                if (gameManager.currentMode == PlayMode.Multiplayer && gameSession != null)
                    gameSession.SendUseSkill(myPassive.data.skillId, new int[] { -1 }, new int[] { -1 }, gameManager.CurrentMoveCount);

                didWhiteUsePassive = true;
            }
        }
        else
        {
            // 상대(AI 포함)가 백돌일 때 상대 스킬 검사
            SkillBase oppPassive = oppSkills.Find(s => s.data.type == "전용");
            if (oppPassive != null)
            {
                // AI 모드라면 기존에 쓰던 방식 그대로 AI 스킬 강제 발동!
                if (gameManager.currentMode == PlayMode.AI)
                {
                    oppPassive.Execute(noTarget, noTarget, gameManager, gameManager.board);
                }
                didWhiteUsePassive = true;
            }
        }

        // 🚨 백돌이 진짜 패시브를 발동했을 때만 1.5초(신성화 번쩍임) 기다려줌!
        if (didWhiteUsePassive) yield return new WaitForSeconds(1.5f);


        // =========================================================
        // [3단계] 연출 끝, 진짜 게임 시작!
        // =========================================================
        // 모든 돌의 렌더링 상태를 최신으로 확정 지음
        gameManager.board.RefreshAllStonesVisuals();

        // GameManager에게 "연출 다 끝났으니 타이머 돌리고 클릭 풀고 게임 시작해라!" 지시
        gameManager.StartFirstTurn();

        Debug.Log("[SkillManager] 패시브 시퀀스 완전 종료! 게임 시작!");
    }

    // InputManager에서 현재 무슨 스킬을 들고 있는지 확인하기 위한 함수
    public int GetSelectedSkillId()
    {
        if (selectedSkillSlot >= 0 && selectedSkillSlot < mySkills.Count)
        {
            return mySkills[selectedSkillSlot].data.skillId;
        }
        return -1;
    }
}
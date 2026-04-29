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
    // ** 네트워크 담당자가 방 입장 시 세팅해 줄 '스킬 번호' 배열 (데이터용)
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

    // UI가 구독할 이벤트들 (데이터만 던져줌)
    public event Action<int> OnSPChanged; // SP가 변했을 때 (내 SP 던져줌)
    public event Action<List<ActiveEffect>> OnActiveEffectsChanged; // 버프/디버프 리스트가 갱신됐을 때
    public event Action<int, bool> OnSkillButtonStateChanged; // 특정 스킬 버튼의 활성화/비활성화 상태가 변했을 때


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
                return new Skill_8_GodBless(data);
            case 9:
                return new Skill_9_Destruction(data);
            case 10: 
                return new Skill_10_Sanctification(data);
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

        // 쿨타임은 방금 턴을 마친 사람이 아니라, '이제 턴을 시작할 사람' 것을 깎습니다!
        StoneColor nextTurnColor = placedColor.Opponent();
        UpdateSkillDurations(nextTurnColor);

        // SP가 바뀌었으니 HUD에 즉시 양쪽 SP 최신화
        if (gameManager.gameHUD != null)
        {
            gameManager.gameHUD.UpdateSPUI(mySP, oppSP);
        }
    }

    private void UpdateSkillDurations(StoneColor nextTurnColor)
    {
        //// 턴이 지날 때마다 스킬들의 OnTurnPassed()를 호출하여 쿨다운 감소
        //if (turnColor == gameManager.localPlayerColor)
        //{
        //    foreach (var skill in mySkills) skill.OnTurnPassed();
        //}
        //else
        //{
        //    foreach (var skill in oppSkills) skill.OnTurnPassed();
        //}
        List<SkillBase> targetSkills = (nextTurnColor == gameManager.localPlayerColor) ? mySkills : oppSkills;

        for (int i = 0; i < targetSkills.Count; i++)
        {
            targetSkills[i].OnTurnPassed(); // 내부 쿨타임 감소 로직 호출

            // UI 텍스트 갱신은 '내 턴'이 돌아왔을 때 내 화면에서만 해주면 됩니다.
            if (nextTurnColor == gameManager.localPlayerColor && gameManager.gameHUD != null)
            {
                Button btn = gameManager.gameHUD.activeSkillButtons[i];
                TMP_Text cdText = gameManager.gameHUD.cooldownTexts[i];

                bool isPassive = targetSkills[i].data.type == "전용";
                bool isSilenced = sealedTurnsRemaining > 0; // 안티매직 걸렸는지
                bool onCooldown = targetSkills[i].currentCooldown > 0;
                bool notEnoughSP = mySP < targetSkills[i].data.spCost; // ** (나중에 사용될 듯)

                // 버튼 잠금 처리
                btn.interactable = !isPassive;

                // ** 패시브거나, 쿨타임이거나, SP가 부족하거나, 안티매직에 걸렸다면 버튼 강제 잠금 (** 이 부분은 추후 기획의도 맞춰서 해야 할 듯)
                btn.interactable = !isPassive && !onCooldown && !notEnoughSP && !isSilenced;

                // 쿨타임 텍스트 표시
                cdText.text = (!isPassive && onCooldown) ? targetSkills[i].currentCooldown.ToString() : "";
            }
        }

        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            activeEffects[i].remainingTurns--;
            if (activeEffects[i].remainingTurns <= 0)
                activeEffects.RemoveAt(i);
        }

        if (gameManager.gameHUD != null)
            gameManager.gameHUD.RefreshBuffIcons(activeEffects, gameManager.localPlayerColor);
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
            case 8: ReceiveSkill_GodBless(xs, ys); break;
            case 9: ReceiveSkill_Destruction(); break;
            case 10: ReceiveSkill_Sanctification(); break;
            default:
                Debug.LogWarning($"[Network] 스킬 ID {skillId} 수신 처리 미구현");
                break;
        }
        // 3. 실제 효과 실행 (Execute 내부에서 보드 데이터 지우기 수행)
        // xs, ys 배열을 돌면서 들어있는 모든 좌표를 지움 (최대 2개)
        // for (int i = 0; i < xs.Length; i++)
        // {
        //     int tx = xs[i];
        //     int ty = ys[i];

        //     // 유효한 좌표(-1이 아님)이고 바둑판에 돌이 있다면 삭제
        //     if (tx != -1 && ty != -1)
        //     {
        //         gameManager.board.grid[tx, ty] = 0;
        //         gameManager.board.RemoveStoneObjectAt(tx, ty);
        //     }
        // }
        if (skillObj.data.durationTurn > 0)
        {
            activeEffects.Add(new ActiveEffect
            {
                skillId = skillId,
                remainingTurns = skillObj.data.durationTurn,
                isBuff = false,
                effectName = skillObj.data.skillName,
                description = skillObj.data.description,
                casterColor = gameManager.localPlayerColor.Opponent()
            });
            gameManager.gameHUD.RefreshBuffIcons(activeEffects, gameManager.localPlayerColor);
        }
        Debug.Log($"[Network] 상대방이 {skillObj.data.skillName}을 사용했습니다.");
    }
    //  1번스킬 추가
    private void ReceiveSkill_StoneShift(int[] xs, int[] ys)
    {
        // xs[0], ys[0] = 이동 전 좌표 제거
        gameManager.board.grid[xs[0], ys[0]] = 0;
        gameManager.board.RemoveStoneObjectAt(xs[0], ys[0]);

        // xs[1], ys[1] = 이동 후 좌표 배치
        StoneColor opponentColor = gameManager.localPlayerColor.Opponent();
        GameObject newStone = gameManager.board.PlaceStone(xs[1], ys[1], opponentColor);

        // ** 상대방이 투명화 상태라면, 방금 옮긴 돌을 내 화면에서 100% 안 보이게 숨김
        if (oppInvisibilityTurns > 0 && newStone != null)
        {
            gameManager.board.ApplyVisibilityToSingleStone(newStone, opponentColor, false, false);
        }
    }
    //  2번스킬 추가
    private void ReceiveSkill_Seal(int[] xs, int[] ys)
    {
         StoneColor opponentColor = gameManager.localPlayerColor.Opponent();
        int duration = skillDatabase[2].durationTurn;
        gameManager.board.ApplySeal(xs[0], ys[0], duration, opponentColor);
    }

    //  3번스킬 추가
    private void ReceiveSkill_DoubleDown(int[] xs, int[] ys)
    {
        gameManager.pendingExtraPlacement = true;
        Debug.Log("[Network] DoubleDown 수신 — 추가 착수 대기 중");
    }
    //  4번스킬 추가
    private void ReceiveSkill_AntiMagic(int[] xs, int[] ys)
    {
        sealedTurnsRemaining = 2;
        Debug.Log("[Network] AntiMagic 수신 — 상대 스킬 2턴간 봉인");

        // 내 화면의 상대방 프로필 쪽에 자물쇠 아이콘 켜기 (직관적 피드백)
        if (gameManager.gameHUD != null)
        {
            gameManager.gameHUD.SetOpponentSilencedUI(true);
            gameManager.gameHUD.ShowSystemMessage("상대방이 안티매직을 사용했습니다. 2턴간 스킬 사용 불가!");
        }
    }

    // 5번스킬 분리
    private void ReceiveSkill_Erase(int[] xs, int[] ys)
    {
        for (int i = 0; i < xs.Length; i++)
        {
            if (xs[i] != -1 && ys[i] != -1)
            {
                // 지우기 전에 무슨 색 돌이었는지 킵하기
                StoneColor deadStoneColor = (StoneColor)gameManager.board.grid[xs[i], ys[i]];

                gameManager.board.grid[xs[i], ys[i]] = 0;
                gameManager.board.RemoveStoneObjectAt(xs[i], ys[i]);

                // 수신자(돌 주인)의 화면에서는 무조건 빨간색 깜빡임 연출
                gameManager.board.BlinkEmptySpaceEffect(xs[i], ys[i], Color.red, deadStoneColor);
            }
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
                gameManager.board.ApplySeal(xs[i], ys[i], 1, opponentColor);//skillDatabase[6].durationTurn에 duration이 0으로 되어있어서 일단 1로 고정
            }
        }

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

        if (gameManager.gameHUD != null)
            gameManager.gameHUD.ShowSystemMessage("상대방이 투명화를 사용했습니다!");
    }

    // 8번 스킬
    private void ReceiveSkill_GodBless(int[] xs, int[] ys)
    {
        // xs, ys 배열에는 선택한 돌과 랜덤으로 선택된 돌의 좌표가 들어있습니다.
        for (int i = 0; i < xs.Length; i++)
        {
            if (xs[i] != -1 && ys[i] != -1)
            {
                // 상대방 화면(내 화면 기준)에 보호막 시각 효과 및 데이터 적용
                gameManager.board.ApplyShield(xs[i], ys[i]);

                // 시각적 피드백: 보호막이 씌워지는 돌을 하늘색으로 깜빡이게 해보죠!
                GameObject stone = gameManager.board.GetStoneObjectAt(xs[i], ys[i]);
                if (stone != null)
                {
                    MeshRenderer mr = stone.GetComponent<MeshRenderer>();

                    // 내 화면에서 이 돌이 보일 때만 하늘색으로 깜빡임! (투명화 스킬 관련 예외처리)
                    if (mr != null && mr.enabled)
                    {
                        gameManager.board.BlinkStoneEffect(stone, Color.cyan);
                    }
                }
            }
        }
    }
    // 9번 스킬
    private void ReceiveSkill_Destruction()
    {
        // 상대방(흑돌)이 룰 파괴를 썼으니, 내 화면의 RuleManager에도 동일하게 적용
        gameManager.board.ruleManager.DisableRenjuRules(1);

        // 금수 마커 즉시 갱신
        gameManager.board.UpdateForbiddenMarks(gameManager.currentTurnColor);

        if (gameManager.gameHUD != null)
            gameManager.gameHUD.ShowSystemMessage("상대방이 룰 파괴를 사용했습니다. 흑돌의 금수가 해제됩니다.");

        Debug.Log("[Network] 룰 파괴 수신 — 흑돌 금수 해제 적용!");
    }

    // 10번 스킬
    private void ReceiveSkill_Sanctification()
    {
        // 상대방(백돌)이 10번 패시브를 가지고 있으면 내 화면(흑돌)의 보드도 신성화 모드로 변경
        gameManager.board.ActivateSanctification();

        if (gameManager.gameHUD != null)
            gameManager.gameHUD.ShowSystemMessage("상대방이 신성화를 장착했습니다. 모든 돌이 백돌로 보입니다!");

        Debug.Log("[Network] 신성화 수신 — 렌더링 방해 적용!");
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

    public void DecreaseSealedTurns()
    {
        if (sealedTurnsRemaining > 0)
        {
            sealedTurnsRemaining--;
            Debug.Log($"[SkillManager] 봉인 턴 감소 → 남은 턴: {sealedTurnsRemaining}");

            // 0이 되면 자물쇠 치우기
            if (sealedTurnsRemaining == 0 && gameManager.gameHUD != null)
            {
                gameManager.gameHUD.SetOpponentSilencedUI(false);
                gameManager.gameHUD.ShowSystemMessage("안티매직 효과가 풀렸습니다!");
            }
        }
    }

    // 매 턴 종료 시 턴을 깎고, 0이 되면 투명화 해제
    // GameManager의 ExecutePlaceStone 에서 턴 넘기기 직전에 이 함수를 호출해주세요.
    public void DecreaseInvisibilityTurns(StoneColor turnColor)
    {
        // 내가 턴을 마칠 때 내 투명화 턴 감소
        if (turnColor == gameManager.localPlayerColor && myInvisibilityTurns > 0)
        {
            myInvisibilityTurns--;
            if (myInvisibilityTurns == 0)
            {
                gameManager.board.SetStoneInvisibility(gameManager.localPlayerColor, true, true);
                gameManager.gameHUD.ShowSystemMessage("내 투명화 효과가 해제되었습니다.");
            }
        }
        // 상대가 턴을 마칠 때 상대 투명화 턴 감소
        else if (turnColor != gameManager.localPlayerColor && oppInvisibilityTurns > 0)
        {
            oppInvisibilityTurns--;
            if (oppInvisibilityTurns == 0)
            {
                StoneColor oppColor = gameManager.localPlayerColor.Opponent();
                gameManager.board.SetStoneInvisibility(oppColor, true, false);
                gameManager.gameHUD.ShowSystemMessage("상대방의 투명화 효과가 해제되었습니다.");
            }
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

                
                if (gameManager.gameHUD != null)
                {
                    bool isPassive = newSkill.data.type == "전용";

                    if (gameManager.gameHUD.skillCostTexts.Length > i)
                        gameManager.gameHUD.skillCostTexts[i].text = newSkill.data.spCost.ToString(); // UI에 코스트(SP) 텍스트 반영

                    if (gameManager.gameHUD.skillNameTexts != null && gameManager.gameHUD.skillNameTexts.Length > i)
                        gameManager.gameHUD.skillNameTexts[i].text = newSkill.data.skillName; // UI에 스킬 이름 텍스트 반영

                    // 패시브 스킬은 클릭 불가
                    gameManager.gameHUD.activeSkillButtons[i].interactable = !isPassive;
                }
            }
        }
        Debug.Log($"[SkillManager] 내 스킬 인스턴스 3개 생성 완료!");

        // 생성 끝났으니 하단 버튼 이벤트 연결
        BindSkillButtons();

        // 게임 시작 직후, 현재 SP와 쿨타임에 맞춰 버튼 상태를 즉시 업데이트
        // 내 로컬 플레이어 컬러를 넘겨서 내 버튼들을 새로고침합니다.
        UpdateSkillDurations(gameManager.localPlayerColor);
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
        List<int> availableSkills = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8};//전용 스킬 9,10 은 별도로.

        if (gameManager.localPlayerColor == StoneColor.Black)
            availableSkills.Add(9);
        else
            availableSkills.Add(10);

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
        // 이미 이번 턴에 스킬을 썼다면 막기
        if (gameManager.hasUsedSkillThisTurn)
        {
            Debug.LogWarning("한 턴에 하나의 스킬만 사용할 수 있습니다!");
            if (gameManager.gameHUD != null)
                gameManager.gameHUD.ShowSystemMessage("이번 턴에는 이미 스킬을 사용했습니다!");
            return;
        }

        // 방어 로직: 내 턴이 아니거나, 게임 중이 아니면 무시
        if (gameManager.currentState != GameState.Playing) return;
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

        // 추가 — targetType이 "none"이면 즉시 실행 (타겟팅 불필요)
        if (selectedSkill.data.targetType == "none")
        {
            selectedSkillSlot = slotIndex;
            // 칼날비(6번)는 최대 20칸 좌표가 필요
            int arraySize = (selectedSkill.data.skillId == 6) ? 20 : 2;
            int[] targetX = new int[arraySize];
            int[] targetY = new int[arraySize];
            for (int j = 0; j < arraySize; j++) { targetX[j] = -1; targetY[j] = -1; }

            if (selectedSkill.Execute(targetX, targetY, gameManager, gameManager.board))
            {
                gameManager.hasUsedSkillThisTurn = true; // ** 스킬 사용 완료!
                mySP -= selectedSkill.data.spCost;
                selectedSkill.currentCooldown = selectedSkill.data.cooldown;
                if (gameManager.gameHUD != null) gameManager.gameHUD.UpdateSPUI(mySP, oppSP);

                if (gameManager.currentMode == PlayMode.Multiplayer && gameSession != null)
                    gameSession.SendUseSkill(selectedSkill.data.skillId, targetX, targetY);
            }
            selectedSkillSlot = -1;
            return; //  타겟팅 모드 진입 없이 바로 종료
        }
        // 상태를 '스킬 타겟팅'으로 변경 (이제 InputManager가 클릭하면 TryPlaceStone 대신 SkillManager한테 좌표를 넘기게 될 겁니다)
        selectedSkillSlot = slotIndex;
        gameManager.currentState = GameState.SkillTargeting;


        // 비주얼 효과 시작: 보드의 모든 자신/상대 돌에 초록색 테두리 표시
        // targetType 기준으로 하이라이트 분기
        if (selectedSkill.data.targetType == "my")
        {
            gameManager.board.HideSkillTargetMarkers();
            if (gameManager.gameHUD != null)
                gameManager.gameHUD.ShowSystemMessage($"{selectedSkill.data.skillName} 사용! 대상이 될 내 돌을 클릭하세요.");
        }
        else if (selectedSkill.data.targetType == "enemy") 
        {
            // 5번(제거) 스킬이면 전체 하이라이트를 하지 않고 메시지만 띄움    // ** 나중에 좀 더 보완할 예정
            if (selectedSkill.data.skillId == 5)
            {
                if (gameManager.gameHUD != null)
                    gameManager.gameHUD.ShowSystemMessage("제거 스킬 사용! 삭제할 상대방 돌을 클릭하세요.");

                gameManager.board.HideSkillTargetMarkers();
            }
            else
            {
                gameManager.board.ShowSkillTargetMarkers(gameManager.localPlayerColor);
            }
        }
        else if (selectedSkill.data.targetType == "cell")
        {
            // TODO: 빈 칸에 호버될 때 특별한 표시를 하거나, 전체 빈칸에 마커 띄우기
            // (일단 마커 안 띄우고 조준점(InputManager)만 바뀌게 냅둬도 무방)
            gameManager.board.HideSkillTargetMarkers();
        }
        else // "none" (타겟팅 필요 없는 즉발 스킬. 예: 4번 안티매직)
        {
            // 타겟팅 모드 진입 안 하고 바로 스킬 실행!
            ExecuteSkillAt(-1, -1);
        }

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
            gameManager.hasUsedSkillThisTurn = true;
            mySP -= skillToUse.data.spCost;
            skillToUse.currentCooldown = skillToUse.data.cooldown;

            // durationTurn이 있는 스킬만 activeEffects에 추가
            if (skillToUse.data.durationTurn > 0)
            {
                ActiveEffect effect = new ActiveEffect
                {
                    skillId = skillToUse.data.skillId,
                    remainingTurns = skillToUse.data.durationTurn,
                    isBuff = true,
                    effectName = skillToUse.data.skillName,
                    description = skillToUse.data.description
                };
                activeEffects.Add(effect);
                gameManager.gameHUD.RefreshBuffIcons(activeEffects, gameManager.localPlayerColor);
            }

            if (gameManager.gameHUD != null) gameManager.gameHUD.UpdateSPUI(mySP, oppSP);

            // 네트워크 담당자에게 전달할 패킷 (좌표 2개가 담긴 배열 전송)
            if (gameManager.currentMode == PlayMode.Multiplayer && gameSession != null)
            {
                gameSession.SendUseSkill(skillToUse.data.skillId, targetX, targetY);
            }

            selectedSkillSlot = -1;
            gameManager.currentState = GameState.Playing;
            gameManager.board.UpdateForbiddenMarks(gameManager.currentTurnColor);

        }
        else
        {
            selectedSkillSlot = -1;
            gameManager.currentState = GameState.Playing;
        }

        // 스킬이 성공하든 실패하든 마커는 지워야 함
        gameManager.board.HideSkillTargetMarkers();
    }

    public void AutoActivatePassiveSkills()
    {
        int[] noTarget = System.Array.Empty<int>();

        SkillBase passiveSkill = mySkills.Find(s => s.data.type == "전용");
        if (passiveSkill == null) return;

        if (passiveSkill.CanUse(mySP, false, gameManager.board, gameManager.localPlayerColor)
            != SkillUseResult.Success) return;

        passiveSkill.Execute(noTarget, noTarget, gameManager, gameManager.board);

        if (gameManager.currentMode == PlayMode.Multiplayer && gameSession != null)
            gameSession.SendUseSkill(passiveSkill.data.skillId, new int[] { -1 }, new int[] { -1 });
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
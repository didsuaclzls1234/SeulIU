using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 인게임 HUD.
///
/// 필요한 UI 오브젝트:
///   turnText       — 현재 차례 표시 텍스트
///   myColorText    — 내 색 표시 텍스트
///   resultPanel    — 결과 화면 패널 (기본 비활성)
///   resultText     — 승/패/무승부 메시지
///   rematchButton  — 다시하기 버튼
///   exitButton     — 나가기 버튼
/// </summary>
public class GameHUD : MonoBehaviour
{   
    [Header("인게임 UI 묶음")]
    public GameObject inGameUI;

    //[Header("인게임 표시")]
    //public TextMeshProUGUI turnText;
    //public TextMeshProUGUI myColorText;
    [Header("턴 표시 이미지")]
    public Image turnImage;
    [Header("턴 이미지 밝기")]
    [Range(0f, 1f)] public float dimAlpha = 0.3f;
    //[Header("인게임 버튼 (상단/하단)")]
    //public Button undoButton;
    //public Button restartButton; // 솔로 전용

    //[Header("무르기(Undo) 요청 팝업")]
    //public GameObject undoPopupPanel;
    //public TextMeshProUGUI undoRequestText;
    //public Button undoAcceptButton; 
    //public Button undoRefuseButton;

    [Header("코어 매니저 연결")]
    public GameManager gameManager;
    public TimerManager timerManager;
    public InputManager inputManager;

    [Header("플레이어 정보 UI (신규)")]
    public TextMeshProUGUI myNicknameText;   // 내 닉네임 
    public TextMeshProUGUI oppNicknameText;  // 상대 닉네임 

    [Header("스킬 & 타이머 UI")]
    public TextMeshProUGUI turnTimerText;       // 턴 시간 표시용
    //public Slider timerSlider;             // (선택) 시각적으로 줄어드는 바
    public TextMeshProUGUI mySPText;        // 내 SP 표시
    public TextMeshProUGUI oppSPText;        // 상대방 SP 표시용 (신규)
    public GameObject[] skillButtons;      // 스킬 버튼들 (비활성화/쿨타임 표시용)
    // public GameObject[] skillButtons; ... 등등

    [Header("결과 패널")]
    public GameObject resultPanel;
    public TextMeshProUGUI resultText;
    public Button rematchButton;
    public Button exitButton;

    [Header("스킬 선택창 UI (Pre-Game)")]
    public TextMeshProUGUI skillSelectTimerText; // 스킬창 전용 타이머
    public GameObject skillSelectPanel;      // 스킬 선택 팝업창 전체
    public Button[] skillSelectButtons;      // 선택 가능한 10개 버튼 (임시)
    public Button readyButton;               // 선택 완료 버튼
    public TextMeshProUGUI skillSelectRoleText;
    //public Image[] skillRowIcons;            // 각 행의 아이콘 Image 배열
    //public TextMeshProUGUI[] skillRowNameTexts;  // 각 행의 스킬명 TMP
    //public TextMeshProUGUI[] skillRowSPTexts;    // 각 행의 SP 비용 TMP
    //public TextMeshProUGUI[] skillRowDescTexts;  // 각 행의 설명 TMP
    public TextMeshProUGUI skillSelectMessageText; // 스킬 선택창 전용 메시지

    [Header("스킬 선택창 UI - 덱 슬롯")]
    public Button[] deckSlotButtons;         // DeckSlot_0, 1, 2
    public TextMeshProUGUI[] deckSlotNameTexts; // 각 슬롯의 스킬명 TMP(이름이 필요 없다면 삭제 가능)

    [Header("스킬 아이콘 (Resources 폴더 없을 경우 null 허용)")]
    // Resources/SkillIcons/skill_1.png ~ skill_10.png 로 관리
    // 아직 이미지가 없으면 인스펙터에서 비워둬도 무관 — 예외처리로 막음
    public Sprite[] skillIcons;              // 인덱스 0 = 스킬 1번, 9 = 스킬 10번

    [Header("인게임 스킬 UI(우측 하단 위치)")]
    public Button[] activeSkillButtons;      // 게임 중 누를 스킬 버튼 3개
    public Image[] skillCooldownImages;      // 쿨타임 표시용 Image (Fill Amount 조절용)
    public TextMeshProUGUI[] skillCostTexts;  // SP 소모량 표시 텍스트
                                              
    public TextMeshProUGUI[] skillNameTexts; // 스킬 이름과 쿨타임 텍스트 배열
    public TextMeshProUGUI[] cooldownTexts;

    public TextMeshProUGUI systemMessageText; // 화면에 띄울 현재 상태 텍스트 UI
    public GameObject opponentSilencedIcon;   // 상대방 스킬 덱 위에 띄울 자물쇠나 X표시 아이콘

    [Header("현재 적용된 스킬(Buff/Debuff) UI")]
    public Transform blackBuffContainer;     // 버프 아이콘들이 배치될 부모(Layout Group)
    public Transform whiteBuffContainer;          
    public GameObject buffIconPrefab;        // 아이콘 하나하나의 프리팹

    [Header("툴팁")]
    public SkillManager skillManager;

    [Header("재도전(Rematch) 팝업")]
    public GameObject rematchPopupPanel;       // 요청자: 대기 / 수신자: 수락-거절 패널 (공용)
    public TextMeshProUGUI rematchPopupText;   // 패널 안 메시지
    public Button rematchAcceptButton;         // 수락 버튼 (수신자에게만 표시)
    public Button rematchDeclineButton;        // 거절 버튼 (수신자에게만 표시)

    // [추가] GameSession 참조 (Rematch 버튼 콜백용)
    public GameSession gameSession;

    [Header("스킬 로그")]
    public TextMeshProUGUI skillLogText;      // Content 오브젝트의 TMP
    public ScrollRect skillLogScrollRect;
    private List<string> _logEntries = new List<string>();

    // 필드 추가
    [Header("상대방 스킬 덱 표시")]
    public Image[] oppDeckSlotImages;

    [Header("사운드 설정")]
    public int victorySFXCount = 1;  // 인스펙터에서 조절
    public int defeatSFXCount = 1;   // 인스펙터에서 조절

    [Header("플레이어 패널 이미지")]
    public Image myPanelImage;          // 내 패널 Image 컴포넌트
    public Image oppPanelImage;         // 상대 패널 Image 컴포넌트

    [Header("패널 스프라이트")]
    public Sprite blackPlayerSprite;    // 흑돌용 이미지
    public Sprite whitePlayerSprite;    // 백돌용 이미지

    // 스킬 이펙트 스프라이트 매핑용 클래스
    [System.Serializable]
    public class SkillEffectSprite
    {
        public int skillId;
        public Sprite sprite;
    }

    [Header("스킬 사용 연출")]
    public Image skillEffectImage;
    [Range(0f, 3f)] public float skillEffectDuration = 1.5f;
    public SkillEffectSprite[] skillEffectSprites; // 인스펙터에서 11개 등록
    private Coroutine _skillEffectCoroutine;

    private void Start()
    {
        // 시작할 때 패널들 닫아두기
        if (resultPanel) resultPanel.SetActive(false);
        //if (undoPopupPanel) undoPopupPanel.SetActive(false);
        if (rematchPopupPanel) rematchPopupPanel.SetActive(false);

        // 리매치 / 나가기 버튼은 여기서 코드로 연결
        if (rematchButton) rematchButton.onClick.AddListener(OnClickRematch);
        if (exitButton) exitButton.onClick.AddListener(OnClickExit);

        // [추가] 준비완료 버튼 초기 비활성화
        if (readyButton)
        {
            readyButton.interactable = false;
            readyButton.onClick.RemoveAllListeners();
            readyButton.onClick.AddListener(OnClickReadyButton);
        }
        // [추가] 덱 슬롯 버튼 초기 비활성화
        if (deckSlotButtons != null)
        foreach (var btn in deckSlotButtons)
            btn.gameObject.SetActive(false);
    }

    // 준비 완료 버튼을 눌렀을 때의 동작
    private void OnClickReadyButton()
    {
        // 1. 멀티플레이어 & GameSession이 존재할 때 -> 기존처럼 패킷 전송
        if (gameManager.currentMode == PlayMode.Multiplayer && gameSession != null)
        {
            gameSession.SendSyncPlayerInfo();
        }
        // 2. AI 또는 솔로 모드일 때 -> 포톤 없이 바로 로컬에서 준비 완료 처리
        else
        {
            // 스킬 3개를 다 채웠는지 한 번 더 검증
            for (int i = 0; i < skillManager.mySkillsID.Length; i++)
            {
                if (skillManager.mySkillsID[i] == -1)
                {
                    ShowSkillSelectMessage("스킬을 3개 모두 선택해 주세요.");
                    return;
                }
            }

            // 내 스킬 덱 생성 및 준비 완료 선언
            skillManager.InitializeSkillDeck(true, skillManager.mySkillsID);
            skillManager.SetPlayerReady(true);
        }
    }

    // ── GameSession에서 호출 ─────────────────────────────────────

    // 닉네임 세팅 함수 (상화님이 방 입장 완료 시 1번 호출할 예정)
    public void SetPlayerNames(string myName, string oppName)
    {
        if (myNicknameText) myNicknameText.text = myName;
        if (oppNicknameText) oppNicknameText.text = oppName;
    }

    // SP 갱신 함수 (SkillManager가 매 턴 호출)
    public void UpdateSPUI(int currentMySP, int currentOppSP)
    {
        if (mySPText) mySPText.text = $"SP: {currentMySP}";
        if (oppSPText) oppSPText.text = $"상대 SP: {currentOppSP}";
    }

    // 멀티플레이 모드일 때 GameSession에서 호출하여 불필요한 버튼 숨기기
    //public void SetupForMultiplayer()
    //{
    //    if (restartButton) restartButton.gameObject.SetActive(false); // 멀티에선 중간 재시작 금지
    //}

    // public void ShowRoleAssigned(StoneColor myColor)
    // {
    //     // if (myColorText)
    //     //     myColorText.text = $"나: {myColor.ToKorean()}";
    // }

    public void UpdateTurnDisplay(StoneColor currentTurn, bool isMyTurn)
    {
        // if (turnText == null) return;
        if (turnImage == null) return;

        // 1. 멀티플레이 모드일 때는 '나' 위주로 표시
        if (gameManager.currentMode == PlayMode.Multiplayer)
        {
            string whose = isMyTurn ? "내 차례" : "상대 차례";
            // turnText.text = $"{currentTurn.ToKorean()} — {whose}";
            // 내 차례: 밝게 / 상대 차례: 어둡게
            turnImage.color = isMyTurn
                ? Color.white
                : new Color(1f, 1f, 1f, dimAlpha);
        }
        // 2. 솔로/AI 모드일 때는 '돌의 색상' 위주로 표시
        else
        {
            string modeSuffix = (gameManager.currentMode == PlayMode.AI && !isMyTurn) ? " (AI)" : "";
            //turnText.text = $"{currentTurn.ToKorean()}돌 차례{modeSuffix}";
            // 내 차례: 밝게 / 상대 차례: 어둡게
            turnImage.color = isMyTurn
                ? Color.white
                : new Color(1f, 1f, 1f, dimAlpha);
        }
    }

    // 타이머 업데이트 함수
    public void UpdateTimerUI(float remainingTime)
    {
        string timeStr = Mathf.CeilToInt(remainingTime).ToString();

        // 1. 스킬 선택 중일 때는 팝업창 타이머에 표시
        if (gameManager.currentState == GameState.WaitingForSkillSelect)
        {
            if (skillSelectTimerText) skillSelectTimerText.text = timeStr;
        }
        // 2. Playing, SkillTargeting 상태일 때 타이머 글씨 업데이트
        else if (gameManager.currentState == GameState.Playing || /*gameManager.currentState == GameState.SkillTargeting||*/ gameManager.currentState == GameState.SkillPreview)
        {
            if (turnTimerText) turnTimerText.text = timeStr;
        }
    }

    // SP 및 스킬 버튼 상태 갱신 (SkillManager에서 호출)
    public void UpdateSkillUI(int currentSP, bool[] isSkillReady)
    {
        if (mySPText) mySPText.text = $"SP: {currentSP}";
        // 스킬 버튼들의 활성화 여부 제어 로직 등
    }

    public void ShowGameOver(StoneColor winner, StoneColor myColor)
    {
        if (resultPanel) resultPanel.SetActive(true);
        if (resultText == null) return;

        // 무승부 공통
        if (winner == StoneColor.None)
        {
            resultText.text = "무승부!";
            return;
        }

        // 1. 멀티플레이: 승리/패배로 표시
        if (gameManager.currentMode == PlayMode.Multiplayer)
        {
            resultText.text = (winner == myColor) ? "승리!" : "패배...";
        }
        // 2. AI 모드: 플레이어/AI 승리로 표시
        else if (gameManager.currentMode == PlayMode.AI)
        {
            resultText.text = (winner == myColor) ? "플레이어 승리!" : "AI 승리!";
        }
        // 3. 솔로 모드: 흑/백 승리로 표시
        else
        {
            resultText.text = $"{winner.ToKorean()}돌 승리!";
        }

        //SoundManager.Instance.StopBGM(); // ↓ 추가

        // BattleBGM과 SFX 동시 재생
        SoundManager.Instance.PlayBGM("BattleBGM");
         bool isWin = (winner == myColor);
        if (isWin)
            SoundManager.Instance.PlaySFXRepeat("VictorySFX", victorySFXCount);
        else
            SoundManager.Instance.PlaySFXRepeat("DefeatSFX", defeatSFXCount);
    }

    public void ShowOpponentLeft()
    {
        if (resultPanel) resultPanel.SetActive(true);
        if (resultText) resultText.text = "상대방이 나갔습니다.";
        if (rematchButton) rematchButton.interactable = false; // 나갔는데 리매치는 불가
    }

    //// (1) 상대방이 Undo 요청했을 때 (버튼 ON)
    //public void ShowUndoPopup()
    //{   
    //    inputManager?.HideHover();
    //    inputManager?.BlockInput();
    //    undoPopupPanel.SetActive(true);
    //    undoRequestText.text = $"상대방이 무르기를 요청했습니다.";
    //    if (undoAcceptButton) undoAcceptButton.gameObject.SetActive(true);
    //    if (undoRefuseButton) undoRefuseButton.gameObject.SetActive(true);

    //    // 요청을 받는 즉시 타이머 일시정지
    //    if (timerManager != null) timerManager.PauseTimer();
    //}

    //// (2) 내가 Undo 요청하고 기다릴 때 (버튼 OFF)
    //public void ShowUndoWaitingPopup()
    //{
    //    inputManager?.HideHover();
    //    inputManager?.BlockInput();
    //    undoPopupPanel.SetActive(true);
    //    undoRequestText.text = "상대방의 응답을 기다리는 중...";
    //    if (undoAcceptButton) undoAcceptButton.gameObject.SetActive(false);
    //    if (undoRefuseButton) undoRefuseButton.gameObject.SetActive(false);

    //    // [네트워크 동기화] 내가 요청을 보낸 순간 내 타이머도 멈춤!
    //    if (timerManager != null) timerManager.PauseTimer();
    //}

    //// (3) 상대방이 응답했을 때 결과를 잠깐 보여주고 닫기 (버튼 OFF 유지)
    //public async void ShowUndoResultAndClose(bool isAccepted)
    //{
    //    if (!undoPopupPanel.activeSelf) undoPopupPanel.SetActive(true);

    //    if (undoAcceptButton) undoAcceptButton.gameObject.SetActive(false);
    //    if (undoRefuseButton) undoRefuseButton.gameObject.SetActive(false);

    //    undoRequestText.text = isAccepted ? "상대방이 무르기를 수락했습니다!" : "상대방이 무르기를 거절했습니다.";

    //    // 수락/거절 결과에 따라 타이머 처리
    //    if (timerManager != null)
    //    {
    //        if (isAccepted) timerManager.RestartTurnTimer(); // 수락 시 턴 초기화
    //        else timerManager.ResumeTimer(); // 거절 시 남은 시간부터 재개
    //    }

    //    // 1.5초 동안 결과 텍스트 보여주고 팝업 닫기
    //    await Task.Delay(1500);

    //    if (undoPopupPanel != null) undoPopupPanel.SetActive(false);
    //    inputManager?.UnblockInput();
    //}

    //public void HideUndoRequestPopup()
    //{
    //    inputManager?.UnblockInput();
    //    undoPopupPanel.SetActive(false);
    //}

    //// ── 팝업 버튼 콜백 (인스펙터 연결 전용) ──────────────────

    //// 1. [수락] 버튼
    //public void OnAcceptUndoClicked()
    //{   
    //    inputManager?.UnblockInput();
    //    if (undoPopupPanel) undoPopupPanel.SetActive(false);

    //    // 내가 수락했으니 턴이 뒤로 감 -> 타이머도 처음부터(30초) 다시 시작
    //    if (timerManager != null) timerManager.RestartTurnTimer();

    //    gameManager.ReplyToUndoRequest(true); // GameManager에게 'true(수락)' 토스
    //}

    //// 2. [거절] 버튼
    //public void OnRefuseUndoClicked()
    //{
    //    inputManager?.UnblockInput();
    //    if (undoPopupPanel) undoPopupPanel.SetActive(false);

    //    // 내가 거절했으니 턴은 그대로 유지 -> 멈췄던 타이머를 다시 흐르게 함
    //    if (timerManager != null) timerManager.ResumeTimer();

    //    gameManager.ReplyToUndoRequest(false); // GameManager에게 'false(거절)' 토스
    //}

    // ── 결과 패널 버튼 콜백 ────────────────────────────────────────────────
    public void OnClickRematch()
    {
        // 1. 멀티플레이 모드일 때
        if (gameManager != null && gameManager.currentMode == PlayMode.Multiplayer)
        {
            // // 방을 나가면 GameSession.OnLeftRoom이 발동해서 로비 씬으로 자동 이동함
            // if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
            // [변경] 이제 바로 끊지 않고 GameSession에 재도전 요청 위임
            if (gameSession != null)
                gameSession.RequestRematch();
        }
        // 2. AI 모드일 때 (새로 추가)
        else if (gameManager != null && gameManager.currentMode == PlayMode.AI)
        {
            // 게임 매니저의 리셋 함수 호출 (보드 클리어 등)
            gameManager.ResetForRematch();

            // 스킬 선택 패널 다시 띄우기
            ShowSkillSelectPanel();

            // 스킬 선택 타이머 다시 시작
            if (timerManager != null) timerManager.StartSkillSelectTimer();

            Debug.Log("[AI Rematch] AI 대전 다시 시작: 보드 초기화 및 스킬 선택 단계 진입");
        }
        // 2. Solo 모드일 때
        else
        {
            // 결과창을 닫고, GameManager의 재시작 코어 로직을 실행
            if (resultPanel) resultPanel.SetActive(false);
            if (gameManager != null) gameManager.RequestRestart();
        }
    }

    public void OnClickExit()
    {
        // 1. 멀티플레이 모드일 때
        if (gameManager != null && gameManager.currentMode == PlayMode.Multiplayer)
        {
            if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
        }
        // 2. Solo / AI 모드일 때
        else
        {
            // 포톤 연결을 끊을 필요 없이 바로 로비(또는 메인메뉴) 씬으로 넘김
            SceneManager.LoadScene("Lobby");
        }
    }

    // 시스템 로그 띄우는 텍스트
    // public void ShowSystemMessage(string message)
    // {
    //     if (systemMessageText == null) return;

    //     systemMessageText.text = message;
    //     systemMessageText.gameObject.SetActive(true);

    //     StopAllCoroutines(); // 기존에 떠있던 메시지가 있다면 취소하고 새로 시작
    //     StartCoroutine(FadeOutMessage());
    // }

    // private IEnumerator FadeOutMessage()
    // {
    //     // 2초 대기 후 서서히 사라짐
    //     yield return new WaitForSeconds(2.0f);
    //     systemMessageText.gameObject.SetActive(false);
    // }
    public void ShowSystemMessage(string message)
    {
        if (systemMessageText == null) return;
        systemMessageText.text = message;
        systemMessageText.gameObject.SetActive(true);
    }

    public void HideSystemMessage()
    {
        if (systemMessageText == null) return;
        systemMessageText.gameObject.SetActive(false);
    }

    // 상대방 스킬버튼 위에 띄울 이미지('안티매직' 스킬 적용된 경우 스킬 사용 불가 특수효과)
    public void SetOpponentSilencedUI(bool isSilenced)
    {
        if (opponentSilencedIcon != null)
        {
            opponentSilencedIcon.SetActive(isSilenced);
        }
    }

    // ------------------------------------------------------

    // 1. 내가 흑인지 백인지 알려주는 UI 업데이트
    public void DisplayMyRole(StoneColor myColor)
    {
        if (skillSelectRoleText) skillSelectRoleText.text = $"당신은 {myColor.ToKorean()}입니다.\n스킬을 3개 고르세요.";
        // 색상 전용 스킬 버튼 제한 (버튼 인덱스 = 스킬 ID - 1)
        if (skillSelectButtons == null) return;
        for (int i = 0; i < skillSelectButtons.Length; i++)
        {
            int skillId = i + 1;

            bool isRestricted = (skillId == 10 && myColor != StoneColor.Black)
                            || (skillId == 11 && myColor != StoneColor.White);

            skillSelectButtons[i].interactable = !isRestricted;
            if (isRestricted)
            {
                Transform iconTr =skillSelectButtons[i].transform.Find("IconImage");
                if (iconTr != null)
                {
                    Image iconImg1 = iconTr.GetComponent<Image>();
                    if (iconImg1 != null)
                    {
                        iconImg1.color = Color.gray;
                    }
                }
            }
            // CSV 데이터 가져오기
            SkillData data = default(SkillData);
            if (skillManager != null && !skillManager.skillDatabase.TryGetValue(skillId, out data)) continue;
            // {
            //     SkillTooltipTrigger trigger = skillSelectButtons[i].GetComponent<SkillTooltipTrigger>();
            //     if (trigger == null) trigger = skillSelectButtons[i].gameObject.AddComponent<SkillTooltipTrigger>();
            //     trigger.SetData(data);
            // }

            // CSV 데이터 스킬 리스트에 뿌려주기
            Transform btnTransform = skillSelectButtons[i].transform;

            // 아이콘
            Image iconImg = btnTransform.Find("IconImage")?.GetComponent<Image>();
            if (iconImg != null)
            {
                Sprite icon = GetSkillIcon(skillId);
                iconImg.sprite = icon;
                iconImg.gameObject.SetActive(icon != null);
            }

            // 스킬명
            TextMeshProUGUI nameText = btnTransform.Find("SkillNameText")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null) nameText.text = data.skillName;

            // SP
            TextMeshProUGUI spText = btnTransform.Find("SPText")?.GetComponent<TextMeshProUGUI>();
            if (spText != null) spText.text = data.type == "전용" ? "패시브" : $"{data.spCost} SP";

            // 설명
            TextMeshProUGUI descText = btnTransform.Find("DescText")?.GetComponent<TextMeshProUGUI>();
            if (descText != null) descText.text = data.description;
            // 3. 툴팁 연결 제거 → 표 데이터 채우기로 교체
            //// 아이콘
            //if (skillRowIcons != null && i < skillRowIcons.Length && skillRowIcons[i] != null)
            //{
            //    Sprite icon = GetSkillIcon(skillId);
            //    skillRowIcons[i].sprite = icon;
            //    skillRowIcons[i].gameObject.SetActive(icon != null);
            //}

            //// 스킬명
            //if (skillRowNameTexts != null && i < skillRowNameTexts.Length && skillRowNameTexts[i] != null)
            //    skillRowNameTexts[i].text = data.skillName;

            //// SP
            //if (skillRowSPTexts != null && i < skillRowSPTexts.Length && skillRowSPTexts[i] != null)
            //    skillRowSPTexts[i].text = data.type == "전용" ? "패시브" : $"{data.spCost} SP";

            //// 설명
            //if (skillRowDescTexts != null && i < skillRowDescTexts.Length && skillRowDescTexts[i] != null)
            //    skillRowDescTexts[i].text = data.description;
            bool isBlack = (myColor == StoneColor.Black);

            if (myPanelImage != null)
                myPanelImage.sprite = isBlack ? blackPlayerSprite : whitePlayerSprite;

            if (oppPanelImage != null)
                oppPanelImage.sprite = isBlack ? whitePlayerSprite : blackPlayerSprite;
        }
    }
    // 2. 버프/디버프 상태 전체 갱신 
    public void RefreshBuffIcons(List<ActiveEffect> effects, StoneColor myColor)
    {
        // 기존 아이콘 전부 제거
        foreach (Transform child in blackBuffContainer) Destroy(child.gameObject);
        foreach (Transform child in whiteBuffContainer) Destroy(child.gameObject);

        foreach (ActiveEffect effect in effects)
        {
            // isBuff면 내 컨테이너, 아니면 상대 컨테이너
            Transform targetContainer = effect.isBuff ?
                (myColor == StoneColor.Black ? blackBuffContainer : whiteBuffContainer) :
                (myColor == StoneColor.Black ? whiteBuffContainer : blackBuffContainer);

            GameObject icon = Instantiate(buffIconPrefab, targetContainer);

            TextMeshProUGUI nameText = icon.transform.Find("SkillNameText").GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI turnsText = icon.transform.Find("RemainingTurnsText").GetComponent<TextMeshProUGUI>();

            if (nameText) nameText.text = effect.effectName;
            if (turnsText) turnsText.text = $"남은 턴: {effect.remainingTurns}";

            SkillTooltipTrigger trigger = icon.GetComponent<SkillTooltipTrigger>();
            if (trigger == null) trigger = icon.AddComponent<SkillTooltipTrigger>();
            trigger.SetEffect(effect);
        }
    }

    // -------------------------------------------------------
    // AI가 생각 중일 때 버튼들을 클릭 못 하게(회색으로) 막는 함수
    //public void SetInteractableButtons(bool isInteractable)
    //{
    //    if (restartButton != null) restartButton.interactable = isInteractable;
    //    if (undoButton != null) undoButton.interactable = isInteractable;
    //}

    // ── Rematch 팝업 ─────────────────────────────────────────

    /// <summary>재도전 요청을 보낸 쪽: "대기 중" 패널 표시</summary>
    public void ShowRematchWaitingPanel()
    {
        if (rematchPopupPanel == null) return;
        rematchPopupPanel.SetActive(true);
        if (rematchPopupText)    rematchPopupText.text = "상대방의 재도전 수락을 기다리는 중...";
        if (rematchAcceptButton)  rematchAcceptButton.gameObject.SetActive(false);
        if (rematchDeclineButton) rematchDeclineButton.gameObject.SetActive(false);
    }

    /// <summary>재도전 요청을 받은 쪽: "수락/거절" 패널 표시</summary>
    public void ShowRematchReceivedPanel()
    {
        if (rematchPopupPanel == null) return;
        rematchPopupPanel.SetActive(true);
        if (rematchPopupText)    rematchPopupText.text = "상대방이 재도전을 요청했습니다!";
        if (rematchAcceptButton)  rematchAcceptButton.gameObject.SetActive(true);
        if (rematchDeclineButton) rematchDeclineButton.gameObject.SetActive(true);
    }

    /// <summary>패널 닫기</summary>
    public void HideRematchPanel()
    {
        if (rematchPopupPanel != null) rematchPopupPanel.SetActive(false);
    }

    /// <summary>재도전 거절당한 쪽에 메시지 표시</summary>
    public void ShowRematchDeclinedMessage()
    {
        HideRematchPanel();
        // resultPanel은 그대로 유지하고, 시스템 메시지만 띄움
        // resultPanel은 이미 열려있으므로 텍스트만 교체
        if (resultText) resultText.text = "상대방이 도망쳤습니다!!";
        // 거절 후엔 리매치 버튼 클릭 막기 (더 이상 요청 못 하게)
        if (rematchButton) rematchButton.interactable = false;
    }
    // ── 스킬 로그 ─────────────────────────────────────────

    public void AddSkillLog(string userName, string skillName, int turnCount)
    {
        if (skillLogText == null) return;

        skillLogText.richText = true;

        string safeUserName = SanitizeRichText(userName);
        string safeSkillName = SanitizeRichText(skillName);

        string logEntry =
            $"<pos=5><color=#FFA500>[{turnCount}턴]</color>" +
            $"<pos=90>{safeUserName}" +
            $"<pos=180>{safeSkillName}";

        _logEntries.Add(logEntry);
        skillLogText.text = string.Join("\n", _logEntries);

        StartCoroutine(ScrollToBottom());
    }

    private string SanitizeRichText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text
            .Replace("<", "＜")
            .Replace(">", "＞");
    }
    private IEnumerator ScrollToBottom()
    {
        yield return new WaitForEndOfFrame();
        skillLogScrollRect.verticalNormalizedPosition = 0f;
    }

     // ── 덱 슬롯 UI 갱신 ─────────────────────────────────────────────
    // 스킬 추가/제거/정렬될 때마다 호출
    public void RefreshDeckSlots(int[] mySkillsID, Dictionary<int, SkillData> skillDatabase)
    {
        if (deckSlotButtons == null) return;

        for (int i = 0; i < deckSlotButtons.Length; i++)
        {
            int skillId = (i < mySkillsID.Length) ? mySkillsID[i] : -1;
            bool hasSkill = skillId != -1;

            // 스킬 없으면 슬롯 숨김
            deckSlotButtons[i].gameObject.SetActive(hasSkill);
            if (!hasSkill) continue;

            Sprite icon = GetSkillIcon(skillId);

            // 1. 자식 오브젝트 중에 "IconImage"가 있는지 먼저 확인 (스킬 목록 버튼과 같은 구조일 경우)
            Transform iconTransform = deckSlotButtons[i].transform.Find("IconImage");
            if (iconTransform != null)
            {
                Image iconImg = iconTransform.GetComponent<Image>();
                if (iconImg != null && icon != null)
                {
                    iconImg.sprite = icon;
                    iconImg.gameObject.SetActive(true); // 꺼져있을까봐 확실하게 켜줌
                }
            }
            else
            {
                // 2. 자식이 없다면? 슬롯 버튼 자신의 배경 Image를 바로 교체
                Image btnImage = deckSlotButtons[i].GetComponent<Image>();
                if (btnImage != null && icon != null)
                {
                    btnImage.sprite = icon;
                }
            }

            // 스킬명 갱신
            if (deckSlotNameTexts != null && i < deckSlotNameTexts.Length && deckSlotNameTexts[i] != null)
            {
                if (hasSkill && skillDatabase.TryGetValue(skillId, out SkillData data))
                    deckSlotNameTexts[i].text = data.skillName;
                else
                    deckSlotNameTexts[i].text = "비어있음";
            }
        }
    }
    // 메서드 추가-상대 덱 슬롯 갱신 (내 덱과 거의 동일한 로직, 단 상대는 이름 텍스트 없이 아이콘만 표시)
    public void RefreshOppDeckSlots(int[] oppSkillsID, Dictionary<int, SkillData> skillDatabase)
    {
        for (int i = 0; i < oppDeckSlotImages.Length; i++)
        {
            int skillId = (i < oppSkillsID.Length) ? oppSkillsID[i] : -1;
            bool hasSkill = skillId != -1;

            oppDeckSlotImages[i].gameObject.SetActive(hasSkill);
            if (!hasSkill) continue;

            // 기존 skillIcons 배열에서 스프라이트 가져와서 적용
            Sprite icon = GetSkillIcon(skillId);
            if (icon != null) oppDeckSlotImages[i].sprite = icon;

            // 호버 툴팁 연결
            if (skillDatabase.TryGetValue(skillId, out SkillData data))
            {
                SkillTooltipTrigger trigger = oppDeckSlotImages[i].GetComponent<SkillTooltipTrigger>();
                if (trigger == null)
                    trigger = oppDeckSlotImages[i].gameObject.AddComponent<SkillTooltipTrigger>();
                trigger.SetData(data);
            }
        }
    }
    
    // ── 확정 버튼 활성화 제어 ───────────────────────────────────────
    public void SetReadyButtonInteractable(bool interactable)
    {
        if (readyButton != null)
            readyButton.interactable = interactable;
    }
 
    // ── 아이콘 안전 로드 (이미지 없어도 null 반환으로 예외 처리) ────
    private Sprite GetSkillIcon(int skillId)
    {
        // 1순위: 인스펙터에서 직접 연결한 배열
        int index = skillId - 1;
        if (skillIcons != null && index >= 0 && index < skillIcons.Length && skillIcons[index] != null)
            return skillIcons[index];
 
        // 2순위: Resources 폴더에서 자동 로드
        Sprite loaded = Resources.Load<Sprite>($"SkillIcons/skill_{skillId}");
        if (loaded != null) return loaded;
 
        // 아이콘 없음 — null 반환 (호출부에서 비활성화 처리)
        Debug.LogWarning($"[GameHUD] 스킬 {skillId}번 아이콘 없음. SkillIcons/skill_{skillId} 확인 필요.");
        return null;
    }

     // ── 인게임 스킬 버튼에 아이콘 적용 ─────────────────────────────
    // GenerateSkillInstances() 완료 후 SkillManager가 호출
    public void ApplySkillIconToActiveButton(int slotIndex, int skillId)
    {
        if (activeSkillButtons == null || slotIndex >= activeSkillButtons.Length) return;

        // 1. 버튼 배경 Image와 자식 아이콘 Image를 분리해서 찾습니다.
        Image bgImage = activeSkillButtons[slotIndex].GetComponent<Image>();
        Transform iconTransform = activeSkillButtons[slotIndex].transform.Find("IconImage");
        Image iconImg = iconTransform != null ? iconTransform.GetComponent<Image>() : bgImage;

        if (iconImg == null) return;

        Sprite icon = GetSkillIcon(skillId);

        if (icon != null)
        {
            iconImg.sprite = icon;
            iconImg.color = Color.white; // 색상 정상화
            if (iconTransform != null) iconImg.gameObject.SetActive(true); // 자식 오브젝트면 켜줌
        }
        else
        {
            // 아이콘이 없을 때의 처리
            if (iconTransform != null)
            {
                // 자식 오브젝트(아이콘 전용)가 따로 있다면, 아이콘 이미지 오브젝트만 꺼버림 
                // -> 버튼의 원래 배경(기본 버튼 이미지)은 아주 예쁘게 그대로 남습니다!
                iconImg.sprite = null;
                iconImg.gameObject.SetActive(false);
            }
            else
            {
                // 버튼 배경 자체를 교체하는 구조라면, 이미지만 지우고 색은 하얗게(투명도 100%) 유지
                // -> 유니티 기본 하얀색 네모 버튼으로 돌아갑니다.
                iconImg.sprite = null;
                iconImg.color = Color.white;
            }

            Debug.LogWarning($"[GameHUD] {skillId}번 스킬 아이콘이 없어 인게임 슬롯 {slotIndex}번을 기본 버튼 상태로 둡니다.");
        }
    }
    //스킬 선택창용 메시지.
    public void ShowSkillSelectMessage(string message)
    {
         if (skillSelectMessageText == null) return;
        skillSelectMessageText.text = message;
        skillSelectMessageText.gameObject.SetActive(true);
        StopCoroutine("HideSkillSelectMessageRoutine");
        StartCoroutine("HideSkillSelectMessageRoutine");
    }

    private IEnumerator HideSkillSelectMessageRoutine()
    {
        yield return new WaitForSeconds(2f);
        if (skillSelectMessageText != null)
            skillSelectMessageText.gameObject.SetActive(false);
    }
    // 스킬 선택 패널 표시/숨김 함수(인게임 요소들과 상호 배타적으로 작동)
    public void ShowSkillSelectPanel()
    {
        skillSelectPanel?.SetActive(true);
        inGameUI?.SetActive(false);
        //SoundManager.Instance.PlayBGM("BattleBGM");
    }

    public void HideSkillSelectPanel()
    {
        skillSelectPanel?.SetActive(false);
        inGameUI?.SetActive(true);
    }
    //스킬로그 초기화 함수
    public void ResetSkillLog()
    {
        _logEntries.Clear();
        if (skillLogText != null) skillLogText.text = "";
    }

    // 스킬 사용 시 이펙트 보여주는 함수
    public void ShowSkillEffect(int skillId)
    {
        Sprite sprite = GetSkillEffectSprite(skillId);
        if (sprite == null || skillEffectImage == null) return;

        if (_skillEffectCoroutine != null)
            StopCoroutine(_skillEffectCoroutine);

        _skillEffectCoroutine = StartCoroutine(SkillEffectRoutine(sprite));
    }
    // 스킬 ID에 맞는 이펙트 스프라이트를 안전하게 가져오는 함수
    private Sprite GetSkillEffectSprite(int skillId)
    {
        foreach (SkillEffectSprite s in skillEffectSprites)
        {
            if (s.skillId == skillId) return s.sprite;
        }
        Debug.LogWarning($"[HUD] 스킬 {skillId}번 이펙트 스프라이트 없음");
        return null;
    }
    // 스킬 이펙트 보여주는 코루틴: 지정된 시간 동안 스킬 이펙트 이미지 활성화 후 자동으로 숨김
    private IEnumerator SkillEffectRoutine(Sprite sprite)
    {
        skillEffectImage.sprite = sprite;
        skillEffectImage.gameObject.SetActive(true);

        yield return new WaitForSeconds(skillEffectDuration);

        skillEffectImage.gameObject.SetActive(false);
    }
}
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
    [Header("인게임 표시")]
    public TextMeshProUGUI turnText;
    public TextMeshProUGUI myColorText;

    [Header("인게임 버튼 (상단/하단)")]
    public Button undoButton;
    public Button restartButton; // 솔로 전용

    [Header("무르기(Undo) 요청 팝업")]
    public GameObject undoPopupPanel;
    public TextMeshProUGUI undoRequestText;
    public Button undoAcceptButton; 
    public Button undoRefuseButton;

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

    private void Start()
    {
        // 시작할 때 패널들 닫아두기
        if (resultPanel) resultPanel.SetActive(false);
        if (undoPopupPanel) undoPopupPanel.SetActive(false);

        // 리매치 / 나가기 버튼은 여기서 코드로 연결
        if (rematchButton) rematchButton.onClick.AddListener(OnClickRematch);
        if (exitButton) exitButton.onClick.AddListener(OnClickExit);
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

    public void ShowRoleAssigned(StoneColor myColor)
    {
        if (myColorText)
            myColorText.text = $"나: {myColor.ToKorean()}";
    }

    public void UpdateTurnDisplay(StoneColor currentTurn, bool isMyTurn)
    {
        if (turnText == null) return;

        // 1. 멀티플레이 모드일 때는 '나' 위주로 표시
        if (gameManager.currentMode == PlayMode.Multiplayer)
        {
            string whose = isMyTurn ? "내 차례" : "상대 차례";
            turnText.text = $"{currentTurn.ToKorean()} — {whose}";
        }
        // 2. 솔로/AI 모드일 때는 '돌의 색상' 위주로 표시
        else
        {
            string modeSuffix = (gameManager.currentMode == PlayMode.AI && !isMyTurn) ? " (AI)" : "";
            turnText.text = $"{currentTurn.ToKorean()}돌 차례{modeSuffix}";
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
        // 2. 인게임 중일 때는 상단 타이머에 표시
        else if (gameManager.currentState == GameState.Playing)
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
    }

    public void ShowOpponentLeft()
    {
        if (resultPanel) resultPanel.SetActive(true);
        if (resultText) resultText.text = "상대방이 나갔습니다.";
        if (rematchButton) rematchButton.gameObject.SetActive(false); // 나갔는데 리매치는 불가
    }

    // (1) 상대방이 Undo 요청했을 때 (버튼 ON)
    public void ShowUndoPopup()
    {   
        inputManager?.HideHover();
        inputManager?.BlockInput();
        undoPopupPanel.SetActive(true);
        undoRequestText.text = $"상대방이 무르기를 요청했습니다.";
        if (undoAcceptButton) undoAcceptButton.gameObject.SetActive(true);
        if (undoRefuseButton) undoRefuseButton.gameObject.SetActive(true);

        // 요청을 받는 즉시 타이머 일시정지
        if (timerManager != null) timerManager.PauseTimer();
    }

    // (2) 내가 Undo 요청하고 기다릴 때 (버튼 OFF)
    public void ShowUndoWaitingPopup()
    {
        inputManager?.HideHover();
        inputManager?.BlockInput();
        undoPopupPanel.SetActive(true);
        undoRequestText.text = "상대방의 응답을 기다리는 중...";
        if (undoAcceptButton) undoAcceptButton.gameObject.SetActive(false);
        if (undoRefuseButton) undoRefuseButton.gameObject.SetActive(false);

        // [네트워크 동기화] 내가 요청을 보낸 순간 내 타이머도 멈춤!
        if (timerManager != null) timerManager.PauseTimer();
    }

    // (3) 상대방이 응답했을 때 결과를 잠깐 보여주고 닫기 (버튼 OFF 유지)
    public async void ShowUndoResultAndClose(bool isAccepted)
    {
        if (!undoPopupPanel.activeSelf) undoPopupPanel.SetActive(true);

        if (undoAcceptButton) undoAcceptButton.gameObject.SetActive(false);
        if (undoRefuseButton) undoRefuseButton.gameObject.SetActive(false);

        undoRequestText.text = isAccepted ? "상대방이 무르기를 수락했습니다!" : "상대방이 무르기를 거절했습니다.";

        // 수락/거절 결과에 따라 타이머 처리
        if (timerManager != null)
        {
            if (isAccepted) timerManager.RestartTurnTimer(); // 수락 시 턴 초기화
            else timerManager.ResumeTimer(); // 거절 시 남은 시간부터 재개
        }

        // 1.5초 동안 결과 텍스트 보여주고 팝업 닫기
        await Task.Delay(1500);

        if (undoPopupPanel != null) undoPopupPanel.SetActive(false);
        inputManager?.UnblockInput();
    }

    public void HideUndoRequestPopup()
    {
        inputManager?.UnblockInput();
        undoPopupPanel.SetActive(false);
    }

    // ── 팝업 버튼 콜백 (인스펙터 연결 전용) ──────────────────

    // 1. [수락] 버튼
    public void OnAcceptUndoClicked()
    {   
        inputManager?.UnblockInput();
        if (undoPopupPanel) undoPopupPanel.SetActive(false);

        // 내가 수락했으니 턴이 뒤로 감 -> 타이머도 처음부터(30초) 다시 시작
        if (timerManager != null) timerManager.RestartTurnTimer();

        gameManager.ReplyToUndoRequest(true); // GameManager에게 'true(수락)' 토스
    }

    // 2. [거절] 버튼
    public void OnRefuseUndoClicked()
    {
        inputManager?.UnblockInput();
        if (undoPopupPanel) undoPopupPanel.SetActive(false);

        // 내가 거절했으니 턴은 그대로 유지 -> 멈췄던 타이머를 다시 흐르게 함
        if (timerManager != null) timerManager.ResumeTimer();

        gameManager.ReplyToUndoRequest(false); // GameManager에게 'false(거절)' 토스
    }

    // ── 결과 패널 버튼 콜백 ────────────────────────────────────────────────
    public void OnClickRematch()
    {
        // 1. 멀티플레이 모드일 때
        if (gameManager != null && gameManager.currentMode == PlayMode.Multiplayer)
        {
            // 방을 나가면 GameSession.OnLeftRoom이 발동해서 로비 씬으로 자동 이동함
            if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
        }
        // 2. Solo / AI 모드일 때
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
    public void ShowSystemMessage(string message)
    {
        if (systemMessageText == null) return;

        systemMessageText.text = message;
        systemMessageText.gameObject.SetActive(true);

        StopAllCoroutines(); // 기존에 떠있던 메시지가 있다면 취소하고 새로 시작
        StartCoroutine(FadeOutMessage());
    }

    private IEnumerator FadeOutMessage()
    {
        // 2초 대기 후 서서히 사라짐
        yield return new WaitForSeconds(2.0f);
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

            bool isRestricted = (skillId == 9 && myColor != StoneColor.Black)
                            || (skillId == 10 && myColor != StoneColor.White);

            skillSelectButtons[i].interactable = !isRestricted;
        }
    }
    // 2. 버프/디버프 상태 전체 갱신 (시온님이 데이터 넘겨주면 상화님 UI가 그림)
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
        }
    }

    // -------------------------------------------------------
    // AI가 생각 중일 때 버튼들을 클릭 못 하게(회색으로) 막는 함수
    public void SetInteractableButtons(bool isInteractable)
    {
        if (restartButton != null) restartButton.interactable = isInteractable;
        if (undoButton != null) undoButton.interactable = isInteractable;
    }
}
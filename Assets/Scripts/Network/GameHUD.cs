using Photon.Pun;
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

    [Header("코어 매니저 연결")]
    public GameManager gameManager;

    [Header("결과 패널")]
    public GameObject resultPanel;
    public TextMeshProUGUI resultText;
    public Button rematchButton;
    public Button exitButton;

    private void Start()
    {
        // 시작할 때 패널들 닫아두기
        if (resultPanel) resultPanel.SetActive(false);
        if (undoPopupPanel) undoPopupPanel.SetActive(false);

        // 리매치 / 나가기 버튼은 여기서 코드로 연결
        if (rematchButton) rematchButton.onClick.AddListener(OnClickRematch);
        if (exitButton)    exitButton.onClick.AddListener(OnClickExit);
    }

    // ── GameSession에서 호출 ─────────────────────────────────────

    // 멀티플레이 모드일 때 GameSession에서 호출하여 불필요한 버튼 숨기기
    public void SetupForMultiplayer()
    {
        if (restartButton) restartButton.gameObject.SetActive(false); // 멀티에선 중간 재시작 금지
    }

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
        if (resultText)  resultText.text = "상대방이 나갔습니다.";
        if (rematchButton) rematchButton.gameObject.SetActive(false); // 나갔는데 리매치는 불가
    }

    public void ShowUndoPopup()
    {
        undoPopupPanel.SetActive(true);
        undoRequestText.text = $"상대방이 무르기를 요청했습니다.";
    }

    public void HideUndoRequestPopup() => undoPopupPanel.SetActive(false);

    // ── 팝업 버튼 콜백 (🌟 인스펙터 연결 전용 🌟) ──────────────────

    // 1. [수락] 버튼
    public void OnAcceptUndoClicked()
    {
        if (undoPopupPanel) undoPopupPanel.SetActive(false);
        gameManager.ReplyToUndoRequest(true); // GameManager에게 'true(수락)' 토스
    }

    // 2. [거절] 버튼
    public void OnRefuseUndoClicked()
    {
        if (undoPopupPanel) undoPopupPanel.SetActive(false);
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
}

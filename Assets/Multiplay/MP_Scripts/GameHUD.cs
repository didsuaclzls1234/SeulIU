using Photon.Pun;
using TMPro;
using UnityEngine;
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

    [Header("결과 패널")]
    public GameObject resultPanel;
    public TextMeshProUGUI resultText;
    public Button rematchButton;
    public Button exitButton;

    private void Start()
    {
        if (resultPanel) resultPanel.SetActive(false);

        if (rematchButton) rematchButton.onClick.AddListener(OnClickRematch);
        if (exitButton)    exitButton.onClick.AddListener(OnClickExit);
    }

    // ── GameSession에서 호출 ─────────────────────────────────────

    public void ShowRoleAssigned(StoneColor myColor)
    {
        if (myColorText)
            myColorText.text = $"나: {myColor.ToKorean()}";
    }

    public void UpdateTurnDisplay(StoneColor currentTurn, bool isMyTurn)
    {
        if (turnText == null) return;
        string whose = isMyTurn ? "내 차례" : "상대 차례";
        turnText.text = $"{currentTurn.ToKorean()} — {whose}";
    }

    public void ShowGameOver(StoneColor winner, StoneColor myColor)
    {
        if (resultPanel) resultPanel.SetActive(true);
        if (resultText == null) return;

        resultText.text = winner == StoneColor.None  ? "무승부!"
                        : winner == myColor          ? "승리! "
                                                     : "패배...";
    }

    public void ShowOpponentLeft()
    {
        if (resultPanel) resultPanel.SetActive(true);
        if (resultText)  resultText.text = "상대방이 나갔습니다.";
    }

    // ── 버튼 콜백 ────────────────────────────────────────────────

    private void OnClickRematch()
    {
        // 방을 나가면 GameSession.OnLeftRoom → Lobby 씬 이동
        PhotonNetwork.LeaveRoom();
    }

    private void OnClickExit()
    {
        PhotonNetwork.LeaveRoom();
    }
}

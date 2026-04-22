using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// VS_Player 씬의 중앙 조율자.
///
/// 역할:
///   1. Photon 이벤트 수신 (AssignRoles / PlaceStone / GameOver)
///   2. 로직 클래스들(ActionLog, TurnManager, WinDetector 등) 연결
///   3. BoardUI / GameHUD에 결과 전달
///
/// 씬에 빈 GameObject를 만들고 이 스크립트와 PhotonView를 붙이세요.
/// </summary>
public class GameSession : MonoBehaviourPunCallbacks, IOnEventCallback
{
    [Header("씬 레퍼런스 (Inspector에서 연결)")]
    public BoardUI boardUI;
    public GameHUD gameHUD;

    // ── 로직 클래스 (MonoBehaviour 불필요) ──────────────────────
    private ActionLog      _log;
    private TurnManager    _turnManager;
    private MoveValidator  _moveValidator;
    private WinDetector    _winDetector;
    private IRuleSet       _ruleSet;

    private bool _gameStarted = false;
    private bool _gameOver    = false;

    // ── 생명주기 ────────────────────────────────────────────────

    private void Awake()
    {
        // 규칙셋을 여기서 교체하면 나머지 시스템은 수정 불필요
        _ruleSet      = new StandardRuleSet();
        _log          = new ActionLog();
        _turnManager  = new TurnManager(_log);
        _moveValidator = new MoveValidator(_ruleSet, _log);
        _winDetector  = new WinDetector(_ruleSet);
    }

    private void Start()
    {
        PhotonNetwork.AddCallbackTarget(this);

        // Master Client가 역할 배정 시작
        if (PhotonNetwork.IsMasterClient)
        {
            RoleAssigner.AssignAndBroadcast();
        }
    }

    private void OnDestroy()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    // ── 로컬 플레이어 착수 요청 (BoardUI → 여기 → Photon) ───────

    /// <summary>BoardUI의 셀 클릭 시 호출됩니다.</summary>
    public void TrySubmitMove(int row, int col)
    {
        if (!_gameStarted || _gameOver) return;
        if (!_turnManager.IsMyTurn)     return;

        StoneColor myColor = _turnManager.MyColor;

        if (!_moveValidator.IsValid(myColor, row, col)) return;

        // ReceiverGroup.All → 자기 자신 포함 양쪽 모두에게 전달
        object[] data = new object[] { (int)myColor, row, col, _log.Count };
        RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(
            PhotonEventCodes.PlaceStone, data, opts, SendOptions.SendReliable);
    }

    // ── Photon 이벤트 수신 ───────────────────────────────────────

    public void OnEvent(EventData photonEvent)
    {
        switch (photonEvent.Code)
        {
            case PhotonEventCodes.AssignRoles:
                HandleAssignRoles((object[])photonEvent.CustomData);
                break;

            case PhotonEventCodes.PlaceStone:
                HandlePlaceStone((object[])photonEvent.CustomData);
                break;

            case PhotonEventCodes.GameOver:
                HandleGameOver((object[])photonEvent.CustomData);
                break;
        }
    }

    // ── 이벤트 핸들러 ────────────────────────────────────────────

    private void HandleAssignRoles(object[] data)
    {
        int blackActorNumber = (int)data[0];
        StoneColor myColor   = PhotonNetwork.LocalPlayer.ActorNumber == blackActorNumber
            ? StoneColor.Black : StoneColor.White;

        _turnManager.SetMyColor(myColor);
        _gameStarted = true;

        Debug.Log($"[GameSession] 내 색: {myColor.ToKorean()}");

        boardUI.InitPreviewStone(myColor);
        gameHUD.ShowRoleAssigned(myColor);
        RefreshHUD();
    }

    private void HandlePlaceStone(object[] data)
    {
        var color = (StoneColor)(int)data[0];
        int row   = (int)data[1];
        int col   = (int)data[2];
        int seq   = (int)data[3];

        // 양쪽 클라이언트가 동일한 로직으로 독립 검증
        if (!_moveValidator.IsValid(color, row, col))
        {
            Debug.LogWarning("[GameSession] 유효하지 않은 착수 수신 — 무시");
            return;
        }

        if (!_log.TryApply(color, row, col, seq)) return;

        boardUI.PlaceStone(row, col, color);

        // 승리 판정
        if (_winDetector.Check(_log.Board, row, col, color))
        {
            SendGameOver(color);
            return;
        }

        // 무승부 (보드가 꽉 찬 경우)
        if (_log.Count == ActionLog.BoardSize * ActionLog.BoardSize)
        {
            SendGameOver(StoneColor.None);
            return;
        }

        RefreshHUD();
    }

    private void HandleGameOver(object[] data)
    {
        if (_gameOver) return;   // 중복 이벤트 방지
        _gameOver = true;

        var winner = (StoneColor)(int)data[0];
        Debug.Log($"[GameSession] 게임 종료 — 승자: {winner.ToKorean()}");

        gameHUD.ShowGameOver(winner, _turnManager.MyColor);
        boardUI.SetInputEnabled(false);
    }

    private void SendGameOver(StoneColor winner)
    {
        object[] data = new object[] { (int)winner };
        RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(
            PhotonEventCodes.GameOver, data, opts, SendOptions.SendReliable);
    }

    // ── Photon 콜백 ──────────────────────────────────────────────

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (!_gameOver)
        {
            _gameOver = true;
            gameHUD.ShowOpponentLeft();
            boardUI.SetInputEnabled(false);
        }
    }

    public override void OnLeftRoom()
    {
        SceneManager.LoadScene("Lobby"); // 로비 씬 이름이 다르면 수정
    }

    // ── 내부 헬퍼 ────────────────────────────────────────────────

    private void RefreshHUD()
    {
        boardUI.SetIsMyTurn(_turnManager.IsMyTurn);
        gameHUD.UpdateTurnDisplay(_turnManager.CurrentTurn, _turnManager.IsMyTurn);
    }
}

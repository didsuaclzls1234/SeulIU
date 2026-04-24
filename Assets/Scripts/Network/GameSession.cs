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
    [Header("UI 연결")]
    public GameHUD gameHUD;

    [Header("코어 매니저 연결")]
    public GameManager gameManager;


    // ── 생명주기 ────────────────────────────────────────────────

    private void Start()
    {
        PhotonNetwork.AddCallbackTarget(this);

        // 1. 내 턴에 돌을 뒀을 때 서버로 쏘는 이벤트 구독
        if (gameManager != null)
        {
            // 멀티플레이 모드로 전환 및 이벤트 연결
            gameManager.currentMode = PlayMode.Multiplayer;
            gameManager.OnStonePlacedLocally += SendPlaceStoneEvent;
            gameManager.OnGameOverLocally += SendGameOverEvent; 
        }

        // * 멀티플레이 세팅: GameHUD에 불필요한 버튼 끄라고 지시
        if (gameHUD != null)
        {
            gameHUD.SetupForMultiplayer();
        }

        // 2. 방장(Master Client)이면 흑/백 배정 시작
        if (PhotonNetwork.IsMasterClient)
        {
            RoleAssigner.AssignAndBroadcast();
        }
    }

    private void OnDestroy()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
        if (gameManager != null)
        {
            gameManager.OnStonePlacedLocally -= SendPlaceStoneEvent;
            gameManager.OnGameOverLocally -= SendGameOverEvent;
        }
    }

    // ── Photon 이벤트 발신 ───────────────────────────────────────

    // [발신] 내 착수 데이터 전송 (Color, X, Y, Seq)
    private void SendPlaceStoneEvent(int x, int y, int seq)
    {
        // 1. 돌 색상, X, Y, 순서(seq) 배열에 담기
        object[] data = new object[] { (int)gameManager.localPlayerColor, x, y, seq };

        // 2. 이미 내 화면엔 돌이 렌더링 됐으므로 '나 빼고(Others)' 전송
        RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(PhotonEventCodes.PlaceStone, data, opts, SendOptions.SendReliable);
    }

    // [발신] 내가 이기거나 무승부 났을 때
    private void SendGameOverEvent(StoneColor winner)
    {
        object[] data = new object[] { (int)winner };
        RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(PhotonEventCodes.GameOver, data, opts, SendOptions.SendReliable);
    }

    // ── Photon 이벤트 수신 ───────────────────────────────────────

    // ==========================================
    // [수신] 서버에서 이벤트(패킷)가 날아왔을 때
    // ==========================================
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

    // 1. 흑/백 배정 결과 수신
    private void HandleAssignRoles(object[] data)
    {
        // 닉네임 세팅 전에 PlayerList 확인
        int blackActorNumber = (int)data[0];

        // 내가 흑인지 백인지 판별 (1: 흑, 2: 백)
        StoneColor myColor = (PhotonNetwork.LocalPlayer.ActorNumber == blackActorNumber)
            ? StoneColor.Black : StoneColor.White;
        // GameManager에 세팅
        gameManager.localPlayerColor = myColor;
        gameManager.currentMode = PlayMode.Multiplayer; // 자동으로 멀티 모드 전환

        // 닉네임 세팅
        foreach (Player player in PhotonNetwork.PlayerList)
        {
        if (player.IsLocal)
            gameManager.localPlayerName = player.NickName;
        else
            gameManager.remotePlayerName = player.NickName;
        }
        gameHUD.SetPlayerNames(gameManager.localPlayerName, gameManager.remotePlayerName);
        
        Debug.Log($"[GameSession] 내 색: {myColor.ToKorean()}");

        if (gameHUD)
        {
            gameHUD.ShowRoleAssigned(myColor);
        }
    }

    // 2. 상대방 착수 수신
    private void HandlePlaceStone(object[] data)
    {
        StoneColor receivedColor = (StoneColor)(int)data[0];
        int x = (int)data[1];
        int y = (int)data[2];
        int seq = (int)data[3]; // 스킬 대비용 시퀀스 넘버

        // GameManager로 색상과 순서까지 전부 넘겨서 안전하게 처리
        gameManager.ReceiveNetworkMove(x, y, receivedColor, seq);
    }

    // 3. 상대방 화면에서 게임 끝났다고 날아올 때
    private void HandleGameOver(object[] data)
    {
        StoneColor winner = (StoneColor)(int)data[0];
        Debug.Log($"[GameSession] 게임 종료 — 승자: {winner.ToKorean()}");

        // GameManager 상태 업데이트
        gameManager.currentState = GameState.GameOver;

        if (gameHUD)
        {
            gameHUD.ShowGameOver(winner, gameManager.localPlayerColor);
        }
    }

    // ── Photon 콜백 ──────────────────────────────────────────────

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
         Debug.Log($"[GameSession] {otherPlayer.NickName} 나감 / 현재 상태: {gameManager.currentState}");
        if (gameManager.currentState != GameState.GameOver)
        {
            gameManager.currentState = GameState.GameOver;
            if (gameHUD) gameHUD.ShowOpponentLeft();
        }
    }

    public override void OnLeftRoom()
    {
        PhotonNetwork.LoadLevel("Lobby");; // 로비 씬 이름이 다르면 수정
    }
}

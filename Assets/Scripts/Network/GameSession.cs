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
    public SkillManager skillManager;
    public TimerManager timerManager;
    
    #region 생명주기
    
    private void Start()
    {
        PhotonNetwork.AddCallbackTarget(this);

        // 1. 내 턴에 돌을 뒀을 때 서버로 쏘는 이벤트 구독
        if (gameManager)
        {
            // 멀티플레이 모드로 전환 및 이벤트 연결
            gameManager.currentMode = PlayMode.Multiplayer;
            gameManager.OnStonePlacedLocally += SendPlaceStoneEvent;
            gameManager.OnGameOverLocally += SendGameOverEvent; 
            //gameManager.OnUndoRequestedLocally += SendUndoRequestEvent;
            //gameManager.OnUndoReplyLocally += SendUndoReplyEvent;
            gameManager.OnDoubleDownExtraPlaced += SendDoubleDownExtraEvent;
        }

        // * 멀티플레이 세팅: GameHUD에 불필요한 버튼 끄라고 지시
        //if (gameHUD)
        //{
        //    gameHUD.SetupForMultiplayer();
        //}
     
        // 스킬 버튼 자동 연결
        InitSkillButtons();

        // 2. 방장(Master Client)이면 흑/백 배정 시작
        if (PhotonNetwork.IsMasterClient)
        {
            RoleAssigner.AssignAndBroadcast();
        }
    }

    private void OnDestroy()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
        if (gameManager)
        {
            gameManager.OnStonePlacedLocally -= SendPlaceStoneEvent;
            gameManager.OnGameOverLocally -= SendGameOverEvent;
            //gameManager.OnUndoRequestedLocally -= SendUndoRequestEvent;
            //gameManager.OnUndoReplyLocally -= SendUndoReplyEvent;
            gameManager.OnDoubleDownExtraPlaced -= SendDoubleDownExtraEvent;
        }
    }

    #endregion

    #region 스킬 선택

    private int[] _selectedSkillIDs = new int[] { -1, -1, -1 }; // -1 = 비어있음

    private void InitSkillButtons()
    {
        for (int i = 0; i < gameHUD.skillSelectButtons.Length; i++)
        {
            int skillId = i + 1;
            gameHUD.skillSelectButtons[i].onClick.AddListener(() =>
                OnSkillButtonClicked(skillId));
        }
    }

    private void OnSkillButtonClicked(int skillId)
    {
        for (int slot = 0; slot < 3; slot++)
        {
            if (_selectedSkillIDs[slot] == skillId)
            {
                _selectedSkillIDs[slot] = -1;
                skillManager.OnSkillSelected(slot, -1);
                return;
            }
        }

        for (int slot = 0; slot < 3; slot++)
        {
            if (_selectedSkillIDs[slot] == -1)
            {
                _selectedSkillIDs[slot] = skillId;
                skillManager.OnSkillSelected(slot, skillId);
                return;
            }
        }

        Debug.Log($"[GameSession] 스킬 3개 이미 선택됨 / 현재 선택: [{string.Join(", ", _selectedSkillIDs)}]");
    }
   

    #endregion

    #region 이벤트 핸들러

    // 1. 흑/백 배정 결과 수신
    private void HandleAssignRoles(object[] data)
    {
        // 닉네임 세팅 전에 PlayerList 확인
        int blackActorNumber = (int)data[0];
        float serverTime     = (float)data[1];

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
        
        Debug.Log($"[GameSession] 내 색: {myColor.ToKorean()}");

        if (gameHUD)
        {
            gameHUD.ShowRoleAssigned(myColor);
            gameHUD.SetPlayerNames(gameManager.localPlayerName, gameManager.remotePlayerName);
            gameHUD.DisplayMyRole(myColor);
            gameHUD.skillSelectPanel?.SetActive(true);
            timerManager.StartSkillSelectTimer();
        }

        // 패킷 전달 지연만큼 보정해서 타이머 동기화
        float elapsed = (float)(PhotonNetwork.Time - serverTime);
        float remaining = timerManager.skillSelectLimit - elapsed;
        timerManager.SyncTimerFromServer(remaining);
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

        // 상대가 뒀으니 내 타이머를 30초부터 새로 시작
        if (timerManager != null)
        {
            timerManager.RestartTurnTimer();
        }
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

    // 4. 스킬 선택 완료 후 Ready 버튼 클릭 시 호출
    private void HandleSyncPlayerInfo(object[] data)
    {
        string oppNickname  = (string)data[0];
        int[]  oppSkillIDs  = (int[])data[1];

        // 상대방 닉네임 저장
        gameManager.remotePlayerName = oppNickname;

        // 상대방 스킬 덱 등록
        skillManager.InitializeSkillDeck(false, oppSkillIDs);

        // 상대방 정보만 저장, SetPlayerReady는 내가 Ready 버튼 눌렀을 때만
        skillManager.isRemotePlayerReady = true;

        Debug.Log($"[GameSession] 상대방 정보 수신 / 내 Ready: {skillManager.isLocalPlayerReady}");

        // 내가 이미 Ready 상태면 게임 시작
        if (skillManager.isLocalPlayerReady && skillManager.isRemotePlayerReady)
        {   
            gameHUD?.skillSelectPanel?.SetActive(false);
            gameManager.StartGameAfterSelection();
        }    

        
    }

    //private void HandleUndoRequest()
    //{
    //gameManager.ReceiveNetworkUndoRequest();
    //}

    //private void HandleUndoReply(object[] data)
    //{
    //bool isAccepted = (bool)data[0];
    //// 결과 보여주고 타이머 재개하는 함수
    //    if (gameHUD != null)
    //    {
    //        gameHUD.ShowUndoResultAndClose(isAccepted);
    //    }
    //    gameManager.ReceiveNetworkUndoReply(isAccepted);
    //}

    private void HandleSyncTimer(object[] data)
    {
        float serverTime     = (float)data[0];
        float elapsed        = (float)(PhotonNetwork.Time - serverTime);
        float remainingTime  = timerManager.turnLimit - elapsed;

        timerManager.SyncTimerFromServer(remainingTime);
    }

    // 스킬 사용 수신
    private void HandleUseSkill(object[] data)
    {
        int   skillId = (int)data[0];
        int[] xs      = (int[])data[1];
        int[] ys      = (int[])data[2];

        // SkillManager에게 넘겨서 상대방 화면에서도 똑같이 실행하게 함
        if (skillManager != null)
        {
            skillManager.ReceiveOpponentSkill(skillId, xs, ys);
        }
    }
    
    private void HandleExtraPlacement(object[] data)
    {
    StoneColor color = (StoneColor)(int)data[0];
    int x = (int)data[1];
    int y = (int)data[2];

    gameManager.ReceiveExtraPlacement(x, y, color);
    }
    
    #endregion

    #region Photon이벤트 발신
    
    // [발신] 내 착수 데이터 전송 (Color, X, Y, Seq)
    private void SendPlaceStoneEvent(int x, int y, int seq)
    {
        // 1. 돌 색상, X, Y, 순서(seq) 배열에 담기
        object[] data = new object[] { (int)gameManager.localPlayerColor, x, y, seq };

        // 2. 이미 내 화면엔 돌이 렌더링 됐으므로 '나 빼고(Others)' 전송
        RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(PhotonEventCodes.PlaceStone, data, opts, SendOptions.SendReliable);
        
        // 내가 착수했으니 타이머 동기화 (방장만)
        if (PhotonNetwork.IsMasterClient)
            SendSyncTimer();

        // 타이머 재시작
        timerManager?.StartTurnTimer();
    }

    // [발신] 내가 이기거나 무승부 났을 때
    private void SendGameOverEvent(StoneColor winner)
    {
        object[] data = new object[] { (int)winner };
        RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(PhotonEventCodes.GameOver, data, opts, SendOptions.SendReliable);
    }

    // Ready 버튼 클릭 시 호출
    private bool _isReadySent = false;//중복전송차단 플래그
    public void SendSyncPlayerInfo()
    {   
         //  슬롯에 -1(선택X)이 하나라도 있으면 전송 차단
        for (int i = 0; i < _selectedSkillIDs.Length; i++)
        {
            if (_selectedSkillIDs[i] == -1)
            {
                Debug.Log($"[GameSession] {i + 1}번 슬롯이 비어있습니다. 스킬을 3개 모두 선택해 주세요.");
                return; // 전송 안 함
            }
        }

        //검증 통과했으면 중복 전송 방지 플래그 체크
        if (_isReadySent) return;
        _isReadySent = true;

        object[] data = new object[]
        {
            gameManager.localPlayerName,
            skillManager.mySkillsID
        };

        RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(PhotonEventCodes.SyncPlayerInfo, data, opts, SendOptions.SendReliable);

        // 내 Ready 처리
        skillManager.InitializeSkillDeck(true, skillManager.mySkillsID);
        skillManager.isLocalPlayerReady = true;

        // 상대방이 이미 Ready면 게임 시작
        if (skillManager.isLocalPlayerReady && skillManager.isRemotePlayerReady)
        {
            gameHUD?.skillSelectPanel?.SetActive(false);
            gameManager.StartGameAfterSelection();
        }
    }

    //무르기 요청 이벤트 발신
    //private void SendUndoRequestEvent()
    //{
    //RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
    //PhotonNetwork.RaiseEvent(PhotonEventCodes.UndoRequest, null, opts, SendOptions.SendReliable);
    //}

    //무르기 수락/거절 이벤트 발신
    //private void SendUndoReplyEvent(bool isAccepted)
    //{
    //object[] data = new object[] { isAccepted };
    //RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
    //PhotonNetwork.RaiseEvent(PhotonEventCodes.UndoReply, data, opts, SendOptions.SendReliable);
    //}

    // 타이머 동기화 - 방장이 턴 시작 시 호출
    public void SendSyncTimer()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        object[] data = new object[] { (float)PhotonNetwork.Time };
        RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(PhotonEventCodes.SyncTimer, data, opts, SendOptions.SendReliable);
    }

    // 스킬 사용 - 발신자 측에서 좌표 미리 계산 후 전송
    public void SendUseSkill(int skillId, int[] xs, int[] ys)
    {
        object[] data = new object[] { skillId, xs, ys };
        RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(PhotonEventCodes.UseSkill, data, opts, SendOptions.SendReliable);
    }
    
    // Double Down 스킬로 AI가 랜덤 착수했을 때 발신
    private void SendDoubleDownExtraEvent(int x, int y, int seq)
    {
    object[] data = new object[] { (int)gameManager.localPlayerColor, x, y, seq };
    RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
    PhotonNetwork.RaiseEvent(PhotonEventCodes.ExtraPlacement, data, opts, SendOptions.SendReliable);
    }
    #endregion
    
    #region Photon 이벤트 수신
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
            case PhotonEventCodes.SyncPlayerInfo:
                HandleSyncPlayerInfo((object[])photonEvent.CustomData);
                break;
            case PhotonEventCodes.SyncTimer:
                HandleSyncTimer((object[])photonEvent.CustomData);
                break;
            case PhotonEventCodes.UseSkill:
                HandleUseSkill((object[])photonEvent.CustomData);
                break;
            //case PhotonEventCodes.UndoRequest:
            //    HandleUndoRequest();
            //    break;
            //case PhotonEventCodes.UndoReply:
            //    HandleUndoReply((object[])photonEvent.CustomData);
            //    break;
            case PhotonEventCodes.ExtraPlacement:
                HandleExtraPlacement((object[])photonEvent.CustomData); // 착수 처리 로직 재활용
                break;
        }
    }
    #endregion  

    #region Photon 콜백

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
         Debug.Log($"[GameSession] {otherPlayer.NickName} 나감 / 현재 상태: {gameManager.currentState}");
        if (gameManager.currentState != GameState.GameOver)
        {
            gameManager.currentState = GameState.GameOver;
            if (gameHUD) gameHUD.ShowOpponentLeft();
        }
    }

    private void OnApplicationQuit()//게임 세션이 종료될 때 네트워크 연결 끊기(유니티 기본 콜백)
    {
    if (PhotonNetwork.IsConnected)
        PhotonNetwork.Disconnect();
    }   

    public override void OnLeftRoom()
    {
        PhotonNetwork.LoadLevel("Lobby");; // 로비 씬 이름이 다르면 수정
    }

    #endregion
}

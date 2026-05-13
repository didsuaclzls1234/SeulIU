using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    [Header("Ready Button UI")]
    [SerializeField] private Image readyButtonImage;
    [SerializeField] private Sprite readyButtonSelectedSprite;

    [Header("Deck Slot Container UI")]
    [SerializeField] private Image deckSlotContainerImage;
    [SerializeField] private Sprite blackDeckSlotContainerSprite;
    [SerializeField] private Sprite whiteDeckSlotContainerSprite;

    // 재도전 요청을 내가 보냈는지 여부 (중복 요청 방지)
    private bool _isRematchRequested = false;

    // Ready 버튼 중복 전송 차단 플래그
    private bool _isReadySent = false;

    #region 생명주기

    private void Start()
    {
        // 로비에서 정해온 모드를 GameManager에 주입
        gameManager.currentMode = LobbyManager.SelectedMode;

        InitSkillButtons();

        // 멀티 플레이 모드
        if (gameManager.currentMode == PlayMode.Multiplayer)
        {
            PhotonNetwork.AddCallbackTarget(this);

            // 멀티플레이 모드로 전환 및 이벤트 연결
            gameManager.currentMode = PlayMode.Multiplayer;
            gameManager.OnStonePlacedLocally += SendPlaceStoneEvent;
            gameManager.OnGameOverLocally += SendGameOverEvent;

            // 방장(Master Client)이면 흑/백 배정 시작
            if (PhotonNetwork.IsMasterClient)
            {
                RoleAssigner.AssignAndBroadcast();
            }
        }
        else if (gameManager.currentMode == PlayMode.AI)
        {
            // AI 모드: 로컬에서 즉시 흑백 배정 및 시작
            StartCoroutine(InitAISessionRoutine());
        }
    }

    private IEnumerator InitAISessionRoutine()
    {
        // 1. 흑/백 랜덤 결정
        StoneColor playerColor = UnityEngine.Random.value < 0.5f ? StoneColor.Black : StoneColor.White;
        gameManager.localPlayerColor = playerColor;
        gameManager.localPlayerName = "나(Player)";
        gameManager.remotePlayerName = "알파오목(AI)";

        // 2. UI 표시
        //gameHUD.ShowRoleAssigned(playerColor);
        gameHUD.SetPlayerNames("나", "AI");
        gameHUD.DisplayMyRole(playerColor);
        ApplyDeckSlotContainerTheme(playerColor);
        gameHUD.ShowSkillSelectPanel();
        timerManager.StartSkillSelectTimer();
        
        // 3. AI가 스킬을 랜덤으로 고를 시간을 줌
        yield return new WaitForSeconds(1.5f);

        // SkillManager에서 알아서 3번 + 패시브 챙기고 준비 완료까지 처리
        if (skillManager != null)
        {
            skillManager.AI_AutoSelectSkills();
        }
    }

    private void OnDestroy()
    {
        // 멀티플레이일 때만 해제
        if (gameManager != null && gameManager.currentMode == PlayMode.Multiplayer)
        {
            PhotonNetwork.RemoveCallbackTarget(this);
        }

        if (gameManager)
        {
            gameManager.OnStonePlacedLocally -= SendPlaceStoneEvent;
            gameManager.OnGameOverLocally -= SendGameOverEvent;
        }
    }

    #endregion

    #region UI

    private void ApplyDeckSlotContainerTheme(StoneColor playerColor)
    {
        if (deckSlotContainerImage == null)
        {
            Debug.LogWarning("[GameSession] DeckSlotContainer Image가 연결되지 않았습니다.");
            return;
        }

        Sprite selectedSprite = null;

        if (playerColor == StoneColor.Black)
        {
            selectedSprite = blackDeckSlotContainerSprite;
        }
        else if (playerColor == StoneColor.White)
        {
            selectedSprite = whiteDeckSlotContainerSprite;
        }

        if (selectedSprite == null)
        {
            Debug.LogWarning($"[GameSession] {playerColor.ToKorean()}용 DeckSlotContainer Sprite가 연결되지 않았습니다.");
            return;
        }

        deckSlotContainerImage.sprite = selectedSprite;
    }

    #endregion

    #region 스킬 선택

    private void InitSkillButtons()
    {
        if (gameHUD == null)
        {
            Debug.LogError("[GameSession] GameHUD가 연결되지 않았습니다.");
            return;
        }

        if (skillManager == null)
        {
            Debug.LogError("[GameSession] SkillManager가 연결되지 않았습니다.");
            return;
        }

        if (gameHUD.skillSelectButtons == null || gameHUD.skillSelectButtons.Length == 0)
        {
            Debug.LogError("[GameSession] GameHUD의 skillSelectButtons 배열이 비어 있습니다.");
            return;
        }

        for (int i = 0; i < gameHUD.skillSelectButtons.Length; i++)
        {
            if (gameHUD.skillSelectButtons[i] == null)
            {
                Debug.LogError($"[GameSession] skillSelectButtons[{i}] 버튼이 연결되지 않았습니다.");
                continue;
            }

            int skillId = i + 1;

            gameHUD.skillSelectButtons[i].onClick.RemoveAllListeners();
            gameHUD.skillSelectButtons[i].onClick.AddListener(() =>
                skillManager.OnSkillSelected(skillId));
        }
    }

    #endregion

    #region 이벤트 핸들러

    // 1. 흑/백 배정 결과 수신
    private void HandleAssignRoles(object[] data)
    {
        // 닉네임 세팅 전에 PlayerList 확인
        int blackActorNumber = (int)data[0];
        float serverTime = (float)data[1];

        // 내가 흑인지 백인지 판별
        StoneColor myColor = (PhotonNetwork.LocalPlayer.ActorNumber == blackActorNumber)
            ? StoneColor.Black
            : StoneColor.White;

        // GameManager에 세팅
        gameManager.localPlayerColor = myColor;
        gameManager.currentMode = PlayMode.Multiplayer;

        // 닉네임 세팅
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player.IsLocal)
            {
                gameManager.localPlayerName = player.NickName;
            }
            else
            {
                gameManager.remotePlayerName = player.NickName;
            }
        }

        Debug.Log($"[GameSession] 내 색: {myColor.ToKorean()}");

        if (gameHUD)
        {
            //gameHUD.ShowRoleAssigned(myColor);
            gameHUD.SetPlayerNames(gameManager.localPlayerName, gameManager.remotePlayerName);
            gameHUD.DisplayMyRole(myColor);
            ApplyDeckSlotContainerTheme(myColor);
            gameHUD.ShowSkillSelectPanel();
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
        int seq = (int)data[3];

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
        string oppNickname = (string)data[0];
        int[] oppSkillIDs = (int[])data[1];

        // 상대방 닉네임 저장
        gameManager.remotePlayerName = oppNickname;

        // 상대방 스킬 덱 등록
        skillManager.InitializeSkillDeck(false, oppSkillIDs);
        skillManager.SetPlayerReady(false);
    }

    private void HandleSyncTimer(object[] data)
    {
        float serverTime = (float)data[0];
        float elapsed = (float)(PhotonNetwork.Time - serverTime);
        float remainingTime = timerManager.turnLimit - elapsed;

        timerManager.SyncTimerFromServer(remainingTime);
    }

    // 스킬 사용 수신
    private void HandleUseSkill(object[] data)
    {
        int skillId = (int)data[0];
        int[] xs = (int[])data[1];
        int[] ys = (int[])data[2];
        int turnCount = (int)data[3];

        // SkillManager에게 넘겨서 상대방 화면에서도 똑같이 실행하게 함
        if (skillManager != null)
        {
            skillManager.ReceiveOpponentSkill(skillId, xs, ys, turnCount);
        }
    }

    #endregion

    #region Photon 이벤트 발신

    // [발신] 내 착수 데이터 전송
    private void SendPlaceStoneEvent(int x, int y, int seq)
    {
        object[] data = new object[] { (int)gameManager.localPlayerColor, x, y, seq };

        // 이미 내 화면엔 돌이 렌더링 됐으므로 나 빼고 전송
        RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(PhotonEventCodes.PlaceStone, data, opts, SendOptions.SendReliable);

        // 내가 착수했으니 타이머 동기화
        if (PhotonNetwork.IsMasterClient)
        {
            SendSyncTimer();
        }

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
    public void SendSyncPlayerInfo()
    {
        // 슬롯에 -1(선택X)이 하나라도 있으면 전송 차단
        for (int i = 0; i < skillManager.mySkillsID.Length; i++)
        {
            if (skillManager.mySkillsID[i] == -1)
            {
                Debug.Log($"[GameSession] {i + 1}번 슬롯이 비어있습니다. 스킬을 3개 모두 선택해 주세요.");
                return;
            }
        }

        // 검증 통과했으면 중복 전송 방지 플래그 체크
        if (_isReadySent)
        {
            return;
        }
        else
        {
            _isReadySent = true;

            if (readyButtonImage != null && readyButtonSelectedSprite != null)
            {
                readyButtonImage.sprite = readyButtonSelectedSprite;
            }
        }

        object[] data = new object[]
        {
            gameManager.localPlayerName,
            skillManager.mySkillsID
        };

        RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(PhotonEventCodes.SyncPlayerInfo, data, opts, SendOptions.SendReliable);

        // 내 Ready 처리
        skillManager.InitializeSkillDeck(true, skillManager.mySkillsID);
        skillManager.SetPlayerReady(true);
    }

    // 타이머 동기화 - 방장이 턴 시작 시 호출
    public void SendSyncTimer()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        object[] data = new object[] { (float)PhotonNetwork.Time };
        RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(PhotonEventCodes.SyncTimer, data, opts, SendOptions.SendReliable);
    }

    // 스킬 사용 - 발신자 측에서 좌표 미리 계산 후 전송
    public void SendUseSkill(int skillId, int[] xs, int[] ys, int turnCount)
    {
        object[] data = new object[] { skillId, xs, ys, turnCount };
        RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(PhotonEventCodes.UseSkill, data, opts, SendOptions.SendReliable);
    }

    // ── 재도전(Rematch) ──────────────────────────────────────

    /// <summary>결과 화면에서 "다시하기" 누르면 GameHUD → 여기로 옴</summary>
    public void RequestRematch()
    {
        if (_isRematchRequested)
        {
            return;
        }

        _isRematchRequested = true;

        // 요청 패킷 전송
        RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(PhotonEventCodes.RematchRequest, null, opts, SendOptions.SendReliable);

        // 요청자 화면: 대기 패널 표시
        if (gameHUD != null)
        {
            gameHUD.ShowRematchWaitingPanel();
        }

        Debug.Log("[GameSession] 재도전 요청 전송");
    }

    /// <summary>수신자가 수락 버튼 눌렀을 때 — 인스펙터에서 버튼에 연결</summary>
    public void OnRematchAccepted()
    {
        SendRematchReply(true);
        ExecuteFullReset();
    }

    /// <summary>수신자가 거절 버튼 눌렀을 때 — 인스펙터에서 버튼에 연결</summary>
    public void OnRematchDeclined()
    {
        SendRematchReply(false);

        if (gameHUD != null)
        {
            gameHUD.HideRematchPanel();
        }
    }

    private void SendRematchReply(bool isAccepted)
    {
        object[] data = new object[] { isAccepted };
        RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(PhotonEventCodes.RematchReply, data, opts, SendOptions.SendReliable);
    }

    /// <summary>상대방이 재도전 요청을 보내왔을 때</summary>
    private void HandleRematchRequest()
    {
        if (gameHUD != null)
        {
            gameHUD.ShowRematchReceivedPanel();
        }

        Debug.Log("[GameSession] 재도전 요청 수신");
    }

    /// <summary>내가 보낸 재도전 요청에 대한 응답 수신</summary>
    private void HandleRematchReply(object[] data)
    {
        bool isAccepted = (bool)data[0];
        _isRematchRequested = false;

        if (isAccepted)
        {
            ExecuteFullReset();
        }
        else
        {
            // 거절 메시지 표시
            if (gameHUD != null)
            {
                gameHUD.ShowRematchDeclinedMessage();
            }

            Debug.Log("[GameSession] 재도전 거절 수신");
        }
    }

    /// <summary>
    /// 재도전 수락 시 양쪽 모두 호출되는 전체 리셋.
    /// 리셋 후 Master Client가 역할 재배정을 브로드캐스트.
    /// </summary>
    private void ExecuteFullReset()
    {
        // 패널/플래그 초기화
        if (gameHUD != null)
        {
            gameHUD.HideRematchPanel();
        }

        _isReadySent = false;
        _isRematchRequested = false;
        gameHUD?.SetReadyButtonInteractable(false);
        // 게임 로직 초기화
        skillManager.ResetForRematch();
        gameManager.ResetForRematch();

        // 스킬 선택창 UI 리셋
        if (gameHUD != null)
        {
            gameHUD.skillSelectPanel?.SetActive(false);
        }

        // Master Client가 역할 재배정 → 양쪽 모두 HandleAssignRoles 수신 후 스킬 선택 시작
        if (PhotonNetwork.IsMasterClient)
        {
            RoleAssigner.AssignAndBroadcast();
        }

        Debug.Log("[GameSession] ExecuteFullReset 완료 — 역할 재배정 대기");
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

            case PhotonEventCodes.RematchRequest:
                HandleRematchRequest();
                break;

            case PhotonEventCodes.RematchReply:
                HandleRematchReply((object[])photonEvent.CustomData);
                break;
        }
    }

    #endregion

    #region Photon 콜백

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"[GameSession] {otherPlayer.NickName} 나감 / 현재 상태: {gameManager.currentState}");

        // 재도전 대기 중이었다면 패널 닫기
        if (gameHUD != null)
        {
            gameHUD.HideRematchPanel();
        }

        _isRematchRequested = false;

        gameManager.currentState = GameState.GameOver;

        if (gameHUD)
        {
            gameHUD.ShowOpponentLeft();
        }
    }

    private void OnApplicationQuit()
    {
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }
    }

    public override void OnLeftRoom()
    {
        PhotonNetwork.LoadLevel("Lobby");
    }

    #endregion
}
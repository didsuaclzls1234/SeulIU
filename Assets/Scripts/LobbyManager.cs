using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviourPunCallbacks
{   
    #region 필드
    [Header("닉네임 패널")]
    public GameObject nicknamePanel;
    public TMP_InputField nicknameInputField;
    public Button confirmButton;
    private static string savedNickname = "";//닉네임 저장할 변수


    [Header("모드 선택 패널")]
    public GameObject modeSelectPanel;
    public Button vsAIButton;
    public Button vsPlayerButton;

    [Header("대기 패널")]
    public GameObject waitingPanel;
    public TextMeshProUGUI waitingTimerText;
    public Button cancelButton;

    private float _waitingTime = 0f;
    private bool _isWaiting = false;
    #endregion

    #region 생명주기

    private void Start()
    {
        // 저장된 닉네임 있으면 input 필드에 미리 채워넣기
        if (!string.IsNullOrEmpty(savedNickname))
            nicknameInputField.text = savedNickname;

        ShowNicknamePanel();

        confirmButton.onClick.AddListener(OnConfirmNickname);
        cancelButton.onClick.AddListener(OnClickCancel);

        // 엔터키로도 닉네임 확인
        nicknameInputField.onEndEdit.AddListener((value) =>
        {
            if (Input.GetKeyDown(KeyCode.Return))
                OnConfirmNickname();
        });
    }
    private void Update()
    {
        if (!_isWaiting) return;

        _waitingTime += Time.deltaTime;

        int minutes = Mathf.FloorToInt(_waitingTime / 60f);
        int seconds = Mathf.FloorToInt(_waitingTime % 60f);
        waitingTimerText.text = $"{minutes:00}:{seconds:00}";
    }
    #endregion

    #region 패널 전환

    private void ShowNicknamePanel()
    {
        nicknamePanel.SetActive(true);
        modeSelectPanel.SetActive(false);
        waitingPanel.SetActive(false);
    }

    private void ShowModeSelect()
    {
        nicknamePanel.SetActive(false);
        modeSelectPanel.SetActive(true);
        waitingPanel.SetActive(false);
    }

    private void ShowWaiting()
    {
        nicknamePanel.SetActive(false);
        modeSelectPanel.SetActive(false);
        waitingPanel.SetActive(true);
        _waitingTime = 0f;
        _isWaiting = true;
    }

    #endregion

    #region 닉네임

    private void OnConfirmNickname()
    {
        string nickname = nicknameInputField.text.Trim();
        if (string.IsNullOrEmpty(nickname)) return;

        savedNickname = nickname;
        PhotonNetwork.NickName = nickname;

        ShowModeSelect();
    }

    #endregion

    #region Photon 콜백

    public override void OnConnectedToMaster()
    {
        Debug.Log("[Photon] 마스터 서버 연결 성공");
        PhotonNetwork.JoinLobby(); // 로비 입장 (선택사항)
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("[Photon] 로비 입장 완료 - 매칭 준비됨");
        PhotonNetwork.JoinRandomRoom();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"[Photon] 연결 끊김: {cause}");
    }

     // VS_Player 방 관련 콜백

    // 랜덤 방 참가 성공
    public override void OnJoinedRoom()
    {
        Debug.Log($"[Photon] 방 참가 성공! 현재 인원: {PhotonNetwork.CurrentRoom.PlayerCount}");
        
        // 방장이 씬 이동 (모든 클라이언트 동기화)
        if (PhotonNetwork.IsMasterClient)
        {
            // 2명이 모이면 게임 시작
            if (PhotonNetwork.CurrentRoom.PlayerCount == 2)
            {
                Debug.Log("[Photon] 2명 모이면 VS_Player 씬으로 이동");
                PhotonNetwork.LoadLevel("VS_Player");
            }
        }
    }

    // 방 참가 실패 -> 새 방 생성
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log($"[Photon] 랜덤 방 없음 ({message}) 새 방 생성");

        RoomOptions options = new RoomOptions
        {
            MaxPlayers = 2,
            IsVisible = true,
            IsOpen = true
        };

        PhotonNetwork.CreateRoom(null, options); // null = 랜덤 방 이름
    }

    // 방 생성 성공
    public override void OnCreatedRoom()
    {
        ShowWaiting();
        Debug.Log("[Photon] 방 생성 완료 - 상대방 기다리는 중...");
    }

    // 다른 플레이어가 방에 들어왔을 때
    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        Debug.Log($"[Photon] {newPlayer.NickName} 입장! 현재 인원: {PhotonNetwork.CurrentRoom.PlayerCount}");

        if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom.PlayerCount == 2)
        {
            Debug.Log("[Photon] 2명 모임 → VS_Player 씬 이동");
            PhotonNetwork.LoadLevel("VS_Player");
        }
    }

     #endregion

    #region 버튼 콜백

    // 씬 이동 버튼 함수
    // PhotonNetwork.LoadLevel-> PhotonNetwork를 사용하여 씬 로드 (멀티플레이어용)
    // SceneManager.LoadScene -> 일반적인 씬 로드 (싱글플레이어용)

    public void OnClick_VS_AI()
    {
        Debug.Log("[Scene] VS_AI 씬으로 이동");
        
        SceneManager.LoadScene("VS_AI");
    }

    public void OnClick_VS_Player()
    {   
        Debug.Log("[Photon] VS_Player 버튼 클릭 후 서버 연결 시도");

        PhotonNetwork.AutomaticallySyncScene = true; // 마스터 클라이언트와 씬 동기화 설정

        if (PhotonNetwork.IsConnected)
        {
            // 이미 연결돼 있으면 바로 방 탐색
            PhotonNetwork.JoinRandomRoom();
        }
        else
        {
            // 연결부터 시작 → OnJoinedLobby에서 방 탐색으로 이어짐
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    private void OnClickCancel()
    {
        _isWaiting = false;
        PhotonNetwork.Disconnect();
        ShowModeSelect();
    }

    #endregion
}
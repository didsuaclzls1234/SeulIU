using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyMenuViewController : MonoBehaviourPunCallbacks
{
    [Header("카메라")]
    [SerializeField] private Animator menuCameraAnimator;

    [Header("데모 메뉴")]
    [SerializeField] private GameObject playMenu;
    [SerializeField] private GameObject mainMenu;

    [Header("추가 캔버스")]
    [SerializeField] private GameObject optionsCanvas;
    [SerializeField] private GameObject matchingCanvas;

    [Header("닉네임")]
    [SerializeField] private TMP_InputField nicknameInputField;
    [SerializeField] private TMP_Text errorTextArea;

    [Header("매칭")]
    [SerializeField] private TMP_Text waitingTimerText;

    [Header("씬")]
    [SerializeField] private string gameSceneName = "VS_Player";

    private static readonly int AnimateParameter = Animator.StringToHash("Animate");
    private static string savedNickname = "";

    private float waitingTime;
    private bool isWaitingForOpponent;
    private bool isMatchmakingRequested;
    private bool isCancelRequested;

    private void Start()
    {
        if (nicknameInputField != null && !string.IsNullOrEmpty(savedNickname))
        {
            nicknameInputField.text = savedNickname;
        }

        ShowOptionsCanvas();
        ClearErrorText();
        StopWaitingTimer();
    }

    private void Update()
    {
        if (!isWaitingForOpponent)
        {
            return;
        }

        waitingTime += Time.deltaTime;

        if (waitingTimerText != null)
        {
            int minutes = Mathf.FloorToInt(waitingTime / 60f);
            int seconds = Mathf.FloorToInt(waitingTime % 60f);
            waitingTimerText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    public void TrySelectAiMode()
    {
        if (!TrySaveNickname())
        {
            return;
        }

        LobbyManager.SelectedMode = PlayMode.AI;

        PhotonNetwork.AutomaticallySyncScene = false;

        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }

        SceneManager.LoadScene(gameSceneName);
    }

    public void TrySelectMultiplayerMode()
    {
        if (!TrySaveNickname())
        {
            return;
        }

        LobbyManager.SelectedMode = PlayMode.Multiplayer;
        PhotonNetwork.NickName = savedNickname;
        PhotonNetwork.AutomaticallySyncScene = true;

        ShowMatchingCanvas();
        StartWaitingTimer();
        StartMatchmaking();
    }

    public void CancelMatchmaking()
    {
        isCancelRequested = true;
        isMatchmakingRequested = false;

        StopWaitingTimer();

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
        else if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }

        ReturnToMainCanvas();
    }

    public void ShowOptionsCanvas()
    {
        if (optionsCanvas != null)
        {
            optionsCanvas.SetActive(true);
        }

        if (matchingCanvas != null)
        {
            matchingCanvas.SetActive(false);
        }
    }

    public void ReturnToMainCanvas()
    {
        ShowOptionsCanvas();
        ClearErrorText();

        if (playMenu != null)
        {
            playMenu.SetActive(false);
        }

        if (mainMenu != null)
        {
            mainMenu.SetActive(true);
        }

        if (menuCameraAnimator != null)
        {
            menuCameraAnimator.SetFloat(AnimateParameter, 0f);
        }
    }

    public void ClearErrorText()
    {
        if (errorTextArea != null)
        {
            errorTextArea.text = string.Empty;
        }
    }

    private bool TrySaveNickname()
    {
        string nickname = GetNickname();

        if (string.IsNullOrWhiteSpace(nickname))
        {
            ShowError("Please enter a nickname.");
            return false;
        }

        savedNickname = nickname;
        PhotonNetwork.NickName = nickname;
        ClearErrorText();

        return true;
    }

    private string GetNickname()
    {
        if (nicknameInputField == null)
        {
            return string.Empty;
        }

        return nicknameInputField.text.Trim();
    }

    private void ShowError(string message)
    {
        if (errorTextArea != null)
        {
            errorTextArea.text = message;
        }
    }

    private void ShowMatchingCanvas()
    {
        if (playMenu != null)
        {
            playMenu.SetActive(false);
        }

        if (mainMenu != null)
        {
            mainMenu.SetActive(true);
        }

        if (optionsCanvas != null)
        {
            optionsCanvas.SetActive(false);
        }

        if (matchingCanvas != null)
        {
            matchingCanvas.SetActive(true);
        }

        if (menuCameraAnimator != null)
        {
            menuCameraAnimator.SetFloat(AnimateParameter, 1f);
        }
    }

    private void StartWaitingTimer()
    {
        waitingTime = 0f;
        isWaitingForOpponent = true;

        if (waitingTimerText != null)
        {
            waitingTimerText.text = "00:00";
        }
    }

    private void StopWaitingTimer()
    {
        waitingTime = 0f;
        isWaitingForOpponent = false;

        if (waitingTimerText != null)
        {
            waitingTimerText.text = "00:00";
        }
    }

    private void StartMatchmaking()
    {
        isCancelRequested = false;
        isMatchmakingRequested = true;

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            return;
        }

        if (PhotonNetwork.IsConnectedAndReady)
        {
            JoinPhotonLobby();
        }
        else
        {
            Debug.Log("[Photon] 서버 연결 시도");
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    private void JoinPhotonLobby()
    {
        if (!isMatchmakingRequested)
        {
            return;
        }

        if (PhotonNetwork.InLobby)
        {
            Debug.Log("[Photon] 랜덤 방 참가 시도");
            PhotonNetwork.JoinRandomRoom();
            return;
        }

        Debug.Log("[Photon] 로비 입장 시도");
        PhotonNetwork.JoinLobby();
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("[Photon] 마스터 서버 연결 성공");

        if (isMatchmakingRequested)
        {
            JoinPhotonLobby();
        }
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("[Photon] 로비 입장 완료");

        if (isMatchmakingRequested)
        {
            Debug.Log("[Photon] 랜덤 방 참가 시도");
            PhotonNetwork.JoinRandomRoom();
        }
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        if (!isMatchmakingRequested)
        {
            return;
        }

        Debug.Log($"[Photon] 랜덤 방 없음: {message}");

        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = 2,
            IsVisible = true,
            IsOpen = true
        };

        PhotonNetwork.CreateRoom(null, roomOptions);
    }

    public override void OnCreatedRoom()
    {
        Debug.Log("[Photon] 방 생성 완료 - 상대방 대기 중");
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"[Photon] 방 참가 성공. 현재 인원: {PhotonNetwork.CurrentRoom.PlayerCount}");

        if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom.PlayerCount == 2)
        {
            LoadMultiplayerScene();
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"[Photon] {newPlayer.NickName} 입장. 현재 인원: {PhotonNetwork.CurrentRoom.PlayerCount}");

        if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom.PlayerCount == 2)
        {
            LoadMultiplayerScene();
        }
    }

    public override void OnLeftRoom()
    {
        Debug.Log("[Photon] 방 나감");

        if (isMatchmakingRequested && !isCancelRequested)
        {
            JoinPhotonLobby();
        }
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"[Photon] 연결 끊김: {cause}");

        if (isMatchmakingRequested && !isCancelRequested)
        {
            isMatchmakingRequested = false;
            StopWaitingTimer();
            ReturnToMainCanvas();
            ShowError("Connection failed. Please try again.");
        }
    }

    private void LoadMultiplayerScene()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        isMatchmakingRequested = false;
        StopWaitingTimer();

        Debug.Log("[Photon] 2명 매칭 완료 - 게임 씬 이동");
        PhotonNetwork.LoadLevel(gameSceneName);
    }
}
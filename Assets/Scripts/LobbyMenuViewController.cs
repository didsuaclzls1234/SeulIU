using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

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

    [Header("인트로")]
    [SerializeField] private GameObject pressAnyKeyObject;

    [Header("로고 이미지 (A)")]
    [SerializeField] private RectTransform logoRect;
    [SerializeField] private Vector2 logoEndPos;       // 목표 위치 (인스펙터)
    [SerializeField] private Vector2 logoEndSize;      // 목표 크기 (인스펙터)
    [SerializeField] private float logoAnimDuration = 1f; // 이동 시간 (인스펙터)

    [Header("배경 이미지 (B)")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private float backgroundTargetAlpha = 0f; // 목표 투명도 (인스펙터)
    [SerializeField] private float backgroundFadeDuration = 0f; // 0이면 즉시 (인스펙터)

    private bool isIntroPhase = false;
    private Vector2 logoStartPos;
    private Vector2 logoStartSize;

    private static readonly int AnimateParameter = Animator.StringToHash("Animate");
    private static string savedNickname = "";

    private float waitingTime;
    private bool isWaitingForOpponent;
    private bool isMatchmakingRequested;
    private bool isCancelRequested;

    private void Start()
    {   
        SoundManager.Instance.PlayBGM("LobbyBGM");

        // if (nicknameInputField != null && !string.IsNullOrEmpty(savedNickname))
        // {
        //     nicknameInputField.text = savedNickname;
        // }
        
        // ShowOptionsCanvas();

        if (!string.IsNullOrEmpty(savedNickname))
        {
            // 재진입 시 인트로 스킵
            if (nicknameInputField != null)
                nicknameInputField.text = savedNickname;
            SkipIntro();
        }
        else
        {
            StartIntro();
        }

        ClearErrorText();
        StopWaitingTimer();
        StartCoroutine(DelayedStart());
    }
    private IEnumerator DelayedStart()
    {
        yield return null; // Animator 초기화 후 한 프레임 대기

        mainMenu?.SetActive(false);
        playMenu?.SetActive(false);
        optionsCanvas?.SetActive(false);
        matchingCanvas?.SetActive(false);

        if (!string.IsNullOrEmpty(savedNickname))
        {
            if (nicknameInputField != null)
                nicknameInputField.text = savedNickname;
            SkipIntro();
        }
        else
        {
            StartIntro();
        }
    }
    private void Update()
    {   
        if (isIntroPhase && Input.anyKeyDown)
        {
            isIntroPhase = false;
            StartCoroutine(IntroTransitionRoutine());
        }
        
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
    private void StartIntro()
    {   
        isIntroPhase = true;
        pressAnyKeyObject?.SetActive(true);
        // 현재 위치/크기를 시작값으로 저장
        if (logoRect != null)
        {
            logoStartPos = logoRect.anchoredPosition;
            logoStartSize = logoRect.sizeDelta;
        }
    }

    private void SkipIntro()
    {
        isIntroPhase = false;
        pressAnyKeyObject?.SetActive(false);

        // 로고 즉시 최종 위치로
        if (logoRect != null)
        {
            logoRect.anchoredPosition = logoEndPos;
            logoRect.sizeDelta = logoEndSize;
        }

        // 배경 즉시 투명도 적용
        if (backgroundImage != null)
        {
            Color c = backgroundImage.color;
            c.a = backgroundTargetAlpha;
            backgroundImage.color = c;
        }
        mainMenu?.SetActive(true);
        ShowOptionsCanvas();
    }

    private IEnumerator IntroTransitionRoutine()
    {
        // Press Any Key 비활성화
        pressAnyKeyObject?.SetActive(false);

        // 배경 투명도 처리
        if (backgroundImage != null)
        {
            if (backgroundFadeDuration <= 0f)
            {
                // 즉시 적용
                Color c = backgroundImage.color;
                c.a = backgroundTargetAlpha;
                backgroundImage.color = c;
            }
            else
            {
                StartCoroutine(FadeBackgroundRoutine());
            }
        }

        // 로고 이동/크기 애니메이션
        if (logoRect != null && logoAnimDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < logoAnimDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / logoAnimDuration);
                logoRect.anchoredPosition = Vector2.Lerp(logoStartPos, logoEndPos, t);
                logoRect.sizeDelta = Vector2.Lerp(logoStartSize, logoEndSize, t);
                yield return null;
            }
            logoRect.anchoredPosition = logoEndPos;
            logoRect.sizeDelta = logoEndSize;
        }
        mainMenu?.SetActive(true);
        ShowOptionsCanvas();
    }

    private IEnumerator FadeBackgroundRoutine()
    {
        float elapsed = 0f;
        float startAlpha = backgroundImage.color.a;

        while (elapsed < backgroundFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / backgroundFadeDuration);
            Color c = backgroundImage.color;
            c.a = Mathf.Lerp(startAlpha, backgroundTargetAlpha, t);
            backgroundImage.color = c;
            yield return null;
        }

        Color final = backgroundImage.color;
        final.a = backgroundTargetAlpha;
        backgroundImage.color = final;
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
        // ↓ 추가: 2명 매칭 시 비마스터 클라이언트도 문구 표시
        if (PhotonNetwork.CurrentRoom.PlayerCount == 2)
        {   
            isWaitingForOpponent = false; // ← 타이머 정지
            if (waitingTimerText != null)
                waitingTimerText.text = "매칭완료!";
        }
        if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom.PlayerCount == 2)
        {
            LoadMultiplayerScene();
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"[Photon] {newPlayer.NickName} 입장. 현재 인원: {PhotonNetwork.CurrentRoom.PlayerCount}");
        if (PhotonNetwork.CurrentRoom.PlayerCount == 2)
        {
            // 양쪽 모두 문구 표시
            if (waitingTimerText != null)
                waitingTimerText.text = "매칭완료!";
        }
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
        isWaitingForOpponent = false;
        StopWaitingTimer();
        
        StartCoroutine(LoadSceneWithDelay());
    }
    private IEnumerator LoadSceneWithDelay()
    {
        // 매칭완료 문구 표시
        if (waitingTimerText != null)
            waitingTimerText.text = "매칭완료!";

        yield return new WaitForSeconds(2f);

        PhotonNetwork.LoadLevel(gameSceneName);
    }
}
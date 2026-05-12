using TMPro;
using UnityEngine;
using System.Collections.Generic;
using System;
using System.Threading.Tasks; // Stack 사용


// 현재 게임 진행 상태 (스킬선택중, 게임중, 게임끝)
public enum GameState { WaitingForSkillSelect, Playing, SkillPreview, /*SkillTargeting*/ GameOver }
public enum PlayMode { Solo, AI, Multiplayer } // 플레이 모드 
// [추가] 착수 종류 구분 — PlayerManual은 턴 카운트 감소, SkillInduced는 감소 없음
public enum PlacementType { PlayerManual, SkillInduced }

// 방금 둔 돌의 정보를 기억할 구조체
public struct MoveRecord
{
    public int x;
    public int y;
    public StoneColor playerColor; // <- Enum 적용
    public GameObject stoneObj; // 화면에 생성된 3D 돌
}

public class GameManager : MonoBehaviour
{
    /** 네트워크 전용 액션 (포톤 매니저에서 구독할 채널들)**/   // ** 네트워크 개발자 분은 아래 액션들 구독하셔서 사용하시면 됩니다! 
    public event Action<int, int, int> OnStonePlacedLocally; // 내 턴에 돌 놨을 때(x, y, seq)
    public event Action<StoneColor> OnGameOverLocally;
    public event Action OnUndoRequestedLocally;         // 내가 무르기 버튼 눌렀을 때
    public event Action<bool> OnUndoReplyLocally;       // 내가 상대방의 무르기를 수락/거절했을 때 발송
    public event Action OnRestartRequestedLocally;      // 내가 재시작 버튼 눌렀을 때
    //public event Action<int> OnSkillUsedLocally;      // 스킬 썼을 때 (나중에 확장)
    //public event Action<int, int, int> OnDoubleDownExtraPlaced; // Double Down 스킬로 랜덤 착수했을 때 (x, y, seq)
    // -----------------------------------------------------------------------------

    public BoardManager board;
    public GameHUD gameHUD;
    public TimerManager timerManager;
    public SkillManager skillManager;
    public CameraSwitcher cameraSwitcher;
    
    [Header("Skill State")]
    // public int extraPlacementCount = 0; // 돌 여러번 놓는 스킬(예: 3번 스킬) 사용 시, 남은 추가 착수 횟수 저장용 변수
    // public bool pendingExtraPlacement = false; // 추가 착수 대기 상태 플래그 (네트워크에서 랜덤 착수 패킷을 기다리는 중인지)
    // public bool hasUsedSkillThisTurn = false; // 이번 턴 스킬 사용 여부 (스킬은 한 턴 당 하나만)
    // [제거] extraPlacementCount, pendingExtraPlacement
    public bool hasUsedSkillThisTurn = false;
    public int pendingSkillId = -1; // [추가] B타입 스킬(바로 발동하지 않고, 착수 후 발동 하는 스킬 예약용)

    private StoneColor _currentTurnColor = StoneColor.Black;    // 1: 흑돌(선공), 2: 백돌
    public StoneColor currentTurnColor
    {
        get { return _currentTurnColor; }
        set
        {
            _currentTurnColor = value;
            // 턴이 변경될 때마다 단 한 곳에서 UI를 자동 갱신!
            RefreshHUD();
        }
    }
    public GameState currentState = GameState.WaitingForSkillSelect;

    [Header("Game Settings")]
    public PlayMode currentMode = PlayMode.Solo;              // * 인스펙터에서 모드 변경 가능!
    private StoneColor _localPlayerColor = StoneColor.Black;    // * 내 컴퓨터에 할당된 색상 (1: 흑, 2: 백) -> 나중에 방장이 선택하거나, 서버에서 지정해 값을 던져주면 됨.
    public StoneColor localPlayerColor
    {
        get { return _localPlayerColor; }
        set
        {
            _localPlayerColor = value;
            // 서버가 내 색깔을 정해주면 알아서 텍스트 갱신
            RefreshHUD();
             // 색상 확정 시점에 카메라 회전 적용
            if (cameraSwitcher != null)
                cameraSwitcher.ApplyColorBasedRotation();

            // 색상이 정해질 때 보드판 글씨 방향도 맞춰서 돌려줌!
            if (board != null) board.SetupBoardTextRotation(value);
        }
    }

    // ** 닉네임 저장용 변수 추가
    public string localPlayerName = "나(Player)";
    public string remotePlayerName = "상대방(Opponent)";

    private Stack<MoveRecord> moveHistory = new Stack<MoveRecord>(); // 기보(히스토리)를 저장할 스택
    // [추가] 스킬 로그용 현재 착수 횟수 (읽기 전용)
    public int CurrentMoveCount => moveHistory.Count;
    // 오목 AI 인스턴스
    private GomokuAI aiPlayer;
    private bool isAITurnProcessing = false; // AI가 중복으로 수 연산하는 것을 방지

    [Range(1, 5)]
    public int aiDifficulty = 3;

    void Start()
    {
        // 씬 시작할 때 BoardManager를 자동으로 찾아옴
        if (board == null) board = FindFirstObjectByType<BoardManager>();
        Debug.Log("[GameManager] 게임 셋업! 스킬 선택 대기 중...");

        //Debug.Log("[GameManager] 게임 시작! 흑돌 턴입니다.");

        // [AI 모드 설정] 내가 흑돌(1)이면 AI는 백돌(2), 내가 백돌(2)이면 AI는 흑돌(1)로 생성
        if (currentMode == PlayMode.AI)
        {
            int aiColorInt = (localPlayerColor == StoneColor.Black) ? 2 : 1;

            // 룰 매니저, 바둑판 사이즈, '난이도'를 넘겨줌
            aiPlayer = new GomokuAI(aiColorInt, board.ruleManager, board.boardSize, aiDifficulty, this);

            Debug.Log($"[GameManager] AI 세팅 완료. AI 색상: {(StoneColor)aiColorInt} / 난이도: {aiDifficulty}");

            // AI 스킬 자동 선택 및 준비 완료 처리!
            if (skillManager != null) skillManager.AI_AutoSelectSkills();

        }

        // 게임 시작 직후 1턴(흑돌)의 금수 자리 표시 (첫 턴이라 없겠지만 구조상 필요)
        board.UpdateForbiddenMarks(currentTurnColor);

        // 시작할 때 "흑 - 내 차례" 글씨 띄우기
        RefreshHUD();

        // 타이머 타임아웃 이벤트 구독
        if (timerManager != null)
        {
            timerManager.OnTimeOut += OnTurnTimeOut;
            timerManager.StartSkillSelectTimer();
        }
    }


    // =========================================================
    // 1. 돌 놓기 파트
    // =========================================================
    // (InputManager가 마우스를 클릭하면 이 함수를 호출함)
    public void TryPlaceStone(int x, int y)
    {
        //Debug.Log($"[TryPlaceStone 시작] extraPlacementCount: {extraPlacementCount}");
        if (currentState != GameState.Playing)
        {
            // (선택) 로그로 확인하고 싶으시면 켜두세요
            Debug.Log("현재 돌을 놓을 수 없는 상태입니다. (스킬 선택 중이거나 게임 종료)");
            return;
        }

        // * '솔로 모드'가 아닐 때만 턴 제어 검사 (솔로 모드면 본인이 흑백 다 둠)
        if (currentMode != PlayMode.Solo && currentTurnColor != localPlayerColor)
        {
            Debug.Log("지금은 상대방(또는 AI)의 턴입니다!");
            return;
        }

        // BoardManager한테 돌을 두어도 되는지 물어봄
        if (board.IsValidMove(x, y, currentTurnColor))
        {
             // [수정] PlacementType.PlayerManual 명시, extraPlacementCount 분기 제거
            ExecutePlaceStone(x, y, currentTurnColor, PlacementType.PlayerManual);
 
            if (currentMode == PlayMode.Multiplayer)
                OnStonePlacedLocally?.Invoke(x, y, moveHistory.Count - 1);
 
            if (currentState == GameState.GameOver) return;
 
            PassTurn(currentTurnColor);
        }
    }

    // 네트워크 수신용 함수: 전달받은 Enum과 seq 쓰기
    public void ReceiveNetworkMove(int x, int y, StoneColor receivedColor, int seq)
    {
        Debug.Log($"[ReceiveNetworkMove] currentTurnColor: {currentTurnColor}, receivedColor: {receivedColor}");
        // 검증: 날아온 패킷 색깔이 현재 차례 색깔이 아니면 무시 (네트워크 꼬임/핵 방지)
        if (currentTurnColor != receivedColor)
        {
            Debug.LogWarning("[GameManager] 잘못된 턴의 패킷 수신!");
            return;
        }

        // 실제 돌을 놓고 여기서 스스로 승리 판정을 하도록 유도 (동기화 핵심)
        ExecutePlaceStone(x, y, receivedColor,PlacementType.PlayerManual);

        if (currentState == GameState.GameOver) return;

        // ExtraPlacement가 올 예정이면 턴 넘기기 보류
        // if (!pendingExtraPlacement)
        // {
        //     PassTurn(receivedColor);
        // }
        PassTurn(receivedColor);
    }
     // [수정] PlacementType 파라미터 추가 / pendingSkillId 처리 / skillManager.OnStonePlaced 호출
    private void ExecutePlaceStone(int x, int y, StoneColor playerColor, PlacementType type = PlacementType.PlayerManual)
    {
        // 1. 돌 생성 및 데이터 저장
        GameObject placedStone = board.PlaceStone(x, y, playerColor);

        // 2. 히스토리에 방금 둔 돌 정보 기록 (무르기를 위해)
        if (type == PlacementType.PlayerManual)
        {
            moveHistory.Push(new MoveRecord { x = x, y = y, playerColor = playerColor, stoneObj = placedStone });
        }

        // 3. [리팩토링] 승패 판정 로직 통합 (CheckWin 삭제, GetWinningStones 재활용)
        var winningCoords = board.GetWinningStones(x, y, playerColor);
        if (winningCoords != null)
        {
            // 승리 좌표뿐만 아니라 승리한 돌의 색상(playerColor)도 넘겨줍니다.
            board.HighlightWinningStones(winningCoords, playerColor);
            EndGame(playerColor);
            return;
        }

        // 4. 무승부 판정 (바둑판이 꽉 찼는가?): 돌을 둔 횟수가 (가로 x 세로) 칸 수와 같아지면 꽉 찬 것
        if (moveHistory.Count >= board.boardSize * board.boardSize)
        {
            //Debug.Log("바둑판이 꽉 찼습니다! 무승부!");
            EndGame(StoneColor.None);
            return;
        }

        // 방금 돌을 둔 사람이 투명화 상태라면, 방금 둔 돌도 즉시 투명화 적용!
        if (skillManager != null)
        {
            if (playerColor == localPlayerColor && skillManager.myInvisibilityTurns > 0)
            {
                StartCoroutine(board.BlinkAndHideRoutine(placedStone, playerColor, true));
            }
            else if (playerColor != localPlayerColor && skillManager.oppInvisibilityTurns > 0)
            {
                StartCoroutine(board.BlinkAndHideRoutine(placedStone, playerColor, false));
            }
        }

        // 5. [추가] B타입 스킬 예약 처리 (PlayerManual 착수 시에만)
        if (type == PlacementType.PlayerManual && pendingSkillId != -1)
        {
            skillManager?.ExecutePendingSkill(pendingSkillId, x, y);
            pendingSkillId = -1;
        }
 
        // 6. [추가] 턴 감소를 SkillManager에 위임
        skillManager?.OnStonePlaced(playerColor, type);
        SoundManager.Instance.PlaySFX("PlaceStone");

        //로그기록용
        if (type == PlacementType.PlayerManual)
        {
            string who = (playerColor == localPlayerColor) ? "나" : "상대";
            gameHUD?.RecordMoveLog(CurrentMoveCount, who, x, y, playerColor);
            gameHUD?.UpdateGameLog();
        }
    }

    // [추가] SkillManager에서 SkillInduced 착수 시 호출할 public 래퍼
    public void ExecutePlaceStonePublic(int x, int y, StoneColor playerColor, PlacementType type)
    {
        ExecutePlaceStone(x, y, playerColor, type);
    }

    // public void ReceiveExtraPlacement(int x, int y, StoneColor color)
    // {
    //     Debug.Log($"[ReceiveExtraPlacement] 호출됨 - x:{x}, y:{y}, color:{color}, currentTurnColor:{currentTurnColor}");
    
    //     GameObject placedStone = board.PlaceStone(x, y, color);
    //     if (placedStone == null)
    //     {
    //         Debug.LogWarning("[ReceiveExtraPlacement] PlaceStone 실패 - null 반환");
    //         return;
    //     }

    //     moveHistory.Push(new MoveRecord { x = x, y = y, playerColor = color, stoneObj = placedStone });

    //     // ** 상대방이 투명화 상태인지 확인
    //     bool isOpponentInvisible = (skillManager != null && skillManager.oppInvisibilityTurns > 0);
    //     if (isOpponentInvisible)
    //     {
    //         // 투명화라면 돌을 숨기고, 노란색 깜빡임 효과를 생략
    //         board.ApplyVisibilityToSingleStone(placedStone, color, false, false);
    //     }
    //     else
    //     {
    //         // 안 투명하면 정상적으로 노란색 깜빡임
    //         board.BlinkStoneEffect(placedStone, board.visualSettings.extraPlaceBlinkColor);
    //     }

    //     if (board.CheckWin(x, y, color))
    //     {
    //         EndGame(color);
    //         return;
    //     }

    //     //  플래그 해제 후 턴 넘기기
    //     pendingExtraPlacement = false;
    //     PassTurn(color);
    // }

    // =========================================================
    // AI 턴 처리 로직
    // =========================================================
    private async void ExecuteAITurn()
    {
        if (aiPlayer == null || currentState != GameState.Playing || isAITurnProcessing) return;

        isAITurnProcessing = true;
        Debug.Log("[GameManager] AI가 수를 고민 중입니다...");

        // AI가 연산을 시작할 때의 턴(착수 횟수)을 기억합니다.
        int expectedMoveCount = moveHistory.Count;

        // AI가 사람처럼 고민하는 딜레이 타임 (0.3초)
        await Task.Delay(300);

        // ** AI에게 넘겨줄 봉인(칼날비/봉인) 데이터 복사본 생성
        BoardManager.SealInfo[,] sealedClone = (BoardManager.SealInfo[,])board.sealedGrid.Clone();

        // 비동기로 AI가 최적의 수를 계산 (화면 멈춤 없음)
        Vector2Int aiMove = await aiPlayer.CalculateBestMoveAsync(board.grid, sealedClone);

        isAITurnProcessing = false;

        // 게임이 그 사이에 종료되지 않았다면 착수
        // **연산이 끝났는데, 만약 타임아웃이 터져서 이미 턴이 넘어갔다면 이 수는 폐기합니다!
        if (currentState == GameState.Playing && currentTurnColor != localPlayerColor && moveHistory.Count == expectedMoveCount)
        {
            ExecutePlaceStone(aiMove.x, aiMove.y, currentTurnColor, PlacementType.PlayerManual);

            if (currentState == GameState.GameOver) return;

            PassTurn(currentTurnColor);
        }
        else
        {
            Debug.LogWarning("[GameManager] 이미 타임아웃 등으로 턴이 넘어갔으므로 AI의 지연 착수를 취소합니다.");
        }
    }

    // 게임 종료 로직 통합 (승리 or 무승부)
    private void EndGame(StoneColor winner)
    {
        currentState = GameState.GameOver;
        // extraPlacementCount = 0;

        // UI는 무조건 GameHUD가 처리하도록 위임
        if (gameHUD != null) gameHUD.ShowGameOver(winner, localPlayerColor);

        // ** 네트워크 전송 조건 수정
        // 5목 승리는 각자 계산하므로, 여기서는 내가 이겼을 때만 '확인 사살용'으로 한 번 보냅니다.
        // 무한 루프 방지를 위해 승리자가 보낼 때만 이벤트를 호출합니다.
        if (currentMode == PlayMode.Multiplayer && winner == localPlayerColor)
        {
            OnGameOverLocally?.Invoke(winner);
        }
    }


    // =========================================================
    // 2. 한 수 무르기 (Undo) 파트
    // =========================================================

    //// UI 무르기 버튼에 연결할 함수
    //public void RequestUndo()
    //{
    //    if (currentMode == PlayMode.Multiplayer) return;
    //    if (moveHistory.Count == 0 || currentState == GameState.GameOver) return; // 무를 수단이 없으면 리턴

    //    // 2. 모드별 분기 처리
    //    //if (currentMode == PlayMode.Multiplayer)
    //    //{
    //    //    // [멀티플레이] 상대방에게 "물러도 될까?" 물어보는 패킷을 GameSession이 쏘도록 이벤트 호출
    //    //    // 실제 실행(ExecuteUndo)은 상대가 수락하여 패킷이 돌아왔을 때(ReceiveNetworkUndo) 진행됩니다.
    //    //    OnUndoRequestedLocally?.Invoke();
    //    //    Debug.Log("[Undo] 상대방에게 무르기 동의를 요청합니다...");

    //    //    // 요청하자마자 '대기 팝업'을 띄움 (돌 놓기 방지)
    //    //    if (gameHUD != null) gameHUD.ShowUndoWaitingPopup();
    //    //}
    //    //else
    //    //{
    //        // [Solo / AI] 기다릴 필요 없이 즉시 내 화면에서 무르기 실행
    //        ExecuteUndo();

    //        // ** [AI 모드] 내가 무르기를 하면 턴이 AI로 넘어가서 바로 다시 두어버림. 
    //        // 따라서 AI의 직전 수도 같이 무르기(총 2번 Pop) 처리
    //        if (currentMode == PlayMode.AI && currentTurnColor != localPlayerColor && moveHistory.Count > 0)
    //        {
    //            ExecuteUndo();
    //        }
    //    //}
    //}

    // ** 상대가 무르기를 요청했을 경우 수신됨
    //public void ReceiveNetworkUndoRequest()
    //{
    //    if (currentState == GameState.GameOver) return;
    //    if (gameHUD != null) gameHUD.ShowUndoPopup();
    //}

    // ** 본인이 무르기 요청 후, 수락/거절 응답을 상대방으로부터 수신할 경우
    //public void ReceiveNetworkUndoReply(bool isAccepted)
    //{
    //    if (currentState == GameState.GameOver) return;

    //    // 상대방의 응답을 받으면 HUD에 결과 텍스트 띄우기 (1.5초 뒤 자동 꺼짐)
    //    if (gameHUD != null) gameHUD.ShowUndoResultAndClose(isAccepted);

    //    if (isAccepted) ExecuteUndo();
    //}

    // ** HUD에서 수락/거절 버튼을 눌렀을 때 호출되는 함수 (발신)
    //public void ReplyToUndoRequest(bool isAccepted)
    //{
    //    // 1. 이미 게임이 끝났다면 무르기 팝업 닫고 무시
    //    if (currentState == GameState.GameOver)
    //    {
    //        if (gameHUD != null) gameHUD.undoPopupPanel.SetActive(false);
    //        return;
    //    }

    //    // 2. 수락(true)을 눌렀다면 내 화면에서도 무르기 진행
    //    if (isAccepted)
    //    {
    //        ExecuteUndo();
    //    }

    //    // 3. GameSession 쪽으로 수락/거절 여부(bool) 전달
    //    OnUndoReplyLocally?.Invoke(isAccepted);
    //}

    //// 3. 한 수 무르기 (Undo) 기능
    //public void ExecuteUndo()
    //{
    //    if (moveHistory.Count == 0) return;

    //    // 가장 마지막에 둔 돌 정보 꺼내기
    //    MoveRecord lastMove = moveHistory.Pop();

    //    // 1. 데이터 배열에서 돌 삭제
    //    board.grid[lastMove.x, lastMove.y] = 0;

    //    // 2. 화면에서 3D 돌 오브젝트 없애기
    //    if (lastMove.stoneObj != null) lastMove.stoneObj.SetActive(false);

    //    // 3. 턴 되돌리기 + 화면 글씨 자동 갱신
    //    currentTurnColor = lastMove.playerColor;

    //    // (게임오버 시 무르기 불가이므로 currentState = GameState.Playing 강제 주입은 제거해도 되지만, 만약의 오류 방지용으로 놔둠)
    //    currentState = GameState.Playing;

    //    // 5. 무르기 완료 후 UI(HUD) 턴 글씨 갱신
    //    if (gameHUD != null)
    //    {
    //        gameHUD.resultPanel.SetActive(false);
    //    }

    //    // 6. 무른 후의 턴에 맞춰 금수 마커 다시 계산!
    //    board.UpdateForbiddenMarks(currentTurnColor);

    //    Debug.Log($"[Undo] ({lastMove.x}, {lastMove.y}) 무르기 완료. 현재 턴: {currentTurnColor.ToKorean()}");
    //}

    // =========================================================
    //  3. 재시작 (Restart) 파트
    // =========================================================

    // UI 재시작 버튼에 연결할 함수
    public void RequestRestart()
    {
        ExecuteRestart();

        // 네트워크로 "게임 재시작하자"고 방송 쏘기
        if (currentMode == PlayMode.Multiplayer)
        {
            OnRestartRequestedLocally?.Invoke();
        }
    }
    public void ResetForRematch()
    {
        // 보드 초기화
        board.ClearBoard();
        moveHistory.Clear();
        pendingSkillId       = -1;
        hasUsedSkillThisTurn = false;

        // 시네마틱 카메라 끄고 원래 탑뷰로 강제 멱살 잡고 끌고 옴!
        cameraSwitcher?.ForceTopView();

        // 게임 상태 초기화 (스킬 선택 대기로 복귀)
        _currentTurnColor = StoneColor.Black;
        currentState      = GameState.WaitingForSkillSelect;

        // 결과 패널 숨기기
        if (gameHUD != null)
        {
            gameHUD.resultPanel.SetActive(false);
            //gameHUD.ResetSkillLog(); // 스킬 로그도 싹 비워줍니다.
        }

        // 승리 조건 초기화 (오목이니까 5로 복구)
        if (board.ruleManager != null)
        {
            board.ruleManager.blackRules.winCondition = 5;
            board.ruleManager.whiteRules.winCondition = 5;

            board.ruleManager.SetPreset_Renju();
        }

        // ** AI 모드일 때의 특수 처리
        if (currentMode == PlayMode.AI)
        {
            // 1. 흑/백 랜덤 재배정 (나와 AI의 색깔을 다시 정함)
            StoneColor newPlayerColor = UnityEngine.Random.value < 0.5f ? StoneColor.Black : StoneColor.White;
            localPlayerColor = newPlayerColor;

            // 2. AI 두뇌 재설정 (새로운 색상에 맞춰서)
            int aiColorInt = (localPlayerColor == StoneColor.Black) ? 2 : 1;
            aiPlayer = new GomokuAI(aiColorInt, board.ruleManager, board.boardSize, aiDifficulty, this);

            // 3. UI 텍스트 갱신 (당신은 ~돌입니다 등)
            gameHUD?.DisplayMyRole(localPlayerColor);
            gameHUD?.SetPlayerNames(localPlayerName, "알파오목(AI)");

            // 4. AI 스킬 자동 선택 및 준비 완료 처리
            if (skillManager != null)
            {
                skillManager.ResetForRematch(); // SkillManager 내부 SP/준비상태 초기화
                skillManager.AI_AutoSelectSkills(); // AI가 다시 3번과 패시브를 들고 준비하게 함
            }
        }

        board.UpdateForbiddenMarks(StoneColor.Black);
        SoundManager.Instance.PlayBGM("LobbyBGM");
        Debug.Log("[GameManager] ResetForRematch 완료 — 스킬 선택 대기 상태로 복귀");
    }

    // 포톤이 상대방의 재시작 신호를 받았을 때 찌를 함수
    public void ReceiveNetworkRestart()
    {
        ExecuteRestart();
    }

    // 버튼 이벤트에 연결할 재시작 함수
    public void ExecuteRestart()
    {
        // 1. 보드 매니저에게 판을 치우라고 지시 (SRP 분리)
        board.ClearBoard();
        moveHistory.Clear(); // 재시작 시 스택 비우기
        pendingSkillId = -1; // [수정] extraPlacementCount 대신 pendingSkillId 초기화

        // 솔로 플레이 등 즉시 재시작할 때도 카메라 강제 복귀
        cameraSwitcher?.ForceTopView();

        // 2. 게임 상태와 턴을 흑돌로 초기화
        currentTurnColor = StoneColor.Black;
        currentState = GameState.Playing;

        // 재시작 시 텍스트 다시 숨기기
        if (gameHUD != null) gameHUD.resultPanel.SetActive(false);
        board.UpdateForbiddenMarks(currentTurnColor);

        // 스킬로 변경된 렌주룰 및 승리 조건(칠죄종) 원상 복구!
        if (board.ruleManager != null)
        {
            // 칠죄종 초기화
            board.ruleManager.blackRules.winCondition = 5;
            board.ruleManager.whiteRules.winCondition = 5;

            // 10번 렌주룰 파괴 초기화 (33, 44, 장목 금수 다시 켬)
            board.ruleManager.SetPreset_Renju();
        }

        // ** [AI 모드] 재시작했는데 AI가 흑(선공)이라면 바로 돌을 두게 만듦
        if (currentMode == PlayMode.AI && currentTurnColor != localPlayerColor)
        {
            ExecuteAITurn();
        }
        Debug.Log("[GameManager] 게임 재시작! 흑돌부터 다시 시작합니다.");
    }

    // =========================================================
    // UI (HUD) 턴 텍스트 갱신 헬퍼 함수
    // =========================================================
    private void RefreshHUD()
    {
        if (gameHUD == null) return;

        bool isMyTurn = (currentTurnColor == localPlayerColor);
        gameHUD.UpdateTurnDisplay(currentTurnColor, isMyTurn);
    }

    // ------------------------------------------------------------
    // 게임 시작 전 모든 준비(닉네임, 스킬 선택 등)를 마치고 최종적으로 호출할 함수
    public void StartGameAfterSelection()
    {
        currentState = GameState.Playing;

        // 게임 상태가 Playing으로 바뀔 때 스킬 인스턴스 생성 및 버튼 연결!
        if (skillManager != null) 
        {
            skillManager.GenerateSkillInstances();
            skillManager.AutoActivatePassiveSkills();
        }
        // 타이머 시작 및 첫 턴 금수 표시 등 초기화
        if (timerManager != null) timerManager.StartTurnTimer();
        board.UpdateForbiddenMarks(currentTurnColor);

        // ** [AI 모드] 만약 AI가 선공(흑돌)이라면 스킬 선택 끝나자마자 바로 두어야 함
        if (currentMode == PlayMode.AI && currentTurnColor != localPlayerColor)
        {
            ExecuteAITurn();
        }

        Debug.Log("모든 준비 완료. 게임을 시작합니다.");
    }

    // 돌 착수를 제한 시간 내로 안 했을 경우 (TimerManager에서 시간 초과 이벤트가 발생하면 이 함수를 호출하도록 연결!)
    public void OnTurnTimeOut()
    {
        // ** 스킬 조준(타겟팅) 중에 시간이 다 되었다면 강제 취소 처리
        // [수정] SkillPreview 상태도 취소 대상 추가
        // [수정] SkillTargeting 상태 제거 
        if (/*currentState == GameState.SkillTargeting ||*/ currentState == GameState.SkillPreview)
        {
            Debug.Log("[GameManager] 타임아웃! 스킬 시전이 취소됩니다.");
            if (skillManager != null)
            {
                skillManager.selectedSkillSlot = -1;
            }
            if (board != null)
            {
                board.HideSkillTargetMarkers();
                board.ClearHoverHighlight(); // 조준점 지우기
            }
            pendingSkillId = -1;
            currentState = GameState.Playing; // 다시 플레이 상태로 복구
        }

        if (currentState != GameState.Playing) return;

        // 방어 코드: 내 턴이 아니면 아무것도 안 함 (상대 클라이언트가 알아서 쏘길 기다림)
        if (currentMode == PlayMode.Multiplayer && currentTurnColor != localPlayerColor)
        {
            return;
        }

        Debug.Log("[GameManager] 타임아웃! 강제로 랜덤 착수를 진행합니다.");

        // 보드 매니저에게 안전한 랜덤 좌표를 달라고 요청
        Vector2Int randomMove = board.GetRandomValidMove(currentTurnColor);

        if (randomMove.x != -1)
        {
            // **  TryPlaceStone은 마우스 클릭 전용(내 턴 검사)이므로, 
            // 타임아웃 시에는 ExecutePlaceStone과 PassTurn을 강제로 다이렉트 호출합니다
            ExecutePlaceStone(randomMove.x, randomMove.y, currentTurnColor, PlacementType.PlayerManual);

            if (currentMode == PlayMode.Multiplayer)
                OnStonePlacedLocally?.Invoke(randomMove.x, randomMove.y, moveHistory.Count - 1);

            if (currentState == GameState.GameOver) return;
             // ↓ 추가: 플레이어 시점이라면 탑뷰로 강제 복귀
            cameraSwitcher?.ForceTopView();
            PassTurn(currentTurnColor);
        }
    }

    // 돌을 몇 번 뒀든, 진짜로 턴이 넘어갈 때 딱 1번만 호출되는 턴 통합 관리 함수
     // [수정] PassTurn — SP/투명화/AntiMagic/봉인 감소 로직 제거
    //        모든 감소 처리는 ExecutePlaceStone → skillManager.OnStonePlaced로 이관
    private void PassTurn(StoneColor placedColor)
    {
        // // 1. 봉인 턴 수 감소 (원래 ExecutePlaceStone에 있던 녀석)
        // for (int i = 0; i < board.boardSize; i++)
        // {
        //     for (int j = 0; j < board.boardSize; j++)
        //     {
        //         if (board.sealedGrid[i, j].turns > 0 && board.sealedGrid[i, j].owner != placedColor)
        //         {
        //             board.sealedGrid[i, j].turns--;
        //             if (board.sealedGrid[i, j].turns == 0)
        //             {
        //                 board.RemoveSealEffect(i, j);
        //                 Debug.Log($"({i}, {j}) 봉인이 해제되었습니다.");
        //             }
        //         }
        //     }
        // }

        // // 2. 투명화 턴 감소 (가장 중요! 여기서 딱 1번만 깎음)
        // skillManager?.DecreaseInvisibilityTurns(placedColor);

        // // 3. SP 증가 및 스킬 쿨타임/안티매직 감소
        // skillManager?.AddSPOnTurnEnd(placedColor);
        // skillManager?.DecreaseSealedTurns();

        // // 4. 진짜로 턴 색상 바꾸고 다음 사람 마커 갱신
        // currentTurnColor = currentTurnColor.Opponent();
        // board.UpdateForbiddenMarks(currentTurnColor);
        // hasUsedSkillThisTurn = false;
        currentTurnColor     = currentTurnColor.Opponent();
        board.UpdateForbiddenMarks(currentTurnColor);
        hasUsedSkillThisTurn = false;

        // 턴이 넘어갈 때마다 모드 상관없이 무조건 타이머 재시작! (0초 멈춤 해결)
        if (timerManager != null) timerManager.RestartTurnTimer();

        // 5. AI 모드라면 AI에게 턴 넘김
        if (currentMode == PlayMode.AI && currentTurnColor != localPlayerColor)
        {
            // AI가 스킬 쓸지 말지 먼저 판별
            if (skillManager != null) skillManager.AI_TryUseSkill();

            // 그 다음 돌 두기
            ExecuteAITurn();
        }
    }

    // 메모리 누수 방지를 위한 구독 해제 (OnDestroy 권장)
    private void OnDestroy()
    {
        if (timerManager != null) timerManager.OnTimeOut -= OnTurnTimeOut;
    }

}
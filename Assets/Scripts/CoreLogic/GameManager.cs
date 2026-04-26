using TMPro;
using UnityEngine;
using System.Collections.Generic;
using System;
using System.Threading.Tasks; // Stack 사용


// 현재 게임 진행 상태 (스킬선택중, 게임중, 게임끝)
public enum GameState { WaitingForSkillSelect, Playing, GameOver }
public enum PlayMode { Solo, AI, Multiplayer } // 플레이 모드 

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

    // -----------------------------------------------------------------------------

    public BoardManager board;
    public GameHUD gameHUD;
    public TimerManager timerManager;

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
        }
    }

    // ** 닉네임 저장용 변수 추가
    public string localPlayerName = "나(Player)";
    public string remotePlayerName = "상대방(Opponent)";

    private Stack<MoveRecord> moveHistory = new Stack<MoveRecord>(); // 기보(히스토리)를 저장할 스택

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
            aiPlayer = new GomokuAI(aiColorInt, board.ruleManager, board.boardSize, aiDifficulty);

            Debug.Log($"[GameManager] AI 세팅 완료. AI 색상: {(StoneColor)aiColorInt} / 난이도: {aiDifficulty}");
        }


        // 게임 시작 직후 1턴(흑돌)의 금수 자리 표시 (첫 턴이라 없겠지만 구조상 필요)
        board.UpdateForbiddenMarks(currentTurnColor);

        // 시작할 때 "흑 - 내 차례" 글씨 띄우기
        RefreshHUD();
    }


    // =========================================================
    // 1. 돌 놓기 파트
    // =========================================================
    // (InputManager가 마우스를 클릭하면 이 함수를 호출함)
    public void TryPlaceStone(int x, int y)
    {
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
        if (board.IsValidMove(x, y, currentTurnColor)
            && !board.ruleManager.IsForbiddenMove(x, y, (int)currentTurnColor, board.grid, board.boardSize, false))
        {
            // 실제 돌 놓는 코어 로직 실행
            ExecutePlaceStone(x, y, currentTurnColor);

            // ** 내 화면에 돌을 성공적으로 놨다면 네트워크로 방송 쏘기
            if (currentMode == PlayMode.Multiplayer)
            {
                // seq = 현재까지 놓은 돌의 개수(인덱스)
                int currentSeq = moveHistory.Count - 1;
                OnStonePlacedLocally?.Invoke(x, y, currentSeq);
            }
        }
    }

    // 네트워크 수신용 함수: 전달받은 Enum과 seq 쓰기
    public void ReceiveNetworkMove(int x, int y, StoneColor receivedColor, int seq)
    {
        // 검증: 날아온 패킷 색깔이 현재 차례 색깔이 아니면 무시 (네트워크 꼬임/핵 방지)
        if (currentTurnColor != receivedColor)
        {
            Debug.LogWarning("[GameManager] 잘못된 턴의 패킷 수신!");
            return;
        }

        ExecutePlaceStone(x, y, receivedColor);
    }

    private void ExecutePlaceStone(int x, int y, StoneColor playerColor)
    {
        // 1. 돌 생성 및 데이터 저장
        GameObject placedStone = board.PlaceStone(x, y, playerColor);

        // 2. 히스토리에 방금 둔 돌 정보 기록 (무르기를 위해)
        moveHistory.Push(new MoveRecord { x = x, y = y, playerColor = playerColor, stoneObj = placedStone });

        // 3. 승패 판정 (방금 놓은 돌 좌표 기준)
        if (board.CheckWin(x, y, playerColor))
        {
            EndGame(playerColor);
            return; // 더 이상 턴을 넘기지 않고 함수 종료
        }

        // 4. 무승부 판정 (바둑판이 꽉 찼는가?): 돌을 둔 횟수가 (가로 x 세로) 칸 수와 같아지면 꽉 찬 것
        if (moveHistory.Count >= board.boardSize * board.boardSize)
        {
            //Debug.Log("바둑판이 꽉 찼습니다! 무승부!");
            EndGame(StoneColor.None);
            return;
        }

        // 5. 승부가 안 났다면 턴 넘기기 
        currentTurnColor = currentTurnColor.Opponent();

        // 다음 사람의 턴으로 바뀌었으니 ❌ 마커 갱신
        board.UpdateForbiddenMarks(currentTurnColor);

        // 6. 만약 AI 모드이고, 방금 턴을 넘겨받은 게 AI라면 AI 연산 시작
        if (currentState == GameState.Playing && currentMode == PlayMode.AI && currentTurnColor != localPlayerColor)
        {
            ExecuteAITurn();
        }
    }

    // =========================================================
    // AI 턴 처리 로직
    // =========================================================
    private async void ExecuteAITurn()
    {
        if (aiPlayer == null || currentState != GameState.Playing || isAITurnProcessing) return;

        isAITurnProcessing = true;
        Debug.Log("[GameManager] AI가 수를 고민 중입니다...");

        // 1. UI 버튼 비활성화 (버튼이 회색으로 변하고 클릭 안 됨)
        if (gameHUD != null) gameHUD.SetInteractableButtons(false);

        // AI가 사람처럼 고민하는 딜레이 타임 (0.3초)
        await Task.Delay(300);

        // 비동기로 AI가 최적의 수를 계산 (화면 멈춤 없음)
        Vector2Int aiMove = await aiPlayer.CalculateBestMoveAsync(board.grid);

        isAITurnProcessing = false;

        // 2. 연산이 끝났으니 UI 버튼 다시 활성화
        if (gameHUD != null) gameHUD.SetInteractableButtons(true);

        // 게임이 그 사이에 종료되지 않았다면 착수
        if (currentState == GameState.Playing && currentTurnColor != localPlayerColor)
        {
            ExecutePlaceStone(aiMove.x, aiMove.y, currentTurnColor);
        }
    }

    // 게임 종료 로직 통합 (승리 or 무승부)
    private void EndGame(StoneColor winner)
    {
        currentState = GameState.GameOver;

        // UI는 무조건 GameHUD가 처리하도록 위임!
        if (gameHUD != null) gameHUD.ShowGameOver(winner, localPlayerColor);

        // 내가 둬서 끝났을 때만 네트워크로 '게임 끝' 방송
        if (currentMode == PlayMode.Multiplayer && (winner == localPlayerColor || winner == StoneColor.None))
        {
            OnGameOverLocally?.Invoke(winner);
        }
    }


    // =========================================================
    // 2. 한 수 무르기 (Undo) 파트
    // =========================================================

    // UI 무르기 버튼에 연결할 함수
    public void RequestUndo()
    {
        if (moveHistory.Count == 0 || currentState == GameState.GameOver) return; // 무를 수단이 없으면 리턴

        // 2. 모드별 분기 처리
        if (currentMode == PlayMode.Multiplayer)
        {
            // [멀티플레이] 상대방에게 "물러도 될까?" 물어보는 패킷을 GameSession이 쏘도록 이벤트 호출
            // 실제 실행(ExecuteUndo)은 상대가 수락하여 패킷이 돌아왔을 때(ReceiveNetworkUndo) 진행됩니다.
            OnUndoRequestedLocally?.Invoke();
            Debug.Log("[Undo] 상대방에게 무르기 동의를 요청합니다...");

            // 요청하자마자 '대기 팝업'을 띄움 (돌 놓기 방지)
            if (gameHUD != null) gameHUD.ShowUndoWaitingPopup();
        }
        else
        {
            // [Solo / AI] 기다릴 필요 없이 즉시 내 화면에서 무르기 실행
            ExecuteUndo();

            // ** [AI 모드] 내가 무르기를 하면 턴이 AI로 넘어가서 바로 다시 두어버림. 
            // 따라서 AI의 직전 수도 같이 무르기(총 2번 Pop) 처리
            if (currentMode == PlayMode.AI && currentTurnColor != localPlayerColor && moveHistory.Count > 0)
            {
                ExecuteUndo();
            }
        }
    }

    // ** 상대가 무르기를 요청했을 경우 수신됨
    public void ReceiveNetworkUndoRequest()
    {
        if (currentState == GameState.GameOver) return;
        if (gameHUD != null) gameHUD.ShowUndoPopup();
    }

    // ** 본인이 무르기 요청 후, 수락/거절 응답을 상대방으로부터 수신할 경우
    public void ReceiveNetworkUndoReply(bool isAccepted)
    {
        if (currentState == GameState.GameOver) return;

        // 상대방의 응답을 받으면 HUD에 결과 텍스트 띄우기 (1.5초 뒤 자동 꺼짐)
        if (gameHUD != null) gameHUD.ShowUndoResultAndClose(isAccepted);

        if (isAccepted) ExecuteUndo();
    }

    // ** HUD에서 수락/거절 버튼을 눌렀을 때 호출되는 함수 (발신)
    public void ReplyToUndoRequest(bool isAccepted)
    {
        // 1. 이미 게임이 끝났다면 무르기 팝업 닫고 무시
        if (currentState == GameState.GameOver)
        {
            if (gameHUD != null) gameHUD.undoPopupPanel.SetActive(false);
            return;
        }

        // 2. 수락(true)을 눌렀다면 내 화면에서도 무르기 진행
        if (isAccepted)
        {
            ExecuteUndo();
        }

        // 3. GameSession 쪽으로 수락/거절 여부(bool) 전달
        OnUndoReplyLocally?.Invoke(isAccepted);
    }

    // 3. 한 수 무르기 (Undo) 기능
    public void ExecuteUndo()
    {
        if (moveHistory.Count == 0) return;

        // 가장 마지막에 둔 돌 정보 꺼내기
        MoveRecord lastMove = moveHistory.Pop();

        // 1. 데이터 배열에서 돌 삭제
        board.grid[lastMove.x, lastMove.y] = 0;

        // 2. 화면에서 3D 돌 오브젝트 없애기
        if (lastMove.stoneObj != null) lastMove.stoneObj.SetActive(false);

        // 3. 턴 되돌리기 + 화면 글씨 자동 갱신
        currentTurnColor = lastMove.playerColor;

        // (게임오버 시 무르기 불가이므로 currentState = GameState.Playing 강제 주입은 제거해도 되지만, 만약의 오류 방지용으로 놔둠)
        currentState = GameState.Playing;

        // 5. 무르기 완료 후 UI(HUD) 턴 글씨 갱신
        if (gameHUD != null)
        {
            gameHUD.resultPanel.SetActive(false);
        }

        // 6. 무른 후의 턴에 맞춰 금수 마커 다시 계산!
        board.UpdateForbiddenMarks(currentTurnColor);

        Debug.Log($"[Undo] ({lastMove.x}, {lastMove.y}) 무르기 완료. 현재 턴: {currentTurnColor.ToKorean()}");
    }

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

        // 2. 게임 상태와 턴을 흑돌로 초기화
        currentTurnColor = StoneColor.Black;
        currentState = GameState.Playing;

        // 재시작 시 텍스트 다시 숨기기
        if (gameHUD != null) gameHUD.resultPanel.SetActive(false);
        board.UpdateForbiddenMarks(currentTurnColor);

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

        // 타이머 시작 및 첫 턴 금수 표시 등 초기화
        if (timerManager != null) timerManager.StartTimer();
        board.UpdateForbiddenMarks(currentTurnColor);

        // ** [AI 모드] 만약 AI가 선공(흑돌)이라면 스킬 선택 끝나자마자 바로 두어야 함
        if (currentMode == PlayMode.AI && currentTurnColor != localPlayerColor)
        {
            ExecuteAITurn();
        }

        Debug.Log("모든 준비 완료. 게임을 시작합니다.");
    }

}
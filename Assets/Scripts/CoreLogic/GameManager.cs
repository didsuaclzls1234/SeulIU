using TMPro;
using UnityEngine;
using System.Collections.Generic;
using System; // Stack 사용


// 현재 게임 진행 상태 (게임중, 게임끝)
public enum GameState { Playing, GameOver }
public enum PlayMode { Solo, AI, Multiplayer } // 플레이 모드 

// 방금 둔 돌의 정보를 기억할 구조체
public struct MoveRecord
{
    public int x;
    public int y;
    public int player;
    public GameObject stoneObj; // 화면에 생성된 3D 돌
}

public class GameManager : MonoBehaviour
{
    /** 네트워크 전용 액션 (포톤 매니저에서 구독할 채널들)**/   // ** 네트워크 개발자 분은 아래 액션들 구독하셔서 사용하시면 됩니다! 
    public event Action<int, int> OnStonePlacedLocally; // 내 턴에 돌 놨을 때
    public event Action OnUndoRequestedLocally;         // 내가 무르기 버튼 눌렀을 때
    public event Action OnRestartRequestedLocally;      // 내가 재시작 버튼 눌렀을 때
    //public event Action<int> OnSkillUsedLocally;      // 스킬 썼을 때 (나중에 확장)

    // -----------------------------------------------------------------------------

    public BoardManager board;
    public int currentTurn = 1;                  // 1: 흑돌(선공), 2: 백돌
    public GameState currentState = GameState.Playing;

    [Header("Game Settings")]
    public PlayMode currentMode = PlayMode.Solo; // * 인스펙터에서 모드 변경 가능!
    public int localPlayerType = 1;              // * 내 컴퓨터에 할당된 색상 (1: 흑, 2: 백) -> 나중에 방장이 선택하거나, 서버에서 지정해 값을 던져주면 됨.

    [Header("UI Elements")]
    public TextMeshProUGUI winnerText;           // 승리 메시지를 띄울 텍스트 UI

    private Stack<MoveRecord> moveHistory = new Stack<MoveRecord>(); // 기보(히스토리)를 저장할 스택

    void Start()
    {
        // 씬 시작할 때 BoardManager를 자동으로 찾아옴
        if (board == null) board = FindFirstObjectByType<BoardManager>();
        Debug.Log("[GameManager] 게임 시작! 흑돌 턴입니다.");

        // 시작할 때 텍스트 숨기기
        if (winnerText != null) winnerText.gameObject.SetActive(false);

        // 게임 시작 직후 1턴(흑돌)의 금수 자리 표시 (첫 턴이라 없겠지만 구조상 필요)
        board.UpdateForbiddenMarks(currentTurn); 
    }


    // =========================================================
    // 1. 돌 놓기 파트
    // =========================================================
    // (InputManager가 마우스를 클릭하면 이 함수를 호출함)
    public void TryPlaceStone(int x, int y)
    {
        // 이미 게임이 끝났다면 클릭 무시
        if (currentState == GameState.GameOver) return;

        // * '솔로 모드'가 아닐 때만 턴 제어 검사 (솔로 모드면 본인이 흑백 다 둠)
        if (currentMode != PlayMode.Solo && currentTurn != localPlayerType)
        {
            Debug.Log("지금은 상대방의 턴입니다!");
            return;
        }

        // BoardManager한테 돌을 두어도 되는지 물어봄
        if (board.IsValidMove(x, y, currentTurn) && !board.ruleManager.IsForbiddenMove(x, y, currentTurn, board.grid, board.boardSize, false))
        {
            // 실제 돌 놓는 코어 로직 실행
            ExecutePlaceStone(x, y, currentTurn);

            // ** 내 화면에 돌을 성공적으로 놨다면 네트워크로 방송 쏘기
            if (currentMode == PlayMode.Multiplayer)
            {
                OnStonePlacedLocally?.Invoke(x, y);
            }
        }
    }

    public void ReceiveNetworkMove(int x, int y)
    {
        // 상대방이 놓은 돌이면 바로 코어 로직 실행
        if (currentTurn != localPlayerType)
        {
            ExecutePlaceStone(x, y, currentTurn);
        }
    }

    private void ExecutePlaceStone(int x, int y, int playerType)
    {
        // 1. 돌 생성 및 데이터 저장
        GameObject placedStone = board.PlaceStone(x, y, currentTurn);

        // 2. 히스토리에 방금 둔 돌 정보 기록 (무르기를 위해)
        moveHistory.Push(new MoveRecord { x = x, y = y, player = currentTurn, stoneObj = placedStone });

        // 3. 승패 판정 (방금 놓은 돌 좌표 기준)
        if (board.CheckWin(x, y, currentTurn))
        {
            string winnerName = (currentTurn == 1) ? "흑돌" : "백돌";
            Debug.Log($"🎉[GameManager] 게임 종료! {winnerName} 승리!");

            // 승리 시 텍스트 내용 바꾸고 화면에 켜기
            if (winnerText != null)
            {
                winnerText.text = $"{winnerName} 승리!";
                winnerText.gameObject.SetActive(true);
            }

            currentState = GameState.GameOver; // 상태를 게임오버로 변경
            return; // 더 이상 턴을 넘기지 않고 함수 종료
        }

        // 3. 승부가 안 났다면 턴 넘기기
        currentTurn = (currentTurn == 1) ? 2 : 1;

        // 다음 사람의 턴으로 바뀌었으니 ❌ 마커 갱신
        board.UpdateForbiddenMarks(currentTurn);

        //string nextTurn = (currentTurn == 1) ? "흑돌" : "백돌";
        //Debug.Log($"[GameManager] 턴 종료! 다음 턴: {nextTurn}");
    }


    // =========================================================
    // 2. 한 수 무르기 (Undo) 파트
    // =========================================================

    // UI 무르기 버튼에 연결할 함수
    public void RequestUndo()
    {
        if (moveHistory.Count == 0) return; // 무를 수단이 없으면 리턴

        ExecuteUndo(); // 내 화면 무르기

        // * 네트워크로 "나 물렀어! 너도 물러!" 방송 쏘기
        if (currentMode == PlayMode.Multiplayer)
        {
            OnUndoRequestedLocally?.Invoke();
        }
    }

    // 포톤이 상대방의 무르기 신호를 받았을 때 찌를 함수
    public void ReceiveNetworkUndo()
    {
        ExecuteUndo(); // 이벤트(방송) 발생 없이 화면만 조용히 갱신
    }

    // 3. 한 수 무르기 (Undo) 기능
    public void ExecuteUndo()
    {
        // 가장 마지막에 둔 돌 정보 꺼내기
        MoveRecord lastMove = moveHistory.Pop();

        // 1. 데이터 배열에서 돌 삭제
        board.grid[lastMove.x, lastMove.y] = 0;

        // 2. 화면에서 3D 돌 오브젝트 파괴
        if (lastMove.stoneObj != null) Destroy(lastMove.stoneObj);

        // 3. 턴 되돌리기
        currentTurn = lastMove.player;

        // 4. 혹시 게임오버 상태였다면 다시 플레이 상태로 복구
        currentState = GameState.Playing;
        if (winnerText != null) winnerText.gameObject.SetActive(false);

        // 5. 무른 후의 턴에 맞춰 금수 마커 다시 계산!
        board.UpdateForbiddenMarks(currentTurn);

        Debug.Log($"[Undo] ({lastMove.x}, {lastMove.y}) 무르기 완료. 현재 턴: {(currentTurn == 1 ? "흑" : "백")}");
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

        // 2. 게임 상태와 턴을 흑돌(1)로 초기화
        currentTurn = 1;
        currentState = GameState.Playing;

        // 재시작 시 텍스트 다시 숨기기
        if (winnerText != null) winnerText.gameObject.SetActive(false);

        Debug.Log("[GameManager] 게임 재시작! 흑돌부터 다시 시작합니다.");

        board.UpdateForbiddenMarks(currentTurn);
    }

}
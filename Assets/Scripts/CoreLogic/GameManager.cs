using TMPro;
using UnityEngine;


// 현재 게임 진행 상태 (게임중, 게임끝)
public enum GameState { Playing, GameOver }

public class GameManager : MonoBehaviour
{
    public BoardManager board;
    public int currentTurn = 1; // 1: 흑돌(선공), 2: 백돌
    public GameState currentState = GameState.Playing;

    [Header("UI Elements")]
    public TextMeshProUGUI winnerText; // 승리 메시지를 띄울 텍스트 UI

    void Start()
    {
        // 씬 시작할 때 BoardManager를 자동으로 찾아옴
        if (board == null) board = FindFirstObjectByType<BoardManager>();
        Debug.Log("[GameManager] 게임 시작! 흑돌 턴입니다.");

        // 시작할 때 텍스트 숨기기
        if (winnerText != null) winnerText.gameObject.SetActive(false);
    }

    // * InputManager가 마우스를 클릭하면 이 함수를 호출함
    public void TryPlaceStone(int x, int y)
    {
        // 이미 게임이 끝났다면 클릭 무시
        if (currentState == GameState.GameOver) return;

        // BoardManager한테 돌을 두어도 되는지 물어봄
        if (board.IsValidMove(x, y))
        {
            // 1. 돌 배치 (배열 데이터 갱신)
            board.PlaceStone(x, y, currentTurn);

            // 2. 승패 판정! (방금 놓은 돌 좌표 기준)
            if (board.CheckWin(x, y, currentTurn))
            {
                string winnerName = (currentTurn == 1) ? "흑돌" : "백돌";
                Debug.Log($"🎉[GameManager] 게임 종료! {winnerName} 승리!");
                
                // 승리 시 텍스트 내용 바꾸고 화면에 켜기!
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
            string nextTurn = (currentTurn == 1) ? "흑돌" : "백돌";
            Debug.Log($"[GameManager] 턴 종료! 다음 턴: {nextTurn}");
        }
    }

    // 버튼 이벤트에 연결할 재시작 함수
    public void RestartGame()
    {
        // 1. 보드 매니저에게 판을 치우라고 지시 (SRP 분리)
        board.ClearBoard();

        // 2. 게임 상태와 턴을 흑돌(1)로 초기화
        currentTurn = 1;
        currentState = GameState.Playing;

        // 재시작 시 텍스트 다시 숨기기
        if (winnerText != null) winnerText.gameObject.SetActive(false);

        Debug.Log("[GameManager] 게임 재시작! 흑돌부터 다시 시작합니다.");
    }

}
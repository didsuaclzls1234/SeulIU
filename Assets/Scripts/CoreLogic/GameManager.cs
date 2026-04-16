using UnityEngine;

public class GameManager : MonoBehaviour
{
    public BoardManager board;
    public int currentTurn = 1; // 1: 흑돌(선공), 2: 백돌

    void Start()
    {
        // 씬 시작할 때 BoardManager를 자동으로 찾아옴
        if (board == null) board = FindFirstObjectByType<BoardManager>();
        Debug.Log("[GameManager] 게임 시작! 흑돌 턴입니다.");
    }

    // * InputManager가 마우스를 클릭하면 이 함수를 호출함
    public void TryPlaceStone(int x, int y)
    {
        // BoardManager한테 돌을 두어도 되는지 물어봄
        if (board.IsValidMove(x, y))
        {
            // 1. 돌 배치 (배열 데이터 갱신)
            board.PlaceStone(x, y, currentTurn);

            // 2. 턴 넘기기
            currentTurn = (currentTurn == 1) ? 2 : 1;
            string nextTurn = (currentTurn == 1) ? "흑돌" : "백돌";
            Debug.Log($"[GameManager] 턴 종료! 다음 턴: {nextTurn}");
        }
    }
}
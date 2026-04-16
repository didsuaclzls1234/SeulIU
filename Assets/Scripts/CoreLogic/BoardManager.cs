using UnityEngine;

public class BoardManager : MonoBehaviour
{
    [Header("Board Settings")]
    public int boardSize = 15; // 오목은 보통 15x15 격자를 씁니다.
    public float gridSize = 1f; // ⭐️ InputManager의 gridSize와 동일해야 함!

    [Header("Prefabs")] // ** 추후 아트 작업 마무리 시 교체
    public GameObject blackStonePrefab; // 흑돌 프리팹
    public GameObject whiteStonePrefab; // 백돌 프리팹


    // 2차원 배열 데이터 (0: 빈칸, 1: 흑돌, 2: 백돌)
    public int[,] grid;

    void Start()
    {
        // 게임 시작과 동시에 15x15 짜리 빈 배열 생성
        grid = new int[boardSize, boardSize];
        Debug.Log($"[BoardManager] {boardSize}x{boardSize} 오목판 데이터 생성 완료!");
    }

    // ⭐️ 1: 돌을 둘 수 있는 정상적인 위치인지 검사
    public bool IsValidMove(int x, int y)
    {
        // 1. 바둑판 범위를 벗어났는가? (IndexOutOfRange 에러 방어)
        if (x < 0 || x >= boardSize || y < 0 || y >= boardSize)
        {
            Debug.LogWarning("[BoardManager] 바둑판 범위를 벗어났습니다!");
            return false;
        }

        // 2. 이미 돌이 놓여져 있는가? (0이어야 빈칸)
        if (grid[x, y] != 0)
        {
            Debug.LogWarning("[BoardManager] 이미 돌이 있는 자리입니다!");
            return false;
        }

        return true;
    }

    // ⭐️ 2: 실제로 배열에 데이터 집어넣기
    public void PlaceStone(int x, int y, int playerType)
    {
        if (IsValidMove(x, y))
        {
            // 1. 데이터 배열 갱신
            grid[x, y] = playerType;

            // 2. 어떤 돌을 생성할지 결정
            GameObject prefabToSpawn = (playerType == 1) ? blackStonePrefab : whiteStonePrefab;

            // 3. 돌이 나타날 실제 3D 위치 (바닥 파묻힘 방지용 Y: 0.2f)
            Vector3 spawnPos = new Vector3(x * gridSize, 0.2f, y * gridSize);

            // 4. 실제로 씬에 3D 모델 생성
            Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);

            Debug.Log($"[BoardManager] 좌표 ({x}, {y})에 3D 돌 생성 완료!");
        }
    }

    // ⭐️ 개발자용 격자 그리기 (유니티 에디터 화면에만 보이는 선)
    void OnDrawGizmos()
    {
        Gizmos.color = Color.black; // 선 색깔

        // 가로선, 세로선 15줄씩 긋기
        for (int i = 0; i < boardSize; i++)
        {
            // 세로선 (Z축 방향으로 쭉 긋기)
            Gizmos.DrawLine(new Vector3(i * gridSize, 0.01f, 0), new Vector3(i * gridSize, 0.01f, (boardSize - 1) * gridSize));
            // 가로선 (X축 방향으로 쭉 긋기)
            Gizmos.DrawLine(new Vector3(0, 0.01f, i * gridSize), new Vector3((boardSize - 1) * gridSize, 0.01f, i * gridSize));
        }
    }

}
using System.Collections.Generic;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    public RuleManager ruleManager;

    [Header("Board Settings")]
    public int boardSize = 19;  // 19x19 격자
    public float gridSize = 1f; // -> InputManager의 gridSize와 동일해야 함!

    [Header("Prefabs")]         // ** 추후 아트 작업 마무리 시 교체
    public GameObject blackStonePrefab; // 흑돌 프리팹
    public GameObject whiteStonePrefab; // 백돌 프리팹
    public GameObject forbiddenMarkPrefab; // ❌(못 놓는 자리 표시) 프리팹

    [Header("Camera Auto Setup")]
    public Camera mainCamera; // 인스펙터에서 MainCamera 연결
    public float cameraPadding = 2f; // 화면 가장자리 여백

    // 2차원 배열 데이터 (0: 빈칸, 1: 흑돌, 2: 백돌)
    public int[,] grid;

    // 생성된 바둑돌들을 기억해둘 리스트 ('다시하기' 기능 시 필요) 
    private List<GameObject> activeStones = new List<GameObject>();
    private List<GameObject> forbiddenMarks = new List<GameObject>(); // '❌' 마커들을 담아둘 리스트


    void Awake()
    {
        // 게임 시작과 동시에 15x15 짜리 빈 배열 생성
        grid = new int[boardSize, boardSize];
        Debug.Log($"[BoardManager] {boardSize}x{boardSize} 오목판 데이터 생성 완료!");

        AdjustCameraToBoardSize(); // 시작 시 카메라 자동 세팅!
    }

    // 1: 돌을 둘 수 있는 정상적인 위치인지 검사
    public bool IsValidMove(int x, int y, StoneColor playerColor)
    {
        // 1-1. 바둑판 범위를 벗어났는가? (IndexOutOfRange 에러 방어)
        if (x < 0 || x >= boardSize || y < 0 || y >= boardSize)
        {
            //Debug.LogWarning("[BoardManager] 바둑판 범위를 벗어났습니다!");
            return false;
        }

        // 1-2. 이미 돌이 놓여져 있는가? (0이어야 빈칸)
        if (grid[x, y] != 0)
        {
            Debug.LogWarning("[BoardManager] 이미 돌이 있는 자리입니다!");
            return false;
        }

        // 1-3. 현재 오목 규칙 기준으로, 돌을 놔도 되는지 RuleManager에게 검사 요청
        if (ruleManager != null && ruleManager.IsForbiddenMove(x, y, (int)playerColor, grid, boardSize))
        {
            Debug.LogWarning("❌ 금수 자리입니다! 돌을 놓을 수 없습니다.");
            return false;
        }

        return true;
    }

    // 2: 실제로 배열에 데이터 집어넣기
    public GameObject PlaceStone(int x, int y, StoneColor playerColor)
    {
        if (IsValidMove(x, y, playerColor))
        {
            // 2-1. 데이터 배열 갱신
            grid[x, y] = (int)playerColor;

            // 2-2. 어떤 돌을 생성할지 결정
            string poolTag = (playerColor == StoneColor.Black) ? "BlackStone" : "WhiteStone";

            // 2-3. 돌이 나타날 실제 3D 위치 (바닥 파묻힘 방지용 Y: 0.2f)
            Vector3 spawnPos = new Vector3(x * gridSize, 0.2f, y * gridSize);

            // 2-4. 실제로 씬에 3D 모델 생성
            GameObject newStone = ObjectPooler.Instance.SpawnFromPool(poolTag, spawnPos, Quaternion.identity);  // 생성한 돌을 변수에 담고            
            activeStones.Add(newStone); // 리스트에 추가해서 기억해둠

            Debug.Log($"[BoardManager] 좌표 ({x}, {y})에 3D 돌 생성 완료!");

            // 성공적으로 돌을 만들었다면 그 돌을 반환(Return)해줌
            return newStone;
        }

        // 만약 금수 자리거나 룰에 막혀서 돌을 못 놓았다면 빈 값(null) 반환
        return null;
    }

    // 전체 바둑판을 스캔해서 ❌ 마커를 그리는 함수
    public void UpdateForbiddenMarks(StoneColor currentPlayerColor)
    {
        // 1. 이전 턴에 그려둔 ❌ 마커들 싹 지우기
        foreach (GameObject mark in forbiddenMarks) Destroy(mark);
        forbiddenMarks.Clear();

        if (ruleManager == null) return;

        // 2. 바둑판 19x19 전체를 순회
        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                // 이미 돌이 있는 자리는 패스
                if (grid[x, y] != 0) continue;

                // [최적화] 주변 2칸 이내에 돌이 하나도 없으면 금수가 될 확률 0% -> 검사 스킵
                if (!HasNeighborInRadius(x, y, 2)) continue;

                // (silent 모드: true로 켜서 로그 도배 없이 조용히 검사만 하도록)
                if (ruleManager.IsForbiddenMove(x, y, (int)currentPlayerColor, grid, boardSize, true))
                {
                    // 금수 자리라면 ❌ 프리팹 생성 (바닥에 안 파묻히게 높이를 0.1f로 띄움)
                    Vector3 pos = new Vector3(x * gridSize, 0.1f, y * gridSize);
                    GameObject newMark = ObjectPooler.Instance.SpawnFromPool("ForbiddenMark", pos, Quaternion.Euler(90, 0, 0));
                    forbiddenMarks.Add(newMark);
                }
            }
        }
    }

    // 재시작 시 호출할 데이터 정리 함수
    public void ClearBoard()
    {
        // 씬에 있는 모든 3D 돌 삭제
        foreach (GameObject stone in activeStones) stone.SetActive(false);
        activeStones.Clear();

        // ❌ 마커도 청소
        foreach (GameObject mark in forbiddenMarks) mark.SetActive(false);
        forbiddenMarks.Clear();

        // 2차원 배열 데이터 초기화 (0으로 덮어쓰기)
        System.Array.Clear(grid, 0, grid.Length);

        Debug.Log("[BoardManager] 바둑판 데이터 및 바둑돌 초기화 완료!");
    }


    // 1. 전체 방향을 검사하는 메인 함수
    public bool CheckWin(int x, int y, StoneColor playerColor)
    {
        // 💡 룰 매니저에게 현재 이 색깔 돌의 승리 조건이 뭔지 물어봄!
        int currentWinCond = ruleManager.GetWinCondition((int)playerColor);

        // 4가지 축을 검사합니다. (함수 파라미터는 dirX, dirY)
        // 만약 하나라도 승리 조건(5)을 만족하면 true를 반환
        if (CountStones(x, y, 1, 0, playerColor) >= currentWinCond) return true; // 가로 (우, 좌)
        if (CountStones(x, y, 0, 1, playerColor) >= currentWinCond) return true; // 세로 (상, 하)
        if (CountStones(x, y, 1, 1, playerColor) >= currentWinCond) return true; // 우상향 대각선
        if (CountStones(x, y, 1, -1, playerColor) >= currentWinCond) return true; // 우하향 대각선

        return false;
    }

    // 2. 특정 방향(벡터)으로 돌이 몇 개 이어졌는지 세는 코어 로직
    private int CountStones(int startX, int startY, int dirX, int dirY, StoneColor playerColor)
    {
        int count = 1; // 방금 놓은 내 돌을 1개로 치고 시작
        int playerInt = (int)playerColor;

        // 1) 정방향 탐색 (+dirX, +dirY)
        int nx = startX + dirX;
        int ny = startY + dirY;

        // 배열 범위를 벗어나지 않고, 해당 위치의 돌이 내 돌과 색이 같을 때까지 전진
        while (nx >= 0 && nx < boardSize && ny >= 0 && ny < boardSize && grid[nx, ny] == playerInt)
        {
            count++;
            nx += dirX;
            ny += dirY;
        }

        // 2) 역방향 탐색 (-dirX, -dirY)
        nx = startX - dirX;
        ny = startY - dirY;

        while (nx >= 0 && nx < boardSize && ny >= 0 && ny < boardSize && grid[nx, ny] == playerInt)
        {
            count++;
            nx -= dirX;
            ny -= dirY;
        }

        return count;
    }

    // --------------------------------------------------------------------

    // =========================================================
    // [최적화 헬퍼] 내 주변(반경)에 돌이 하나라도 있는지 검사
    // =========================================================
    private bool HasNeighborInRadius(int cx, int cy, int radius)
    {
        int startX = Mathf.Max(0, cx - radius);
        int endX = Mathf.Min(boardSize - 1, cx + radius);
        int startY = Mathf.Max(0, cy - radius);
        int endY = Mathf.Min(boardSize - 1, cy + radius);

        for (int x = startX; x <= endX; x++)
        {
            for (int y = startY; y <= endY; y++)
            {
                if (grid[x, y] != 0) return true; // 돌이 하나라도 있으면 true
            }
        }
        return false;
    }

    // 바둑판 크기에 맞춰 카메라 위치와 시야각을 자동 조절하는 함수
    public void AdjustCameraToBoardSize()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        // 1. 바둑판의 정중앙 좌표 계산 (예: 19x19면 인덱스 9가 중앙)
        float centerOffset = (boardSize - 1) * gridSize / 2f;

        // 2. 카메라를 바둑판 정중앙 위로 이동 (Y축 높이는 임시로 10 지정)
        mainCamera.transform.position = new Vector3(centerOffset, 10f, centerOffset);

        // 3. 카메라가 바둑판을 내려다보게 90도 회전
        mainCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // 4. 화면 크기에 맞춰 카메라 시야(Size) 조절
        // Orthographic(2D/직교) 카메라일 경우
        if (mainCamera.orthographic)
        {
            mainCamera.orthographicSize = (boardSize * gridSize) / 2f + cameraPadding;
        }
        else // Perspective(3D/원근) 카메라일 경우 (높이 조절)
        {
            float fov = mainCamera.fieldOfView;
            float requiredHeight = ((boardSize * gridSize) / 2f + cameraPadding) / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            mainCamera.transform.position = new Vector3(centerOffset, requiredHeight, centerOffset);
        }
    }

    // ** 개발자용 격자 그리기 (유니티 에디터 화면에만 보이는 선)
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
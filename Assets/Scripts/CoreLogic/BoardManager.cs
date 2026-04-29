using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    public RuleManager ruleManager;
    public GameManager gameManager;

    private GameObject currentHoveredStone = null; // 현재 마우스가 올라가 있는 돌 기억용 (스킬 관련)

    [Header("Board Settings")]
    public int boardSize = 19;  // 19x19 격자
    public float gridSize = 1f; // -> InputManager의 gridSize와 동일해야 함!

    [Header("Camera Auto Setup")]
    public Camera mainCamera; // 인스펙터에서 MainCamera 연결
    public float cameraPadding = 2f; // 화면 가장자리 여백

    [Header("Skill Visuals")]
    private List<GameObject> skillTargetMarkers = new List<GameObject>();

    [Header("Normal Stone Materials")] // '제거' 스킬 적용 시 사라질 때 사용
    public Material normalBlackMat; // 일반 흑돌 머티리얼 연결
    public Material normalWhiteMat; // 일반 백돌 머티리얼 연결 

    // 2차원 배열 데이터 (0: 빈칸, 1: 흑돌, 2: 백돌)
    public int[,] grid;

    // 생성된 바둑돌들을 기억해둘 리스트 ('다시하기' 기능 시 필요) 
    private List<GameObject> activeStones = new List<GameObject>();
    private List<GameObject> forbiddenMarks = new List<GameObject>(); // '❌' 마커들을 담아둘 리스트

    // 좌표(Vector2Int)별로 떠 있는 자물쇠/신의가호(방패) 오브젝트를 기억하는 사전
    private Dictionary<Vector2Int, GameObject> activeSealMarkers = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, GameObject> activeShieldMarkers = new Dictionary<Vector2Int, GameObject>();

    // ---------------------------------------------------
    // 스킬 '봉인' 정보를 담을 구조체 선언 (클래스 안에 선언)
    public struct SealInfo
    {
        public int turns;
        public StoneColor owner;
    }
    // 봉인 스킬 관련: 0이면 정상, 1 이상이면 남은 봉인 턴 수
    public SealInfo[,] sealedGrid;
    // 보호막 여부를 저장하는 배열 (true면 보호받음)
    public bool[,] shieldGrid;

    [Header("Invisibility Settings")] // 스킬용
    public Material blackGhostMat; // 반투명 검은돌 연결
    public Material whiteGhostMat; // 반투명 하얀돌 연결

    // 원래 머티리얼을 기억해둘 딕셔너리 (투명화 풀렸을 때 원상복구용)
    private Dictionary<GameObject, Material> originalMats = new Dictionary<GameObject, Material>();

    // 신성화(10번) 스킬 전용
    [Header("Sanctification State")]
    public bool isSanctificationActive = false;

    void Awake()
    {
        // 게임 시작과 동시에 15x15 짜리 빈 배열 생성
        grid = new int[boardSize, boardSize];
        sealedGrid = new SealInfo[boardSize, boardSize];
        shieldGrid = new bool[boardSize, boardSize];

        Debug.Log($"[BoardManager] {boardSize}x{boardSize} 오목판 데이터 생성 완료!");

        AdjustCameraToBoardSize(); // 시작 시 카메라 자동 세팅
    }

    // 1: 돌을 둘 수 있는 정상적인 위치인지 검사
    public bool IsValidMove(int x, int y, StoneColor playerColor, bool silent = false)
    {
        // 1-1. 바둑판 범위를 벗어났는가? (IndexOutOfRange 에러 방어)
        if (x < 0 || x >= boardSize || y < 0 || y >= boardSize)
        {
            //if (!silent) Debug.LogWarning("[BoardManager] 바둑판 범위를 벗어났습니다!");
            return false;
        }

        // 1-2. 이미 돌이 놓여져 있는가? (0이어야 빈칸)
        if (grid[x, y] != 0)
        {
            if (!silent) Debug.LogWarning("[BoardManager] 이미 돌이 있는 자리입니다!");
            return false;
        }

        // 1-3. 스킬로 봉인된 칸인지 검사
        if (sealedGrid[x, y].turns > 0)
        {
            // 기획: 나는 놓을 수 있음. 상대방만 못 놓음
            // 방금 턴을 넘겨받은 사람이 상대방이라면 막기
            if (playerColor != sealedGrid[x, y].owner)
            {
                if (!silent) Debug.LogWarning("상대방에 의해 봉인된 칸입니다!");
                return false;
            }
        }

        // 1-4. 현재 오목 규칙 기준으로, 돌을 놔도 되는지 RuleManager에게 검사 요청
        if (ruleManager != null && ruleManager.IsForbiddenMove(x, y, (int)playerColor, grid, boardSize, silent))
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
            // (10번 스킬 발동 중이면 무조건 백돌 프리팹만 꺼냄)
            string poolTag = "WhiteStone";
            if (!isSanctificationActive && playerColor == StoneColor.Black)
            {
                poolTag = "BlackStone";
            }

            // 2-3. 돌이 나타날 실제 3D 위치 (바닥 파묻힘 방지용 Y: 0.2f)
            Vector3 spawnPos = new Vector3(x * gridSize, 0.2f, y * gridSize);

            // 2-4. 실제로 씬에 3D 모델 생성
            GameObject newStone = ObjectPooler.Instance.SpawnFromPool(poolTag, spawnPos, Quaternion.identity);  // 생성한 돌을 변수에 담고            
            activeStones.Add(newStone); // 리스트에 추가해서 기억해둠

            // ** 10번(신성화) 스킬 시전자(백돌 플레이어)의 화면에서만 피아 식별용 아웃라인 켜기
            if (isSanctificationActive && gameManager.localPlayerColor == StoneColor.White)
            {
                VisualOutline outline = newStone.GetComponent<VisualOutline>();
                if (outline != null)
                {
                    // 내 돌(백돌)은 파란색 테두리, 상대 돌(흑돌)은 노란색 테두리로 구분
                    if (playerColor == StoneColor.White) outline.EnableOutline(Color.yellow);
                }
            }

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
        foreach (GameObject mark in forbiddenMarks)
        {
            if (mark != null) mark.SetActive(false);
        }
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

        // '봉인' 마커 청소
        foreach (var marker in activeSealMarkers.Values) marker.SetActive(false);
        activeSealMarkers.Clear();

        // '신의 가호' 방패 아이콘 청소
        foreach (var marker in activeShieldMarkers.Values) marker.SetActive(false);
        activeShieldMarkers.Clear();

        // 2차원 배열 데이터 초기화 (0으로 덮어쓰기)
        System.Array.Clear(grid, 0, grid.Length);
        System.Array.Clear(shieldGrid, 0, shieldGrid.Length);

        // 신성화 스킬적용 여부 false로
        isSanctificationActive = false;

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

    // 돌 랜덤 착수를 위한, 좌표를 반환하는 함수
    public Vector2Int GetRandomValidMove(StoneColor playerColor)
    {
        List<Vector2Int> validMoves = new List<Vector2Int>();

        // 바둑판 전체를 순회하며 둘 수 있는 모든 좌표를 수집
        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                // silent를 true로 넘겨서 경고 로그 도배 방지
                if (IsValidMove(x, y, playerColor, silent: true))
                {
                    validMoves.Add(new Vector2Int(x, y));
                }
            }
        }

        // 바둑판이 꽉 차서 둘 곳이 없으면 (-1, -1) 반환
        if (validMoves.Count == 0) return new Vector2Int(-1, -1);

        // 수집된 합법적 자리 중 하나를 랜덤으로 뽑아서 반환
        int randomIndex = UnityEngine.Random.Range(0, validMoves.Count);
        return validMoves[randomIndex];
    }

    // 특정 좌표(x, y)에 있는 3D 돌 오브젝트를 찾아서 비활성화(풀 반환) 하는 함수
    public void RemoveStoneObjectAt(int x, int y)
    {
        // 바둑판 좌표를 실제 월드 좌표(x * gridSize, y * gridSize)로 변환
        float targetWorldX = x * gridSize;
        float targetWorldZ = y * gridSize;

        GameObject stoneToRemove = null;

        // activeStones 리스트를 뒤져서 해당 좌표에 있는 돌을 찾음
        foreach (GameObject stone in activeStones)
        {
            if (stone.activeSelf &&
                Mathf.Approximately(stone.transform.position.x, targetWorldX) &&
                Mathf.Approximately(stone.transform.position.z, targetWorldZ))
            {
                stoneToRemove = stone;
                break; // 찾았으니 검색 중단
            }
        }

        if (stoneToRemove != null)
        {
            // 씬에서 숨김 처리 (ObjectPooler 반환)
            stoneToRemove.SetActive(false);
            activeStones.Remove(stoneToRemove); // 활성 리스트에서도 제거
        }
    }

    // 제거 가능한 상대방 돌들 위에 하이라이트 표시
    public void ShowSkillTargetMarkers(StoneColor myColor)
    {
        HideSkillTargetMarkers(); // 기존 마커 청소

        int enemyColorInt = (myColor == StoneColor.Black) ? 2 : 1;

        // BoardManager는 모든 돌을 activeStones 리스트로 가지고 있으므로, 
        // 리스트를 순회하며 상대 돌의 VisualOutline 컴포넌트를 켜주면 됨
        foreach (GameObject stoneObj in activeStones)
        {
            if (stoneObj == null || !stoneObj.activeSelf) continue;

            // 돌의 위치로 데이터 배열 좌표(x, y)를 역계산
            int x = Mathf.RoundToInt(stoneObj.transform.position.x / gridSize);
            int y = Mathf.RoundToInt(stoneObj.transform.position.z / gridSize);

            // 상대 돌이라면 테두리를 켜줌
            if (grid[x, y] == enemyColorInt)
            {
                VisualOutline outline = stoneObj.GetComponent<VisualOutline>();
                if (outline != null)
                {
                    // 제거 가능한 돌을 초록색 테두리로 만듦
                    outline.EnableOutline(Color.green); // unlit 머티리얼이 여기에 녹색을 입힙니다.
                }
            }
        }
    }

    // 현재 '봉인'된 위치 남은 지속턴과 함께 기록하기
    public void ApplySeal(int x, int y, int turns, StoneColor ownerColor)
    {
        sealedGrid[x, y].turns = turns;
        sealedGrid[x, y].owner = ownerColor;

        Vector2Int posKey = new Vector2Int(x, y);

        // 이미 그 자리에 자물쇠가 떠 있지 않다면 생성
        if (!activeSealMarkers.ContainsKey(posKey))
        {
            // 바둑판 바닥보다 살짝 위(0.15f)에 띄움
            Vector3 spawnPos = new Vector3(x * gridSize, 0.15f, y * gridSize);
            GameObject marker = ObjectPooler.Instance.SpawnFromPool("SealMarker", spawnPos, Quaternion.Euler(90, 0, 0));

            if (marker == null)
            {
                Debug.LogError("[ObjectPooler] 'SealMarker'를 찾을 수 없습니다! 인스펙터 Pool 설정을 확인하세요.");
                return;
            }

            // 자물쇠 색상 변경 로직
            MeshRenderer mr = marker.GetComponent<MeshRenderer>();

            if (mr != null)
            {
                // OCP 원칙 준수: 원본 머티리얼을 훼손하지 않기 위해 인스턴스를 만듦
                // (mr.material을 부르는 순간 인스턴스가 자동으로 생성됨)
                Material instancedMat = mr.material;

                Color finalColor;
                if (ownerColor == gameManager.localPlayerColor)
                {
                    finalColor = new Color(0.2f, 0.6f, 1f, 0.8f); // 아군 (파랑)
                }
                else
                {
                    finalColor = new Color(1f, 0.2f, 0.2f, 0.8f); // 적군 (빨강)
                }

                // URP Unlit/Lit 셰이더의 메인 색상 변수 이름은 보통 "_BaseColor" 입니다.
                // (만약 빌트인 Standard 셰이더라면 "_Color" 입니다.)
                if (instancedMat.HasProperty("_BaseColor"))
                {
                    instancedMat.SetColor("_BaseColor", finalColor);
                }
                else if (instancedMat.HasProperty("_Color")) // 빌트인 대응
                {
                    instancedMat.SetColor("_Color", finalColor);
                }
            }

            // 사전에 등록!
            activeSealMarkers.Add(posKey, marker);
        }

        Debug.Log($"({x}, {y}) 좌표에 자물쇠 이펙트 생성 완료!");
    }

    // 턴이 다 되어서 자물쇠를 치워야 할 때 부르는 함수
    public void RemoveSealEffect(int x, int y)
    {
        Vector2Int posKey = new Vector2Int(x, y);

        // 사전에 해당 좌표 자물쇠가 있는지 확인
        if (activeSealMarkers.TryGetValue(posKey, out GameObject marker))
        {
            marker.SetActive(false); // 씬에서 숨기기 (풀러 반환)
            activeSealMarkers.Remove(posKey); // 사전에서도 삭제
        }
    }

    // 보호막 씌우는 핵심 API
    public void ApplyShield(int x, int y)
    {
        shieldGrid[x, y] = true;
        Vector2Int posKey = new Vector2Int(x, y);

        if (!activeShieldMarkers.ContainsKey(posKey))
        {
            // 바둑돌 위에 예쁘게 씌워지도록 높이 조절
            Vector3 spawnPos = new Vector3(x * gridSize, 0.6f, y * gridSize);
            GameObject marker = ObjectPooler.Instance.SpawnFromPool("ShieldMarker", spawnPos, Quaternion.Euler(90, 0, 0));

            if (marker != null)
            {
                activeShieldMarkers.Add(posKey, marker);

                // 방패를 씌운 돌이 현재 내 눈에 보이는지 검사
                GameObject stone = GetStoneObjectAt(x, y);
                if (stone != null)
                {
                    MeshRenderer mr = stone.GetComponent<MeshRenderer>();
                    // 내 화면에서 이 돌이 렌더링 오프(투명화) 상태라면 방패도 같이 끔
                    if (mr != null && !mr.enabled)
                    {
                        marker.SetActive(false);
                    }
                }
            }
        }
        Debug.Log($"({x}, {y}) 좌표 돌에 신의 가호(보호막)가 부여되었습니다!");
    }

    // 해당 스킬이 어떤 돌(내 돌 or 상대 돌)을 수정하는가?
    public void ShowSkillTargetMarkers_My(StoneColor myColor)
    {
        HideSkillTargetMarkers();

        int myColorInt = (int)myColor;

        foreach (GameObject stoneObj in activeStones)
        {
            if (stoneObj == null || !stoneObj.activeSelf) continue;

            int x = Mathf.RoundToInt(stoneObj.transform.position.x / gridSize);
            int y = Mathf.RoundToInt(stoneObj.transform.position.z / gridSize);

            if (grid[x, y] == myColorInt)
            {
                VisualOutline outline = stoneObj.GetComponent<VisualOutline>();
                if (outline != null)
                    outline.EnableOutline(Color.blue); // 내 돌은 파란색 테두리
            }
        }
    }

    // 특정 좌표에 있는 돌 오브젝트를 반환하는 헬퍼 함수
    public GameObject GetStoneObjectAt(int x, int y)
    {
        float targetWorldX = x * gridSize;
        float targetWorldZ = y * gridSize;

        foreach (GameObject stone in activeStones)
        {
            if (stone.activeSelf &&
                Mathf.Approximately(stone.transform.position.x, targetWorldX) &&
                Mathf.Approximately(stone.transform.position.z, targetWorldZ))
            {
                return stone;
            }
        }
        return null;
    }

    public void HideSkillTargetMarkers()
    {
        // 모든 활성 돌을 순회하며 테두리를 끔 (SRP: 테두리 관리는 BoardManager가 총괄)
        foreach (GameObject stoneObj in activeStones)
        {
            if (stoneObj != null)
            {
                VisualOutline outline = stoneObj.GetComponent<VisualOutline>();
                if (outline != null) RestoreSanctificationOutline(stoneObj, outline); // 수정됨!
            }
        }
    }

    // 특정 돌 1개의 렌더링 상태를 바꾸는 코어 함수
    public void ApplyVisibilityToSingleStone(GameObject stone, StoneColor stoneColor, bool isVisible, bool isMyStone)
    {
        MeshRenderer mr = stone.GetComponent<MeshRenderer>();
        if (mr == null) return;

        // 처음 건드리는 돌이면 원래 머티리얼 저장
        if (!originalMats.ContainsKey(stone))
        {
            originalMats[stone] = mr.sharedMaterial;
        }

        // 좌표 역계산
        int x = Mathf.RoundToInt(stone.transform.position.x / gridSize);
        int y = Mathf.RoundToInt(stone.transform.position.z / gridSize);
        Vector2Int posKey = new Vector2Int(x, y);

        if (isVisible)
        {
            // 1. 투명화 해제 (정상 복구)
            mr.enabled = true;
            mr.material = originalMats[stone];
            // 방패 마커 복구
            if (activeShieldMarkers.TryGetValue(posKey, out GameObject shield)) shield.SetActive(true);
        }
        else
        {
            // 2. 투명화 적용 중!
            if (isMyStone)
            {
                // 내 돌이면 반투명(Ghost) 머티리얼 씌우기
                mr.enabled = true;
                mr.material = (stoneColor == StoneColor.Black) ? blackGhostMat : whiteGhostMat;
                // 내 화면의 내 돌이면 방패도 반투명하게 보이게 (필요시 머티리얼 변경, 일단은 켜둠)
                if (activeShieldMarkers.TryGetValue(posKey, out GameObject shield)) shield.SetActive(true);
            }
            else
            {
                // 상대방 돌이면 렌더러 자체를 꺼서 100% 안 보이게 만듦! (최고의 투명화)
                mr.enabled = false;
                // 상대방 화면에서 내 돌이 투명해지면 방패 마커도 같이 끔!
                if (activeShieldMarkers.TryGetValue(posKey, out GameObject shield)) shield.SetActive(false);
            }
        }
    }

    // 바둑판에 깔려있는 특정 플레이어의 모든 돌을 한 번에 업데이트하는 함수
    public void SetStoneInvisibility(StoneColor casterColor, bool isVisible, bool isMyStone)
    {
        int colorInt = (int)casterColor;
        foreach (GameObject stone in activeStones)
        {
            if (!stone.activeSelf) continue;

            // 돌의 실제 위치로 grid 배열 좌표 역계산
            int x = Mathf.RoundToInt(stone.transform.position.x / gridSize);
            int y = Mathf.RoundToInt(stone.transform.position.z / gridSize);

            // 시전자의 돌만 골라서 렌더링 변경
            if (grid[x, y] == colorInt)
            {
                ApplyVisibilityToSingleStone(stone, casterColor, isVisible, isMyStone);
            }
        }
    }

    // UX/UI 요소 ---------------------------------------------------------
    // 1. 돌이 생성될 때 깜빡임 ('3번' 이중 착수 등)
    public void BlinkStoneEffect(GameObject stoneObj, Color blinkColor)
    {
        if (stoneObj != null && stoneObj.activeSelf)
        {
            StartCoroutine(BlinkRoutine(stoneObj.GetComponent<MeshRenderer>(), blinkColor));
        }
    }

    // 2. 빈 자리가 깜빡임 ('5번' 제거 스킬 등 - 바닥의 하이라이트 마커를 잠시 켰다 끄기)
    public void BlinkEmptySpaceEffect(int x, int y, Color blinkColor, StoneColor originalColor)
    {
        Vector3 pos = new Vector3(x * gridSize, 0.2f, y * gridSize);
        GameObject marker = ObjectPooler.Instance.SpawnFromPool("TargetHighlight", pos, Quaternion.identity);

        if (marker != null)
        {
            MeshRenderer mr = marker.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                // 깜빡이기 전에, 마커를 방금 죽은 돌과 똑같은 머티리얼(흑/백)로 위장시킵니다!
                mr.material = (originalColor == StoneColor.Black) ? normalBlackMat : normalWhiteMat;
            }

            StartCoroutine(BlinkRoutine(mr, blinkColor, true, marker));
        }
    }

    private IEnumerator BlinkRoutine(MeshRenderer mr, Color color, bool destroyAfter = false, GameObject objToHide = null)
    {
        if (mr == null) yield break;

        Material mat = mr.material; // 인스턴스화
        Color originalColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.color;

        // 0.1초 간격으로 3번 깜빡임
        for (int i = 0; i < 3; i++)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else mat.color = color;
            yield return new WaitForSeconds(0.1f);

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", originalColor);
            else mat.color = originalColor;
            yield return new WaitForSeconds(0.1f);
        }

        if (destroyAfter && objToHide != null)
        {
            objToHide.SetActive(false);
        }
    }

    // 3-1. 단일 돌 하이라이트 기능 (마우스 호버용)
    public void HighlightSingleStone(int x, int y, Color highlightColor)
    {
        GameObject stone = GetStoneObjectAt(x, y);

        // 마우스가 다른 돌로 넘어갔다면, 이전 돌의 테두리를 끈다.
        if (currentHoveredStone != stone)
        {
            ClearHoverHighlight();
        }

        // 새로운 돌 테두리 켜기
        if (stone != null)
        {
            VisualOutline outline = stone.GetComponent<VisualOutline>();
            if (outline != null) outline.EnableOutline(highlightColor);

            currentHoveredStone = stone;
        }
    }

    // 3-2. 단일 하이라이트 끄기
    public void ClearHoverHighlight()
    {
        if (currentHoveredStone != null)
        {
            VisualOutline outline = currentHoveredStone.GetComponent<VisualOutline>();
            if (outline != null) RestoreSanctificationOutline(currentHoveredStone, outline); 
            currentHoveredStone = null;
        }
    }

    // 신성화(10번) 스킬 적용
    public void ActivateSanctification()
    {
        isSanctificationActive = true;
    }

    // 아웃라인을 완전히 끄는 대신, 신성화 상태면 파랑/빨강으로 되돌리는 헬퍼 함수
    public void RestoreSanctificationOutline(GameObject stoneObj, VisualOutline outline)
    {
        // 백돌 플레이어(시전자)의 화면에서만 작동
        if (isSanctificationActive && gameManager.localPlayerColor == StoneColor.White)
        {
            int x = Mathf.RoundToInt(stoneObj.transform.position.x / gridSize);
            int y = Mathf.RoundToInt(stoneObj.transform.position.z / gridSize);

            if (grid[x, y] == (int)StoneColor.White) 
                outline.EnableOutline(Color.yellow);
            else 
                outline.DisableOutline(); // 상대방 돌(흑돌)은 아웃라인 무조건 끔
        }
        else
        {
            outline.DisableOutline(); // 신성화가 아니면 그냥 끔
        }
    }

    // ------------------------------------------------------------------------
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
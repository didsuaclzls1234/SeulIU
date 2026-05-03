using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable] // ** 돌 스킬 적용 셰이더 수치 조절용
public class VisualSettings
{
    [Header("Hover - Black Stone (흑돌 호버)")]
    [Range(0f, 1f)] public float blackHoverAlpha = 0.5f; // 흑돌 호버 투명도
    [Range(0f, 1f)] public float blackHoverMetallic = 0f; // 흑돌 호버 금속성
    [Range(0f, 1f)] public float blackHoverSmoothness = 0.1f; // 흑돌 호버 빛반사

    [Header("Hover - White Stone (백돌 호버)")]
    [Range(0f, 1f)] public float whiteHoverAlpha = 0.4f; // 백돌 호버 투명도 (흰색이 더 밝으니 기본값을 조금 낮춤)
    [Range(0f, 1f)] public float whiteHoverMetallic = 0f; // 백돌 호버 금속성
    [Range(0f, 1f)] public float whiteHoverSmoothness = 0.05f; // 백돌 호버 빛반사 (눈부심 방지)

    [Header("Hover Highlight (타겟팅 테두리)")]
    public Color myHoverHighlightColor = Color.blue; // 1번, 8번 스킬 등 내 돌 타겟팅 테두리 색
    public Color enemyHoverHighlightColor = Color.green; // 5번 스킬 등 상대 돌 타겟팅 테두리 색
    [Range(0f, 1f)] public float hoverHighlightBlend = 0.6f; // 호버 테두리 진하기

    [Header("Ghost Skill - Black Stone (흑돌 투명화)")]
    [Range(0f, 1f)] public float blackGhostAlpha = 0.3f;
    [Range(0f, 1f)] public float blackGhostMetallic = 0f;
    [Range(0f, 1f)] public float blackGhostSmoothness = 0.1f;

    [Header("Ghost Skill - White Stone (백돌 투명화)")]
    [Range(0f, 1f)] public float whiteGhostAlpha = 0.3f;
    [Range(0f, 1f)] public float whiteGhostMetallic = 0f;
    [Range(0f, 1f)] public float whiteGhostSmoothness = 0.05f;

    [Header("Consecration Skill (10번 신성화 스킬)")]
    public Color consecrationOutlineColor = Color.yellow; // 신성화 발동 시 테두리 색상
    [Range(0.1f, 5f)] public float consecrationThickness = 5; // 테두리 두께 (작을수록 두꺼워짐.)
    [Range(1f, 10f)] public float consecrationGlow = 4.0f; // 테두리 발광 강도 (수치가 높을수록 더 많이 빛남)

    [Header("Blink Effect (제거 스킬 등 깜빡임)")]
    public Color removeBlinkColor = Color.red; // 제거 시 깜빡이는 색
    public Color extraPlaceBlinkColor = Color.yellow; // 추가 착수(3번 스킬) 깜빡이는 색
    public Color godBlessBlinkColor = Color.cyan; // 신의 가호(8번 스킬) 깜빡이는 색
    [Range(0f, 1f)] public float blinkOverlayBlend = 0.7f; // 깜빡일 때 색상이 덮어씌워지는 강도
}

// -------------------------------------------------------------------------------------

public class BoardManager : MonoBehaviour
{
    public RuleManager ruleManager;
    public GameManager gameManager;

    private GameObject currentHoveredStone = null; // 현재 마우스가 올라가 있는 돌 기억용 (스킬 관련)

    [Header("Board Settings")]
    public int boardSize = 19;  // 19x19 격자
    public float gridSize = 1f; // -> InputManager의 gridSize와 동일해야 함!

    [Header("Visual Settings (기획자 튜닝용)")]
    public VisualSettings visualSettings; // 인스펙터에 노출

    [Header("Camera Auto Setup")]
    public Camera mainCamera; // 인스펙터에서 MainCamera 연결
    public float cameraPadding = 2f; // 화면 가장자리 여백

    [Header("Y 오프셋.조절 완료 후 코드 기본값도 동일하게 수정 필요")]
    public float stoneYOffset = 0.4f;       // 바둑돌 높이
    public float forbiddenYOffset = 0.1f;   // 금수(❌) 마커 높이
    public float sealYOffset = 0.15f;       // 봉인(자물쇠) 마커 높이
    public float shieldYOffset = 0.6f;      // 보호막(신의 가호) 마커 높이
    public float blinkYOffset = 0.15f;      // 제거 스킬 빈자리 깜빡임 높이

    [Header("Stone Rotation")]
    public float blackStoneYRotation = 0f; // 흑돌 Y 회전
    public float whiteStoneYRotation = 0f; // 백돌 Y 회전

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

    // 신성화(10번) 스킬 전용
    [Header("Consecration State")]
    public bool isConsecrationActive = false;

    void Awake()
    {
        // 게임 시작과 동시에 빈 배열 생성
        grid = new int[boardSize, boardSize];
        sealedGrid = new SealInfo[boardSize, boardSize];
        shieldGrid = new bool[boardSize, boardSize];

        AdjustCameraToBoardSize(); // 시작 시 카메라 자동 세팅
    }

    // 1: 돌을 둘 수 있는 정상적인 위치인지 검사
    public bool IsValidMove(int x, int y, StoneColor playerColor, bool silent = false)
    {
        if (x < 0 || x >= boardSize || y < 0 || y >= boardSize) return false;
        if (grid[x, y] != 0)
        {
            if (!silent) Debug.LogWarning("[BoardManager] 이미 돌이 있는 자리입니다!");
            return false;
        }

        if (sealedGrid[x, y].turns > 0)
        {
            if (playerColor != sealedGrid[x, y].owner)
            {
                if (!silent) Debug.LogWarning("상대방에 의해 봉인된 칸입니다!");
                return false;
            }
        }

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
            string poolTag = (!isConsecrationActive && playerColor == StoneColor.Black) ? "BlackStone" : "WhiteStone";

            // 2-3. 돌이 나타날 실제 3D 위치
            Vector3 spawnPos = new Vector3(x * gridSize, stoneYOffset, y * gridSize);

            // 2-4. 실제로 씬에 3D 모델 생성
            float yRotation = (playerColor == StoneColor.Black) ? blackStoneYRotation : whiteStoneYRotation;
            GameObject newStone = ObjectPooler.Instance.SpawnFromPool(poolTag, spawnPos, Quaternion.Euler(0, yRotation, 0));
            activeStones.Add(newStone);

            // 신성화 상태 처리 
            if (isConsecrationActive && gameManager.localPlayerColor == StoneColor.White)
            {
                StoneVisualController svc = newStone.GetComponent<StoneVisualController>();
                if (svc != null && playerColor == StoneColor.White)
                {
                    svc.SetConsecration(true, visualSettings.consecrationOutlineColor, visualSettings.consecrationThickness, visualSettings.consecrationGlow);
                }
            }

            Debug.Log($"[BoardManager] 좌표 ({x}, {y})에 3D 돌 생성 완료!");
            return newStone;
        }

        // 만약 금수 자리거나 룰에 막혀서 돌을 못 놓았다면 빈 값(null) 반환
        return null;
    }

    // 전체 바둑판을 스캔해서 ❌ 마커를 그리는 함수
    public void UpdateForbiddenMarks(StoneColor currentPlayerColor)
    {
        foreach (GameObject mark in forbiddenMarks)
        {
            if (mark != null) mark.SetActive(false);
        }
        forbiddenMarks.Clear();

        if (ruleManager == null) return;

        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                if (grid[x, y] != 0) continue;
                if (!HasNeighborInRadius(x, y, 2)) continue;

                if (ruleManager.IsForbiddenMove(x, y, (int)currentPlayerColor, grid, boardSize, true))
                {
                    Vector3 pos = new Vector3(x * gridSize, forbiddenYOffset, y * gridSize);
                    GameObject newMark = ObjectPooler.Instance.SpawnFromPool("ForbiddenMark", pos, Quaternion.Euler(90, 0, 0));
                    forbiddenMarks.Add(newMark);
                }
            }
        }
    }

    // 재시작 시 호출할 데이터 정리 함수
    public void ClearBoard()
    {
        foreach (GameObject stone in activeStones) stone.SetActive(false);
        activeStones.Clear();

        foreach (GameObject mark in forbiddenMarks) mark.SetActive(false);
        forbiddenMarks.Clear();

        foreach (var marker in activeSealMarkers.Values) marker.SetActive(false);
        activeSealMarkers.Clear();

        foreach (var marker in activeShieldMarkers.Values) marker.SetActive(false);
        activeShieldMarkers.Clear();

        System.Array.Clear(grid, 0, grid.Length);
        System.Array.Clear(shieldGrid, 0, shieldGrid.Length);

        isConsecrationActive = false;
        Debug.Log("[BoardManager] 바둑판 데이터 및 바둑돌 초기화 완료!");
    }

    // 1. 전체 방향을 검사하는 메인 함수
    public bool CheckWin(int x, int y, StoneColor playerColor)
    {
        int currentWinCond = ruleManager.GetWinCondition((int)playerColor);

        if (CountStones(x, y, 1, 0, playerColor) >= currentWinCond) return true; // 가로
        if (CountStones(x, y, 0, 1, playerColor) >= currentWinCond) return true; // 세로
        if (CountStones(x, y, 1, 1, playerColor) >= currentWinCond) return true; // 우상향
        if (CountStones(x, y, 1, -1, playerColor) >= currentWinCond) return true; // 우하향

        return false;
    }

    // 2. 특정 방향(벡터)으로 돌이 몇 개 이어졌는지 세는 코어 로직
    private int CountStones(int startX, int startY, int dirX, int dirY, StoneColor playerColor)
    {
        int count = 1;
        int playerInt = (int)playerColor;

        int nx = startX + dirX;
        int ny = startY + dirY;
        while (nx >= 0 && nx < boardSize && ny >= 0 && ny < boardSize && grid[nx, ny] == playerInt)
        {
            count++;
            nx += dirX;
            ny += dirY;
        }

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

    // [최적화 헬퍼] 내 주변(반경)에 돌이 하나라도 있는지 검사
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
                if (grid[x, y] != 0) return true;
            }
        }
        return false;
    }

    // 바둑판 크기에 맞춰 카메라 위치와 시야각을 자동 조절하는 함수
    public void AdjustCameraToBoardSize()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        float centerOffset = (boardSize - 1) * gridSize / 2f;
        mainCamera.transform.position = new Vector3(centerOffset, 10f, centerOffset);
        mainCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        if (mainCamera.orthographic)
        {
            mainCamera.orthographicSize = (boardSize * gridSize) / 2f + cameraPadding;
        }
        else
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
        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                if (IsValidMove(x, y, playerColor, silent: true))
                {
                    validMoves.Add(new Vector2Int(x, y));
                }
            }
        }
        if (validMoves.Count == 0) return new Vector2Int(-1, -1);
        int randomIndex = UnityEngine.Random.Range(0, validMoves.Count);
        return validMoves[randomIndex];
    }

    // 특정 좌표(x, y)에 있는 3D 돌 오브젝트를 찾아서 비활성화(풀 반환) 하는 함수
    public void RemoveStoneObjectAt(int x, int y)
    {
        float targetWorldX = x * gridSize;
        float targetWorldZ = y * gridSize;
        GameObject stoneToRemove = null;

        foreach (GameObject stone in activeStones)
        {
            if (stone.activeSelf &&
                Mathf.Approximately(stone.transform.position.x, targetWorldX) &&
                Mathf.Approximately(stone.transform.position.z, targetWorldZ))
            {
                stoneToRemove = stone;
                break;
            }
        }

        if (stoneToRemove != null)
        {
            stoneToRemove.SetActive(false);
            activeStones.Remove(stoneToRemove);
        }
    }

    // 제거 가능한 상대방 돌들 위에 하이라이트 표시
    public void ShowSkillTargetMarkers(StoneColor myColor)
    {
        HideSkillTargetMarkers();
        int enemyColorInt = (myColor == StoneColor.Black) ? 2 : 1;

        foreach (GameObject stoneObj in activeStones)
        {
            if (stoneObj == null || !stoneObj.activeSelf) continue;

            int x = Mathf.RoundToInt(stoneObj.transform.position.x / gridSize);
            int y = Mathf.RoundToInt(stoneObj.transform.position.z / gridSize);

            if (grid[x, y] == enemyColorInt)
            {
                StoneVisualController svc = stoneObj.GetComponent<StoneVisualController>();
                // 인스펙터 연동!
                if (svc != null) svc.SetOverlay(visualSettings.enemyHoverHighlightColor, visualSettings.hoverHighlightBlend);
            }
        }
    }

    // 현재 '봉인'된 위치 남은 지속턴과 함께 기록하기
    public void ApplySeal(int x, int y, int turns, StoneColor ownerColor)
    {
        sealedGrid[x, y].turns = turns;
        sealedGrid[x, y].owner = ownerColor;
        Vector2Int posKey = new Vector2Int(x, y);

        if (!activeSealMarkers.ContainsKey(posKey))
        {
            Vector3 spawnPos = new Vector3(x * gridSize, sealYOffset, y * gridSize);
            GameObject marker = ObjectPooler.Instance.SpawnFromPool("SealMarker", spawnPos, Quaternion.Euler(90, 0, 0));

            if (marker == null) return;

            MeshRenderer mr = marker.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Material instancedMat = mr.material;
                Color finalColor = (ownerColor == gameManager.localPlayerColor) ? new Color(0.2f, 0.6f, 1f, 0.8f) : new Color(1f, 0.2f, 0.2f, 0.8f);

                if (instancedMat.HasProperty("_BaseColor")) instancedMat.SetColor("_BaseColor", finalColor);
                else if (instancedMat.HasProperty("_Color")) instancedMat.SetColor("_Color", finalColor);
            }

            activeSealMarkers.Add(posKey, marker);
        }
    }

    // 턴이 다 되어서 자물쇠를 치워야 할 때 부르는 함수
    public void RemoveSealEffect(int x, int y)
    {
        Vector2Int posKey = new Vector2Int(x, y);
        if (activeSealMarkers.TryGetValue(posKey, out GameObject marker))
        {
            marker.SetActive(false);
            activeSealMarkers.Remove(posKey);
        }
    }

    // 보호막 씌우는 핵심 API
    public void ApplyShield(int x, int y)
    {
        shieldGrid[x, y] = true;
        Vector2Int posKey = new Vector2Int(x, y);

        if (!activeShieldMarkers.ContainsKey(posKey))
        {
            Vector3 spawnPos = new Vector3(x * gridSize, shieldYOffset, y * gridSize);
            GameObject marker = ObjectPooler.Instance.SpawnFromPool("ShieldMarker", spawnPos, Quaternion.Euler(90, 0, 0));

            if (marker != null)
            {
                activeShieldMarkers.Add(posKey, marker);

                GameObject stone = GetStoneObjectAt(x, y);
                if (stone != null)
                {
                    StoneVisualController svc = stone.GetComponent<StoneVisualController>();
                    if (svc != null && !svc.IsVisible) marker.SetActive(false);
                }
            }
        }
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

            StoneVisualController svc = stoneObj.GetComponent<StoneVisualController>();
            if (svc != null && grid[x, y] == myColorInt)
            {
                // 인스펙터 연동!
                svc.SetOverlay(visualSettings.myHoverHighlightColor, visualSettings.hoverHighlightBlend);
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
        foreach (GameObject stoneObj in activeStones)
        {
            if (stoneObj != null && stoneObj.activeSelf)
            {
                StoneVisualController svc = stoneObj.GetComponent<StoneVisualController>();
                if (svc != null)
                {
                    svc.SetOverlay(Color.black, 0f);
                    RestoreConsecrationOutline(stoneObj, svc);
                }
            }
        }
    }

    // 특정 돌 1개의 렌더링 상태를 바꾸는 코어 함수
    public void ApplyVisibilityToSingleStone(GameObject stone, StoneColor stoneColor, bool isVisible, bool isMyStone)
    {
        StoneVisualController svc = stone.GetComponent<StoneVisualController>();
        if (svc == null) return;

        int x = Mathf.RoundToInt(stone.transform.position.x / gridSize);
        int y = Mathf.RoundToInt(stone.transform.position.z / gridSize);
        Vector2Int posKey = new Vector2Int(x, y);

        // 흑돌인지 백돌인지에 따라 분리된 기획자 세팅값을 가져옴
        float gAlpha = (stoneColor == StoneColor.Black) ? visualSettings.blackGhostAlpha : visualSettings.whiteGhostAlpha;
        float gMet = (stoneColor == StoneColor.Black) ? visualSettings.blackGhostMetallic : visualSettings.whiteGhostMetallic;
        float gSmo = (stoneColor == StoneColor.Black) ? visualSettings.blackGhostSmoothness : visualSettings.whiteGhostSmoothness;

        if (isVisible)
        {
            svc.SetVisibility(true, false);
            if (activeShieldMarkers.TryGetValue(posKey, out GameObject shield)) shield.SetActive(true);
        }
        else
        {
            if (isMyStone)
            {
                // 인스펙터 연동
                svc.SetVisibility(true, true, gAlpha, gMet, gSmo);
                if (activeShieldMarkers.TryGetValue(posKey, out GameObject shield)) shield.SetActive(true);
            }
            else
            {
                svc.SetVisibility(false, false); // 완전 숨김
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

            int x = Mathf.RoundToInt(stone.transform.position.x / gridSize);
            int y = Mathf.RoundToInt(stone.transform.position.z / gridSize);

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
            StoneVisualController svc = stoneObj.GetComponent<StoneVisualController>();
            // 인스펙터 연동!
            if (svc != null) svc.PlayBlinkEffect(blinkColor, visualSettings.blinkOverlayBlend);
        }
    }

    // 2. 빈 자리가 깜빡임 ('5번' 제거 스킬 등 - 바닥의 하이라이트 마커를 잠시 켰다 끄기)
    public void BlinkEmptySpaceEffect(int x, int y, Color blinkColor, StoneColor originalColor)
    {
        Vector3 pos = new Vector3(x * gridSize, stoneYOffset, y * gridSize);
        string poolTag = (originalColor == StoneColor.Black) ? "BlackStone" : "WhiteStone";

        float yRot = (originalColor == StoneColor.Black) ? blackStoneYRotation : whiteStoneYRotation;
        Quaternion rot = Quaternion.Euler(0, yRot, 0);

        GameObject dummyStone = ObjectPooler.Instance.SpawnFromPool(poolTag, pos, rot);

        if (dummyStone != null)
        {
            StoneVisualController svc = dummyStone.GetComponent<StoneVisualController>();
            if (svc != null)
            {
                svc.SetVisibility(true, false);
                StartCoroutine(DummyBlinkRoutine(svc, dummyStone, blinkColor));
            }
        }
    }

    private IEnumerator DummyBlinkRoutine(StoneVisualController svc, GameObject dummy, Color color)
    {
        // 인스펙터 연동!
        svc.PlayBlinkEffect(color, visualSettings.blinkOverlayBlend);
        yield return new WaitForSeconds(0.6f);
        dummy.SetActive(false);
    }

    // 3-1. 단일 돌 하이라이트 기능 (마우스 호버용)
    public void HighlightSingleStone(int x, int y, Color highlightColor)
    {
        GameObject stone = GetStoneObjectAt(x, y);

        if (currentHoveredStone != stone) ClearHoverHighlight();

        if (stone != null)
        {
            StoneVisualController svc = stone.GetComponent<StoneVisualController>();
            if (svc != null)
            {
                if (!svc.IsVisible)
                {
                    // 투명화된 돌이 '제거 스킬' 조준에 걸렸을 때도 흑/백 분리된 세팅 사용
                    StoneColor stoneColor = (StoneColor)grid[x, y];
                    float gAlpha = (stoneColor == StoneColor.Black) ? visualSettings.blackGhostAlpha : visualSettings.whiteGhostAlpha;
                    float gMet = (stoneColor == StoneColor.Black) ? visualSettings.blackGhostMetallic : visualSettings.whiteGhostMetallic;
                    float gSmo = (stoneColor == StoneColor.Black) ? visualSettings.blackGhostSmoothness : visualSettings.whiteGhostSmoothness;

                    svc.SetVisibility(true, true, gAlpha, gMet, gSmo);
                }

                svc.SetOverlay(highlightColor, visualSettings.hoverHighlightBlend);
            }
            currentHoveredStone = stone;
        }
    }

    // 3-2. 단일 하이라이트 끄기
    public void ClearHoverHighlight()
    {
        if (currentHoveredStone != null)
        {
            StoneVisualController svc = currentHoveredStone.GetComponent<StoneVisualController>();
            if (svc != null)
            {
                svc.SetOverlay(Color.black, 0f);
                RestoreConsecrationOutline(currentHoveredStone, svc);

                // 호버가 끝났을 때, 원래 숨겨져 있어야 할 돌이면 다시 끄기
                int x = Mathf.RoundToInt(currentHoveredStone.transform.position.x / gridSize);
                int y = Mathf.RoundToInt(currentHoveredStone.transform.position.z / gridSize);
                StoneColor stoneColor = (StoneColor)grid[x, y];
                bool isMyStone = (stoneColor == gameManager.localPlayerColor);

                // 현재 상대방의 투명화 스킬이 발동 중인지 확인
                bool isOpponentInvisible = (gameManager.skillManager != null && gameManager.skillManager.oppInvisibilityTurns > 0);

                // 만약 상대방 돌이고 + 상대가 투명화 상태라면 다시 완전 숨김 처리
                if (!isMyStone && isOpponentInvisible)
                {
                    ApplyVisibilityToSingleStone(currentHoveredStone, stoneColor, false, false);
                }
            }
            currentHoveredStone = null;
        }
    }

    // 신성화(10번) 스킬 적용
    public void ActivateConsecration()
    {
        isConsecrationActive = true;
        foreach (GameObject stone in activeStones)
        {
            if (stone.activeSelf)
            {
                StoneVisualController svc = stone.GetComponent<StoneVisualController>();
                if (svc != null) RestoreConsecrationOutline(stone, svc);
            }
        }
    }

    // 아웃라인을 완전히 끄는 대신, 신성화 상태면 파랑/빨강으로 되돌리는 헬퍼 함수
    public void RestoreConsecrationOutline(GameObject stoneObj, StoneVisualController svc)
    {
        // 백돌 플레이어(시전자)의 화면에서만 작동
        if (isConsecrationActive && gameManager.localPlayerColor == StoneColor.White)
        {
            int x = Mathf.RoundToInt(stoneObj.transform.position.x / gridSize);
            int y = Mathf.RoundToInt(stoneObj.transform.position.z / gridSize);

            if (grid[x, y] == (int)StoneColor.White)
            {
                // 인스펙터 연동 (두께와 발광 강도 적용)
                svc.SetConsecration(true, visualSettings.consecrationOutlineColor, visualSettings.consecrationThickness, visualSettings.consecrationGlow);
            }
            else
            {
                svc.SetConsecration(false, Color.black); // 상대방 돌(흑돌)은 아웃라인 무조건 끔
            }
        }
        else
        {
            svc.SetConsecration(false, Color.black); // 신성화가 아니면 그냥 끔
        }
    }

    // ** 개발자용 격자 그리기
    void OnDrawGizmos()
    {
        Gizmos.color = Color.black;
        for (int i = 0; i < boardSize; i++)
        {
            Gizmos.DrawLine(new Vector3(i * gridSize, 0.01f, 0), new Vector3(i * gridSize, 0.01f, (boardSize - 1) * gridSize));
            Gizmos.DrawLine(new Vector3(0, 0.01f, i * gridSize), new Vector3((boardSize - 1) * gridSize, 0.01f, i * gridSize));
        }
    }

    // ------------------------------------------------------------------------
    // ** 실시간 인스펙터 렌더링 반영 로직 (에디터 전용)
    // ------------------------------------------------------------------------
#if UNITY_EDITOR
    // 유니티 에디터의 인스펙터에서 값을 수정할 때마다 자동으로 호출되는 마법의 함수입니다!
    private void OnValidate()
    {
        // 게임이 플레이 중이고, 매니저들이 정상적으로 세팅된 상태일 때만 새로고침을 실행합니다.
        if (Application.isPlaying && gameManager != null && activeStones != null)
        {
            RefreshAllStonesVisuals();
        }
    }
#endif

    // 현재 바둑판에 깔려있는 모든 돌의 비주얼을 최신 세팅값으로 싹 다 덮어씌우는 함수
    public void RefreshAllStonesVisuals()
    {
        bool isOpponentInvisible = (gameManager.skillManager != null && gameManager.skillManager.oppInvisibilityTurns > 0);
        bool isMyInvisibilityActive = (gameManager.skillManager != null && gameManager.skillManager.myInvisibilityTurns > 0);

        foreach (GameObject stoneObj in activeStones)
        {
            if (stoneObj == null || !stoneObj.activeSelf) continue;

            StoneVisualController svc = stoneObj.GetComponent<StoneVisualController>();
            if (svc == null) continue;

            int x = Mathf.RoundToInt(stoneObj.transform.position.x / gridSize);
            int y = Mathf.RoundToInt(stoneObj.transform.position.z / gridSize);
            StoneColor stoneColor = (StoneColor)grid[x, y];
            bool isMyStone = (stoneColor == gameManager.localPlayerColor);

            // 실시간 새로고침에도 흑/백 분리 세팅 적용
            float gAlpha = (stoneColor == StoneColor.Black) ? visualSettings.blackGhostAlpha : visualSettings.whiteGhostAlpha;
            float gMet = (stoneColor == StoneColor.Black) ? visualSettings.blackGhostMetallic : visualSettings.whiteGhostMetallic;
            float gSmo = (stoneColor == StoneColor.Black) ? visualSettings.blackGhostSmoothness : visualSettings.whiteGhostSmoothness;

            if (isMyStone && isMyInvisibilityActive)
            {
                svc.SetVisibility(true, true, gAlpha, gMet, gSmo);
            }
            else if (!isMyStone && isOpponentInvisible)
            {
                svc.SetVisibility(false, false);
            }
            else
            {
                svc.SetVisibility(true, false);
            }

            RestoreConsecrationOutline(stoneObj, svc);
        }
    }
}
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

    [Header("Consecration Skill (11번 신성화 스킬)")]
    public Color consecrationOutlineColor = Color.yellow; // 신성화 발동 시 테두리 색상
    [Range(0.1f, 5f)] public float consecrationThickness = 5; // 테두리 두께 (작을수록 두꺼워짐.)
    [Range(1f, 10f)] public float consecrationGlow = 4.0f; // 테두리 발광 강도 (수치가 높을수록 더 많이 빛남)

    [Header("Blink Effect (제거 스킬 등 깜빡임)")]
    public Color removeBlinkColor = Color.red; // 제거 시 깜빡이는 색
    public Color extraPlaceBlinkColor = Color.yellow; // 추가 착수(3번 스킬) 깜빡이는 색
    public Color godBlessBlinkColor = Color.cyan; // 신의 가호(9번 스킬) 깜빡이는 색
    [Range(0f, 1f)] public float blinkOverlayBlend = 0.7f; // 깜빡일 때 색상이 덮어씌워지는 강도

    [Header("Board Skill Overlay (전체 타겟 스킬용)")]
    public Color boardReadyTint = new Color(1f, 1f, 1f, 0.4f);
    public Color boardHoverTint = new Color(0.1f, 0.5f, 0.1f, 1f); // 살짝 푸른빛이 도는 밝은 색
    public Color boardClickTint = new Color(0.2f, 0.8f, 0.2f, 1f); // 클릭 시 확 짙어지는 색

    [Header("Anti-Magic Skill (4번 안티매직 스킬 (본인 돌색깔 변경))")]
    public Color antiMagicOverlayColor = Color.cyan; // 안티매직 발동 시 내 돌을 덮어씌울 색상
    [Range(0f, 1f)] public float antiMagicOverlayBlend = 0.4f; // 색상이 덮어씌워지는 투명도(강도)
}

// -------------------------------------------------------------------------------------

public class BoardManager : MonoBehaviour
{
    public RuleManager ruleManager;
    public GameManager gameManager;

    [Header("3D Click Text Settings")]
    public GameObject boardClickTextObj;
    public Vector3 blackTextRotation = new Vector3(90, 0, 90);     // 흑돌 시점 회전값
    public Vector3 whiteTextRotation = new Vector3(-270, 180, 90); // 백돌 시점 회전값 

    [Header("Shield 3D Settings")]
    public Vector3 blackShieldRotation = new Vector3(0, 0, 0); // 흑돌 시점 방패 회전
    public Vector3 whiteShieldRotation = new Vector3(0, 180, 0); // 백돌 시점 방패 회전

    [Header("Forbidden Mark Settings (금수)")]
    public bool debugForbiddenMode = false; // 인스펙터 체크용 디버그 스위치
    private GameObject hoverForbiddenMarkInstance; // 마우스 올렸을 때 띄울 단일 마커

    [Header("Seal 3D Settings")]
    public Vector3 blackSealRotation = new Vector3(90, 90, 0);       // 흑돌 시점 자물쇠 회전
    public Vector3 whiteSealRotation = new Vector3(90, 270, 0);     // 백돌 시점 자물쇠 회전 

    private GameObject currentHoveredStone = null; // 현재 마우스가 올라가 있는 돌 기억용 (스킬 관련)

    [Header("Board Settings")]
    public int boardSize = 19;  // 19x19 격자
    public float gridSize = 1f; // -> InputManager의 gridSize와 동일해야 함!

    [Header("Visual Settings (기획자 튜닝용)")]
    public VisualSettings visualSettings; // 인스펙터에 노출

    [Header("Board Visuals")]
    public MeshRenderer boardRenderer; // 인스펙터에서 바둑판 3D 모델을 꼭 드래그해서 넣어주세요!
    private Material boardMaterial;
    private Color originalBoardColor;

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

    [Header("칼날비 칼 VFX 설정")]
    public float knifeStartHeight = 5f;    // 낙하 시작 높이
    public float knifeLandYOffset = -0.1f; // 착지 Y (보드판에 박히는 깊이)
    public float knifeFallDuration = 0.3f; // 낙하 시간 (초)
    public Vector3 knifeScale = Vector3.one; //칼 오브젝트 스케일 (인스펙터에서 조절 가능)
    public Vector3 knifeRotation = Vector3.zero; //칼 오브젝트 초기 회전 (인스펙터에서 조절 가능)
    public float knifeOffsetX = 0f; // ← X 오프셋
    public float knifeOffsetZ = 0f; // ← Z 오프셋

    // 2차원 배열 데이터 (0: 빈칸, 1: 흑돌, 2: 백돌)
    public int[,] grid;

    // 생성된 바둑돌들을 기억해둘 리스트 ('다시하기' 기능 시 필요) 
    private List<GameObject> activeStones = new List<GameObject>();
    private List<GameObject> forbiddenMarks = new List<GameObject>(); // '❌' 마커들을 담아둘 리스트

    // 좌표(Vector2Int)별로 떠 있는 자물쇠/신의가호(방패)/칼날비 칼 오브젝트를 기억하는 사전
    private Dictionary<Vector2Int, GameObject> activeSealMarkers = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, GameObject> activeShieldMarkers = new Dictionary<Vector2Int, GameObject>();
    public Dictionary<Vector2Int, GameObject> activeKnifeObjects = new Dictionary<Vector2Int, GameObject>();

    // ** 승리한 돌들을 담아둘 리스트
    private List<GameObject> winningStones = new List<GameObject>();

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

    // 방금 착수되어 0.6초간 깜빡이는(불투명 유지) 돌들을 보호할 리스트 추가
    private HashSet<GameObject> blinkingStones = new HashSet<GameObject>();
    void Awake()
    {
        // 게임 시작과 동시에 빈 배열 생성
        grid = new int[boardSize, boardSize];
        sealedGrid = new SealInfo[boardSize, boardSize];
        shieldGrid = new bool[boardSize, boardSize];

        AdjustCameraToBoardSize(); // 시작 시 카메라 자동 세팅

        // 바둑판 머티리얼 및 원본 색상 백업
        if (boardRenderer != null)
        {
            boardMaterial = boardRenderer.material; // 인스턴스화 됨
            if (boardMaterial.HasProperty("_BaseColor")) // URP 기준
                originalBoardColor = boardMaterial.GetColor("_BaseColor");
            else if (boardMaterial.HasProperty("_Color")) // 레거시 기준
                originalBoardColor = boardMaterial.GetColor("_Color");
            else
                originalBoardColor = Color.white;
        }
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
            // 칼날비(None)는 피아 식별 없이 무조건 입구컷
            // if (sealedGrid[x, y].owner == StoneColor.None)
            // {
            //     if (!silent) Debug.LogWarning("칼날비가 내린 곳에는 착수할 수 없습니다!");
            //     return false;
            // }
            // // if (playerColor != sealedGrid[x, y].owner)
            // {
            //     if (!silent) Debug.LogWarning("상대방에 의해 봉인된 칸입니다!");
            //     return false;
            // }
            if (sealedGrid[x, y].turns > 0)
            {
                if (!silent) Debug.LogWarning("봉인된 칸입니다!");
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

            // ** 신성화 상태라면 무조건 백돌 방향으로 강제 통일 (앞뒤 구분 방지)
            if (isConsecrationActive)
            {
                yRotation = whiteStoneYRotation;
            }

            GameObject newStone = ObjectPooler.Instance.SpawnFromPool(poolTag, spawnPos, Quaternion.Euler(0, yRotation, 0));
            MeshRenderer mr = newStone.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.enabled = true;
                mr.material.color = Color.white;
            }
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

            // 생성된 직후 "착수 애니메이션" 
            Animator anim = newStone.GetComponent<Animator>();
            if (anim != null) anim.SetTrigger("DoChaksu");

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

        // 디버그 모드가 꺼져있다면 전체 표시는 아예 안 하고 종료
        if (!debugForbiddenMode || ruleManager == null) return;

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

    // 단일 호버 ❌ 마커 제어 함수
    public void ShowHoverForbiddenMark(int x, int y)
    {
        if (hoverForbiddenMarkInstance == null)
        {
            // 첫 호버 시 풀에서 하나만 꺼내서 재사용
            hoverForbiddenMarkInstance = ObjectPooler.Instance.SpawnFromPool("ForbiddenMark", Vector3.zero, Quaternion.Euler(90, 0, 0));
        }
        hoverForbiddenMarkInstance.SetActive(true);
        hoverForbiddenMarkInstance.transform.position = new Vector3(x * gridSize, forbiddenYOffset, y * gridSize);
    }

    public void HideHoverForbiddenMark()
    {
        if (hoverForbiddenMarkInstance != null)
            hoverForbiddenMarkInstance.SetActive(false);
    }

    // 다음 게임을 위한 완벽 초기화 보장
    public void ClearBoard()
    {
        //  진행 중이던 시네마틱 연출(코루틴) 멱살 잡고 강제 종료
        StopAllCoroutines();

        // 무대 조명 끄기
        if (SkillVFXManager.Instance != null) SkillVFXManager.Instance.ClearVictoryStageEffect();

        foreach (GameObject stone in activeStones) stone.SetActive(false);
        activeStones.Clear();

        foreach (GameObject mark in forbiddenMarks) mark.SetActive(false);
        forbiddenMarks.Clear();

        foreach (var marker in activeSealMarkers.Values) marker.SetActive(false);
        activeSealMarkers.Clear();

        foreach (var knife in activeKnifeObjects.Values)
            if (knife != null) knife.SetActive(false);
        activeKnifeObjects.Clear();

        foreach (var marker in activeShieldMarkers.Values) marker.SetActive(false);
        activeShieldMarkers.Clear();

        System.Array.Clear(grid, 0, grid.Length);
        System.Array.Clear(shieldGrid, 0, shieldGrid.Length);
        System.Array.Clear(sealedGrid, 0, sealedGrid.Length);

        blinkingStones.Clear();
        winningStones.Clear(); 

        isConsecrationActive = false;
        Debug.Log("[BoardManager] 바둑판 데이터 및 바둑돌 초기화 완료!");
    }

    // 1. 전체 방향을 검사하는 메인 함수
    //public bool CheckWin(int x, int y, StoneColor playerColor)
    //{
    //    int currentWinCond = ruleManager.GetWinCondition((int)playerColor);

    //    if (CountStones(x, y, 1, 0, playerColor) >= currentWinCond) return true; // 가로
    //    if (CountStones(x, y, 0, 1, playerColor) >= currentWinCond) return true; // 세로
    //    if (CountStones(x, y, 1, 1, playerColor) >= currentWinCond) return true; // 우상향
    //    if (CountStones(x, y, 1, -1, playerColor) >= currentWinCond) return true; // 우하향

    //    return false;
    //}

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
    public bool HasNeighborInRadius(int cx, int cy, int radius)
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
        int radius = 2; // 반경 2칸 제한! (너무 멀리 안 날아가게)

        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                if (IsValidMove(x, y, playerColor, silent: true))
                {
                    // 주변 반경 내에 돌이 있을 때만 후보에 추가
                    if (HasNeighborInRadius(x, y, radius))
                    {
                        validMoves.Add(new Vector2Int(x, y));
                    }
                }
            }
        }

        // 주변 빈칸이 아예 없으면 (초반이거나 너무 꽉 찼을 때) 전체 유효 칸에서 다시 검색
        if (validMoves.Count == 0)
        {
            for (int x = 0; x < boardSize; x++)
                for (int y = 0; y < boardSize; y++)
                    if (IsValidMove(x, y, playerColor, silent: true))
                        validMoves.Add(new Vector2Int(x, y));
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
        
        if (activeSealMarkers.ContainsKey(posKey))
        {
            RemoveSealEffect(x, y);
        }
            
        if (!activeSealMarkers.ContainsKey(posKey))
        {
            Vector3 spawnPos = new Vector3(x * gridSize, sealYOffset, y * gridSize);

            // 내 컬러에 맞춰서 자물쇠 방향을 돌려서 생성
            Vector3 spawnRot = (gameManager.localPlayerColor == StoneColor.Black) ? blackSealRotation : whiteSealRotation;
            GameObject marker = ObjectPooler.Instance.SpawnFromPool("SealMarker", spawnPos, Quaternion.Euler(spawnRot));

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

    // 턴이 다 되어서 칼/자물쇠를 치워야 할 때 부르는 함수
    public void RemoveSealEffect(int x, int y)
    {
        Vector2Int posKey = new Vector2Int(x, y);
        if (activeSealMarkers.TryGetValue(posKey, out GameObject marker))
        {
            marker.SetActive(false);
            activeSealMarkers.Remove(posKey);
        }

        // [추가] 칼날비 칼 오브젝트 제거
        if (activeKnifeObjects.TryGetValue(posKey, out GameObject knife))
        {
            knife.SetActive(false);
            activeKnifeObjects.Remove(posKey);
        }
    }

    // 칼날비 전용 봉인 — 마커 없이 칼 오브젝트 낙하
    public void ApplySealWithKnife(int x, int y, int turns, StoneColor ownerColor)//x,y로 적혀있지만, 유니티상에서는 x,z 좌표입니다. y는 높이값으로 고정
    {
        // 봉인 데이터는 그대로 적용
        sealedGrid[x, y].turns = turns;
        sealedGrid[x, y].owner = ownerColor;
 
        Vector2Int posKey = new Vector2Int(x, y);
        if (activeKnifeObjects.ContainsKey(posKey)) return;
 
        // 칼 소환 — 시작 위치는 높은 곳
        Vector3 startPos = new Vector3(
        x * gridSize + knifeOffsetX,
        knifeStartHeight,
        y * gridSize + knifeOffsetZ);

        GameObject knife = ObjectPooler.Instance.SpawnFromPool("BladefallKnife", startPos, Quaternion.identity);
        Debug.Log($"[BladefallKnife] 꺼낸 결과: {(knife == null ? "null" : knife.name)}");
        if (knife == null) return;

        // ** 칼날비 오브젝트에 빨간색 아웃라인(오버레이) 강하게 적용
        StoneVisualController svc = knife.GetComponent<StoneVisualController>();
        if (svc != null)
        {
            svc.SetOverlay(Color.red, 0.8f);
        }

        knife.transform.localScale = knifeScale; // 인스펙터에서 조절한 스케일 적용
        knife.transform.rotation = Quaternion.Euler(knifeRotation);

        activeKnifeObjects.Add(posKey, knife);
 
        // 착지 목표 위치
        Vector3 targetPos = new Vector3(
        x * gridSize + knifeOffsetX,
        knifeLandYOffset,
        y * gridSize + knifeOffsetZ);
 
        // 낙하 코루틴 시작
        StartCoroutine(KnifeFallRoutine(knife, startPos, targetPos, knifeFallDuration));
    }

     // 낙하 코루틴 — 위에서 아래로 가속하며 떨어짐
    private IEnumerator KnifeFallRoutine(GameObject knife, Vector3 from, Vector3 to, float duration)
    {   
        Debug.Log($"[BladefallKnife] 낙하 시작 위치: {from}");  // ← 추가
        float elapsed = 0f;
 
        while (elapsed < duration)
        {
            if (knife == null || !knife.activeSelf) yield break;
 
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
 
            // 가속감 — EaseIn (처음엔 느리다가 빠르게)
            float eased = t * t;
 
            knife.transform.position = Vector3.Lerp(from, to, eased);
            yield return null;
        }
 
        // 정확한 착지 위치 고정
        if (knife != null && knife.activeSelf)
            knife.transform.position = to;

        Debug.Log($"[BladefallKnife] 착지 완료 위치: {to}");  // ← 추가
    }

    // 보호막 씌우는 핵심 API
    public void ApplyShield(int x, int y)
    {
        shieldGrid[x, y] = true;
        Vector2Int posKey = new Vector2Int(x, y);

        if (!activeShieldMarkers.ContainsKey(posKey))
        {
            // 설정된 shieldYOffset 적용
            Vector3 spawnPos = new Vector3(x * gridSize, shieldYOffset, y * gridSize);

            // 내 컬러에 맞춰서 방패 방향을 돌려서 생성 (자물쇠와 동일한 방식)
            Vector3 spawnRot = (gameManager.localPlayerColor == StoneColor.Black) ? blackShieldRotation : whiteShieldRotation;
            GameObject marker = ObjectPooler.Instance.SpawnFromPool("ShieldMarker", spawnPos, Quaternion.Euler(spawnRot));

            if (marker != null)
            {
                activeShieldMarkers.Add(posKey, marker);

                StoneColor stoneColor = (StoneColor)grid[x, y];
                bool isMyStone = (stoneColor == gameManager.localPlayerColor);
                bool isOpponentInvisible = (gameManager.skillManager != null && gameManager.skillManager.oppInvisibilityTurns > 0);

                // 돌이 상대방 투명 상태라면 방패도 태어날 때부터 안 보이게 처리
                if (!isMyStone && isOpponentInvisible)
                {
                    marker.SetActive(false);
                }
                else
                {
                    marker.SetActive(true);
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
                    // 타겟팅 마커를 끄면서 안티매직/신성화 상태 복구
                    ApplyStoneBuffVisuals(stoneObj, svc);
                }
            }
        }
    }

    // 방패 지우는 함수
    public void RemoveShield(int x, int y)
    {
        shieldGrid[x, y] = false;
        Vector2Int posKey = new Vector2Int(x, y);
        if (activeShieldMarkers.TryGetValue(posKey, out GameObject marker))
        {
            marker.SetActive(false);
            activeShieldMarkers.Remove(posKey);
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
                GameObject tempStone = currentHoveredStone;
                currentHoveredStone = null;

                ApplyStoneBuffVisuals(tempStone, svc);

                int x = Mathf.RoundToInt(tempStone.transform.position.x / gridSize);
                int y = Mathf.RoundToInt(tempStone.transform.position.z / gridSize);
                StoneColor stoneColor = (StoneColor)grid[x, y];
                bool isMyStone = (stoneColor == gameManager.localPlayerColor);

                bool isOpponentInvisible = (gameManager.skillManager != null && gameManager.skillManager.oppInvisibilityTurns > 0);
                bool isMyInvisibilityActive = (gameManager.skillManager != null && gameManager.skillManager.myInvisibilityTurns > 0);

                // 돌의 렌더링(투명화) 상태를 완벽하게 재평가하여 강제 적용!
                if (isMyStone && isMyInvisibilityActive)
                {
                    float gAlpha = (stoneColor == StoneColor.Black) ? visualSettings.blackGhostAlpha : visualSettings.whiteGhostAlpha;
                    float gMet = (stoneColor == StoneColor.Black) ? visualSettings.blackGhostMetallic : visualSettings.whiteGhostMetallic;
                    float gSmo = (stoneColor == StoneColor.Black) ? visualSettings.blackGhostSmoothness : visualSettings.whiteGhostSmoothness;
                    svc.SetVisibility(true, true, gAlpha, gMet, gSmo);
                }
                else if (!isMyStone && isOpponentInvisible)
                {
                    // 상대 투명돌이면 얄짤없이 완전 숨김!
                    svc.SetVisibility(false, false);
                }
                else
                {
                    svc.SetVisibility(true, false);
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
                if (svc != null) ApplyStoneBuffVisuals(stone, svc);
            }
        }
    }

    public void ApplyStoneBuffVisuals(GameObject stoneObj, StoneVisualController svc)
    {
        // 1순위: 승리한 돌 (가장 중요하므로 무조건 빨간 굵은 테두리 유지)
        if (winningStones.Contains(stoneObj))
        {
            svc.SetConsecration(true, Color.red, 2f, 8f);
            svc.SetOverlay(Color.black, 0f); // 혹시 모를 색깔 제거
            return;
        }

        int x = Mathf.RoundToInt(stoneObj.transform.position.x / gridSize);
        int y = Mathf.RoundToInt(stoneObj.transform.position.z / gridSize);
        StoneColor stoneColor = (StoneColor)grid[x, y];

        // --- A. 신성화 테두리 (백돌 시전자 기준) ---
        if (isConsecrationActive && gameManager.localPlayerColor == StoneColor.White && stoneColor == StoneColor.White)
        {
            svc.SetConsecration(true, visualSettings.consecrationOutlineColor, visualSettings.consecrationThickness, visualSettings.consecrationGlow);
        }
        else
        {
            svc.SetConsecration(false, Color.black);
        }

        // --- B. 오버레이(색상) 덮어쓰기 로직 ---
        bool isPendingRemove = false;
        bool shouldShowAntiMagic = false;

        if (gameManager.skillManager != null)
        {
            if (gameManager.skillManager.pendingRemoveTarget.x == x && gameManager.skillManager.pendingRemoveTarget.y == y)
                isPendingRemove = true;

            if (stoneColor == gameManager.localPlayerColor)
            {
                foreach (var eff in gameManager.skillManager.activeEffects)
                {
                    if (eff.skillId == 4 && eff.casterColor == gameManager.localPlayerColor)
                    {
                        shouldShowAntiMagic = true;
                        break;
                    }
                }
            }
        }

        // 마우스 호버 중이 아닐 때만 버프 색상 표시
        if (currentHoveredStone != stoneObj)
        {
            // 2순위: 삭제 대기 중인 돌 (진한 빨간색 오버레이)
            if (isPendingRemove)
            {
                svc.SetOverlay(Color.red, 0.8f);
            }
            // 3순위: 안티매직 (하늘색 오버레이)
            else if (shouldShowAntiMagic)
            {
                svc.SetOverlay(visualSettings.antiMagicOverlayColor, visualSettings.antiMagicOverlayBlend);
            }
            // 4순위: 버프 없음 (기본 상태)
            else
            {
                svc.SetOverlay(Color.black, 0f);
            }
        }
    }

    // 투명화 상태라도 착수 시 0.6초간 보여준 뒤 숨기는 헬퍼 코루틴
    public IEnumerator BlinkAndHideRoutine(GameObject stone, StoneColor color, bool isMyStone)
    {
        StoneVisualController svc = stone.GetComponent<StoneVisualController>();
        if (svc == null) yield break;

        blinkingStones.Add(stone); // 🚨 락(Lock) 걸기: 새로고침 간섭 차단

        // 1. 0.6초 동안 100% 불투명한 상태로 노랗게 번쩍이게 만듦
        svc.SetVisibility(true, false);
        svc.PlayBlinkEffect(visualSettings.extraPlaceBlinkColor, 0.7f);

        // 2. 대기 (상대방 눈에도, 내 눈에도 불투명도 100%로 확실하게 보임)
        yield return new WaitForSeconds(0.6f);

        blinkingStones.Remove(stone); // 🚨 락 해제

        // 3. 0.6초가 지난 후 다시 싹 새로고침!
        // (이때 안티매직 색깔, 턴이 지나서 풀린 투명화, 유지되는 투명화 등이 완벽하게 재평가되어 입혀짐)
        RefreshAllStonesVisuals();
    }

    // 바둑판 전체 색상 변경 (0: 원상복구, 1: 호버, 2: 클릭)
    public void SetBoardOverlayState(int state)
    {
        if (boardMaterial == null) return;

        Color baseColor = originalBoardColor; // 원래 바둑판 텍스처/색상
        Color emissionColor = Color.black;    // 빛 반사(오버레이) 끄기

        if (state == 1)
        {
            // 1. 대기: 보드판 자체가 반투명해짐 (원래 색상에 알파값만 깎음)
            baseColor = new Color(originalBoardColor.r, originalBoardColor.g, originalBoardColor.b, visualSettings.boardReadyTint.a);
        }
        else if (state == 2)
        {
            // 2. 호버: 불투명 바둑판(원상복구) + 은은한 반투명 초록색 오버레이
            baseColor = originalBoardColor;
            emissionColor = visualSettings.boardHoverTint;
        }
        else if (state == 3)
        {
            // 3. 클릭: 진한 초록색 오버레이
            baseColor = originalBoardColor;
            emissionColor = visualSettings.boardClickTint;
        }

        // _BaseColor (URP) 또는 _Color (Legacy) 에 알파값이 포함된 베이스 컬러 적용
        if (boardMaterial.HasProperty("_BaseColor")) boardMaterial.SetColor("_BaseColor", baseColor);
        else if (boardMaterial.HasProperty("_Color")) boardMaterial.SetColor("_Color", baseColor);

        // 은은한 초록색 덧씌우기는 자체 발광(Emission) 기능을 활용하여 오버레이 효과 냄
        boardMaterial.EnableKeyword("_EMISSION");
        boardMaterial.SetColor("_EmissionColor", emissionColor);

        // 상태가 1(대기)이거나 2(호버)일 때만 3D 'Click!' 글씨를 켭니다
        if (boardClickTextObj != null)
            boardClickTextObj.SetActive(state == 1 || state == 2);
    }

    // 내 색깔에 맞춰 'Click!' 텍스트 방향 세팅하는 함수
    public void SetupBoardTextRotation(StoneColor localColor)
    {
        if (boardClickTextObj != null)
        {
            boardClickTextObj.transform.rotation = Quaternion.Euler(localColor == StoneColor.Black ? blackTextRotation : whiteTextRotation);
        }
    }

    // 승리한 돌 좌표들만 쏙 뽑아오는 함수 (기존 AI 코드를 망치지 않기 위함)
    public List<Vector2Int> GetWinningStones(int x, int y, StoneColor playerColor)
    {
        int currentWinCond = ruleManager.GetWinCondition((int)playerColor);

        List<Vector2Int> result = GetLineStones(x, y, 1, 0, playerColor);
        if (result.Count >= currentWinCond) return result;

        result = GetLineStones(x, y, 0, 1, playerColor);
        if (result.Count >= currentWinCond) return result;

        result = GetLineStones(x, y, 1, 1, playerColor);
        if (result.Count >= currentWinCond) return result;

        result = GetLineStones(x, y, 1, -1, playerColor);
        if (result.Count >= currentWinCond) return result;

        return null;
    }

    private List<Vector2Int> GetLineStones(int startX, int startY, int dirX, int dirY, StoneColor playerColor)
    {
        List<Vector2Int> line = new List<Vector2Int>();
        line.Add(new Vector2Int(startX, startY));
        int playerInt = (int)playerColor;

        int nx = startX + dirX; int ny = startY + dirY;
        while (nx >= 0 && nx < boardSize && ny >= 0 && ny < boardSize && grid[nx, ny] == playerInt)
        {
            line.Add(new Vector2Int(nx, ny));
            nx += dirX; ny += dirY;
        }

        nx = startX - dirX; ny = startY - dirY;
        while (nx >= 0 && nx < boardSize && ny >= 0 && ny < boardSize && grid[nx, ny] == playerInt)
        {
            line.Add(new Vector2Int(nx, ny));
            nx -= dirX; ny -= dirY;
        }
        return line;
    }

    // 인자에 winnerColor 추가
    public void HighlightWinningStones(List<Vector2Int> winningCoords, StoneColor winnerColor)
    {
        winningStones.Clear();

        foreach (Vector2Int pos in winningCoords)
        {
            GameObject stone = GetStoneObjectAt(pos.x, pos.y);
            if (stone != null)
            {
                winningStones.Add(stone);
                StoneVisualController svc = stone.GetComponent<StoneVisualController>();
                if (svc != null)
                {
                    // [투명화 즉시 해제] 승리한 주인공들은 그 즉시 정체를 드러냄
                    svc.SetVisibility(true, false);
                    svc.SetOverlay(Color.black, 0f);

                    // 빨간 테두리 씌우기
                    svc.SetConsecration(true, Color.red, 2f, 8f);
                }
            }
        }

        // 시네마틱 루틴 시작
        StartCoroutine(VictoryCinematicRoutine(winningStones, winnerColor, winningCoords));
    }

    // 엔딩 시네마틱 마스터 컨트롤러
    private IEnumerator VictoryCinematicRoutine(List<GameObject> winners, StoneColor winnerColor, List<Vector2Int> winningCoords)
    {
        // [A] 주인공들 빨간 테두리 상태로 2초간 여운 (투명화는 이미 풀린 상태)
        yield return new WaitForSeconds(2.0f);

        // [B] 신성화 상태라면 이제서야 흑돌로 촤라락 변신 (회전값 보정 포함)
        if (isConsecrationActive)
        {
            yield return StartCoroutine(RevealAllBlackStonesWave(winners));
        }

        // [C] 카메라 이동 시작
        Vector3 centerPos = GetWinningLineCenter(winningCoords);
        if (gameManager.cameraSwitcher != null)
            StartCoroutine(gameManager.cameraSwitcher.VictoryCinematicCamera(centerPos, winnerColor, winningCoords));

        // SkillVFXManager를 호출해서 조명 3개 방황 + 폭죽 터뜨리기! (시간은 카메라 이동 시간과 맞춤)
        if (SkillVFXManager.Instance != null)
        {
            float camDuration = gameManager.cameraSwitcher.cinematicDuration; // 보통 3~3.5초
            SkillVFXManager.Instance.PlayVictoryStageEffect(centerPos, camDuration);
        }

        // [D] 돌 정리 (진 돌 날리기 등)
        CleanUpStonesForVictory(winners, winnerColor);

        // [E] 주인공들 빨간 테두리 벗기기
        foreach (var stone in winners)
        {
            stone.GetComponent<StoneVisualController>()?.SetConsecration(false, Color.black);
        }

        // ** 돌들이 카메라 렌즈를 쳐다보게 회전
        if (gameManager.cameraSwitcher != null && gameManager.cameraSwitcher.victoryCamera != null)
        {
            Vector3 camPos = gameManager.cameraSwitcher.victoryCamera.transform.position;

            foreach (var stone in winners)
            {
                if (stone != null)
                {
                    // 돌에서 카메라를 바라보는 방향 계산
                    Vector3 lookDir = camPos - stone.transform.position;
                    lookDir.y = 0; // 캐릭터가 하늘을 보며 뒤로 눕지 않게 Y축 회전만 고정!

                    // 자연스럽게 휙! 하고 돌아봄
                    stone.transform.rotation = Quaternion.LookRotation(lookDir);
                }
            }
        }

        // [F] 주인공들 파도타기 점프 (7목이든 뭐든 리스트 순서대로 뜀)
        for (int i = 0; i < 3; i++) // 3번 반복
        {
            foreach (var stone in winners)
            {
                if (stone != null) stone.GetComponent<Animator>()?.SetTrigger("DoWin");
                yield return new WaitForSeconds(0.05f); // 0.1초 간격 파도타기
            }
            yield return new WaitForSeconds(0.5f); // 한 세트 점프 후 휴식
        }
    }

    // 신성화 해제 파도 연출 (바둑판 전체의 흑돌을 싹 다 본모습으로)
    private IEnumerator RevealAllBlackStonesWave(List<GameObject> winners)
    {
        List<GameObject> blackStonesToReveal = new List<GameObject>();

        // 1. 맵 전체를 뒤져서 '진짜 흑돌'인 녀석들만 모음
        foreach (GameObject stone in activeStones)
        {
            int x = Mathf.RoundToInt(stone.transform.position.x / gridSize);
            int y = Mathf.RoundToInt(stone.transform.position.z / gridSize);

            if (grid[x, y] == (int)StoneColor.Black)
            {
                blackStonesToReveal.Add(stone);
            }
        }

        // 2. 찾은 흑돌들을 순차적으로 변신!
        for (int i = 0; i < blackStonesToReveal.Count; i++)
        {
            GameObject stone = blackStonesToReveal[i];
            Vector3 pos = stone.transform.position;

            // 흑돌 원래의 정면 각도
            Quaternion correctRot = Quaternion.Euler(0, blackStoneYRotation, 0);

            // 기존 가짜 백돌 비활성
            stone.SetActive(false);

            // 진짜 흑돌 소환
            GameObject realBlack = ObjectPooler.Instance.SpawnFromPool("BlackStone", pos, correctRot);

            // 리스트 안전하게 교체 (에러 방지 & 초기화 완벽 대응)
            int activeIndex = activeStones.IndexOf(stone);
            if (activeIndex != -1) activeStones[activeIndex] = realBlack;

            // 만약 이 흑돌이 5목(winners)에 포함된 녀석이라면? (흑돌이 이긴 경우)
            // winners 리스트도 교체해주고 빨간 테두리를 유지해줍니다.
            int winnerIndex = winners.IndexOf(stone);
            if (winnerIndex != -1)
            {
                winners[winnerIndex] = realBlack;
                realBlack.GetComponent<StoneVisualController>()?.SetConsecration(true, Color.red, 2f, 8f);
            }

            // 0.05초면 맵 전체가 빠르게 촤라락! 하고 변하는 느낌이 납니다.
            yield return new WaitForSeconds(0.05f);
        }

        isConsecrationActive = false; // 파도가 끝났으니 신성화 상태 완전 해제
    }

    // 1. 돌 정리 + 보드판 위 찌꺼기 완벽 제거
    private void CleanUpStonesForVictory(List<GameObject> winners, StoneColor winnerColor)
    {
        StoneColor loserColor = winnerColor.Opponent();

        // [돌 정리]
        foreach (var stone in activeStones)
        {
            if (winners.Contains(stone)) continue; // 이긴 돌은 무시

            int x = Mathf.RoundToInt(stone.transform.position.x / gridSize);
            int y = Mathf.RoundToInt(stone.transform.position.z / gridSize);
            StoneColor stoneColor = (StoneColor)grid[x, y];

            if (stoneColor == loserColor)
            {
                // 상대방 돌은 영혼 소환해서 썅! 날려버리기
                string poolTag = (loserColor == StoneColor.Black) ? "BlackStone" : "WhiteStone";
                GameObject fake = ObjectPooler.Instance.SpawnFromPool(poolTag, stone.transform.position, stone.transform.rotation);
                if (fake != null) StartCoroutine(StoneFlyRoutine(fake));
            }
            stone.SetActive(false); // 본체는 숨김 (우리 팀 안 이긴 돌은 여기서 그냥 뿅 사라짐)
        }

        // [찌꺼기 가리기] 승리 연출을 방해하는 모든 마커 강제 숨김
        foreach (var marker in activeShieldMarkers.Values) marker.SetActive(false);
        foreach (var marker in activeSealMarkers.Values) marker.SetActive(false);
        foreach (var knife in activeKnifeObjects.Values) if (knife != null) knife.SetActive(false);
        foreach (var mark in forbiddenMarks) if (mark != null) mark.SetActive(false);

        // 타겟팅 테두리 및 바닥 오버레이 색상 강제 초기화
        SetBoardOverlayState(0);
        HideSkillTargetMarkers();
    }

    // ** 이중착수 시 확실하게 번쩍거리는 전용 코루틴
    public IEnumerator HighlightExtraStoneRoutine(GameObject stone, Color blinkColor)
    {
        StoneVisualController svc = stone.GetComponent<StoneVisualController>();
        if (svc == null) yield break;

        blinkingStones.Add(stone); // 깜빡이는 동안 렌더 갱신 차단

        // 1.5초 동안 지정한 색상(빨간색 등)으로 크고 강하게 3번 점멸!
        for (int i = 0; i < 3; i++)
        {
            svc.SetOverlay(blinkColor, 0.9f); // 0.9f면 엄청 찐합니다
            yield return new WaitForSeconds(0.25f);
            svc.SetOverlay(Color.black, 0f);
            yield return new WaitForSeconds(0.25f);
        }

        blinkingStones.Remove(stone);
        RefreshAllStonesVisuals(); // 점멸 끝나면 버프 원상복구
    }

    // =========================================================
    //  패배 돌 튕겨내기 연출 (시네마틱)
    // =========================================================
    public IEnumerator BlowUpDefeatedStones(List<GameObject> winners, StoneColor loserColor)
    {
        int loserInt = (int)loserColor;

        foreach (var stone in activeStones)
        {
            // 승리한 5목 돌이면 무시
            if (winners.Contains(stone)) continue;

            int x = Mathf.RoundToInt(stone.transform.position.x / gridSize);
            int y = Mathf.RoundToInt(stone.transform.position.z / gridSize);

            // 🚨 "진 돌(패배자 색상)"만 날려버리기
            if (grid[x, y] == loserInt)
            {
                // 1. 가짜 돌(더미) 생성 (원래 돌 위치/회전 그대로)
                string poolTag = (loserColor == StoneColor.Black) ? "BlackStone" : "WhiteStone";
                GameObject fakeStone = ObjectPooler.Instance.SpawnFromPool(poolTag, stone.transform.position, stone.transform.rotation);

                // 2. 가짜 돌 날리기!
                if (fakeStone != null) StartCoroutine(StoneFlyRoutine(fakeStone));

                // 3. (선택) 보드에 남아있는 진짜 돌은 살짝 어둡게(까맣게) 만들어서 죽은 돌 느낌 내기
                StoneVisualController svc = stone.GetComponent<StoneVisualController>();
                if (svc != null) svc.SetOverlay(Color.black, 0.6f);
            }
        }
        yield return null;
    }

    private IEnumerator StoneFlyRoutine(GameObject stone)
    {
        // 돌의 메쉬를 끄지 않고, 콜라이더만 꺼서 서로 안 부딪히게 함 (성능 최적화)
        if (stone.TryGetComponent<Collider>(out var col)) col.enabled = false;

        // 랜덤한 비행 방향 설정 (바깥쪽으로 퍼지게 + 위로 솟구치게)
        Vector3 flyDirection = new Vector3(
            UnityEngine.Random.Range(-1f, 1f),
            UnityEngine.Random.Range(1.5f, 2.5f), // 위로 솟구치는 힘
            UnityEngine.Random.Range(-1f, 1f)
        ).normalized;

        float speed = UnityEngine.Random.Range(8f, 15f); // 날아가는 속도
        float rotationSpeed = UnityEngine.Random.Range(300f, 600f); // 빙글빙글 회전 속도
        Vector3 rotAxis = UnityEngine.Random.onUnitSphere; // 회전축 랜덤

        float elapsed = 0f;
        while (elapsed < 2f) // 2초 동안 날아감
        {
            elapsed += Time.deltaTime;

            // 중력 가속도 계산 (점점 아래로 떨어지게)
            flyDirection += Vector3.down * Time.deltaTime * 2.5f;

            // 위치 이동
            stone.transform.position += flyDirection * speed * Time.deltaTime;

            // 빙글빙글 회전
            stone.transform.Rotate(rotAxis, rotationSpeed * Time.deltaTime);

            // 점점 작아지면서 사라지는 효과 (선택 사항)
            stone.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, elapsed / 2f);

            yield return null;
        }

        stone.SetActive(false); // 마지막에 비활성화
    }

    // 승리한 돌들의 "중심점"과 "정면" 계산하기 (시네머신 준비)
    public Vector3 GetWinningLineCenter(List<Vector2Int> winningCoords)
    {
        Vector3 sum = Vector3.zero;
        foreach (var pos in winningCoords)
        {
            sum += new Vector3(pos.x * gridSize, stoneYOffset, pos.y * gridSize);
        }
        return sum / winningCoords.Count; // 평균 위치 (중심점)
    }

    // =========================================================
    // 🚨 배열 좌표(x, y)를 3D 실제 좌표(Vector3)로 변환해주는 헬퍼 함수
    // =========================================================
    public Vector3 GetWorldPosition(int x, int y)
    {
        return new Vector3(x * gridSize, stoneYOffset, y * gridSize);
    }

    // ------------------------------------------------------------
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

            if (gameManager.currentTurnColor != StoneColor.None) UpdateForbiddenMarks(gameManager.currentTurnColor);
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

            // 핵심 추가: 깜빡이는 연출이 진행 중인 돌은 새로고침에서 제외 (깜빡임 끝난 후 스스로 호출함)
            if (blinkingStones.Contains(stoneObj)) continue;

            StoneVisualController svc = stoneObj.GetComponent<StoneVisualController>();
            if (svc == null) continue;

            int x = Mathf.RoundToInt(stoneObj.transform.position.x / gridSize);
            int y = Mathf.RoundToInt(stoneObj.transform.position.z / gridSize);
            StoneColor stoneColor = (StoneColor)grid[x, y];
            bool isMyStone = (stoneColor == gameManager.localPlayerColor);

            float gAlpha = (stoneColor == StoneColor.Black) ? visualSettings.blackGhostAlpha : visualSettings.whiteGhostAlpha;
            float gMet = (stoneColor == StoneColor.Black) ? visualSettings.blackGhostMetallic : visualSettings.whiteGhostMetallic;
            float gSmo = (stoneColor == StoneColor.Black) ? visualSettings.blackGhostSmoothness : visualSettings.whiteGhostSmoothness;

            Vector2Int posKey = new Vector2Int(x, y);

            if (isMyStone && isMyInvisibilityActive)
            {
                svc.SetVisibility(true, true, gAlpha, gMet, gSmo);
                if (activeShieldMarkers.TryGetValue(posKey, out GameObject shield)) shield.SetActive(true);
            }
            else if (!isMyStone && isOpponentInvisible)
            {
                svc.SetVisibility(false, false);
                // 갱신될 때도 방패 끄기
                if (activeShieldMarkers.TryGetValue(posKey, out GameObject shield)) shield.SetActive(false);
            }
            else
            {
                svc.SetVisibility(true, false);
                if (activeShieldMarkers.TryGetValue(posKey, out GameObject shield)) shield.SetActive(true);
            }

            ApplyStoneBuffVisuals(stoneObj, svc);
        }
    }
}
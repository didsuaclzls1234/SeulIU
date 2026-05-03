using UnityEngine;
using UnityEngine.EventSystems;

public class InputManager : MonoBehaviour
{
    public GameManager gameManager;
    public SkillManager skillManager;
    public float gridSize = 1f;

    [Header("Hover Settings")]
    public GameObject blackHoverPrefab;  // 흑돌 호버 프리팹
    public GameObject whiteHoverPrefab;  // 백돌 호버 프리팹
    private GameObject blackHoverIndicator;
    private GameObject whiteHoverIndicator;
    public float hoverYOffset = 0.1f;

    [Header("Skill Visual Settings")] // ** 스킬 타겟팅 시 나오는 빨간 점 (돌이동, 제거, 봉인 등)
    public GameObject targetingHoverPrefab; // 인스펙터에서 빨간 점 프리팹 연결
    private GameObject targetingHoverIndicator;
    public float targetingHoverYOffset = 0.6f; // 돌 위에 잘 보이도록 살짝 높게 띄움

    // 콜라이더 대신 사용할 '수학적인 무한 평면 (Y=0 바닥)'
    private Plane mathPlane = new Plane(Vector3.up, Vector3.zero);

    // 찰나의 중복 클릭이나 마우스 이동을 막기 위한 잠금 변수
    private bool isProcessingClick = false;
    // 외부에서 입력 차단할 때 사용하는 변수
    private bool _isInputBlocked = false;

    void Start()
    {
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        if (skillManager == null) skillManager = FindFirstObjectByType<SkillManager>();

        // 호버 투명도를 BoardManager의 세팅값에서 가져오기 위한 안전 장치 (VisualSettings 통째로 가져오기)
        VisualSettings vs = (gameManager != null && gameManager.board != null) ? gameManager.board.visualSettings : new VisualSettings();

        // ==========================================
        // 1. 흑돌 호버 세팅
        // ==========================================
        if (blackHoverPrefab != null)
        {
            blackHoverIndicator = Instantiate(blackHoverPrefab);
            blackHoverIndicator.name = "BlackHoverIndicator_Dynamic";
            blackHoverIndicator.SetActive(false);

            StoneVisualController svc = blackHoverIndicator.GetComponent<StoneVisualController>();
            // 인스펙터 연동! (흑돌 전용 세팅값 주입)
            if (svc != null) svc.SetVisibility(true, true, vs.blackHoverAlpha, vs.blackHoverMetallic, vs.blackHoverSmoothness);
        }

        // ==========================================
        // 2. 백돌 호버 세팅 
        // ==========================================
        if (whiteHoverPrefab != null)
        {
            whiteHoverIndicator = Instantiate(whiteHoverPrefab);
            whiteHoverIndicator.name = "WhiteHoverIndicator_Dynamic";
            whiteHoverIndicator.SetActive(false);

            StoneVisualController svc = whiteHoverIndicator.GetComponent<StoneVisualController>();
            // 인스펙터 연동! (백돌 전용 세팅값 주입)
            if (svc != null) svc.SetVisibility(true, true, vs.whiteHoverAlpha, vs.whiteHoverMetallic, vs.whiteHoverSmoothness);
        }

        // ==========================================
        // 3. 스킬 타겟팅 조준점(빨간 점) 세팅
        // ==========================================
        if (targetingHoverPrefab != null)
        {
            targetingHoverIndicator = Instantiate(targetingHoverPrefab);
            targetingHoverIndicator.name = "TargetingHoverIndicator_Dynamic";
            targetingHoverIndicator.SetActive(false);
        }
    }

    void Update()
    {
        if (ShouldBlockInput())
        {
            HideHover();
            return;
        }

        if (Input.GetMouseButtonDown(1) && gameManager.currentState == GameState.SkillTargeting)
        {
            CancelSkillTargeting();
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!mathPlane.Raycast(ray, out float enter))
        {
            HideHover();
            return;
        }

        Vector3 hitPoint = ray.GetPoint(enter);
        int x = Mathf.RoundToInt(hitPoint.x / gridSize);
        int y = Mathf.RoundToInt(hitPoint.z / gridSize);

        if (gameManager.board == null || x < 0 || x >= gameManager.board.boardSize || y < 0 || y >= gameManager.board.boardSize)
        {
            HideHover();
            return;
        }

        UpdateHoverVisuals(x, y);

        if (Input.GetMouseButtonDown(0))
        {
            ProcessClick(x, y);
        }
    }

    private bool ShouldBlockInput()
    {
        bool isPointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        bool isInvalidState = gameManager.currentState != GameState.Playing && gameManager.currentState != GameState.SkillTargeting;
        bool isNotMyTurn = gameManager.currentMode != PlayMode.Solo && gameManager.currentTurnColor != gameManager.localPlayerColor;

        return isPointerOverUI || isInvalidState || isNotMyTurn || isProcessingClick || _isInputBlocked;
    }

    private void UpdateHoverVisuals(int x, int y)
    {
        // 1. 상태에 따라 캐릭터 프리뷰 vs 빨간 점 스위칭
        if (gameManager.currentState == GameState.SkillTargeting)
        {
            // 스킬 조준 중: 일반 바둑돌 숨기고 빨간 점 켜기
            if (blackHoverIndicator != null) blackHoverIndicator.SetActive(false);
            if (whiteHoverIndicator != null) whiteHoverIndicator.SetActive(false);

            if (targetingHoverIndicator != null)
            {
                targetingHoverIndicator.SetActive(true);
                // 마우스 위치(바둑판 교차점) 위로 띄워서 따라다니게 함
                targetingHoverIndicator.transform.position = new Vector3(x * gridSize, targetingHoverYOffset, y * gridSize);
            }
        }
        else
        {
            // 일반 착수 중: 빨간 점 숨기고 캐릭터 바둑돌 켜기!
            if (targetingHoverIndicator != null) targetingHoverIndicator.SetActive(false);

            int displayColor = (gameManager.currentMode == PlayMode.Solo) ?
                               (int)gameManager.currentTurnColor :
                               (int)gameManager.localPlayerColor;

            GameObject activeHover = (displayColor == 1) ? blackHoverIndicator : whiteHoverIndicator;
            GameObject inactiveHover = (displayColor == 1) ? whiteHoverIndicator : blackHoverIndicator;

            if (inactiveHover != null) inactiveHover.SetActive(false);

            if (activeHover != null)
            {
                activeHover.SetActive(true);
                activeHover.transform.position = new Vector3(x * gridSize, hoverYOffset, y * gridSize);

                float yRotation = (displayColor == 1) ?
                                  gameManager.board.blackStoneYRotation :
                                  gameManager.board.whiteStoneYRotation;
                activeHover.transform.rotation = Quaternion.Euler(0, yRotation, 0);
            }
        }

        // 2. 타겟팅 호버링 아웃라인(테두리) 처리
        if (gameManager.currentState == GameState.SkillTargeting && skillManager != null)
        {
            int skillId = skillManager.GetSelectedSkillId();
            int myColorInt = (int)gameManager.localPlayerColor;
            int enemyColorInt = (gameManager.localPlayerColor == StoneColor.Black) ? 2 : 1;

            if (skillId == 5) // 상대 돌 타겟팅 스킬 (제거 등)
            {
                if (gameManager.board.grid[x, y] == enemyColorInt && !gameManager.board.shieldGrid[x, y])
                    gameManager.board.HighlightSingleStone(x, y, gameManager.board.visualSettings.enemyHoverHighlightColor);
                else
                    gameManager.board.ClearHoverHighlight();
            }
            else if (skillId == 1 || skillId == 8) // 내 돌 타겟팅 스킬 (신의가호, 돌이동 등)
            {
                if (gameManager.board.grid[x, y] == myColorInt)
                    gameManager.board.HighlightSingleStone(x, y, gameManager.board.visualSettings.myHoverHighlightColor);
                else
                    gameManager.board.ClearHoverHighlight();
            }
            else
            {
                gameManager.board.ClearHoverHighlight();
            }
        }
    }

    private void ProcessClick(int x, int y)
    {
        isProcessingClick = true;
        HideHover();

        if (gameManager.currentState == GameState.Playing)
        {
            gameManager.TryPlaceStone(x, y);
        }
        else if (gameManager.currentState == GameState.SkillTargeting && skillManager != null)
        {
            skillManager.ExecuteSkillAt(x, y);
        }

        isProcessingClick = false;
    }

    private void CancelSkillTargeting()
    {
        Debug.Log("스킬 사용 취소");
        gameManager.currentState = GameState.Playing;
        skillManager.selectedSkillSlot = -1;
        gameManager.board.HideSkillTargetMarkers();
        gameManager.board.ClearHoverHighlight();
    }

    public void BlockInput() => _isInputBlocked = true;
    public void UnblockInput() => _isInputBlocked = false;
    public void HideHover()
    {
        if (blackHoverIndicator != null) blackHoverIndicator.SetActive(false);
        if (whiteHoverIndicator != null) whiteHoverIndicator.SetActive(false);
        if (targetingHoverIndicator != null) targetingHoverIndicator.SetActive(false); 

        if (gameManager.board != null) gameManager.board.ClearHoverHighlight();
    }

}
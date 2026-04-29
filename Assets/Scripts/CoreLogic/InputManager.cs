using UnityEngine;
using UnityEngine.EventSystems;

public class InputManager : MonoBehaviour
{
    public GameManager gameManager;
    public SkillManager skillManager;
    public float gridSize = 1f;

    [Header("Hover Settings")]
    public GameObject hoverIndicatorPrefab; // 프리팹
    private GameObject hoverIndicator;       // 코드로 생성한 객체를 담을 변수
    private MeshRenderer hoverRenderer;   // 호버 돌의 색깔을 바꿔주기 위한 렌더러
    public Material blackAlphaMat; // 반투명 흑돌 재질
    public Material whiteAlphaMat; // 반투명 백돌 재질

    [Header("Skill Visual Settings")]
    public Material targetingMat; // 조준용 빨간색/주황색 재질

    // 콜라이더 대신 사용할 '수학적인 무한 평면 (Y=0 바닥)'
    private Plane mathPlane = new Plane(Vector3.up, Vector3.zero);

    // 찰나의 중복 클릭이나 마우스 이동을 막기 위한 잠금 변수
    private bool isProcessingClick = false;

    private bool _isInputBlocked = false; // 외부에서 입력 차단할 때 사용하는 변수

    void Start()
    {
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        if (skillManager == null) skillManager = FindFirstObjectByType<SkillManager>();

        // 1. 프리팹이 연결되어 있다면, 코드로 직접 Instantiate 해서 생성합니다.
        if (hoverIndicatorPrefab != null)
        {
            hoverIndicator = Instantiate(hoverIndicatorPrefab);
            hoverIndicator.name = "HoverIndicator_Dynamic"; // 하이어라키에서 보기 편하게 이름 변경
            hoverIndicator.SetActive(false); // 처음엔 숨겨둡니다.

            hoverRenderer = hoverIndicator.GetComponent<MeshRenderer>();
        }
    }

    void Update()
    {
        // 1. 입력 차단 조건 검사 (가드 클로즈)
        if (ShouldBlockInput())
        {
            HideHover();
            return;
        }

        // 2. 우클릭 취소 로직
        if (Input.GetMouseButtonDown(1) && gameManager.currentState == GameState.SkillTargeting)
        {
            CancelSkillTargeting();
            return; // 취소했으면 이번 프레임은 여기서 끝!
        }

        // 3. 레이캐스트 및 보드 좌표 계산
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!mathPlane.Raycast(ray, out float enter))
        {
            HideHover();
            return; // 허공을 가리키면 끝!
        }

        Vector3 hitPoint = ray.GetPoint(enter);
        int x = Mathf.RoundToInt(hitPoint.x / gridSize);
        int y = Mathf.RoundToInt(hitPoint.z / gridSize);

        // 4. 바둑판 범위 검사
        if (gameManager.board == null || x < 0 || x >= gameManager.board.boardSize || y < 0 || y >= gameManager.board.boardSize)
        {
            HideHover();
            return; // 보드 밖이면 끝!
        }

        // 여기까지 내려왔다는 건 '정상적인 바둑판 위를 가리키고 있다'는 뜻!
        // 5. 호버 비주얼 업데이트
        UpdateHoverVisuals(x, y);

        // 6. 좌클릭 처리
        if (Input.GetMouseButtonDown(0))
        {
            ProcessClick(x, y);
        }
    }

    // ==========================================
    // 헬퍼 함수들 (기능별 분리)
    // ==========================================

    private bool ShouldBlockInput()
    {
        bool isPointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        bool isInvalidState = gameManager.currentState != GameState.Playing && gameManager.currentState != GameState.SkillTargeting;
        bool isNotMyTurn = gameManager.currentMode != PlayMode.Solo && gameManager.currentTurnColor != gameManager.localPlayerColor;

        return isPointerOverUI || isInvalidState || isNotMyTurn || isProcessingClick || _isInputBlocked;
    }

    private void UpdateHoverVisuals(int x, int y)
    {
        if (hoverIndicator == null) return;

        // 1. 기본 호버 인디케이터(바닥 마커) 처리
        if (!hoverIndicator.activeSelf) hoverIndicator.SetActive(true);
        hoverIndicator.transform.position = new Vector3(x * gridSize, 0.1f, y * gridSize);

        if (hoverRenderer != null)
        {
            if (gameManager.currentState == GameState.SkillTargeting)
            {
                hoverRenderer.material = targetingMat;
                hoverIndicator.transform.localScale = Vector3.one * 0.5f;
            }
            else
            {
                int displayColor = (gameManager.currentMode == PlayMode.Solo) ? (int)gameManager.currentTurnColor : (int)gameManager.localPlayerColor;
                hoverRenderer.material = (displayColor == 1) ? blackAlphaMat : whiteAlphaMat;
                hoverIndicator.transform.localScale = Vector3.one;
            }
        }

        // 🚨 2. 타겟팅 호버링 아웃라인(테두리) 처리
        if (gameManager.currentState == GameState.SkillTargeting && skillManager != null)
        {
            int skillId = skillManager.GetSelectedSkillId();
            int myColorInt = (int)gameManager.localPlayerColor;
            int enemyColorInt = (gameManager.localPlayerColor == StoneColor.Black) ? 2 : 1;

            // * 5번(제거) - 상대 돌 호버 (기존 코드)
            if (skillId == 5)
            {
                if (gameManager.board.grid[x, y] == enemyColorInt && !gameManager.board.shieldGrid[x, y])
                    gameManager.board.HighlightSingleStone(x, y, Color.green);
                else
                    gameManager.board.ClearHoverHighlight();
            }
            // * 1번(돌 이동), 8번(신의 가호) - 내 돌 호버
            else if (skillId == 1 || skillId == 8)
            {
                // 마우스 올린 곳이 내 돌이면 파란색 테두리 켜기
                if (gameManager.board.grid[x, y] == myColorInt)
                    gameManager.board.HighlightSingleStone(x, y, Color.blue);
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
        gameManager.board.HideSkillTargetMarkers(); // 하이라이트 끄기

        // 호버 하이라이트도 끔
        gameManager.board.ClearHoverHighlight();
    }

    // 외부에서 강제로 호버를 숨겨야 할 때 사용할 함수
    public void BlockInput() => _isInputBlocked = true;
    public void UnblockInput() => _isInputBlocked = false;
    public void HideHover()
    {
        if (hoverIndicator != null && hoverIndicator.activeSelf) hoverIndicator.SetActive(false);

        // 보드 밖으로 나가면 켜져있던 테두리도 끔
        if (gameManager.board != null) gameManager.board.ClearHoverHighlight();
    }
}
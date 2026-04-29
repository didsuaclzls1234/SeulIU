using UnityEngine;
using UnityEngine.EventSystems;

public class InputManager : MonoBehaviour
{
    public GameManager gameManager;
    public SkillManager skillManager;
    public float gridSize = 1f;

    [Header("Hover Settings")]
    // public GameObject hoverIndicatorPrefab; // 프리팹
    // // private GameObject hoverIndicator;       // 코드로 생성한 객체를 담을 변수
    // private MeshRenderer hoverRenderer;   // 호버 돌의 색깔을 바꿔주기 위한 렌더러
    // public Material blackAlphaMat; // 반투명 흑돌 재질
    // public Material whiteAlphaMat; // 반투명 백돌 재질
    public GameObject blackHoverPrefab;  // 흑돌 호버 프리팹
    public GameObject whiteHoverPrefab;  // 백돌 호버 프리팹
    private GameObject blackHoverIndicator;
    private GameObject whiteHoverIndicator;
    public float hoverYOffset = 0.1f;
    public float hoverAlpha = 0.5f;
    
    [Header("Skill Visual Settings")]
    public Material targetingMat; // 조준용 빨간색/주황색 재질

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

        // 1. 프리팹이 연결되어 있다면, 코드로 직접 Instantiate 해서 생성합니다.
        // if (hoverIndicatorPrefab != null)
        // {
        //     hoverIndicator = Instantiate(hoverIndicatorPrefab);
        //     hoverIndicator.name = "HoverIndicator_Dynamic"; // 하이어라키에서 보기 편하게 이름 변경
        //     hoverIndicator.SetActive(false); // 처음엔 숨겨둡니다.

        //     hoverRenderer = hoverIndicator.GetComponent<MeshRenderer>();
        // }

        // 두 프리팹 미리 생성 후 숨김
        if (blackHoverPrefab != null)
        {
            blackHoverIndicator = Instantiate(blackHoverPrefab);
            blackHoverIndicator.name = "BlackHoverIndicator_Dynamic";
            blackHoverIndicator.SetActive(false);
            // 반투명 처리
            MeshRenderer blackMr = blackHoverIndicator.GetComponent<MeshRenderer>();
            if (blackMr != null)
            {
                Color c = blackMr.material.color;
                c.a = hoverAlpha;
                blackMr.material.color = c;
            }
        }

        if (whiteHoverPrefab != null)
        {
            whiteHoverIndicator = Instantiate(whiteHoverPrefab);
            whiteHoverIndicator.name = "WhiteHoverIndicator_Dynamic";
            whiteHoverIndicator.SetActive(false);
            // 반투명 처리
            MeshRenderer whiteMr = whiteHoverIndicator.GetComponent<MeshRenderer>();
            if (whiteMr != null)
            {
                Color c = whiteMr.material.color;
                c.a = hoverAlpha;
                whiteMr.material.color = c;
            }
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
        // if (hoverIndicator == null) return;

        // if (!hoverIndicator.activeSelf) hoverIndicator.SetActive(true);
        // hoverIndicator.transform.position = new Vector3(x * gridSize, 0.1f, y * gridSize);

        // if (hoverRenderer != null)
        // {
        //     if (gameManager.currentState == GameState.SkillTargeting)
        //     {
        //         hoverRenderer.material = targetingMat;
        //         hoverIndicator.transform.localScale = Vector3.one * 1.2f;
        //     }
        //     else
        //     {
        //         int displayColor = (gameManager.currentMode == PlayMode.Solo) ? (int)gameManager.currentTurnColor : (int)gameManager.localPlayerColor;
        //         hoverRenderer.material = (displayColor == 1) ? blackAlphaMat : whiteAlphaMat;
        //         hoverIndicator.transform.localScale = Vector3.one;
        //     }
        // }
        if (gameManager.currentState == GameState.SkillTargeting)
        {
            HideHover();
            return;
        }

        int displayColor = (gameManager.currentMode == PlayMode.Solo) ?
                           (int)gameManager.currentTurnColor :
                           (int)gameManager.localPlayerColor;
         // 색상에 맞는 호버만 활성화
        GameObject activeHover   = (displayColor == 1) ? blackHoverIndicator : whiteHoverIndicator;
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
    }

    // 외부에서 강제로 호버를 숨겨야 할 때 사용할 함수
    public void BlockInput() => _isInputBlocked = true;
    public void UnblockInput() => _isInputBlocked = false;
    public void HideHover()
    {
        // if (hoverIndicator != null && hoverIndicator.activeSelf) hoverIndicator.SetActive(false);
        if (blackHoverIndicator != null) blackHoverIndicator.SetActive(false);
        if (whiteHoverIndicator != null) whiteHoverIndicator.SetActive(false);
    }
}
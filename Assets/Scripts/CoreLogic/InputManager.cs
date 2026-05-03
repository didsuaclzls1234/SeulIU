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

        // ==========================================
        // 1. 흑돌 호버 세팅
        // ==========================================
        if (blackHoverPrefab != null)
        {
            blackHoverIndicator = Instantiate(blackHoverPrefab);
            blackHoverIndicator.name = "BlackHoverIndicator_Dynamic";
            blackHoverIndicator.SetActive(false);

            // 🚨 모든 자식 렌더러를 가져옴
            Renderer[] renderers = blackHoverIndicator.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
            {
                Material[] mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue; // 빈 머티리얼(Null) 에러 방어!

                    if (mats[i].HasProperty("_BaseColor"))
                    {
                        // 1) 알파값 깎기
                        Color c = mats[i].GetColor("_BaseColor");
                        c.a = hoverAlpha;
                        mats[i].SetColor("_BaseColor", c);

                        // 2) URP 머티리얼을 '투명(Transparent)' 모드로 강제 개조
                        mats[i].SetFloat("_Surface", 1); // Surface Type: Transparent
                        mats[i].SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mats[i].SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        mats[i].SetInt("_ZWrite", 0); // ZWrite Off
                        mats[i].renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    }
                }
                r.materials = mats;
            }
        }

        // ==========================================
        // 2. 백돌 호버 세팅 (흑돌과 동일한 구조)
        // ==========================================
        if (whiteHoverPrefab != null)
        {
            whiteHoverIndicator = Instantiate(whiteHoverPrefab);
            whiteHoverIndicator.name = "WhiteHoverIndicator_Dynamic";
            whiteHoverIndicator.SetActive(false);

            // 🚨 백돌도 똑같이 모든 자식 렌더러를 가져옴
            Renderer[] renderers = whiteHoverIndicator.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
            {
                Material[] mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue; // 빈 머티리얼(Null) 에러 방어!

                    if (mats[i].HasProperty("_BaseColor"))
                    {
                        // 1) 알파값 깎기
                        Color c = mats[i].GetColor("_BaseColor");
                        c.a = hoverAlpha;
                        mats[i].SetColor("_BaseColor", c);

                        // 2) URP 머티리얼을 '투명(Transparent)' 모드로 강제 개조
                        mats[i].SetFloat("_Surface", 1);
                        mats[i].SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mats[i].SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        mats[i].SetInt("_ZWrite", 0);
                        mats[i].renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    }
                }
                r.materials = mats;
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
        
        // 1. 착수 프리뷰 (3D 캐릭터 로직 + 타겟팅 예외처리)
        if (gameManager.currentState == GameState.SkillTargeting)
        {
            // 스킬 타겟팅 중에는 새로운 돌을 놓는 게 아니므로 캐릭터 프리뷰를 숨깁니다.
            if (blackHoverIndicator != null) blackHoverIndicator.SetActive(false);
            if (whiteHoverIndicator != null) whiteHoverIndicator.SetActive(false);
            
            // (과거의 작은 빨간 점 조준점은 캐릭터 모델과 충돌하므로 생략하고, 
            // 아래의 초록/파랑 테두리(아웃라인)로 타겟팅을 직관적으로 보여줍니다.)
        }
        else
        {
            // 일반 돌 착수 상태: 캐릭터 호버링 로직 적용
            int displayColor = (gameManager.currentMode == PlayMode.Solo) ?
                               (int)gameManager.currentTurnColor :
                               (int)gameManager.localPlayerColor;
                               
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

        // 2. 타겟팅 호버링 아웃라인(테두리) 처리
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
        // 1. 두 호버 인디케이터 모두 끄기
        if (blackHoverIndicator != null) blackHoverIndicator.SetActive(false);
        if (whiteHoverIndicator != null) whiteHoverIndicator.SetActive(false);

        // 2. 보드 밖으로 나가면 켜져있던 테두리(아웃라인)도 끄기
        if (gameManager.board != null) gameManager.board.ClearHoverHighlight();
    }
}
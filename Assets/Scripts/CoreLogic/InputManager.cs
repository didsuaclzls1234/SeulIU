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
        // UI 클릭 방지: 마우스가 UI(팝업창, 버튼, 블로커 등) 위에 있는지 확인
        // (블로커를 깔아뒀기 때문에 팝업이 뜨면 이게 무조건 true가 됨.)
        bool isPointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        // 상태 차단: 게임 중(Playing)이 아니거나, 내 턴이 아니거나, UI 위라면 호버 끄고 로직 중단
        if ((gameManager.currentState != GameState.Playing && gameManager.currentState != GameState.SkillTargeting) ||
        (gameManager.currentMode != PlayMode.Solo && gameManager.currentTurnColor != gameManager.localPlayerColor) ||
        isProcessingClick || isPointerOverUI || _isInputBlocked)
        {
            if (hoverIndicator != null && hoverIndicator.activeSelf) hoverIndicator.SetActive(false);
            return;
        }

        // 우클릭 취소 로직(어느 상태에서든 우클릭하면 평소로 복귀)
        if (Input.GetMouseButtonDown(1))
        {
            if (gameManager.currentState == GameState.SkillTargeting)
            {
                CancelSkillTargeting();
                return;
            }
        }

        if (gameManager.currentState != GameState.Playing && gameManager.currentState != GameState.SkillTargeting)
        {
            HideHover();
            return;
        }

        // 1. 카메라에서 마우스 위치로 레이저 쏘기
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        // 2. Physics.Raycast 대신, 수학 평면과 레이저가 만나는지 검사
        if (mathPlane.Raycast(ray, out float enter))
        {
            // 레이저가 바닥에 닿은 정확한 3D 좌표 
            Vector3 hitPoint = ray.GetPoint(enter);

            // 3D 좌표를 2D 배열 인덱스로 변환
            int x = Mathf.RoundToInt(hitPoint.x / gridSize);
            int y = Mathf.RoundToInt(hitPoint.z / gridSize);

            // 3. 바둑판 밖으로 마우스가 나갔는지 검사 (19x19 범위 이탈 시 안 보이게)
            if (gameManager.board != null && (x < 0 || x >= gameManager.board.boardSize || y < 0 || y >= gameManager.board.boardSize))
            {
                // 보드 바깥이면 호버 숨기고 리턴
                if (hoverIndicator != null && hoverIndicator.activeSelf) hoverIndicator.SetActive(false);
                return;
            }

            // 호버 표시기 이동 (정상 범위 안일 때만)
            if (hoverIndicator != null)
            {
                if (!hoverIndicator.activeSelf) hoverIndicator.SetActive(true);
                hoverIndicator.transform.position = new Vector3(x * gridSize, 0.1f, y * gridSize);

                if (gameManager.currentState == GameState.SkillTargeting)
                {
                    // 스킬 모드일 때는 빨간색 조준점 재질로 변경
                    hoverRenderer.material = targetingMat;
                    // 필요하다면 호버 돌의 크기를 살짝 키워서 더 티 나게 할 수도 있음
                    hoverIndicator.transform.localScale = Vector3.one * 1.2f;
                }
                else
                {
                    // 일반 모드일 때는 다시 돌 모양으로 복구
                    int displayColor = (gameManager.currentMode == PlayMode.Solo) ? (int)gameManager.currentTurnColor : (int)gameManager.localPlayerColor;
                    hoverRenderer.material = (displayColor == 1) ? blackAlphaMat : whiteAlphaMat;
                    hoverIndicator.transform.localScale = Vector3.one;
                }
            }

            // 4. 클릭 처리
            if (Input.GetMouseButtonDown(0))
            {
                // 클릭하는 순간 락을 걸어서, 프레임이 밀려도 좌표가 바뀌지 않게 고정
                isProcessingClick = true;

                // 호버 인디케이터 즉시 숨김
                if (hoverIndicator != null) hoverIndicator.SetActive(false);

                if (gameManager.currentState == GameState.Playing)
                {
                    // [일반 모드] 돌을 놓는다
                    gameManager.TryPlaceStone(x, y);
                }
                else if (gameManager.currentState == GameState.SkillTargeting)
                {
                    // [스킬 모드] 스킬 매니저에게 좌표를 전달하고 스킬을 쏜다
                    if (skillManager != null)
                    {
                        skillManager.ExecuteSkillAt(x, y);
                    }
                }

                // 연산이 끝난 후 락 해제
                isProcessingClick = false;
            }
        }
        else
        {
            // 마우스가 허공(보드 밖)을 가리킬 때 호버 돌 숨기기
            if (hoverIndicator != null && hoverIndicator.activeSelf) hoverIndicator.SetActive(false);
        }
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
        if (hoverIndicator != null) hoverIndicator.SetActive(false);
    }
}
using UnityEngine;
using UnityEngine.EventSystems;

public class InputManager : MonoBehaviour
{
    public GameManager gameManager;
    public float gridSize = 1f;

    [Header("Hover Settings")]
    public GameObject hoverIndicatorPrefab; // 프리팹
    private GameObject hoverIndicator;       // 코드로 생성한 객체를 담을 변수
    private MeshRenderer hoverRenderer;   // 호버 돌의 색깔을 바꿔주기 위한 렌더러
    public Material blackAlphaMat; // 반투명 흑돌 재질
    public Material whiteAlphaMat; // 반투명 백돌 재질

    // 콜라이더 대신 사용할 '수학적인 무한 평면 (Y=0 바닥)'
    private Plane mathPlane = new Plane(Vector3.up, Vector3.zero);

    // 찰나의 중복 클릭이나 마우스 이동을 막기 위한 잠금 변수
    private bool isProcessingClick = false;

    //팝업이 떴을떄 입력을 차단하기 위한 변수
    private bool _isInputBlocked = false;

    public void BlockInput() => _isInputBlocked = true;
    public void UnblockInput() => _isInputBlocked = false;

    void Start()
    {
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();

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
        if (gameManager.currentState != GameState.Playing ||
            (gameManager.currentMode != PlayMode.Solo && gameManager.currentTurnColor != gameManager.localPlayerColor) ||
            isProcessingClick ||
            isPointerOverUI)
        {
            if (hoverIndicator != null && hoverIndicator.activeSelf) hoverIndicator.SetActive(false);
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

                // 내 색깔에 맞춰서 반투명 재질 변경
                if (hoverRenderer != null)
                {
                    // 호버 돌 색상 로직: 솔로 모드면 현재 턴 색상, 멀티면 내 색상 고정
                    int displayColor = (gameManager.currentMode == PlayMode.Solo) ? (int)gameManager.currentTurnColor : (int)gameManager.localPlayerColor;
                    hoverRenderer.material = (displayColor == 1) ? blackAlphaMat : whiteAlphaMat;
                }
            }

            // 4. 클릭 처리
            if (Input.GetMouseButtonDown(0))
            {
                // 클릭하는 순간 락을 걸어서, 프레임이 밀려도 좌표가 바뀌지 않게 고정
                isProcessingClick = true;

                // 호버 인디케이터 즉시 숨김
                if (hoverIndicator != null) hoverIndicator.SetActive(false);

                // 돌 놓기 실행
                gameManager.TryPlaceStone(x, y);

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
}
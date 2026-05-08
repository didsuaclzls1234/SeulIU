using System.Collections;
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
            // UI(스킬 버튼) 위에 마우스가 있어도 스킬 대기 중이면 보드판을 하얗게(1번) 띄움
            if (!isProcessingClick && gameManager.board != null)
            {
                if (IsGlobalTargetSkill()) gameManager.board.SetBoardOverlayState(1);
                else gameManager.board.SetBoardOverlayState(0);
            }
            return;
        }

        // 우클릭 취소
        if (Input.GetMouseButtonDown(1) &&
            (/*gameManager.currentState == GameState.SkillTargeting ||*/
             gameManager.currentState == GameState.SkillPreview))
        {
            gameManager.gameHUD?.HideSystemMessage();
            CancelSkill();
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!mathPlane.Raycast(ray, out float enter))
        {
            HideHover();
            // 허공을 가리켜도 스킬 대기 중이면 하얗게(1번)
            if (!isProcessingClick && gameManager.board != null)
            {
                if (IsGlobalTargetSkill()) gameManager.board.SetBoardOverlayState(1);
                else gameManager.board.SetBoardOverlayState(0);
            }
            return;
        }

        Vector3 hitPoint = ray.GetPoint(enter);
        int x = Mathf.RoundToInt(hitPoint.x / gridSize);
        int y = Mathf.RoundToInt(hitPoint.z / gridSize);

        // 마우스가 보드판 범위 안에 있는지 확인
        bool isMouseOverBoard = (x >= 0 && x < gameManager.board.boardSize && y >= 0 && y < gameManager.board.boardSize);

        if (!isMouseOverBoard)
        {
            HideHover();
            // 보드판 밖으로 마우스가 나가면 보드판 색상 원상복구
            // 보드판 밖이어도 스킬 대기 중이면 하얗게(1번)
            if (!isProcessingClick && gameManager.board != null)
            {
                if (IsGlobalTargetSkill()) gameManager.board.SetBoardOverlayState(1);
                else gameManager.board.SetBoardOverlayState(0);
            }
            return;
        }

        // -- 여기서부터는 마우스가 정확히 보드판 위에 올라온 상태
        UpdateHoverVisuals(x, y);

        // 보드판 안에 마우스가 들어왔으므로 호버 색상(2번) 적용
        if (!isProcessingClick && gameManager.board != null)
        {
            if (IsGlobalTargetSkill()) gameManager.board.SetBoardOverlayState(2);
            else gameManager.board.SetBoardOverlayState(0);
        }

        // 스킬 타입에 따른 클릭(좌클릭) 분기 처리
        if (Input.GetMouseButtonDown(0))
        {
            gameManager.gameHUD?.HideSystemMessage();

            // 즉발/전체 스킬(이중착수, 투명화 등)일 경우 보드판 클릭 피드백 띄우기
            if (gameManager.currentState == GameState.SkillPreview && IsGlobalTargetSkill())
            {
                StartCoroutine(BoardClickFeedbackRoutine(x, y));
            }
            else
            {
                ProcessClick(x, y);
            }
        }
    }

    // 헬퍼 함수: 현재 선택된 스킬이 전체 클릭(none) 타입인지 확인
    private bool IsGlobalTargetSkill()
    {
        if (gameManager.currentState != GameState.SkillPreview) return false;

        int skillId = skillManager.GetSelectedSkillId();
        if (skillManager.skillDatabase.TryGetValue(skillId, out SkillData data))
        {
            return data.targetType == "none";
        }
        return false;
    }

    // ==========================================
    // 보드판 클릭 시 0.1초 동안 색이 짙어졌다가 확정되는 코루틴
    // ==========================================
    private IEnumerator BoardClickFeedbackRoutine(int x, int y)
    {
        isProcessingClick = true;
        HideHover();

        // 1. 보드판을 '클릭된 색상'으로 변경
        gameManager.board.SetBoardOverlayState(3);

        // 2. 0.1초 대기 (유저가 버튼이 눌렸다는 타격감을 느끼는 시간)
        yield return new WaitForSeconds(0.1f);

        // 3. 스킬 확정 및 색상 원상복구
        gameManager.board.SetBoardOverlayState(0);
        skillManager.ConfirmSkill(x, y);

        isProcessingClick = false;
    }

    private bool ShouldBlockInput()
    {
        bool isPointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        bool isInvalidState = gameManager.currentState != GameState.Playing && /*gameManager.currentState != GameState.SkillTargeting &&*/ gameManager.currentState != GameState.SkillPreview;
        bool isNotMyTurn = gameManager.currentMode != PlayMode.Solo && gameManager.currentTurnColor != gameManager.localPlayerColor;

        return isPointerOverUI || isInvalidState || isNotMyTurn || isProcessingClick || _isInputBlocked;
    }

    private void UpdateHoverVisuals(int x, int y)
    {
        // 현재 상태가 스킬 시전 대기 중(Preview)이거나 타겟팅 중일 때
        bool isSkillMode = (gameManager.currentState == GameState.SkillPreview /*|| gameManager.currentState == GameState.SkillTargeting*/);

        if (isSkillMode)
        {
            // 스킬 모드일 땐 금수 마커 무조건 숨김
            gameManager.board.HideHoverForbiddenMark();

            // 1. 일반 캐릭터 바둑돌 호버는 무조건 숨김!
            if (blackHoverIndicator != null) blackHoverIndicator.SetActive(false);
            if (whiteHoverIndicator != null) whiteHoverIndicator.SetActive(false);

            int skillId = skillManager.GetSelectedSkillId();
            bool showRedDot = false;

            // 2. 스킬 타입(targetType)에 따라 빨간 점 표시 여부 결정
            if (skillManager.skillDatabase.TryGetValue(skillId, out SkillData data))
            {
                // 타겟팅이 필요한 스킬(돌이동, 봉인, 제거 등)만 빨간 점 표시
                if (data.targetType == "my" || data.targetType == "enemy" || data.targetType == "cell")
                {
                    showRedDot = true;
                }
                // "none" (이중착수, 칼날비, 투명화, 칠죄종)은 빨간 점도 안 띄움
            }

            if (showRedDot)
            {
                if (targetingHoverIndicator != null)
                {
                    targetingHoverIndicator.SetActive(true);
                    targetingHoverIndicator.transform.position = new Vector3(x * gridSize, targetingHoverYOffset, y * gridSize);
                }
            }
            else
            {
                if (targetingHoverIndicator != null) targetingHoverIndicator.SetActive(false);
            }

            // 3. 타겟팅 호버링 아웃라인(테두리) 처리
            int myColorInt = (int)gameManager.localPlayerColor;
            int enemyColorInt = (gameManager.localPlayerColor == StoneColor.Black) ? 2 : 1;

            if (skillId == 5) // 5번: 제거 (상대 돌 타겟팅)
            {
                if (gameManager.board.grid[x, y] == enemyColorInt)
                    gameManager.board.HighlightSingleStone(x, y, gameManager.board.visualSettings.enemyHoverHighlightColor);
                else
                    gameManager.board.ClearHoverHighlight();
            }
            else if (skillId == 1) // 1번: 돌 이동 (내 돌 타겟팅)
            {
                if (gameManager.board.grid[x, y] == myColorInt)
                    gameManager.board.HighlightSingleStone(x, y, gameManager.board.visualSettings.myHoverHighlightColor);
                else
                    gameManager.board.ClearHoverHighlight();
            }
            else
            {
                // 타겟팅 스킬이 아니면 아웃라인 끄기
                gameManager.board.ClearHoverHighlight();
            }
        }
        else // GameState.Playing (일반 착수 상태)
        {
            // 1. 빨간 조준점 숨기기
            if (targetingHoverIndicator != null) targetingHoverIndicator.SetActive(false);

            // 2. 돌이 이미 존재하는지 먼저 파악 
            bool hasStone = gameManager.board.grid[x, y] != 0;
            bool isTargetInvisible = false;

            if (hasStone)
            {
                GameObject stoneObj = gameManager.board.GetStoneObjectAt(x, y);
                if (stoneObj != null)
                {
                    StoneVisualController svc = stoneObj.GetComponent<StoneVisualController>();
                    if (svc != null && !svc.IsVisible) isTargetInvisible = true; // 상대 투명돌
                }
            }

            // 3. 금수 검사는 '빈 칸(!hasStone)'일 때만 실행
            bool isForbidden = false;
            if (!hasStone && gameManager.currentTurnColor == gameManager.localPlayerColor)
            {
                isForbidden = gameManager.board.ruleManager.IsForbiddenMove(x, y, (int)gameManager.currentTurnColor, gameManager.board.grid, gameManager.board.boardSize, silent: true);
            }

            if (isForbidden)
            {
                // 금수 자리면 투명돌(호버) 숨기고 ❌ 마커 띄우기
                if (blackHoverIndicator != null) blackHoverIndicator.SetActive(false);
                if (whiteHoverIndicator != null) whiteHoverIndicator.SetActive(false);

                gameManager.board.ShowHoverForbiddenMark(x, y);
            }
            else
            {
                // 금수가 아니면 ❌ 마커 숨기고 일반 투명돌(호버) 띄우기
                gameManager.board.HideHoverForbiddenMark();

                // 돌이 있고, 그 돌이 내 눈에 보인다면 호버 완전 숨김 (겹침 방지)
                if (hasStone && !isTargetInvisible)
                {
                    if (blackHoverIndicator != null) blackHoverIndicator.SetActive(false);
                    if (whiteHoverIndicator != null) whiteHoverIndicator.SetActive(false);
                }
                else
                {
                    // 빈 칸이거나 상대 투명돌이면 정상적으로 호버 돌 표시
                    int displayColor = (gameManager.currentMode == PlayMode.Solo) ? (int)gameManager.currentTurnColor : (int)gameManager.localPlayerColor;
                    GameObject activeHover = (displayColor == 1) ? blackHoverIndicator : whiteHoverIndicator;
                    GameObject inactiveHover = (displayColor == 1) ? whiteHoverIndicator : blackHoverIndicator;

                    if (inactiveHover != null) inactiveHover.SetActive(false);

                    if (activeHover != null)
                    {
                        activeHover.SetActive(true);
                        activeHover.transform.position = new Vector3(x * gridSize, hoverYOffset, y * gridSize);
                        float yRotation = (displayColor == 1) ? gameManager.board.blackStoneYRotation : gameManager.board.whiteStoneYRotation;
                        activeHover.transform.rotation = Quaternion.Euler(0, yRotation, 0);

                        // 백돌이고 신성화가 발동 중이면 호버에도 쨍한 아웃라인 추가
                        StoneVisualController hoverSvc = activeHover.GetComponent<StoneVisualController>();
                        if (hoverSvc != null)
                        {
                            if (gameManager.board.isConsecrationActive && displayColor == 2)
                            {
                                hoverSvc.SetConsecration(true, gameManager.board.visualSettings.consecrationOutlineColor,
                                                               gameManager.board.visualSettings.consecrationThickness,
                                                               gameManager.board.visualSettings.consecrationGlow);
                            }
                            else
                            {
                                hoverSvc.SetConsecration(false, Color.black);
                            }
                        }
                    }
                }
            }

            // 4. 일반 착수 중에는 아웃라인 무조건 끄기
            gameManager.board.ClearHoverHighlight();
        }

        if (IsGlobalTargetSkill())
            gameManager.board.SetBoardOverlayState(1); // 호버 색상
        else
            gameManager.board.SetBoardOverlayState(0); // 원상 복구
    }

    private void ProcessClick(int x, int y)
    {
        isProcessingClick = true;
        HideHover();

        if (gameManager.currentState == GameState.Playing)
        {
            gameManager.TryPlaceStone(x, y);
        }
        // else if (gameManager.currentState == GameState.SkillTargeting && skillManager != null)
        // {
        //     skillManager.ExecuteSkillAt(x, y);
        // }
        // [추가] SkillPreview 상태에서 좌클릭 → 스킬 확정
        else if (gameManager.currentState == GameState.SkillPreview)
        {
            skillManager.ConfirmSkill(x, y);
        }

        isProcessingClick = false;
    }

    private void CancelSkill()
    {
        Debug.Log("스킬 사용 취소");
        gameManager.currentState = GameState.Playing;
        skillManager.selectedSkillSlot = -1;
        gameManager.pendingSkillId        = -1; // 예약된 스킬도 초기화
        gameManager.board.HideSkillTargetMarkers();
        gameManager.board.ClearHoverHighlight();

        // 취소 시 보드판 색상 원상복구
        gameManager.board.SetBoardOverlayState(0);
    }

    public void BlockInput() => _isInputBlocked = true;
    public void UnblockInput() => _isInputBlocked = false;
    public void HideHover()
    {
        if (blackHoverIndicator != null) blackHoverIndicator.SetActive(false);
        if (whiteHoverIndicator != null) whiteHoverIndicator.SetActive(false);
        if (targetingHoverIndicator != null) targetingHoverIndicator.SetActive(false);

        if (gameManager.board != null)
        {
            gameManager.board.ClearHoverHighlight();
            if (IsGlobalTargetSkill()) gameManager.board.SetBoardOverlayState(0);

            // 보드 밖으로 나가면 금수 마커도 치움
            gameManager.board.HideHoverForbiddenMark();
        }
    }

}
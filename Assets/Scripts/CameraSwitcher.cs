using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraSwitcher : MonoBehaviour
{
    public Camera topCamera;
    public Camera blackPlayerCamera;
    public Camera whitePlayerCamera;
    public GameManager gameManager;
    public InputManager inputManager;
    public Camera victoryCamera; // 승리 시네마틱용 전용 카메라

    [Header("승리 카메라 연출 세팅")]
    public AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("최종적으로 돌에 얼마나 가까이 다가갈지 (작을수록 줌인)")]
    [Range(2f, 10f)] public float zoomDistance = 4.5f;

    [Tooltip("카메라의 높이 (돌을 살짝 위에서 내려다볼지, 눈높이를 맞출지)")]
    [Range(0.5f, 5f)] public float cameraHeight = 2.5f;

    [Tooltip("슈우욱 다가오면서 회전할 총 각도 (예: 90 = 4분의 1바퀴 돔)")]
    [Range(0f, 180f)] public float spinAngle = 90f;

    [Tooltip("연출에 걸리는 총 시간 (초)")]
    [Range(1f, 5f)] public float cinematicDuration = 3.0f;

    [Tooltip("시작 사선 거리")]
    public float startDistance = 7.0f;  // 시작 사선 거리

    private bool isTopView = true;

    private Vector3 topCameraFixedPos;
    private Quaternion topCameraFixedRot;

    void Start()
    {
        // Awake(AdjustCameraToBoardSize) 이후 시점이므로 확정된 위치 저장
        topCameraFixedPos = topCamera.transform.position;
        topCameraFixedRot = topCamera.transform.rotation;

        ApplyColorBasedRotation();

        SetTopView(true);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            isTopView = !isTopView;
            SetTopView(isTopView);
        }
    }

    void SetTopView(bool value)
    {
        isTopView = value;

        // 리셋: 빅토리 카메라의 Priority(Depth)를 -1로 확실히 내려버립니다.
        if (victoryCamera != null) victoryCamera.depth = -1;

        // SetActive 대신 depth로 전환 (topCamera 꺼지면 Camera.main null 방지)
        topCamera.depth = value ? 1 : 0;
        blackPlayerCamera.depth = 0;
        whitePlayerCamera.depth = 0;
        if (victoryCamera != null) victoryCamera.depth = -1; // 평소엔 무조건 끄기

        if (!value)
        {
            bool isBlack = (gameManager.localPlayerColor == StoneColor.Black);
            blackPlayerCamera.depth = isBlack ? 1 : 0;
            whitePlayerCamera.depth = isBlack ? 0 : 1;
        }
        else
        {
            // 탑뷰 복귀 시 코드가 계산한 위치로 복원
            topCamera.transform.position = topCameraFixedPos;
            topCamera.transform.rotation = topCameraFixedRot;
        }

        // 탑뷰일 때만 입력 허용
        if (value) inputManager.UnblockInput();
        else       inputManager.BlockInput();
    }

    public void ApplyColorBasedRotation()
    {
        float zRotation = (gameManager.localPlayerColor == StoneColor.Black) ? 90f : -90f;
        topCamera.transform.rotation = Quaternion.Euler(90f, 0f, zRotation);
        topCameraFixedPos = topCamera.transform.position;
        topCameraFixedRot = topCamera.transform.rotation;
    }
    //타임아웃시 강제로 탑뷰 전환
    public void ForceTopView()
    {
        StopAllCoroutines(); // 진행 중인 카메라 무빙 코루틴 즉시 사살

        SetTopView(true); // 무조건 탑뷰 세팅 돌림

        if (victoryCamera != null)
        {
            victoryCamera.depth = -1;
            victoryCamera.gameObject.SetActive(false); 
        }
    }

    // ==============================================================
    // 승리 연출 카메라 (무조건 가로 배치 + 자동 줌)
    // ==============================================================
    public IEnumerator VictoryCinematicCamera(Vector3 targetCenter, StoneColor winnerColor, List<Vector2Int> winningCoords)
    {
        if (victoryCamera == null) yield break;
        victoryCamera.gameObject.SetActive(true);
        victoryCamera.depth = 10;

        // --- [핵심 계산 블록: 알잘딱 구도 잡기] ---

        // 1. 승리 라인의 실제 벡터 및 길이 계산
        Vector2Int first = winningCoords[0];
        Vector2Int last = winningCoords[winningCoords.Count - 1];
        Vector3 worldFirst = new Vector3(first.x, 0, first.y) * 0.75f; // 보드그리드Size 적용
        Vector3 worldLast = new Vector3(last.x, 0, last.y) * 0.75f;

        Vector3 lineVec = worldLast - worldFirst;
        float lineLength = lineVec.magnitude; // 라인의 실제 길이
        Vector3 lineDir = lineVec.normalized; // 라인의 방향

        // 2. 라인을 화면 가로(Side-by-Side)로 비추기 위한 정면 방향 계산 (Line과 90도 직교)
        // 캐릭터의 기본 정면(North/South)과 라인 방향을 고려하여, 
        // 라인의 '앞쪽' 90도 영역을 찾습니다.
        Vector3 charFaceBase = (winnerColor == StoneColor.Black) ? Vector3.forward : Vector3.back;

        // 라인과 수직인 두 방향(왼쪽/오른쪽) 중 캐릭터 얼굴 방향과 가까운 쪽 선택
        Vector3 bestViewDir = Vector3.Cross(lineDir, Vector3.up);
        if (Vector3.Dot(bestViewDir, charFaceBase) < 0) bestViewDir = -bestViewDir;

        // 3. [자동 줌] 돌 개수에 따라 거리와 높이를 비례해서 조절 (잘림 방지)
        // 돌 5개면 가깝게, 7개면 멀게 자동으로 세팅됨.
        float dynamicDistance = 3.5f + (lineLength * 0.6f); // 기본 거리 + 길이 보정
        float dynamicHeight = 1.8f + (lineLength * 0.2f);   // 기본 높이 + 길이 보정

        // 4. 최종 목적지(End)와 살짝 옆에서 시작할 출발지(Start) 세팅
        // 최종점은 라인의 완전 정면 90도.
        Vector3 endPos = targetCenter + (bestViewDir * dynamicDistance) + (Vector3.up * dynamicHeight);

        // 출발점은 최종점에서 라인 방향으로 살짝 비낀 사선 위치 (동적인 느낌을 위해)
        Vector3 startPos = targetCenter + ((bestViewDir + lineDir * 0.3f).normalized * dynamicDistance * 1.2f) + (Vector3.up * dynamicHeight * 1.3f);

        // --- [연출 실행 블록] ---

        float elapsed = 0f;
        while (elapsed < cinematicDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / cinematicDuration;
            float curveT = movementCurve.Evaluate(t);

            // 출발점(사선)에서 최종점(정면)으로 부드럽게 이동
            victoryCamera.transform.position = Vector3.Lerp(startPos, endPos, curveT);

            // 회전: 항상 돌들의 중심을 바라봄
            // 끝나는 순간엔 카메라의 Right벡터와 승리 라인 벡터가 평행해져서 완벽한 가로 배치가 됨.
            Quaternion lookRotation = Quaternion.LookRotation(targetCenter + (Vector3.up * 0.4f) - victoryCamera.transform.position);
            victoryCamera.transform.rotation = lookRotation;

            yield return null;
        }
    }
}
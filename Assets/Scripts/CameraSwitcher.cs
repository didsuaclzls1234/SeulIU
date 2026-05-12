using System.Collections;
using UnityEngine;

public class CameraSwitcher : MonoBehaviour
{
    public Camera topCamera;
    public Camera blackPlayerCamera;
    public Camera whitePlayerCamera;
    public GameManager gameManager;
    public InputManager inputManager;
    public Camera victoryCamera; // 승리 시네마틱용 전용 카메라

    [Header("Cinematic Settings")] // 인스펙터에서 마우스로 조절할 '속도 곡선'
    public AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

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
    //  승리 시네마틱 카메라 무빙 (기존 구조 완벽 호환)
    // ==============================================================
    // 승리 시네마틱: 1단계(사선 이동) -> 2단계(곡선 회전 줌인)
    public IEnumerator VictoryCinematicCamera(Vector3 targetCenter, StoneColor winnerColor)
    {
        if (victoryCamera == null) yield break;

        victoryCamera.gameObject.SetActive(true);

        Camera activeCam = isTopView ? topCamera :
            (gameManager.localPlayerColor == StoneColor.Black ? blackPlayerCamera : whitePlayerCamera);

        victoryCamera.transform.position = activeCam.transform.position;
        victoryCamera.transform.rotation = activeCam.transform.rotation;
        victoryCamera.depth = 10; // 화면 덮기

        float side = (winnerColor == StoneColor.Black) ? -1f : 1f;
        Vector3 distantPos = targetCenter + new Vector3(5f * side, 6f, 8f * side);

        // --- 1단계: 사선 이동 ---
        float elapsed = 0f;
        float duration = 1.5f;
        Vector3 startPos = victoryCamera.transform.position;
        Quaternion startRot = victoryCamera.transform.rotation;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 인스펙터에서 그린 곡선에 따라 t값이 변합니다.
            float curveT = movementCurve.Evaluate(t);

            victoryCamera.transform.position = Vector3.Lerp(startPos, distantPos, curveT);
            victoryCamera.transform.rotation = Quaternion.Slerp(startRot, Quaternion.LookRotation(targetCenter - distantPos), curveT);
            yield return null;
        }

        // --- [2단계] 곡선을 그리며 휘리릭 줌인! ---
        elapsed = 0f;
        duration = 2.0f; // 2초 동안 회전하며 접근
        Vector3 startRotatePos = victoryCamera.transform.position;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float easeT = Mathf.SmoothStep(0, 1, t); // 부드러운 가속

            // 원형 회전 계산 (반지름이 점점 줄어듦)
            float angle = easeT * Mathf.PI * 0.5f; // 90도 정도 회전
            float radius = Vector3.Distance(startRotatePos, targetCenter) * (1f - easeT * 0.6f);

            float x = Mathf.Cos(angle) * radius * side;
            float z = Mathf.Sin(angle) * radius * side;

            // 최종 카메라 위치 (약간 우측으로 치우치게 오프셋 추가)
            Vector3 offset = (winnerColor == StoneColor.Black) ? new Vector3(-2f, 3f, -4f) : new Vector3(2f, 3f, 4f);
            Vector3 nextPos = targetCenter + new Vector3(x, 3f, z) + (offset * easeT);

            victoryCamera.transform.position = nextPos;
            victoryCamera.transform.LookAt(targetCenter + Vector3.up * 0.5f);

            yield return null;
        }
    }
}
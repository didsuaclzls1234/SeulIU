using UnityEngine;

public class CameraSwitcher : MonoBehaviour
{
    public Camera topCamera;
    public Camera blackPlayerCamera;
    public Camera whitePlayerCamera;
    public GameManager gameManager;
    public InputManager inputManager;

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

        // SetActive 대신 depth로 전환 (topCamera 꺼지면 Camera.main null 방지)
        topCamera.depth = value ? 1 : 0;
        blackPlayerCamera.depth = 0;
        whitePlayerCamera.depth = 0;

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
}
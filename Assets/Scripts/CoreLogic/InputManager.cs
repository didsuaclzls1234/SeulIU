using UnityEngine;

public class InputManager : MonoBehaviour
{
    public GameManager gameManager;
    public float gridSize = 1f;

    [Header("UI & Visuals")]
    public GameObject hoverIndicator;

    // 콜라이더 대신 사용할 '수학적인 무한 평면 (Y=0 바닥)'
    private Plane mathPlane = new Plane(Vector3.up, Vector3.zero);

    void Start()
    {
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
    }

    void Update()
    {
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

            // 호버 표시기 이동
            if (hoverIndicator != null)
            {
                if (!hoverIndicator.activeSelf) hoverIndicator.SetActive(true);
                hoverIndicator.transform.position = new Vector3(x * gridSize, 0.1f, y * gridSize);
            }

            // 클릭 처리
            if (Input.GetMouseButtonDown(0))
            {
                gameManager.TryPlaceStone(x, y);
            }
        }
        else
        {
            if (hoverIndicator != null && hoverIndicator.activeSelf)
            {
                hoverIndicator.SetActive(false);
            }
        }
    }
}
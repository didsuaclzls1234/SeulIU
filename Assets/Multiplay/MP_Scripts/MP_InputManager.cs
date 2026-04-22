using UnityEngine;
using UnityEngine.InputSystem;

public class MP_InputManager : MonoBehaviour
{
    public GameSession gameSession;
    public BoardUI boardUI;

    private Plane _groundPlane = new Plane(Vector3.up, Vector3.zero);
    private BoardCell _lastHoveredCell = null;

    private void Update()
    {
        
        if (Mouse.current == null || Camera.main == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);

        // 마우스 → 월드 XZ 좌표 변환
        if (!_groundPlane.Raycast(ray, out float enter)) return;
        Vector3 worldPos = ray.GetPoint(enter);

        // 월드 XZ 좌표에서 위에서 아래로 고정 레이캐스트
        Vector3 rayOrigin = new Vector3(worldPos.x, 10f, worldPos.z);

    PhysicsScene physicsScene = gameObject.scene.GetPhysicsScene();

        if (physicsScene.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 20f))
        {
        Debug.DrawRay(rayOrigin, Vector3.down * hit.distance, Color.green);

        BoardCell cell = hit.collider.GetComponent<BoardCell>();
        if (cell != null && !cell.HasStone)
            {
            if (cell != _lastHoveredCell)
            {
                boardUI.OnCellHoverExit();
                boardUI.OnCellHoverEnter(cell.Row, cell.Col);
                _lastHoveredCell = cell;
            }

            if (Mouse.current.leftButton.wasPressedThisFrame)
                gameSession.TrySubmitMove(cell.Row, cell.Col);

            return;
            }
        }

    Debug.DrawRay(rayOrigin, Vector3.down * 20f, Color.red);

    if (_lastHoveredCell != null)
    {
        boardUI.OnCellHoverExit();
        _lastHoveredCell = null;
    }
  
    }
}
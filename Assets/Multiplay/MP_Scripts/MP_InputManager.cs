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

        // 월드 좌표 → 격자 인덱스로 직접 변환
        float halfBoard = (ActionLog.BoardSize - 1) * boardUI.cellSpacing * 0.5f;
        int col = Mathf.RoundToInt((worldPos.x + halfBoard) / boardUI.cellSpacing);
        int row = Mathf.RoundToInt((worldPos.z + halfBoard) / boardUI.cellSpacing);

        // 보드 범위 안인지 확인
        if (col < 0 || col >= ActionLog.BoardSize || row < 0 || row >= ActionLog.BoardSize)
        {
            if (_lastHoveredCell != null)
            {
                boardUI.OnCellHoverExit();
                _lastHoveredCell = null;
            }
            return;
        }

        BoardCell cell = boardUI.GetCell(row, col);
        if (cell != null && !cell.HasStone)
        {
            if (cell != _lastHoveredCell)
            {
                boardUI.OnCellHoverEnter(cell.Row, cell.Col);
                _lastHoveredCell = cell;
            }

            if (Mouse.current.leftButton.wasPressedThisFrame)
                gameSession.TrySubmitMove(cell.Row, cell.Col);
        }
        else if (_lastHoveredCell != null)
        {
            boardUI.OnCellHoverExit();
            _lastHoveredCell = null;
        }
    }
}
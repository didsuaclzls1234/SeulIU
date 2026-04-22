using UnityEngine;

/// <summary>
/// 15×15 오목판을 월드 공간 오브젝트로 구성합니다.
///
/// </summary>
public class BoardUI : MonoBehaviour
{
    [Header("씬 레퍼런스")]
    public GameSession gameSession;

    [Header("프리팹")]
    public GameObject cellPrefab;       
    public GameObject blackStonePrefab;
    public GameObject whiteStonePrefab;

    [Header("배치 설정")]
    public float cellSpacing = 1f;
    public float stoneYOffset = 0.1f;

    [Header("호버 표시기 (선택)")]
    public GameObject hoverIndicator;
    


    [Range(0f, 1f)]
    [Tooltip("미리보기 돌 투명도")]
    public float previewAlpha = 0.4f;

    // ── 런타임 ───────────────────────────────────────────────────
    private BoardCell[,] _cells;
    private bool         _isMyTurn     = false;
    private bool         _inputEnabled = true;
    private GameObject _previewStone;

    // ── 초기화 ───────────────────────────────────────────────────

    private void Awake()
    {
        BuildGrid();
        BuildGridLines();
        if (hoverIndicator != null) hoverIndicator.SetActive(false);
    }

    private void BuildGrid()
    {
        int size = ActionLog.BoardSize;           // 15
        float halfBoard = (size - 1) * cellSpacing * 0.5f; // 7f (cellSpacing=1 기준)
        _cells   = new BoardCell[size, size];

         for (int row = 0; row < size; row++)
    {
        for (int col = 0; col < size; col++)
        {
            int r = row, c = col;

            // col = X축 증가, Y = 0 고정, row = Z축 증가
            Vector3 worldPos = new Vector3(
               col * cellSpacing - halfBoard,  // -7 ~ +7
                0f,
                row * cellSpacing - halfBoard); // -7 ~ +7

            GameObject go = Instantiate(cellPrefab, worldPos, Quaternion.identity);
            go.transform.SetParent(transform);
            go.name = $"Cell_{r}_{c}";

            BoardCell cell = go.GetComponent<BoardCell>();
            cell.Init(r, c);
            _cells[r, c] = cell;
        }
    }
    }

    // ── GameSession에서 호출 ─────────────────────────────────────

    /// <summary>역할 배정 후 호출. 미리보기 색을 내 색으로 설정합니다.</summary>
 

     public void SetIsMyTurn(bool isMyTurn)
    {
        _isMyTurn = isMyTurn;
        if (!isMyTurn && _previewStone != null) _previewStone.SetActive(false);
    }

     public void SetInputEnabled(bool enabled)
    {
        _inputEnabled = enabled;
        if (!enabled && _previewStone != null) _previewStone.SetActive(false);
    }
     public void InitPreviewStone(StoneColor myColor)
    {
        if (_previewStone != null) Destroy(_previewStone);

        GameObject prefab = myColor == StoneColor.Black ? blackStonePrefab : whiteStonePrefab;
        _previewStone = Instantiate(prefab);
        _previewStone.SetActive(false);

        foreach (Renderer r in _previewStone.GetComponentsInChildren<Renderer>())
        {
            foreach (Material mat in r.materials)
            {
                mat.SetFloat("_Mode", 3);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;

                Color c = mat.color;
                mat.color = new Color(c.r, c.g, c.b, 0.4f);
            }
        }
    }
    /// <summary>네트워크 이벤트 수신 후 실제 돌을 표시합니다.</summary>
    public void PlaceStone(int row, int col, StoneColor color)
    {
        GameObject prefab = color == StoneColor.Black ? blackStonePrefab : whiteStonePrefab;
        if (prefab == null)
        {
            Debug.LogError($"[BoardUI] {color} 프리팹이 연결되지 않았습니다!");
            return;
        }

        Vector3 cellPos  = _cells[row, col].transform.position;
        Vector3 spawnPos = new Vector3(cellPos.x, stoneYOffset, cellPos.z);
        Instantiate(prefab, spawnPos, Quaternion.identity);
        _cells[row, col].MarkOccupied();

        if (hoverIndicator != null && hoverIndicator.activeSelf)
            hoverIndicator.SetActive(false);
    }

    // ── BoardCell에서 호출 ───────────────────────────────────────

    public void OnCellClicked(int row, int col)
    {
        gameSession.TrySubmitMove(row, col);
    }

    public void OnCellHoverEnter(int row, int col)
    {
        if (hoverIndicator == null) return;
        Vector3 cellPos = _cells[row, col].transform.position;
        hoverIndicator.transform.position = new Vector3(cellPos.x, stoneYOffset, cellPos.z);
        hoverIndicator.SetActive(true);
    }

    public void OnCellHoverExit()
    {
        if (_previewStone != null) _previewStone.SetActive(false);
    }

    private void BuildGridLines()
{
    int   size      = ActionLog.BoardSize;
    float halfBoard = (size - 1) * cellSpacing * 0.5f;
    float baseX     = transform.position.x - halfBoard;
    float baseZ     = transform.position.z - halfBoard;
    float y         = transform.position.y + 0.01f;

    for (int i = 0; i < size; i++)
    {
        // 가로선
        CreateLine(
            new Vector3(baseX, y, baseZ + i * cellSpacing),
            new Vector3(baseX + (size - 1) * cellSpacing, y, baseZ + i * cellSpacing));

        // 세로선
        CreateLine(
            new Vector3(baseX + i * cellSpacing, y, baseZ),
            new Vector3(baseX + i * cellSpacing, y, baseZ + (size - 1) * cellSpacing));
    }
}

private void CreateLine(Vector3 start, Vector3 end)
{
    GameObject go = new GameObject("GridLine");
    go.transform.SetParent(transform);

    LineRenderer lr = go.AddComponent<LineRenderer>();
    lr.positionCount = 2;
    lr.SetPosition(0, start);
    lr.SetPosition(1, end);
    lr.startWidth = 0.05f;
    lr.endWidth   = 0.05f;
    lr.material   = new Material(Shader.Find("Sprites/Default"));
    lr.startColor = Color.black;
    lr.endColor   = Color.black;
    lr.useWorldSpace = true;
}


}

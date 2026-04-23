using System;
using UnityEngine;

/// <summary>
/// 보드 교차점 하나를 담당하는 월드 공간 오브젝트.
///
/// ─── CellPrefab 구조 ───────────────────────────────────────
///   CellPrefab
///     └─ BoxCollider (또는 MeshCollider)  ← 클릭 감지용
///        (시각적 요소 불필요 — 돌은 Instantiate로 따로 생성)
/// ────────────────────────────────────────────────────────────
/// </summary>
[RequireComponent(typeof(Collider))]
public class BoardCell : MonoBehaviour
{
    public int Row { get; private set; }
    public int Col { get; private set; }

    private BoardUI    _boardUI;
    private Func<bool> _canInteract; // () => 내 턴 && 입력 활성
    private bool       _hasStone = false;

    public void Init(int row, int col)
    {
        Row          = row;
        Col          = col;
    }


    /// <summary>돌이 놓이면 호출됩니다. 이후 이 칸은 클릭/호버 무시.</summary>
    public bool HasStone => _hasStone;
    public void MarkOccupied() => _hasStone = true;
}

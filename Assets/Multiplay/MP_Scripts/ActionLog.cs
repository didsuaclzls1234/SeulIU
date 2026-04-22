using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임의 단일 진실 공급원(Source of Truth).
///
/// 보드 상태는 이 로그를 재생해서 도출합니다.
/// 스킬 시스템이나 돌 변형 등이 추가되어도 로그만 확장하면 됩니다.
/// </summary>
public class ActionLog
{
    public const int BoardSize = 15;

    private readonly List<StoneAction> _actions = new();
    private readonly StoneColor[,] _board = new StoneColor[BoardSize, BoardSize];

    /// <summary>지금까지 기록된 모든 착수 (읽기 전용)</summary>
    public IReadOnlyList<StoneAction> Actions => _actions;

    /// <summary>
    /// 현재 보드 배열 (읽기 전용으로 사용).
    /// WinDetector, MoveValidator 등에 직접 전달해도 됩니다.
    /// </summary>
    public StoneColor[,] Board => _board;

    /// <summary>총 착수 수 (= 다음 기대 Seq 번호)</summary>
    public int Count => _actions.Count;

    /// <summary>
    /// 착수를 검증하고 로그에 추가합니다.
    /// seq == Count 가 아니면 순서 불일치로 거부됩니다.
    /// </summary>
    /// <returns>성공 시 true</returns>
    public bool TryApply(StoneColor color, int row, int col, int seq)
    {
        if (seq != _actions.Count)
        {
            Debug.LogWarning($"[ActionLog] 순서 불일치 — expected {_actions.Count}, got {seq}");
            return false;
        }

        if (row < 0 || row >= BoardSize || col < 0 || col >= BoardSize)
        {
            Debug.LogWarning($"[ActionLog] 범위 초과: ({row},{col})");
            return false;
        }

        if (_board[row, col] != StoneColor.None)
        {
            Debug.LogWarning($"[ActionLog] 이미 돌이 있음: ({row},{col})");
            return false;
        }

        _board[row, col] = color;
        _actions.Add(new StoneAction(color, row, col, seq));
        return true;
    }

    public void Clear()
    {
        _actions.Clear();
        System.Array.Clear(_board, 0, _board.Length);
    }
}

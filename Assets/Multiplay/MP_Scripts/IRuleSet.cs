/// <summary>
/// 오목 룰셋 인터페이스.
///
/// 팀이 룰을 확정하면 이 인터페이스를 구현하는 클래스만 만들면 됩니다.
/// GameSession, WinDetector, MoveValidator 등은 수정 불필요.
///
/// 구현 예시:
///   StandardRuleSet  — 순수 5목 (금수 없음)
///   RenjuRuleSet     — 렌주 룰 (삼삼·사사·장목 금수)
/// </summary>
public interface IRuleSet
{
    /// <summary>
    /// 돌이 놓인 이후 승리 여부를 확인합니다.
    /// 호출 시점: board[row,col]에 이미 color가 기록된 상태.
    /// </summary>
    bool IsWin(StoneColor[,] board, int row, int col, StoneColor color);

    /// <summary>
    /// 착수 전에 해당 수가 금수인지 확인합니다.
    /// 호출 시점: board[row,col]은 아직 None.
    /// 금수 규칙이 없으면 false를 반환하면 됩니다.
    /// </summary>
    bool IsForbidden(StoneColor[,] board, int row, int col, StoneColor color);
}

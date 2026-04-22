/// <summary>
/// 착수를 ActionLog에 적용하기 전에 유효성을 검사합니다.
/// 양쪽 클라이언트가 동일한 로직으로 독립 검증합니다(대등 검증).
/// </summary>
public class MoveValidator
{
    private readonly IRuleSet _ruleSet;
    private readonly ActionLog _log;

    public MoveValidator(IRuleSet ruleSet, ActionLog log)
    {
        _ruleSet = ruleSet;
        _log     = log;
    }

    /// <summary>
    /// 해당 착수가 합법인지 반환합니다.
    /// - 보드 범위 내
    /// - 빈 칸
    /// - IRuleSet 금수 아님
    /// </summary>
    public bool IsValid(StoneColor color, int row, int col)
    {
        if (row < 0 || row >= ActionLog.BoardSize || col < 0 || col >= ActionLog.BoardSize)
            return false;

        if (_log.Board[row, col] != StoneColor.None)
            return false;

        if (_ruleSet.IsForbidden(_log.Board, row, col, color))
            return false;

        return true;
    }
}

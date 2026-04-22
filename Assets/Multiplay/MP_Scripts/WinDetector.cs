/// <summary>
/// 착수 후 승리 여부를 판정합니다.
/// IRuleSet에 위임하므로 룰 교체 시 이 클래스는 수정 불필요.
/// </summary>
public class WinDetector
{
    private readonly IRuleSet _ruleSet;

    public WinDetector(IRuleSet ruleSet) => _ruleSet = ruleSet;

    /// <summary>
    /// board[row,col]에 color가 이미 놓인 상태에서 호출하세요.
    /// </summary>
    public bool Check(StoneColor[,] board, int row, int col, StoneColor color)
        => _ruleSet.IsWin(board, row, col, color);
}

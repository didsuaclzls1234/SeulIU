/// <summary>
/// 표준 오목 룰셋.
/// 5개 이상 연속 = 승리 / 금수 없음.
///
/// 팀 합의 후 RenjuRuleSet 등으로 교체하세요.
/// </summary>
public class StandardRuleSet : IRuleSet
{
    // 금수 없음
    public bool IsForbidden(StoneColor[,] board, int row, int col, StoneColor color) => false;

    public bool IsWin(StoneColor[,] board, int row, int col, StoneColor color)
    {
        // 4방향(가로·세로·대각 두 방향)을 각각 검사
        return LineCount(board, row, col, color, 1, 0) >= 5   // 가로
            || LineCount(board, row, col, color, 0, 1) >= 5   // 세로
            || LineCount(board, row, col, color, 1, 1) >= 5   // 대각선 ↘
            || LineCount(board, row, col, color, 1, -1) >= 5; // 대각선 ↗
    }

    // (row,col)을 기준으로 (dr,dc) 방향 + 반대 방향의 연속 돌 수 합산
    private static int LineCount(StoneColor[,] board, int row, int col,
                                  StoneColor color, int dr, int dc)
    {
        int size  = board.GetLength(0);
        int count = 1; // 현재 칸 포함

        for (int sign = -1; sign <= 1; sign += 2) // +1 방향, -1 방향
        {
            for (int i = 1; i < size; i++)
            {
                int r = row + dr * i * sign;
                int c = col + dc * i * sign;
                if (r < 0 || r >= size || c < 0 || c >= size) break;
                if (board[r, c] != color) break;
                count++;
            }
        }

        return count;
    }
}

/// <summary>
/// 돌 색. None = 빈 칸.
/// </summary>
public enum StoneColor
{
    None  = 0,
    Black = 1,
    White = 2
}

public static class StoneColorExtensions
{
    public static StoneColor Opponent(this StoneColor c)
        => c == StoneColor.Black ? StoneColor.White
         : c == StoneColor.White ? StoneColor.Black
         : StoneColor.None;

    public static string ToKorean(this StoneColor c)
        => c == StoneColor.Black ? "흑" : c == StoneColor.White ? "백" : "없음";
}

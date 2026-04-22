/// <summary>
/// 현재 차례를 ActionLog.Count에서 계산합니다.
/// 짝수 번째 착수 = 흑 차례, 홀수 번째 = 백 차례.
///
/// 스킬 시스템이나 턴 스킵이 생기면 ActionLog 엔트리를 확장해서
/// 이 클래스에서 처리하면 됩니다.
/// </summary>
public class TurnManager
{
    private readonly ActionLog _log;

    /// <summary>로컬 플레이어의 돌 색 (역할 배정 후 SetMyColor로 설정)</summary>
    public StoneColor MyColor { get; private set; } = StoneColor.None;

    public TurnManager(ActionLog log) => _log = log;

    public void SetMyColor(StoneColor color) => MyColor = color;

    /// <summary>지금 착수해야 하는 색</summary>
    public StoneColor CurrentTurn
        => _log.Count % 2 == 0 ? StoneColor.Black : StoneColor.White;

    /// <summary>지금이 로컬 플레이어의 차례인가?</summary>
    public bool IsMyTurn => MyColor != StoneColor.None && CurrentTurn == MyColor;
}

/// <summary>
/// ActionLog에 기록되는 단일 착수 데이터.
/// Seq(순서 번호)가 있어서 리플레이, 무르기, 기보 저장에 자연스럽게 확장됩니다.
/// </summary>
[System.Serializable]
public struct StoneAction
{
    public StoneColor Color;
    public int Row;
    public int Col;
    public int Seq;     // 0-based 순서 번호 (= 착수 시점의 ActionLog.Count)

    public StoneAction(StoneColor color, int row, int col, int seq)
    {
        Color = color;
        Row   = row;
        Col   = col;
        Seq   = seq;
    }

    public override string ToString() => $"[{Seq}] {Color} ({Row},{Col})";
}

/// <summary>
/// RaiseEvent에서 사용하는 커스텀 이벤트 코드 상수.
/// 새 이벤트를 추가할 때 여기에만 추가하면 됩니다.
/// </summary>
public static class PhotonEventCodes
{
    public const byte AssignRoles = 1;  // Master → All : 흑/백 배정
    public const byte PlaceStone  = 2;  // 착수한 플레이어 → All
    public const byte GameOver    = 3;  // 승부 확정
}

/// <summary>
/// RaiseEvent에서 사용하는 커스텀 이벤트 코드 상수.
/// 새 이벤트를 추가할 때 여기에만 추가하면 됩니다.
/// </summary>
public static class PhotonEventCodes
{
    public const byte AssignRoles = 1;  // Master → All : 흑/백 배정
    public const byte PlaceStone  = 2;  // 착수한 플레이어 → All
    public const byte GameOver    = 3;  // 승부 확정

    // [신규 추가] 상화님, 아래 3개 이벤트를 추가로 구현해 주세요!

    // 1. 방 접속 시 교환할 정보 (닉네임 + 선택한 스킬덱 3개 ID)
    public const byte SyncPlayerInfo = 4; // Data: object[] { string 닉네임, int 스킬1, int 스킬2, int 스킬3 }

    // 2. 턴 타이머 동기화 (방장이 매 턴 시작 시 시간 기준점을 쏴줌)
    public const byte SyncTimer = 5; // Data: object[] { float 서버타임스탬프 }

    // 3. 스킬 사용 (가장 중요)
    // - 타겟이 여러 개일 수 있으므로(랜덤 스킬 등) 좌표를 배열로 묶어서 보냅니다.
    public const byte UseSkill = 6; // Data: object[] { int 스킬ID, int[] x좌표들, int[] y좌표들 }
    
    //무르기 요청,수락||거절
    public const byte UndoRequest  = 7; // 무르기 요청
    public const byte UndoReply    = 8; // 무르기 수락/거절 

    public const byte PlayerReady = 9;  // 준비 완료 신호
}

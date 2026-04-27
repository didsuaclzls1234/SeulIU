using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

/// <summary>
/// Master Client가 흑/백을 랜덤으로 배정하고 두 클라이언트 모두에게 알립니다.
/// 게임 시작 시 1회만 호출됩니다.
/// </summary>
public static class RoleAssigner
{
    /// <summary>
    /// Master Client에서만 호출하세요.
    /// 결과는 PhotonEventCodes.AssignRoles 이벤트로 ReceiverGroup.All에 전달됩니다.
    /// </summary>
    public static void AssignAndBroadcast()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("[RoleAssigner] Master Client가 아닌 곳에서 호출됨, 무시");
            return;
        }

        Player[] players = PhotonNetwork.PlayerList;
        if (players.Length != 2)
        {
            Debug.LogError($"[RoleAssigner] 플레이어 수 오류: {players.Length}명");
            return;
        }

        // 랜덤으로 흑 담당 ActorNumber 결정
        int blackActorNumber = Random.value < 0.5f
            ? players[0].ActorNumber
            : players[1].ActorNumber;

        object[] data = new object[] { blackActorNumber ,(float)PhotonNetwork.Time};

        // AddToRoomCache: 나중에 재연결한 클라이언트도 받을 수 있도록 캐싱
        RaiseEventOptions opts = new RaiseEventOptions
        {
            Receivers     = ReceiverGroup.All,
            CachingOption = EventCaching.AddToRoomCache
        };

        PhotonNetwork.RaiseEvent(
            PhotonEventCodes.AssignRoles, data, opts, SendOptions.SendReliable);

        Debug.Log($"[RoleAssigner] 흑 ActorNumber={blackActorNumber} 배정 및 브로드캐스트");
    }
}

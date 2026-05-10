using UnityEngine;
using System.Collections.Generic;

public class Skill_1_StoneShift : SkillBase
{
    public Skill_1_StoneShift(SkillData skillData) : base(skillData) { }

    public override SkillUseResult CanUse(int currentSP, bool isAntiMagicActive, BoardManager board, StoneColor myColor)
    {
        SkillUseResult baseResult = base.CanUse(currentSP, isAntiMagicActive, board, myColor);
        if (baseResult != SkillUseResult.Success) return baseResult;
        
        // 내 돌이 하나라도 있어야 사용 가능
        int myColorInt = (int)myColor;
        for (int x = 0; x < board.boardSize; x++)
            for (int y = 0; y < board.boardSize; y++)
                if (board.grid[x, y] == myColorInt) return SkillUseResult.Success;

        Debug.LogWarning("[StoneShift] 이동할 내 돌이 없습니다!");
        return SkillUseResult.NoValidTarget;
    }

    public override bool Execute(int[] targetX, int[] targetY, GameManager gm, BoardManager board)
    {
        int tx = targetX[0];
        int ty = targetY[0];

        // 현재 이 스킬을 시전한 사람의 색상 (네트워크 양쪽에서 동일함)
        StoneColor casterColor = gm.currentTurnColor;

        // 1. 선택한 칸이 내 돌인지 검증
        if (board.grid[tx, ty] != (int)casterColor)
        {
            Debug.LogWarning("[StoneShift] 시전자의 돌이 아닙니다!");
            return false;
        }

        // 네트워크 패킷에 담겨올(또는 담을) 목적지 좌표
        int destX = targetX[1];
        int destY = targetY[1];

        // 2. 합법적인 빈 칸 수집 (금수 제외) - destX가 -1 이면 내가 직접 클릭한 것 -> 랜덤 연산 수행
        if (destX == -1)
        {
            List<Vector2Int> validDestinations = new List<Vector2Int>();
            int radius = 2; // 반경 2칸 이내 

            for (int x = 0; x < board.boardSize; x++)
            {
                for (int y = 0; y < board.boardSize; y++)
                {
                    if (x == tx && y == ty) continue;

                    // IsValidMove가 '상대 봉인', '칼날비(None)', '금수', '이미 있는 돌'을 완벽히 차단함!
                    if (board.IsValidMove(x, y, casterColor, silent: true))
                    {
                        if (HasNeighborInRadius(board.grid, x, y, radius, board.boardSize))
                            validDestinations.Add(new Vector2Int(x, y));
                    }
                }
            }

            // 주변에 자리가 없으면 전체 보드에서 찾음 (안전장치)
            if (validDestinations.Count == 0)
            {
                for (int x = 0; x < board.boardSize; x++)
                    for (int y = 0; y < board.boardSize; y++)
                        if (!(x == tx && y == ty) && board.IsValidMove(x, y, casterColor, silent: true))
                            validDestinations.Add(new Vector2Int(x, y));
            }

            if (validDestinations.Count == 0)
            {
                Debug.LogWarning("[StoneShift] 이동 가능한 합법적인 빈 칸이 없습니다!");
                return false;
            }

            // 3. 랜덤 목적지 선택
            Vector2Int dest = validDestinations[Random.Range(0, validDestinations.Count)];
            destX = dest.x;
            destY = dest.y;

            // 상대방에게 보낼 패킷용으로 좌표 저장
            targetX[1] = destX;
            targetY[1] = destY;
        }

        // ** 돌이 가진 '신의 가호(방패)' 여부 확인
        bool hadShield = board.shieldGrid[tx, ty];

        // 4. 원래 자리 제거 (데이터 + 3D 오브젝트)
        board.grid[tx, ty] = 0;
        board.RemoveStoneObjectAt(tx, ty);
        if (hadShield) board.RemoveShield(tx, ty); // 기존 방패 제거

        // 5. 목적지에 배치 (데이터 + 3D 오브젝트)
        GameObject newStone = board.PlaceStone(destX, destY, casterColor);
        if (hadShield) board.ApplyShield(destX, destY); // 새 자리에 방패 부여

        // ** 투명화 상태라면 이동된 돌도 잠깐 보였다가 사라지게
        if (gm.skillManager.myInvisibilityTurns > 0 && newStone != null)
        {
            gm.StartCoroutine(board.BlinkAndHideRoutine(newStone, gm.localPlayerColor, true));
        }

        Debug.Log($"[StoneShift] ({tx},{ty}) → ({destX},{destY}) 이동 완료!");

        return true;
    }

    // 주변 반경 탐색 헬퍼 함수
    private bool HasNeighborInRadius(int[,] grid, int cx, int cy, int radius, int size)
    {
        int startX = Mathf.Max(0, cx - radius);
        int endX = Mathf.Min(size - 1, cx + radius);
        int startY = Mathf.Max(0, cy - radius);
        int endY = Mathf.Min(size - 1, cy + radius);

        for (int i = startX; i <= endX; i++)
            for (int j = startY; j <= endY; j++)
                if (grid[i, j] != 0) return true;

        return false;
    }
}

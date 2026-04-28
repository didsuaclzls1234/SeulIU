using UnityEngine;
using System.Collections.Generic;

public class Skill_1_StoneShift : SkillBase
{
    public Skill_1_StoneShift(SkillData skillData) : base(skillData) { }

    public override bool CanUse(int currentSP, bool isAntiMagicActive, BoardManager board, StoneColor myColor)
    {
        if (!base.CanUse(currentSP, isAntiMagicActive, board, myColor)) return false;

        // 내 돌이 하나라도 있어야 사용 가능
        int myColorInt = (int)myColor;
        for (int x = 0; x < board.boardSize; x++)
            for (int y = 0; y < board.boardSize; y++)
                if (board.grid[x, y] == myColorInt) return true;

        Debug.LogWarning("[StoneShift] 이동할 내 돌이 없습니다!");
        return false;
    }

    public override bool Execute(int[] targetX, int[] targetY, GameManager gm, BoardManager board)
    {
        int tx = targetX[0];
        int ty = targetY[0];

        // 1. 선택한 칸이 내 돌인지 검증
        if (board.grid[tx, ty] != (int)gm.localPlayerColor)
        {
            Debug.LogWarning("[StoneShift] 선택한 칸이 내 돌이 아닙니다!");
            return false;
        }

        // 2. 합법적인 빈 칸 수집 (금수 제외)
        List<Vector2Int> validDestinations = new List<Vector2Int>();
        for (int x = 0; x < board.boardSize; x++)
        {
            for (int y = 0; y < board.boardSize; y++)
            {
                // 원래 자리는 제외
                if (x == tx && y == ty) continue;

                // IsValidMove로 금수 포함 합법적 빈 칸만 수집
                if (board.IsValidMove(x, y, gm.localPlayerColor, silent: true))
                    validDestinations.Add(new Vector2Int(x, y));
            }
        }

        if (validDestinations.Count == 0)
        {
            Debug.LogWarning("[StoneShift] 이동 가능한 합법적인 빈 칸이 없습니다!");
            return false;
        }

        // 3. 랜덤 목적지 선택
        Vector2Int dest = validDestinations[Random.Range(0, validDestinations.Count)];

        // 4. 원래 자리 제거 (데이터 + 3D 오브젝트)
        board.grid[tx, ty] = 0;
        board.RemoveStoneObjectAt(tx, ty);

        // 5. 목적지에 배치 (데이터 + 3D 오브젝트)
        board.PlaceStone(dest.x, dest.y, gm.localPlayerColor);

        // 6. 네트워크 전송용 목적지 저장
        targetX[1] = dest.x;
        targetY[1] = dest.y;

        Debug.Log($"[StoneShift] ({tx},{ty}) → ({dest.x},{dest.y}) 이동 완료!");
        return true;
    }
}

using UnityEngine;
using System.Collections.Generic;

public class Skill_8_GodBless : SkillBase
{
    public Skill_8_GodBless(SkillData skillData) : base(skillData) { }

    public override bool Execute(int[] targetX, int[] targetY, GameManager gm, BoardManager board)
    {
        int tx = targetX[0];
        int ty = targetY[0];
        StoneColor casterColor = gm.currentTurnColor;

        // 1. 선택한 내 돌 검증
        if (board.grid[tx, ty] != (int)casterColor) return false;

        // 2. 이미 보호받고 있으면 사용 불가
        if (board.shieldGrid[tx, ty])
        {
            Debug.LogWarning("[GodBless] 이미 보호받고 있는 돌입니다!");
            return false;
        }

        // 네트워크에서 온 랜덤 타겟 좌표
        int randomX = targetX[1];
        int randomY = targetY[1];

        // 3. 내가 직접 시전한 거라면? 랜덤 돌 찾기
        if (randomX == -1)
        {
            List<Vector2Int> myOtherStones = new List<Vector2Int>();
            for (int x = 0; x < board.boardSize; x++)
            {
                for (int y = 0; y < board.boardSize; y++)
                {
                    // 내 돌이고, 방금 선택한 돌이 아니며, 아직 보호막이 없는 돌 수집
                    if (board.grid[x, y] == (int)casterColor && !(x == tx && y == ty) && !board.shieldGrid[x, y])
                    {
                        myOtherStones.Add(new Vector2Int(x, y));
                    }
                }
            }

            if (myOtherStones.Count > 0)
            {
                Vector2Int randPos = myOtherStones[Random.Range(0, myOtherStones.Count)];
                randomX = randPos.x;
                randomY = randPos.y;

                // 패킷 쏘기 위해 배열 업데이트
                targetX[1] = randomX;
                targetY[1] = randomY;
            }
        }

        // 4. 보호막 적용 (타겟 1 + 랜덤 타겟 1)
        board.ApplyShield(tx, ty);
        if (randomX != -1 && randomY != -1)
        {
            board.ApplyShield(randomX, randomY);
        }

        return true;
    }
}
using UnityEngine;

public class Skill_2_Seal : SkillBase
{
    public Skill_2_Seal(SkillData skillData) : base(skillData) { }

    public override bool Execute(int[] targetX, int[] targetY, GameManager gameManager, BoardManager board)
    {
        int tx = targetX[0];
        int ty = targetY[0];

        StoneColor casterColor = gameManager.currentTurnColor;

        // 1. 이미 돌이 놓여있는 곳은 봉인 불가
        if (board.grid[tx, ty] != 0)
        {
            Debug.LogWarning("이미 돌이 놓인 칸은 봉인할 수 없습니다.");
            return false;
        }

        // 내가 쏘든 상대가 쏘든 정확한 주인의 색상으로 자물쇠가 걸림
        board.ApplySeal(tx, ty, data.durationTurn, casterColor);

        // 3. 네트워크 동기화를 위한 배열 세팅
        // (타겟이 1개이므로 1번 인덱스는 -1로 둠)
        targetX[1] = -1;
        targetY[1] = -1;

        return true;
    }
}

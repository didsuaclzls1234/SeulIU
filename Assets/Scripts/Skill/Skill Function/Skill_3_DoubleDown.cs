using UnityEngine;

public class Skill_3_DoubleDown : SkillBase
{
    public Skill_3_DoubleDown(SkillData skillData) : base(skillData) { }

    public override bool CanUse(int currentSP, bool isAntiMagicActive, BoardManager board, StoneColor myColor)
    {
        if (!base.CanUse(currentSP, isAntiMagicActive, board, myColor)) return false;

        // 합법적인 빈 칸이 2개 이상 있어야 사용 가능 (현재 착수 + 추가 착수)
        int validCount = 0;
        for (int x = 0; x < board.boardSize; x++)
            for (int y = 0; y < board.boardSize; y++)
                if (board.IsValidMove(x, y, myColor, silent: true))
                    if (++validCount >= 2) return true;

        Debug.LogWarning("[DoubleDown] 착수 가능한 칸이 부족합니다!");
        return false;
    }

    public override bool Execute(int[] targetX, int[] targetY, GameManager gm, BoardManager board)
    {
        // extraPlacementCount를 1로 설정 → GameManager가 다음 착수 시 랜덤위치에 자동 착수
        gm.extraPlacementCount = 1;

        Debug.Log("[DoubleDown] 이번 턴 추가 착수 1회 부여!");
        return true;
    }
}
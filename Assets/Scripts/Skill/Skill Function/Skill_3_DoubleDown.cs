using UnityEngine;

public class Skill_3_DoubleDown : SkillBase
{
    public Skill_3_DoubleDown(SkillData skillData) : base(skillData) { }

    public override SkillUseResult CanUse(int currentSP, bool isAntiMagicActive, BoardManager board, StoneColor myColor)
    {
        SkillUseResult baseResult = base.CanUse(currentSP, isAntiMagicActive, board, myColor);
        if (baseResult != SkillUseResult.Success) return baseResult;

        // 합법적인 빈 칸이 2개 이상 있어야 사용 가능 (현재 착수 + 추가 착수)
        int validCount = 0;
        for (int x = 0; x < board.boardSize; x++)
            for (int y = 0; y < board.boardSize; y++)
                if (board.IsValidMove(x, y, myColor, silent: true))
                    if (++validCount >= 2) return SkillUseResult.Success;

        Debug.LogWarning("[DoubleDown] 착수 가능한 칸이 부족합니다!");
        return SkillUseResult.NoValidTarget;
    }

    public override bool Execute(int[] targetX, int[] targetY, GameManager gm, BoardManager board)
    {
        // extraPlacementCount를 1로 설정 → GameManager가 다음 착수 시 랜덤위치에 자동 착수
        gm.extraPlacementCount = 1;

        // 네트워크 수신을 위해 배열 세팅 (특별한 좌표가 필요 없으므로 -1)
        targetX[1] = -1;
        targetY[1] = -1;

        // 시스템 메시지 띄우기
        if (gm.gameHUD != null)
            gm.gameHUD.ShowSystemMessage("이중 착수 발동! 이번 턴에 돌을 2번 놓습니다.");

        Debug.Log("[DoubleDown] 이번 턴 추가 착수 1회 부여!");
        return true;
    }
}
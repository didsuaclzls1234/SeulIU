using UnityEngine;

public class Skill_10_Sanctification : SkillBase
{
    public Skill_10_Sanctification(SkillData skillData) : base(skillData) { }

    public override SkillUseResult CanUse(int currentSP, bool isAntiMagicActive, BoardManager board, StoneColor myColor)
    {
        if (myColor != StoneColor.White)
        {
            Debug.LogWarning("[Sanctification] 백돌 전용 스킬입니다!");
            return SkillUseResult.NoValidTarget;
        }
        return SkillUseResult.Success;
    }

    public override bool Execute(int[] targetX, int[] targetY, GameManager gm, BoardManager board)
    {
        // 보드 매니저에 신성화 모드 ON!
        board.ActivateSanctification();

        if (gm.gameHUD != null)
            gm.gameHUD.ShowSystemMessage("신성화 발동! 모든 돌이 백돌의 모습으로 나타납니다.");

        Debug.Log("[Sanctification] 백돌 신성화 렌더링 모드 활성화 완료!");
        return true;
    }
}
using UnityEngine;

public class Skill_4_AntiMagic : SkillBase
{
    public Skill_4_AntiMagic(SkillData skillData) : base(skillData) { }

    public override SkillUseResult CanUse(int currentSP, bool isAntiMagicActive, BoardManager board, StoneColor myColor)
    {
        SkillUseResult baseResult = base.CanUse(currentSP, isAntiMagicActive, board, myColor);
        if (baseResult != SkillUseResult.Success) return baseResult;

        return SkillUseResult.Success;
    }

    public override bool Execute(int[] targetX, int[] targetY, GameManager gm, BoardManager board)
    {
        // 내가 썼을 때 시스템 메시지 띄우기
        if (gm.gameHUD != null)
            gm.gameHUD.ShowSystemMessage("안티매직 발동! 상대방의 스킬을 2턴간 봉인합니다.");

        Debug.Log("[AntiMagic] 상대 스킬 2턴간 봉인 패킷 전송!");
        return true;
    }
}
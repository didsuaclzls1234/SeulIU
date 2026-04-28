using UnityEngine;

public class Skill_4_AntiMagic : SkillBase
{
    public Skill_4_AntiMagic(SkillData skillData) : base(skillData) { }

    public override bool CanUse(int currentSP, bool isAntiMagicActive, BoardManager board, StoneColor myColor)
    {
        if (!base.CanUse(currentSP, isAntiMagicActive, board, myColor)) return false;
        return true;
    }

    public override bool Execute(int[] targetX, int[] targetY, GameManager gm, BoardManager board)
    {
        Debug.Log("[AntiMagic] 상대 스킬 2턴간 봉인 패킷 전송!");
        return true;
    }
}
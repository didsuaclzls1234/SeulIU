using UnityEngine;

public class Skill_9_Destruction : SkillBase
{
    public Skill_9_Destruction(SkillData skillData) : base(skillData) { }

    // 런타임 방어 — 덱 구성 단계에서 막히겠지만 혹시 모를 상황 대비
    public override SkillUseResult CanUse(int currentSP, bool isAntiMagicActive, BoardManager board, StoneColor myColor)
    {
        if (myColor != StoneColor.Black)
        {
            Debug.LogWarning("[Destruction] 흑돌 전용 스킬입니다!");
            return SkillUseResult.NoValidTarget; // 흑돌이 아닌 플레이어는 타겟팅 자체가 불가능하도록
        }
        return SkillUseResult.Success;
    }

    // SkillManager.AutoActivatePassiveSkills()에서 게임 시작 시 1회 자동 호출
    public override bool Execute(int[] targetX, int[] targetY, GameManager gm, BoardManager board)
    {
        if (board.ruleManager == null) return false;

        board.ruleManager.DisableRenjuRules(1); // 흑돌(1)의 모든 금수 해제

        if (gm.gameHUD != null)
            gm.gameHUD.ShowSystemMessage("룰 파괴 발동! 흑돌의 모든 렌주 규칙이 제거됩니다.");

        Debug.Log("[Destruction] 흑돌 렌주 규칙 비활성화 완료!");
        return true;
    }
}
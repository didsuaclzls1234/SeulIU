using UnityEngine;

public class Skill_8_SevenSins : SkillBase
{
    public Skill_8_SevenSins(SkillData skillData) : base(skillData) { }

    public override bool Execute(int[] targetX, int[] targetY, GameManager gm, BoardManager board)
    {
        // 타겟팅 없는 스킬이므로 targetX[0]에 들어온 좌표는 무시하고 발동합니다.

        // 상대방 색상 알아내기
        int oppColorInt = (gm.currentTurnColor == StoneColor.Black) ? 2 : 1;

        // RuleManager를 통해 상대방의 승리 조건을 7목으로 변경
        RuleSettings oppRules = (oppColorInt == 1) ? board.ruleManager.blackRules : board.ruleManager.whiteRules;
        oppRules.winCondition = 7;

        if (gm.gameHUD != null)
            gm.gameHUD.ShowSystemMessage("칠죄종 발동! 상대방의 승리 조건이 7목으로 영구 변경되었습니다.");

        Debug.Log("[SevenSins] 상대방 승리 조건 7목 변경 완료!");

        // 네트워크 전송용 빈 좌표 세팅
        targetX[1] = -1;
        targetY[1] = -1;

        return true;
    }
}
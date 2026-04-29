using UnityEngine;

public class Skill_7_Invisibility : SkillBase
{
    public Skill_7_Invisibility(SkillData skillData) : base(skillData) { }

    public override bool Execute(int[] targetX, int[] targetY, GameManager gm, BoardManager board)
    {
        // 1. 투명화 턴수 설정
        gm.skillManager.myInvisibilityTurns = data.durationTurn;

        // 2. 내 화면에 있는 내 돌들을 반투명(Ghost) 상태로 싹 바꿈
        board.SetStoneInvisibility(gm.currentTurnColor, false, true);

        if (gm.gameHUD != null)
            gm.gameHUD.ShowSystemMessage("투명화 발동! 내 돌이 유령처럼 변합니다.");

        // 네트워크 전송용 빈 좌표 세팅
        targetX[1] = -1;
        targetY[1] = -1;

        return true;
    }
}
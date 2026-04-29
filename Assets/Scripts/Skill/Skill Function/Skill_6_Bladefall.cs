using System.Collections.Generic;
using UnityEngine;

public class Skill_6_Bladefall : SkillBase
{
    public Skill_6_Bladefall(SkillData skillData) : base(skillData) { }

    public override SkillUseResult CanUse(int currentSP, bool isAntiMagicActive, BoardManager board, StoneColor myColor)
    {
        SkillUseResult baseResult = base.CanUse(currentSP, isAntiMagicActive, board, myColor);
        if (baseResult != SkillUseResult.Success) return baseResult;

        // 봉인할 빈 칸이 1개라도 있어야 사용 가능
        for (int x = 0; x < board.boardSize; x++)
            for (int y = 0; y < board.boardSize; y++)
                if (board.grid[x, y] == 0 && board.sealedGrid[x, y].turns == 0)
                    return SkillUseResult.Success;

        Debug.LogWarning("[Bladefall] 봉인할 빈 칸이 없습니다!");
        return SkillUseResult.NoValidTarget;
    }

    public override bool Execute(int[] targetX, int[] targetY, GameManager gm, BoardManager board)
    {
        StoneColor casterColor = gm.currentTurnColor;
        List<Vector2Int> targets = SelectSealTargets(board,casterColor);

        // 선택한 칸에 봉인 적용 + 좌표 배열에 기록 (네트워크 전송용)
        for (int i = 0; i < targets.Count; i++)
        {
            board.ApplySeal(targets[i].x, targets[i].y, 1, casterColor);//data.durationTurn 이 0으로 되어있어서 일단 1로 고정 csv 수정하면 다시 고쳐야할듯
            targetX[i] = targets[i].x;
            targetY[i] = targets[i].y;
        }

        if (gm.gameHUD != null)
            gm.gameHUD.ShowSystemMessage($"칼날비 발동! 빈 교차점 {targets.Count}칸이 봉인됩니다.");

        Debug.Log($"[Bladefall] {targets.Count}칸 봉인 완료!");
        return true;
    }

    // 봉인 대상 선정 — 돌 인접 칸 우선, 최대 10칸
    private List<Vector2Int> SelectSealTargets(BoardManager board, StoneColor casterColor, int count = 20)
    {
        List<Vector2Int> adjacent = new List<Vector2Int>();
        List<Vector2Int> others   = new List<Vector2Int>();
        int enemyColorInt = (casterColor == StoneColor.Black) ? 2 : 1; // 상대 돌 색상

        int[] dx = { -1, -1, -1, 0, 0, 1, 1, 1 };
        int[] dy = { -1, 0, 1, -1, 1, -1, 0, 1 };

        for (int x = 0; x < board.boardSize; x++)
        {
            for (int y = 0; y < board.boardSize; y++)
            {
                if (board.grid[x, y] != 0) continue;           // 이미 돌이 있음
                if (board.sealedGrid[x, y].turns > 0) continue; // 이미 봉인됨

                bool isAdjacent = false;
                for (int d = 0; d < 8; d++)
                {
                    int nx = x + dx[d], ny = y + dy[d];
                    if (nx >= 0 && nx < board.boardSize && ny >= 0 && ny < board.boardSize)
                        if (board.grid[nx, ny] == enemyColorInt) { isAdjacent = true; break; }
                }

                if (isAdjacent) adjacent.Add(new Vector2Int(x, y));
                else others.Add(new Vector2Int(x, y));
            }
        }

        // 인접 칸 우선 셔플, 부족하면 나머지로 채움
        Shuffle(adjacent);
        Shuffle(others);
        adjacent.AddRange(others);

        return adjacent.GetRange(0, Mathf.Min(count, adjacent.Count));
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
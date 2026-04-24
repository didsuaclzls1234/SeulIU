using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class GomokuAI
{
    private int maxDepth; // AI가 몇 수 앞까지 내다볼 것인가 (높을수록 똑똑하지만 느림)
    private int aiColor;      // AI의 돌 색상 (흑 or 백)
    private int playerColor;  // 플레이어의 돌 색상

    private RuleManager ruleManager;
    private int boardSize;

    public GomokuAI(int aiColor, RuleManager rm, int size, int difficulty)
    {
        this.aiColor = aiColor;
        this.playerColor = (aiColor == 1) ? 2 : 1;
        this.ruleManager = rm;
        this.boardSize = size;
        this.maxDepth = difficulty;
    }

    // =========================================================
    // 1. AI 턴 진입점 (GameManager에서 호출)
    // =========================================================
    public async Task<Vector2Int> CalculateBestMoveAsync(int[,] currentGrid)
    {
        // 원본 배열이 망가지지 않게 복사본 생성
        int[,] gridClone = (int[,])currentGrid.Clone();

        // 1. 연산 시작 전에 현재 룰의 '복사본'을 미리 따놓음
        RuleSettings aiRulesClone = ruleManager.GetRulesSnapshot(aiColor);
        RuleSettings playerRulesClone = ruleManager.GetRulesSnapshot(playerColor);

        // 2. Task에 들어갈 때 원본(ruleManager)이 아니라 복사본 2개를 쥐여서 보냄
        // (메인 스레드(유니티 화면)가 멈추지 않게 별도 스레드에서 무거운 탐색을 돌림)
        Vector2Int bestMove = await Task.Run(() => GetBestMove(gridClone, aiRulesClone, playerRulesClone));
        return bestMove;

        
    }

    // =========================================================
    // 2. 미니맥스 탐색 시작점
    // =========================================================
    private Vector2Int GetBestMove(int[,] grid, RuleSettings aiRules, RuleSettings playerRules)
    {
        int bestScore = int.MinValue;
        int size = grid.GetLength(0);
        Vector2Int bestMove = new Vector2Int(size / 2, size / 2); // 첫 수라면 정중앙 

        // 최적화 1: 19x19 전체를 돌지 않고, '돌이 놓여있는 곳 주변'만 후보군으로 추림
        List<Vector2Int> candidateMoves = GetCandidateMoves(grid, aiColor, aiRules);

        if (candidateMoves.Count == 0) return bestMove;

        foreach (Vector2Int move in candidateMoves)
        {
            grid[move.x, move.y] = aiColor; // 가상으로 한 수 두어봄

            // 미니맥스 + 알파베타 재귀 호출 시작 (상대방 턴으로 넘김 -> isMaximizing = false)
            // 재귀 호출할 때 복사본 룰 2개도 같이 계속 토스!
            int score = Minimax(grid, maxDepth - 1, int.MinValue, int.MaxValue, false, aiRules, playerRules);

            grid[move.x, move.y] = 0; // 가상으로 둔 수 원상복구 (백트래킹)

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }
        }
        return bestMove;
    }

    // =========================================================
    // 3. 미니맥스 + 알파베타 가지치기 코어 로직
    // =========================================================
    private int Minimax(int[,] grid, int depth, int alpha, int beta, bool isMaximizing, RuleSettings aiRules, RuleSettings playerRules)
    {
        // 1. 현재 판의 점수를 먼저 확인합니다.
        int currentScore = EvaluateBoard(grid, aiRules.winCondition, playerRules.winCondition);

        // 2. 바닥에 도달했거나, '누군가 승리조건(5목)을 달성했다면(점수가 5천만 점 이상 차이 난다면)'
        // 즉시 그 점수를 리턴하고 탐색 종료
        if (Mathf.Abs(currentScore) >= 50000000)
        {
            // 이기는 거면 최대한 빨리(depth가 클 때) 이길수록 점수를 더 주고,
            // 지는 거면 최대한 늦게(depth가 작을 때) 질수록 점수를 덜 깎음.
            // 이렇게 하면 AI가 질 것 같아도 끝까지 한쪽을 막으면서 발악(?)함
            if (currentScore > 0) return currentScore + depth * 1000; // 내가 이김
            else return currentScore - depth * 1000;                  // 내가 짐
        }

        if (depth == 0) return currentScore;

        // 지금 턴이 누구 턴인지에 따라 다른 색상 넘겨줌
        int currentTurnColor = isMaximizing ? aiColor : playerColor;

        // 지금 누구 턴인지에 따라 적용할 룰 복사본 선택
        RuleSettings currentTurnRules = isMaximizing ? aiRules : playerRules;

        List<Vector2Int> candidateMoves = GetCandidateMoves(grid, currentTurnColor, currentTurnRules);

        if (isMaximizing) // AI 턴
        {
            int maxEval = int.MinValue;
            foreach (Vector2Int move in candidateMoves)
            {
                grid[move.x, move.y] = aiColor;
                int eval = Minimax(grid, depth - 1, alpha, beta, false, aiRules, playerRules);
                grid[move.x, move.y] = 0;

                maxEval = Mathf.Max(maxEval, eval);
                alpha = Mathf.Max(alpha, eval);

                if (beta <= alpha) break; // 가지치기 
            }
            return maxEval;
        }
        else // 플레이어 턴
        {
            int minEval = int.MaxValue;
            foreach (Vector2Int move in candidateMoves)
            {
                grid[move.x, move.y] = playerColor;
                int eval = Minimax(grid, depth - 1, alpha, beta, true, aiRules, playerRules);
                grid[move.x, move.y] = 0;

                minEval = Mathf.Min(minEval, eval);
                beta = Mathf.Min(beta, eval);

                if (beta <= alpha) break; // 가지치기 
            }
            return minEval;
        }
    }

    // =========================================================
    // 4. 후보군 최적화 (돌이 있는 곳 주변 반경 2칸만 탐색!)
    // =========================================================
    private List<Vector2Int> GetCandidateMoves(int[,] grid, int turnColor, RuleSettings snapshotRules)
    {
        List<Vector2Int> moves = new List<Vector2Int>();
        int radius = 2;

        // 이 색깔(턴)에 금수가 아예 없으면 굳이 딥하게 검사 안 함 (최적화)
        bool hasForbidden = snapshotRules.ban33 || snapshotRules.ban44 || snapshotRules.banOverline;

        for (int y = 0; y < boardSize; y++)
        {
            for (int x = 0; x < boardSize; x++)
            {
                if (grid[x, y] == 0 && HasNeighbor(grid, x, y, radius))
                {
                    // 흑이든 백이든(turnColor) 현재 룰 매니저 기준 금수라면 후보에서 제외
                    // (silent를 true로 줘서 AI가 수천 번 상상할 때 로그창 도배되는 걸 막습니다)
               
                    // + 원본 ruleManager의 IsForbiddenMove가 아니라, 
                    // 코어 엔진(CheckIsForbidden)에 직접 '복사본 룰'을 넣어서 검사
                    if (hasForbidden && ruleManager != null &&
                        ruleManager.CheckIsForbidden(x, y, turnColor, grid, boardSize, snapshotRules, 0, true))
                    {
                        continue; // 금수니까 패스!
                    }

                    moves.Add(new Vector2Int(x, y));
                }
            }
        }
        return moves;
    }

    // 팁: Minimax 함수 안에서 GetCandidateMoves를 호출할 때 
    // GetCandidateMoves(grid, isMaximizing ? aiColor : playerColor) 처럼 
    // 지금 시뮬레이션 중인 색상을 넘겨주시면 됩니다!

    private bool HasNeighbor(int[,] grid, int x, int y, int radius)
    {
        int size = grid.GetLength(0);
        int startX = Mathf.Max(0, x - radius);
        int endX = Mathf.Min(size - 1, x + radius);
        int startY = Mathf.Max(0, y - radius);
        int endY = Mathf.Min(size - 1, y + radius);

        for (int i = startX; i <= endX; i++)
        {
            for (int j = startY; j <= endY; j++)
            {
                if (grid[i, j] != 0) return true;
            }
        }
        return false;
    }

    // =========================================================
    // 5. 휴리스틱 평가 (누가누가 유리한가?)
    // =========================================================
    private int EvaluateBoard(int[,] grid, int aiWinCond, int playerWinCond)
    {
        // 원본 안 쳐다보고 인자로 넘어온 승리조건(스냅샷) 바로 사용
        int aiScore = EvaluateForColor(grid, aiColor, aiWinCond);
        int playerScore = EvaluateForColor(grid, playerColor, playerWinCond);

        // [절대 원칙 1] 내가 5목을 완성했다면? 
        // 상대방 점수 빼지 말고 무조건 1억 점 리턴 (바로 승리 꽂아버림)
        if (aiScore >= 100000000) return 100000000;

        // [절대 원칙 2] 상대방이 5목을 완성했다면?
        // 내 점수 무시하고 무조건 -1억 점 리턴! (절망 회로)
        if (playerScore >= 100000000) return -100000000;

        // 위처럼 승부가 안 났을 때만, 상대방의 공격 형태에 1.5배 가중치를 줘서 뺄셈
        return aiScore - (int)(playerScore * 1.5f);
    }

    private int EvaluateForColor(int[,] grid, int color, int winCond)
    {
        int size = grid.GetLength(0);
        int totalScore = 0;

        // 4방향 검사 (가로, 세로, 우하 대각, 우상 대각)
        int[] dx = { 1, 0, 1, 1 };
        int[] dy = { 0, 1, 1, -1 };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (grid[x, y] != color) continue;

                for (int dir = 0; dir < 4; dir++)
                {
                    int consecutive = 1;
                    int blocks = 0; // 양 끝이 막혔는지 체크

                    // 역방향 체크 (내 뒤가 막혔는가?)
                    int prevX = x - dx[dir];
                    int prevY = y - dy[dir];
                    if (prevX < 0 || prevX >= size || prevY < 0 || prevY >= size || (grid[prevX, prevY] != 0 && grid[prevX, prevY] != color))
                        blocks++;

                    // 정방향 체크 (돌이 몇 개 이어졌는가?)
                    int curX = x;
                    int curY = y;
                    while (true)
                    {
                        curX += dx[dir];
                        curY += dy[dir];

                        if (curX < 0 || curX >= size || curY < 0 || curY >= size)
                        {
                            blocks++; // 벽에 막힘
                            break;
                        }
                        else if (grid[curX, curY] == color)
                        {
                            consecutive++;
                        }
                        else if (grid[curX, curY] == 0)
                        {
                            break; // 뚫림
                        }
                        else
                        {
                            blocks++; // 상대 돌에 막힘
                            break;
                        }
                    }

                    // 중복 계산을 피하기 위해, 이어진 돌의 '시작점'일 때만 점수 부여
                    if (prevX >= 0 && prevX < size && prevY >= 0 && prevY < size && grid[prevX, prevY] == color)
                        continue;

                    totalScore += GetShapeScore(consecutive, blocks, winCond);
                }
            }
        }
        return totalScore;
    }

    // 돌이 이어진 개수와 막힌 여부에 따라 점수 반환
    private int GetShapeScore(int consecutive, int blocks, int winCond)
    {
        // 양쪽이 막혔고 승리 조건에 도달하지 못했다면 죽은 돌
        if (blocks == 2 && consecutive < winCond) return 0;

        // 1. 승리 조건 달성! (5목이든 6목이든 1억 점)
        if (consecutive >= winCond) return 100000000;

        // 2. 승리 직전 단계 (예: 5목 룰의 '4목', 6목 룰의 '5목')
        if (consecutive == winCond - 1)
        {
            if (blocks == 0) return 100000; // 열린 상태 (무조건 이김)
            if (blocks == 1) return 10000;  // 닫힌 상태 (공격/방어용 한 수)
        }

        // 3. 승리 2단계 전 (예: 5목 룰의 '3목', 6목 룰의 '4목')
        if (consecutive == winCond - 2)
        {
            if (blocks == 0) return 1000; // 열린 3
            if (blocks == 1) return 100;  // 닫힌 3
        }

        // 4. 승리 3단계 전 (예: 5목 룰의 '2목')
        if (consecutive == winCond - 3)
        {
            if (blocks == 0) return 100;
            if (blocks == 1) return 10;
        }

        return 1; // 기본 돌 하나
    }

}

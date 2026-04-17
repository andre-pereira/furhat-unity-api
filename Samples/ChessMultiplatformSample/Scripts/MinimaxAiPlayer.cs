using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

[Serializable]
public class ThinkTimeRange
{
    [Min(0)] public int minMilliseconds = 300;
    [Min(0)] public int maxMilliseconds = 700;

    public int GetRandomMilliseconds()
    {
        int clampedMin = Mathf.Min(minMilliseconds, maxMilliseconds);
        int clampedMax = Mathf.Max(minMilliseconds, maxMilliseconds);
        return UnityEngine.Random.Range(clampedMin, clampedMax + 1);
    }
}

[Serializable]
public class MinimaxAiSettings
{
    [Header("Search")]
    [Min(1)] public int maxDepth = 5;
    [Min(50)] public int maxThinkingTimeMilliseconds = 2000;
    [Min(0)] public int quiescenceMaxDepth = 6;
    [Min(0)] public int transpositionTableSize = 20000;

    [Header("Search Tuning")]
    public bool useAspirationWindows = true;
    [Min(8)] public int aspirationWindowSize = 45;

    [Header("Position Classification")]
    [Min(0)] public int openingPieceThreshold = 24;
    [Min(0)] public int endgamePieceThreshold = 10;
    [Min(0)] public int tacticalMoveCountThreshold = 28;

    [Header("Human-like Delays")]
    public ThinkTimeRange forcedMoveDelay = new ThinkTimeRange { minMilliseconds = 200, maxMilliseconds = 400 };
    public ThinkTimeRange openingDelay = new ThinkTimeRange { minMilliseconds = 250, maxMilliseconds = 500 };
    public ThinkTimeRange normalDelay = new ThinkTimeRange { minMilliseconds = 300, maxMilliseconds = 700 };
    public ThinkTimeRange tacticalDelay = new ThinkTimeRange { minMilliseconds = 700, maxMilliseconds = 1500 };
    public ThinkTimeRange endgameDelay = new ThinkTimeRange { minMilliseconds = 400, maxMilliseconds = 900 };
}

public readonly struct MinimaxSearchResult
{
    public Move? BestMove { get; }
    public int CompletedDepth { get; }
    public int SearchElapsedMilliseconds { get; }
    public int TargetThinkTimeMilliseconds { get; }
    public bool TimedOut { get; }
    public bool UsedForcedMoveDelay { get; }

    public MinimaxSearchResult(
        Move? bestMove,
        int completedDepth,
        int searchElapsedMilliseconds,
        int targetThinkTimeMilliseconds,
        bool timedOut,
        bool usedForcedMoveDelay)
    {
        BestMove = bestMove;
        CompletedDepth = completedDepth;
        SearchElapsedMilliseconds = searchElapsedMilliseconds;
        TargetThinkTimeMilliseconds = targetThinkTimeMilliseconds;
        TimedOut = timedOut;
        UsedForcedMoveDelay = usedForcedMoveDelay;
    }
}

internal enum TranspositionNodeType
{
    Exact,
    LowerBound,
    UpperBound
}

internal struct TranspositionEntry
{
    public string Key;
    public int Depth;
    public int Score;
    public TranspositionNodeType NodeType;
    public Move BestMove;
}

public class MinimaxAiPlayer
{
    private const int CheckmateScore = 100000;
    private const int InfinityScore = 1000000;
    private const int MaxSearchPly = 64;
    private const int PawnValue = 100;
    private const int KnightValue = 320;
    private const int BishopValue = 330;
    private const int RookValue = 500;
    private const int QueenValue = 900;
    private const int KingValue = 20000;
    private const int EndgamePhaseLimit = 24;

    private readonly Dictionary<string, TranspositionEntry> transpositionTable = new();
    private readonly Move?[,] killerMoves = new Move?[MaxSearchPly, 2];
    private readonly int[,] historyHeuristic = new int[64, 64];

    private int currentRootPhase;

    public MinimaxSearchResult ChooseMove(BoardState board, MoveGenerator moveGenerator, PieceSide side, MinimaxAiSettings settings)
    {
        PrepareTranspositionTable(settings);
        ResetSearchHeuristics();
        currentRootPhase = GetGamePhase(board);

        var legalMoves = moveGenerator.GenerateLegalMoves(board, side);
        if (legalMoves.Count == 0)
            return new MinimaxSearchResult(null, 0, 0, 0, false, false);

        if (legalMoves.Count == 1)
        {
            return new MinimaxSearchResult(
                legalMoves[0],
                0,
                0,
                settings.forcedMoveDelay.GetRandomMilliseconds(),
                false,
                true);
        }

        var stopwatch = Stopwatch.StartNew();
        var orderedRootMoves = OrderMoves(board, legalMoves, side, 0);

        Move bestMove = orderedRootMoves[0];
        int completedDepth = 0;
        bool timedOut = false;
        bool hasPreviousScore = false;
        int previousScore = 0;

        for (int depth = 1; depth <= Mathf.Max(1, settings.maxDepth); depth++)
        {
            if (HasTimedOut(stopwatch, settings))
            {
                timedOut = true;
                break;
            }

            bool depthCompleted = false;
            int aspirationWindow = Mathf.Max(8, settings.aspirationWindowSize);
            int searchAlpha = -InfinityScore;
            int searchBeta = InfinityScore;

            if (settings.useAspirationWindows && hasPreviousScore && depth >= 2)
            {
                searchAlpha = Mathf.Max(-InfinityScore, previousScore - aspirationWindow);
                searchBeta = Mathf.Min(InfinityScore, previousScore + aspirationWindow);
            }

            while (!depthCompleted)
            {
                bool depthTimedOut = false;
                int alpha = searchAlpha;
                int beta = searchBeta;
                int bestScoreAtDepth = -InfinityScore;
                Move bestMoveAtDepth = bestMove;

                foreach (var move in orderedRootMoves)
                {
                    if (HasTimedOut(stopwatch, settings))
                    {
                        depthTimedOut = true;
                        break;
                    }

                    var simulatedBoard = board.Clone();
                    simulatedBoard.ApplyMove(move);

                    int score = -Negamax(
                        simulatedBoard,
                        depth - 1,
                        -beta,
                        -alpha,
                        OpponentOf(side),
                        side,
                        1,
                        moveGenerator,
                        stopwatch,
                        settings,
                        ref depthTimedOut);

                    if (depthTimedOut)
                        break;

                    if (score > bestScoreAtDepth)
                    {
                        bestScoreAtDepth = score;
                        bestMoveAtDepth = move;
                    }

                    if (score > alpha)
                        alpha = score;
                }

                if (depthTimedOut)
                {
                    timedOut = true;
                    break;
                }

                bool failedAspiration = settings.useAspirationWindows && hasPreviousScore && depth >= 2 &&
                    (bestScoreAtDepth <= searchAlpha || bestScoreAtDepth >= searchBeta);

                if (failedAspiration)
                {
                    searchAlpha = -InfinityScore;
                    searchBeta = InfinityScore;
                    continue;
                }

                bestMove = bestMoveAtDepth;
                previousScore = bestScoreAtDepth;
                hasPreviousScore = true;
                completedDepth = depth;
                orderedRootMoves = ReorderPrincipalVariationFirst(orderedRootMoves, bestMoveAtDepth);
                depthCompleted = true;
            }

            if (timedOut)
                break;
        }

        stopwatch.Stop();

        return new MinimaxSearchResult(
            bestMove,
            completedDepth,
            (int)stopwatch.ElapsedMilliseconds,
            DetermineTargetThinkTime(board, moveGenerator, side, legalMoves, settings),
            timedOut,
            false);
    }

    private int Negamax(
        BoardState board,
        int depth,
        int alpha,
        int beta,
        PieceSide sideToMove,
        PieceSide maximizingSide,
        int ply,
        MoveGenerator moveGenerator,
        Stopwatch stopwatch,
        MinimaxAiSettings settings,
        ref bool timedOut)
    {
        if (HasTimedOut(stopwatch, settings))
        {
            timedOut = true;
            return 0;
        }

        int originalAlpha = alpha;
        string positionKey = GetBoardKey(board, sideToMove);
        if (TryProbeTransposition(positionKey, depth, ref alpha, ref beta, out int cachedScore))
            return cachedScore;

        var legalMoves = moveGenerator.GenerateLegalMoves(board, sideToMove);
        if (legalMoves.Count == 0)
        {
            if (moveGenerator.IsKingInCheck(board, sideToMove))
                return -CheckmateScore + ply;

            return 0;
        }

        if (depth <= 0)
            return Quiescence(board, alpha, beta, sideToMove, maximizingSide, moveGenerator, stopwatch, settings, 0, ref timedOut);

        var orderedMoves = OrderMoves(board, legalMoves, sideToMove, ply);
        int bestScore = -InfinityScore;
        Move bestMove = orderedMoves[0];

        foreach (var move in orderedMoves)
        {
            if (HasTimedOut(stopwatch, settings))
            {
                timedOut = true;
                return 0;
            }

            var simulatedBoard = board.Clone();
            simulatedBoard.ApplyMove(move);

            int score = -Negamax(
                simulatedBoard,
                depth - 1,
                -beta,
                -alpha,
                OpponentOf(sideToMove),
                maximizingSide,
                ply + 1,
                moveGenerator,
                stopwatch,
                settings,
                ref timedOut);

            if (timedOut)
                return 0;

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }

            if (score > alpha)
                alpha = score;

            if (alpha >= beta)
            {
                if (!move.IsCapture)
                {
                    StoreKillerMove(move, ply);
                    IncreaseHistory(move, depth);
                }

                break;
            }
        }

        StoreTransposition(positionKey, depth, bestScore, originalAlpha, beta, bestMove);
        return bestScore;
    }

    private int Quiescence(
        BoardState board,
        int alpha,
        int beta,
        PieceSide sideToMove,
        PieceSide maximizingSide,
        MoveGenerator moveGenerator,
        Stopwatch stopwatch,
        MinimaxAiSettings settings,
        int quiescenceDepth,
        ref bool timedOut)
    {
        if (HasTimedOut(stopwatch, settings))
        {
            timedOut = true;
            return 0;
        }

        int standPat = sideToMove == maximizingSide
            ? Evaluate(board, moveGenerator, maximizingSide)
            : -Evaluate(board, moveGenerator, maximizingSide);
        if (standPat >= beta)
            return beta;

        if (standPat > alpha)
            alpha = standPat;

        if (quiescenceDepth >= settings.quiescenceMaxDepth)
            return standPat;

        var legalMoves = moveGenerator.GenerateLegalMoves(board, sideToMove);
        var forcingMoves = FilterQuiescenceMoves(legalMoves);
        var orderedMoves = OrderMoves(board, forcingMoves, sideToMove, Mathf.Min(MaxSearchPly - 1, quiescenceDepth));

        foreach (var move in orderedMoves)
        {
            if (HasTimedOut(stopwatch, settings))
            {
                timedOut = true;
                return 0;
            }

            var simulatedBoard = board.Clone();
            simulatedBoard.ApplyMove(move);

            int score = -Quiescence(
                simulatedBoard,
                -beta,
                -alpha,
                OpponentOf(sideToMove),
                maximizingSide,
                moveGenerator,
                stopwatch,
                settings,
                quiescenceDepth + 1,
                ref timedOut);

            if (timedOut)
                return 0;

            if (score >= beta)
                return beta;

            if (score > alpha)
                alpha = score;
        }

        return alpha;
    }

    private int Evaluate(BoardState board, MoveGenerator moveGenerator, PieceSide maximizingSide)
    {
        int phase = GetGamePhase(board);
        int middlegameScore = EvaluateForPhase(board, moveGenerator, maximizingSide, false);
        int endgameScore = EvaluateForPhase(board, moveGenerator, maximizingSide, true);
        return ((middlegameScore * phase) + (endgameScore * (EndgamePhaseLimit - phase))) / EndgamePhaseLimit;
    }

    private int EvaluateForPhase(BoardState board, MoveGenerator moveGenerator, PieceSide maximizingSide, bool endgame)
    {
        int score = 0;

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                var coord = new PieceCoord(x, y);
                var piece = board.GetPiece(coord);
                if (!piece.HasValue)
                    continue;

                int pieceScore = GetMaterialValue(piece.Value.Type);
                pieceScore += GetPieceSquareValue(piece.Value, coord, endgame);
                pieceScore += GetDevelopmentScore(piece.Value, coord, endgame);
                pieceScore += GetPawnStructureContribution(board, piece.Value, coord, endgame);
                pieceScore += GetPassedPawnBonus(board, piece.Value, coord, endgame);
                pieceScore += GetRookOrQueenFileBonus(board, piece.Value, coord);
                pieceScore += GetPieceSafetyScore(board, moveGenerator, piece.Value, coord);

                score += piece.Value.Side == maximizingSide ? pieceScore : -pieceScore;
            }
        }

        score += GetBishopPairContribution(board, maximizingSide);
        score -= GetBishopPairContribution(board, OpponentOf(maximizingSide));

        if (!endgame)
        {
            score += GetCenterControlScore(board, moveGenerator, maximizingSide);
            score -= GetCenterControlScore(board, moveGenerator, OpponentOf(maximizingSide));
        }

        score += GetKingSafetyScore(board, moveGenerator, maximizingSide, endgame);
        score -= GetKingSafetyScore(board, moveGenerator, OpponentOf(maximizingSide), endgame);

        if (moveGenerator.IsKingInCheck(board, maximizingSide))
            score -= 60;

        if (moveGenerator.IsKingInCheck(board, OpponentOf(maximizingSide)))
            score += 60;

        return score;
    }

    private List<Move> OrderMoves(BoardState board, List<Move> moves, PieceSide side, int ply)
    {
        var scoredMoves = new List<(Move move, int score)>(moves.Count);
        string positionKey = GetBoardKey(board, side);
        bool hasTtMove = transpositionTable.TryGetValue(positionKey, out TranspositionEntry ttEntry);
        int localPly = Mathf.Clamp(ply, 0, MaxSearchPly - 1);

        foreach (var move in moves)
        {
            int score = 0;
            var movingPiece = board.GetPiece(move.From);
            var capturedPiece = board.GetPiece(move.GetCaptureSquare());

            if (hasTtMove && MovesEqual(move, ttEntry.BestMove))
                score += 5000;

            if (move.IsCapture && movingPiece.HasValue)
            {
                int victimValue = capturedPiece.HasValue ? GetMaterialValue(capturedPiece.Value.Type) : 0;
                int attackerValue = GetMaterialValue(movingPiece.Value.Type);
                score += 2000 + (victimValue * 10) - attackerValue;
            }

            if (move.IsCastleKingSide || move.IsCastleQueenSide)
                score += 500;

            if (movingPiece.HasValue)
            {
                score += GetMoveStyleScore(board, move, movingPiece.Value, currentRootPhase < 8);
                score += GetPromotionScore(move, movingPiece.Value);
            }

            score += GetKillerScore(move, localPly);
            score += GetHistoryScore(move);

            score += GetCenterPushBonus(move.To);

            scoredMoves.Add((move, score));
        }

        scoredMoves.Sort((a, b) => b.score.CompareTo(a.score));
        var orderedMoves = new List<Move>(scoredMoves.Count);
        foreach (var entry in scoredMoves)
            orderedMoves.Add(entry.move);

        return orderedMoves;
    }

    private List<Move> FilterQuiescenceMoves(List<Move> legalMoves)
    {
        var forcingMoves = new List<Move>();
        foreach (var move in legalMoves)
        {
            if (move.IsCapture)
                forcingMoves.Add(move);
        }

        return forcingMoves;
    }

    private List<Move> ReorderPrincipalVariationFirst(List<Move> moves, Move principalVariationMove)
    {
        int bestIndex = -1;
        for (int i = 0; i < moves.Count; i++)
        {
            if (MovesEqual(moves[i], principalVariationMove))
            {
                bestIndex = i;
                break;
            }
        }

        if (bestIndex <= 0)
            return moves;

        var reordered = new List<Move>(moves.Count) { moves[bestIndex] };
        for (int i = 0; i < moves.Count; i++)
        {
            if (i != bestIndex)
                reordered.Add(moves[i]);
        }

        return reordered;
    }

    private int DetermineTargetThinkTime(BoardState board, MoveGenerator moveGenerator, PieceSide side, List<Move> legalMoves, MinimaxAiSettings settings)
    {
        if (legalMoves.Count <= 1)
            return settings.forcedMoveDelay.GetRandomMilliseconds();

        bool isTacticalPosition = moveGenerator.IsKingInCheck(board, side) ||
            moveGenerator.IsKingInCheck(board, OpponentOf(side)) ||
            HasTacticalCandidate(board, moveGenerator, legalMoves, side) ||
            legalMoves.Count >= settings.tacticalMoveCountThreshold;

        if (isTacticalPosition)
            return settings.tacticalDelay.GetRandomMilliseconds();

        int pieceCount = CountPieces(board);
        if (pieceCount >= settings.openingPieceThreshold)
            return settings.openingDelay.GetRandomMilliseconds();

        if (pieceCount <= settings.endgamePieceThreshold)
            return settings.endgameDelay.GetRandomMilliseconds();

        return settings.normalDelay.GetRandomMilliseconds();
    }

    private bool HasTacticalCandidate(BoardState board, MoveGenerator moveGenerator, List<Move> legalMoves, PieceSide side)
    {
        foreach (var move in legalMoves)
        {
            if (move.IsCapture)
                return true;

            var simulatedBoard = board.Clone();
            simulatedBoard.ApplyMove(move);
            if (moveGenerator.IsKingInCheck(simulatedBoard, OpponentOf(side)))
                return true;
        }

        return false;
    }

    private void PrepareTranspositionTable(MinimaxAiSettings settings)
    {
        if (settings.transpositionTableSize <= 0)
        {
            transpositionTable.Clear();
            return;
        }

        if (transpositionTable.Count > settings.transpositionTableSize)
            transpositionTable.Clear();
    }

    private bool TryProbeTransposition(string key, int depth, ref int alpha, ref int beta, out int score)
    {
        score = 0;
        if (!transpositionTable.TryGetValue(key, out TranspositionEntry entry))
            return false;

        if (entry.Depth < depth)
            return false;

        score = entry.Score;
        switch (entry.NodeType)
        {
            case TranspositionNodeType.Exact:
                return true;
            case TranspositionNodeType.LowerBound:
                alpha = Mathf.Max(alpha, entry.Score);
                break;
            case TranspositionNodeType.UpperBound:
                beta = Mathf.Min(beta, entry.Score);
                break;
        }

        return alpha >= beta;
    }

    private void StoreTransposition(string key, int depth, int score, int originalAlpha, int beta, Move bestMove)
    {
        var nodeType = TranspositionNodeType.Exact;
        if (score <= originalAlpha)
            nodeType = TranspositionNodeType.UpperBound;
        else if (score >= beta)
            nodeType = TranspositionNodeType.LowerBound;

        transpositionTable[key] = new TranspositionEntry
        {
            Key = key,
            Depth = depth,
            Score = score,
            NodeType = nodeType,
            BestMove = bestMove
        };
    }

    private bool HasTimedOut(Stopwatch stopwatch, MinimaxAiSettings settings)
    {
        return stopwatch.ElapsedMilliseconds >= settings.maxThinkingTimeMilliseconds;
    }

    private void ResetSearchHeuristics()
    {
        for (int ply = 0; ply < MaxSearchPly; ply++)
        {
            killerMoves[ply, 0] = null;
            killerMoves[ply, 1] = null;
        }

        Array.Clear(historyHeuristic, 0, historyHeuristic.Length);
    }

    private void StoreKillerMove(Move move, int ply)
    {
        int clampedPly = Mathf.Clamp(ply, 0, MaxSearchPly - 1);
        var primary = killerMoves[clampedPly, 0];
        if (primary.HasValue && MovesEqual(primary.Value, move))
            return;

        killerMoves[clampedPly, 1] = primary;
        killerMoves[clampedPly, 0] = move;
    }

    private int GetKillerScore(Move move, int ply)
    {
        if (move.IsCapture)
            return 0;

        int clampedPly = Mathf.Clamp(ply, 0, MaxSearchPly - 1);
        var primary = killerMoves[clampedPly, 0];
        if (primary.HasValue && MovesEqual(primary.Value, move))
            return 900;

        var secondary = killerMoves[clampedPly, 1];
        if (secondary.HasValue && MovesEqual(secondary.Value, move))
            return 700;

        return 0;
    }

    private void IncreaseHistory(Move move, int depth)
    {
        if (move.IsCapture)
            return;

        int fromIndex = GetSquareIndex(move.From);
        int toIndex = GetSquareIndex(move.To);
        historyHeuristic[fromIndex, toIndex] += depth * depth;
    }

    private int GetHistoryScore(Move move)
    {
        if (move.IsCapture)
            return 0;

        int fromIndex = GetSquareIndex(move.From);
        int toIndex = GetSquareIndex(move.To);
        return Mathf.Min(600, historyHeuristic[fromIndex, toIndex]);
    }

    private int GetSquareIndex(PieceCoord coord)
    {
        return coord.y * 8 + coord.x;
    }

    private int GetPromotionScore(Move move, Piece movingPiece)
    {
        if (movingPiece.Type != PieceType.Pawn)
            return 0;

        bool promotes = (movingPiece.Side == PieceSide.White && move.To.y == 7) ||
            (movingPiece.Side == PieceSide.Black && move.To.y == 0);

        return promotes ? 1800 : 0;
    }

    private int GetCenterPushBonus(PieceCoord to)
    {
        int dx = Mathf.Abs(to.x - 3) + Mathf.Abs(to.x - 4);
        int dy = Mathf.Abs(to.y - 3) + Mathf.Abs(to.y - 4);
        return Mathf.Max(0, 12 - (dx + dy) * 3);
    }

    private int CountPieces(BoardState board)
    {
        int count = 0;
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                if (board.GetPiece(new PieceCoord(x, y)).HasValue)
                    count++;
            }
        }

        return count;
    }

    private int GetGamePhase(BoardState board)
    {
        int phase = 0;
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                var piece = board.GetPiece(new PieceCoord(x, y));
                if (!piece.HasValue)
                    continue;

                phase += piece.Value.Type switch
                {
                    PieceType.Queen => 4,
                    PieceType.Rook => 2,
                    PieceType.Bishop => 1,
                    PieceType.Knight => 1,
                    _ => 0
                };
            }
        }

        return Mathf.Clamp(phase, 0, EndgamePhaseLimit);
    }

    private int GetMaterialValue(PieceType type)
    {
        return type switch
        {
            PieceType.Pawn => PawnValue,
            PieceType.Knight => KnightValue,
            PieceType.Bishop => BishopValue,
            PieceType.Rook => RookValue,
            PieceType.Queen => QueenValue,
            PieceType.King => KingValue,
            _ => 0
        };
    }

    private int GetPieceSquareValue(Piece piece, PieceCoord coord, bool endgame)
    {
        int rankFromOwnSide = piece.Side == PieceSide.White ? coord.y : 7 - coord.y;
        int fileCenterDistance = Mathf.Abs(coord.x - 3) + Mathf.Abs(coord.x - 4);
        int rankCenterDistance = Mathf.Abs(coord.y - 3) + Mathf.Abs(coord.y - 4);
        int centerDistance = fileCenterDistance + rankCenterDistance;

        return piece.Type switch
        {
            PieceType.Pawn => rankFromOwnSide * 8 - Mathf.Abs(coord.x - 3) * 2,
            PieceType.Knight => 26 - centerDistance * 4,
            PieceType.Bishop => 20 - centerDistance * 3,
            PieceType.Rook => rankFromOwnSide * 2,
            PieceType.Queen => endgame ? 12 - centerDistance * 2 : 4 - centerDistance,
            PieceType.King => endgame ? 24 - centerDistance * 3 : -rankFromOwnSide * 8,
            _ => 0
        };
    }

    private int GetDevelopmentScore(Piece piece, PieceCoord coord, bool endgame)
    {
        if (endgame)
            return 0;

        int homeRank = piece.Side == PieceSide.White ? 0 : 7;
        int pawnRank = piece.Side == PieceSide.White ? 1 : 6;

        return piece.Type switch
        {
            PieceType.Knight => coord.y == homeRank ? -30 : 18,
            PieceType.Bishop => coord.y == homeRank ? -26 : 16,
            PieceType.Queen => coord.y == homeRank && coord.x == 3 ? 4 : -20,
            PieceType.Rook => coord.y == homeRank ? 4 : 10,
            PieceType.King => coord.y == homeRank && (coord.x == 6 || coord.x == 2) ? 70 : (coord.y == homeRank && coord.x == 4 ? -25 : -85),
            PieceType.Pawn => coord.y == pawnRank ? 0 : 5,
            _ => 0
        };
    }

    private int GetPieceSafetyScore(BoardState board, MoveGenerator moveGenerator, Piece piece, PieceCoord coord)
    {
        if (piece.Type == PieceType.King)
            return 0;

        PieceSide opponent = OpponentOf(piece.Side);
        bool attacked = moveGenerator.IsSquareAttacked(board, coord, opponent);
        bool defended = moveGenerator.IsSquareAttacked(board, coord, piece.Side);

        if (!attacked)
            return defended ? 6 : 0;

        int value = GetMaterialValue(piece.Type);
        if (!defended)
            return -value;

        return -Mathf.Max(15, value / 6);
    }

    private int GetKingSafetyScore(BoardState board, MoveGenerator moveGenerator, PieceSide side, bool endgame)
    {
        var kingSquare = board.FindKing(side);
        if (!kingSquare.HasValue)
            return 0;

        if (endgame)
            return 0;

        int score = 0;
        int homeRank = side == PieceSide.White ? 0 : 7;
        int pawnShieldRank = side == PieceSide.White ? 1 : 6;
        var king = kingSquare.Value;

        if ((king.x == 6 || king.x == 2) && king.y == homeRank)
            score += 70;
        else if (king.x == 4 && king.y == homeRank)
            score -= 35;
        else
            score -= 100;

        for (int fileOffset = -1; fileOffset <= 1; fileOffset++)
        {
            int file = king.x + fileOffset;
            if (file < 0 || file > 7)
                continue;

            var shieldPawn = board.GetPiece(new PieceCoord(file, pawnShieldRank));
            if (shieldPawn.HasValue && shieldPawn.Value.Side == side && shieldPawn.Value.Type == PieceType.Pawn)
                score += 12;
            else
                score -= 15;
        }

        if (moveGenerator.IsSquareAttacked(board, king, OpponentOf(side)))
            score -= 40;

        return score;
    }

    private int GetCenterControlScore(BoardState board, MoveGenerator moveGenerator, PieceSide side)
    {
        int score = 0;
        PieceCoord[] centralSquares =
        {
            new PieceCoord(3, 3), new PieceCoord(4, 3),
            new PieceCoord(3, 4), new PieceCoord(4, 4)
        };

        foreach (var square in centralSquares)
        {
            if (moveGenerator.IsSquareAttacked(board, square, side))
                score += 6;
        }

        return score;
    }

    private int GetPawnStructureContribution(BoardState board, Piece piece, PieceCoord coord, bool endgame)
    {
        if (piece.Type != PieceType.Pawn)
            return 0;

        int score = 0;
        if (IsIsolatedPawn(board, piece.Side, coord))
            score -= endgame ? 10 : 15;

        int doubledCount = CountPawnsOnFile(board, piece.Side, coord.x);
        if (doubledCount > 1)
            score -= (doubledCount - 1) * 12;

        if (HasFriendlyPawnOnAdjacentFile(board, piece.Side, coord))
            score += 4;

        return score;
    }

    private int GetPassedPawnBonus(BoardState board, Piece piece, PieceCoord coord, bool endgame)
    {
        if (piece.Type != PieceType.Pawn || !IsPassedPawn(board, piece.Side, coord))
            return 0;

        int advancement = piece.Side == PieceSide.White ? coord.y : 7 - coord.y;
        int bonus = 20 + advancement * 10;
        return endgame ? bonus + 20 : bonus;
    }

    private int GetRookOrQueenFileBonus(BoardState board, Piece piece, PieceCoord coord)
    {
        if (piece.Type != PieceType.Rook && piece.Type != PieceType.Queen)
            return 0;

        bool ownPawnOnFile = HasPawnOnFile(board, piece.Side, coord.x);
        bool enemyPawnOnFile = HasPawnOnFile(board, OpponentOf(piece.Side), coord.x);

        if (!ownPawnOnFile && !enemyPawnOnFile)
            return piece.Type == PieceType.Rook ? 22 : 12;

        if (!ownPawnOnFile)
            return piece.Type == PieceType.Rook ? 12 : 6;

        return 0;
    }

    private int GetBishopPairContribution(BoardState board, PieceSide side)
    {
        int bishopCount = 0;
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                var piece = board.GetPiece(new PieceCoord(x, y));
                if (piece.HasValue && piece.Value.Side == side && piece.Value.Type == PieceType.Bishop)
                    bishopCount++;
            }
        }

        return bishopCount >= 2 ? 18 : 0;
    }

    private int GetMoveStyleScore(BoardState board, Move move, Piece movingPiece, bool endgame)
    {
        int score = 0;
        int homeRank = movingPiece.Side == PieceSide.White ? 0 : 7;

        if (!endgame)
        {
            if (movingPiece.Type == PieceType.King && !move.IsCastleKingSide && !move.IsCastleQueenSide)
                score -= 240;

            if ((movingPiece.Type == PieceType.Knight || movingPiece.Type == PieceType.Bishop) && move.From.y == homeRank)
                score += 45;

            if (movingPiece.Type == PieceType.Queen && move.From.y == homeRank && move.From.x == 3)
                score -= 35;
        }

        return score;
    }

    private bool IsIsolatedPawn(BoardState board, PieceSide side, PieceCoord coord)
    {
        for (int adjacentFile = coord.x - 1; adjacentFile <= coord.x + 1; adjacentFile += 2)
        {
            if (adjacentFile < 0 || adjacentFile > 7)
                continue;

            if (HasPawnOnFile(board, side, adjacentFile))
                return false;
        }

        return true;
    }

    private bool HasFriendlyPawnOnAdjacentFile(BoardState board, PieceSide side, PieceCoord coord)
    {
        for (int adjacentFile = coord.x - 1; adjacentFile <= coord.x + 1; adjacentFile += 2)
        {
            if (adjacentFile < 0 || adjacentFile > 7)
                continue;

            if (HasPawnOnFile(board, side, adjacentFile))
                return true;
        }

        return false;
    }

    private int CountPawnsOnFile(BoardState board, PieceSide side, int file)
    {
        int count = 0;
        for (int y = 0; y < 8; y++)
        {
            var piece = board.GetPiece(new PieceCoord(file, y));
            if (piece.HasValue && piece.Value.Side == side && piece.Value.Type == PieceType.Pawn)
                count++;
        }

        return count;
    }

    private bool HasPawnOnFile(BoardState board, PieceSide side, int file)
    {
        return CountPawnsOnFile(board, side, file) > 0;
    }

    private bool IsPassedPawn(BoardState board, PieceSide side, PieceCoord coord)
    {
        int direction = side == PieceSide.White ? 1 : -1;
        for (int file = Mathf.Max(0, coord.x - 1); file <= Mathf.Min(7, coord.x + 1); file++)
        {
            for (int y = coord.y + direction; y >= 0 && y < 8; y += direction)
            {
                var piece = board.GetPiece(new PieceCoord(file, y));
                if (piece.HasValue && piece.Value.Side != side && piece.Value.Type == PieceType.Pawn)
                    return false;
            }
        }

        return true;
    }

    private string GetBoardKey(BoardState board, PieceSide sideToMove)
    {
        char[] buffer = new char[80];
        int index = 0;

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                var piece = board.GetPiece(new PieceCoord(x, y));
                buffer[index++] = EncodePiece(piece);
            }
        }

        buffer[index++] = sideToMove == PieceSide.White ? 'w' : 'b';
        buffer[index++] = board.WhiteCanCastleKingSide ? 'K' : '-';
        buffer[index++] = board.WhiteCanCastleQueenSide ? 'Q' : '-';
        buffer[index++] = board.BlackCanCastleKingSide ? 'k' : '-';
        buffer[index++] = board.BlackCanCastleQueenSide ? 'q' : '-';

        if (board.EnPassantTarget.HasValue)
        {
            buffer[index++] = (char)('a' + board.EnPassantTarget.Value.x);
            buffer[index++] = (char)('1' + board.EnPassantTarget.Value.y);
        }
        else
        {
            buffer[index++] = '-';
            buffer[index++] = '-';
        }

        return new string(buffer, 0, index);
    }

    private char EncodePiece(Piece? piece)
    {
        if (!piece.HasValue)
            return '.';

        char symbol = piece.Value.Type switch
        {
            PieceType.Pawn => 'p',
            PieceType.Knight => 'n',
            PieceType.Bishop => 'b',
            PieceType.Rook => 'r',
            PieceType.Queen => 'q',
            PieceType.King => 'k',
            _ => '.'
        };

        return piece.Value.Side == PieceSide.White ? char.ToUpperInvariant(symbol) : symbol;
    }

    private bool MovesEqual(Move a, Move b)
    {
        return a.From.Equals(b.From) &&
            a.To.Equals(b.To) &&
            a.IsCapture == b.IsCapture &&
            a.IsEnPassant == b.IsEnPassant &&
            a.IsCastleKingSide == b.IsCastleKingSide &&
            a.IsCastleQueenSide == b.IsCastleQueenSide;
    }

    private PieceSide OpponentOf(PieceSide side)
    {
        return side == PieceSide.White ? PieceSide.Black : PieceSide.White;
    }
}

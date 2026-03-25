using System.Collections.Generic;
using UnityEngine;

public class RandomAiPlayer
{
    public Move? ChooseMove(BoardState board, MoveGenerator moveGenerator, PieceSide side)
    {
        List<Move> moves = moveGenerator.GenerateLegalMoves(board, side);
        if (moves.Count == 0)
            return null;

        int index = Random.Range(0, moves.Count);
        return moves[index];
    }
}

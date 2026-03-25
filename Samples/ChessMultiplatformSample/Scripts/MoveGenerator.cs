using System.Collections.Generic;

public class MoveGenerator
{
    public List<Move> GenerateLegalMoves(BoardState board, PieceSide side)
    {
        var moves = new List<Move>();

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                var from = new PieceCoord(x, y);
                var piece = board.GetPiece(from);
                if (!piece.HasValue || piece.Value.Side != side)
                    continue;

                GeneratePseudoMovesForPiece(board, from, piece.Value, moves);
            }
        }

        // For a starter project, we skip king-in-check validation and castling/en passant.
        // This is fine for a first playable prototype with random AI.
        return moves;
    }

    private void GeneratePseudoMovesForPiece(BoardState board, PieceCoord from, Piece piece, List<Move> moves)
    {
        switch (piece.Type)
        {
            case PieceType.Pawn: GeneratePawn(board, from, piece.Side, moves); break;
            case PieceType.Rook: GenerateSliding(board, from, piece.Side, moves, new[] { (1,0), (-1,0), (0,1), (0,-1) }); break;
            case PieceType.Bishop: GenerateSliding(board, from, piece.Side, moves, new[] { (1,1), (1,-1), (-1,1), (-1,-1) }); break;
            case PieceType.Queen: GenerateSliding(board, from, piece.Side, moves, new[] { (1,0), (-1,0), (0,1), (0,-1), (1,1), (1,-1), (-1,1), (-1,-1) }); break;
            case PieceType.Knight: GenerateKnight(board, from, piece.Side, moves); break;
            case PieceType.King: GenerateKing(board, from, piece.Side, moves); break;
        }
    }

    private void GeneratePawn(BoardState board, PieceCoord from, PieceSide side, List<Move> moves)
    {
        int dir = side == PieceSide.White ? 1 : -1;
        int startRow = side == PieceSide.White ? 1 : 6;

        var one = new PieceCoord(from.x, from.y + dir);
        if (BoardState.InBounds(one) && !board.GetPiece(one).HasValue)
        {
            moves.Add(new Move(from, one));

            var two = new PieceCoord(from.x, from.y + 2 * dir);
            if (from.y == startRow && !board.GetPiece(two).HasValue)
            {
                moves.Add(new Move(from, two));
            }
        }

        foreach (int dx in new[] { -1, 1 })
        {
            var capture = new PieceCoord(from.x + dx, from.y + dir);
            if (!BoardState.InBounds(capture)) continue;
            var target = board.GetPiece(capture);
            if (target.HasValue && target.Value.Side != side)
            {
                moves.Add(new Move(from, capture));
            }
        }
    }

    private void GenerateSliding(BoardState board, PieceCoord from, PieceSide side, List<Move> moves, (int dx, int dy)[] dirs)
    {
        foreach (var (dx, dy) in dirs)
        {
            int x = from.x + dx;
            int y = from.y + dy;
            while (BoardState.InBounds(new PieceCoord(x, y)))
            {
                var to = new PieceCoord(x, y);
                var target = board.GetPiece(to);

                if (!target.HasValue)
                {
                    moves.Add(new Move(from, to));
                }
                else
                {
                    if (target.Value.Side != side)
                        moves.Add(new Move(from, to));
                    break;
                }

                x += dx;
                y += dy;
            }
        }
    }

    private void GenerateKnight(BoardState board, PieceCoord from, PieceSide side, List<Move> moves)
    {
        var jumps = new[] {
            (1,2), (2,1), (-1,2), (-2,1),
            (1,-2), (2,-1), (-1,-2), (-2,-1)
        };

        foreach (var (dx, dy) in jumps)
        {
            var to = new PieceCoord(from.x + dx, from.y + dy);
            if (!BoardState.InBounds(to)) continue;
            var target = board.GetPiece(to);
            if (!target.HasValue || target.Value.Side != side)
                moves.Add(new Move(from, to));
        }
    }

    private void GenerateKing(BoardState board, PieceCoord from, PieceSide side, List<Move> moves)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                var to = new PieceCoord(from.x + dx, from.y + dy);
                if (!BoardState.InBounds(to)) continue;
                var target = board.GetPiece(to);
                if (!target.HasValue || target.Value.Side != side)
                    moves.Add(new Move(from, to));
            }
        }
    }
}

using System.Collections.Generic;

public class MoveGenerator
{
    public List<Move> GenerateLegalMoves(BoardState board, PieceSide side)
    {
        var pseudoMoves = new List<Move>();
        var legalMoves = new List<Move>();

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                var from = new PieceCoord(x, y);
                var piece = board.GetPiece(from);
                if (!piece.HasValue || piece.Value.Side != side)
                    continue;

                GeneratePseudoMovesForPiece(board, from, piece.Value, pseudoMoves);
            }
        }

        foreach (var move in pseudoMoves)
        {
            var simulatedBoard = board.Clone();
            simulatedBoard.ApplyMove(move);
            if (!IsKingInCheck(simulatedBoard, side))
                legalMoves.Add(move);
        }

        return legalMoves;
    }

    public bool IsKingInCheck(BoardState board, PieceSide side)
    {
        var kingSquare = board.FindKing(side);
        if (!kingSquare.HasValue)
            return false;

        return IsSquareAttacked(board, kingSquare.Value, OpponentOf(side));
    }

    public bool IsSquareAttacked(BoardState board, PieceCoord square, PieceSide attacker)
    {
        int pawnDirection = attacker == PieceSide.White ? 1 : -1;
        foreach (int dx in new[] { -1, 1 })
        {
            var pawnSquare = new PieceCoord(square.x - dx, square.y - pawnDirection);
            if (!BoardState.InBounds(pawnSquare))
                continue;

            var pawn = board.GetPiece(pawnSquare);
            if (pawn.HasValue && pawn.Value.Side == attacker && pawn.Value.Type == PieceType.Pawn)
                return true;
        }

        var knightJumps = new[]
        {
            (1, 2), (2, 1), (-1, 2), (-2, 1),
            (1, -2), (2, -1), (-1, -2), (-2, -1)
        };

        foreach (var (dx, dy) in knightJumps)
        {
            var knightSquare = new PieceCoord(square.x + dx, square.y + dy);
            if (!BoardState.InBounds(knightSquare))
                continue;

            var knight = board.GetPiece(knightSquare);
            if (knight.HasValue && knight.Value.Side == attacker && knight.Value.Type == PieceType.Knight)
                return true;
        }

        if (IsAttackedBySlidingPiece(board, square, attacker, new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }, PieceType.Rook, PieceType.Queen))
            return true;

        if (IsAttackedBySlidingPiece(board, square, attacker, new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) }, PieceType.Bishop, PieceType.Queen))
            return true;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                var kingSquare = new PieceCoord(square.x + dx, square.y + dy);
                if (!BoardState.InBounds(kingSquare))
                    continue;

                var king = board.GetPiece(kingSquare);
                if (king.HasValue && king.Value.Side == attacker && king.Value.Type == PieceType.King)
                    return true;
            }
        }

        return false;
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
                moves.Add(new Move(from, capture, true));
            }
        }

        if (board.EnPassantTarget.HasValue)
        {
            var enPassantTarget = board.EnPassantTarget.Value;
            if (enPassantTarget.y == from.y + dir && System.Math.Abs(enPassantTarget.x - from.x) == 1)
            {
                var adjacentPawnSquare = new PieceCoord(enPassantTarget.x, from.y);
                var adjacentPawn = board.GetPiece(adjacentPawnSquare);
                if (adjacentPawn.HasValue &&
                    adjacentPawn.Value.Side != side &&
                    adjacentPawn.Value.Type == PieceType.Pawn)
                {
                    moves.Add(new Move(from, enPassantTarget, true, isEnPassant: true));
                }
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
                        moves.Add(new Move(from, to, true));
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
            if (!target.HasValue)
            {
                moves.Add(new Move(from, to));
            }
            else if (target.Value.Side != side)
            {
                moves.Add(new Move(from, to, true));
            }
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
                if (!target.HasValue)
                {
                    moves.Add(new Move(from, to));
                }
                else if (target.Value.Side != side)
                {
                    moves.Add(new Move(from, to, true));
                }
            }
        }

        GenerateCastleMoves(board, from, side, moves);
    }

    private void GenerateCastleMoves(BoardState board, PieceCoord from, PieceSide side, List<Move> moves)
    {
        int rank = side == PieceSide.White ? 0 : 7;
        if (from.x != 4 || from.y != rank || IsKingInCheck(board, side))
            return;

        if (board.CanCastleKingSide(side) &&
            CanCastleThroughSquares(board, side, rank, new[] { 5, 6 }, new PieceCoord(7, rank)))
        {
            moves.Add(new Move(from, new PieceCoord(6, rank), false, isCastleKingSide: true));
        }

        if (board.CanCastleQueenSide(side) &&
            CanCastleThroughSquares(board, side, rank, new[] { 3, 2 }, new PieceCoord(0, rank), extraEmptyFile: 1))
        {
            moves.Add(new Move(from, new PieceCoord(2, rank), false, isCastleQueenSide: true));
        }
    }

    private bool CanCastleThroughSquares(
        BoardState board,
        PieceSide side,
        int rank,
        int[] kingPathFiles,
        PieceCoord rookSquare,
        int? extraEmptyFile = null)
    {
        var rook = board.GetPiece(rookSquare);
        if (!rook.HasValue || rook.Value.Side != side || rook.Value.Type != PieceType.Rook)
            return false;

        foreach (int file in kingPathFiles)
        {
            var square = new PieceCoord(file, rank);
            if (board.GetPiece(square).HasValue || IsSquareAttacked(board, square, OpponentOf(side)))
                return false;
        }

        if (extraEmptyFile.HasValue)
        {
            var extraSquare = new PieceCoord(extraEmptyFile.Value, rank);
            if (board.GetPiece(extraSquare).HasValue)
                return false;
        }

        return true;
    }

    private bool IsAttackedBySlidingPiece(
        BoardState board,
        PieceCoord square,
        PieceSide attacker,
        (int dx, int dy)[] directions,
        PieceType primaryType,
        PieceType secondaryType)
    {
        foreach (var (dx, dy) in directions)
        {
            int x = square.x + dx;
            int y = square.y + dy;

            while (BoardState.InBounds(new PieceCoord(x, y)))
            {
                var piece = board.GetPiece(new PieceCoord(x, y));
                if (piece.HasValue)
                {
                    if (piece.Value.Side == attacker &&
                        (piece.Value.Type == primaryType || piece.Value.Type == secondaryType))
                    {
                        return true;
                    }

                    break;
                }

                x += dx;
                y += dy;
            }
        }

        return false;
    }

    private PieceSide OpponentOf(PieceSide side)
    {
        return side == PieceSide.White ? PieceSide.Black : PieceSide.White;
    }
}

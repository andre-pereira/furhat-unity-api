using System;
using UnityEngine;

public enum PieceType { King, Queen, Rook, Bishop, Knight, Pawn }
public enum PieceSide { White, Black }

[Serializable]
public struct Piece
{
    public PieceType Type;
    public PieceSide Side;

    public Piece(PieceType type, PieceSide side)
    {
        Type = type;
        Side = side;
    }
}

[Serializable]
public struct PieceCoord
{
    public int x;
    public int y;

    public PieceCoord(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    public bool Equals(PieceCoord other) => x == other.x && y == other.y;
}

[Serializable]
public struct Move
{
    public PieceCoord From;
    public PieceCoord To;
    public bool IsCapture;
    public bool IsEnPassant;
    public bool IsCastleKingSide;
    public bool IsCastleQueenSide;

    public Move(PieceCoord from, PieceCoord to)
    {
        From = from;
        To = to;
        IsCapture = false;
        IsEnPassant = false;
        IsCastleKingSide = false;
        IsCastleQueenSide = false;
    }

    public Move(
        PieceCoord from,
        PieceCoord to,
        bool isCapture,
        bool isEnPassant = false,
        bool isCastleKingSide = false,
        bool isCastleQueenSide = false)
    {
        From = from;
        To = to;
        IsCapture = isCapture;
        IsEnPassant = isEnPassant;
        IsCastleKingSide = isCastleKingSide;
        IsCastleQueenSide = isCastleQueenSide;
    }

    public PieceCoord GetCaptureSquare()
    {
        return IsEnPassant ? new PieceCoord(To.x, From.y) : To;
    }
}

public class BoardState
{
    private readonly Piece?[,] squares = new Piece?[8, 8];
    public PieceSide SideToMove { get; private set; } = PieceSide.White;
    public PieceCoord? EnPassantTarget { get; private set; }
    public bool WhiteCanCastleKingSide { get; private set; } = true;
    public bool WhiteCanCastleQueenSide { get; private set; } = true;
    public bool BlackCanCastleKingSide { get; private set; } = true;
    public bool BlackCanCastleQueenSide { get; private set; } = true;

    public Piece? GetPiece(PieceCoord coord) => InBounds(coord) ? squares[coord.x, coord.y] : null;
    public void SetPiece(PieceCoord coord, Piece? piece) { if (InBounds(coord)) squares[coord.x, coord.y] = piece; }

    public void SetupStartingPosition()
    {
        Array.Clear(squares, 0, squares.Length);
        SideToMove = PieceSide.White;
        EnPassantTarget = null;
        WhiteCanCastleKingSide = true;
        WhiteCanCastleQueenSide = true;
        BlackCanCastleKingSide = true;
        BlackCanCastleQueenSide = true;

        PieceType[] back = {
            PieceType.Rook, PieceType.Knight, PieceType.Bishop, PieceType.Queen,
            PieceType.King, PieceType.Bishop, PieceType.Knight, PieceType.Rook
        };

        for (int x = 0; x < 8; x++)
        {
            squares[x, 0] = new Piece(back[x], PieceSide.White);
            squares[x, 1] = new Piece(PieceType.Pawn, PieceSide.White);
            squares[x, 6] = new Piece(PieceType.Pawn, PieceSide.Black);
            squares[x, 7] = new Piece(back[x], PieceSide.Black);
        }
    }

    public BoardState Clone()
    {
        var clone = new BoardState();

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                clone.squares[x, y] = squares[x, y];
            }
        }

        clone.SideToMove = SideToMove;
        clone.EnPassantTarget = EnPassantTarget;
        clone.WhiteCanCastleKingSide = WhiteCanCastleKingSide;
        clone.WhiteCanCastleQueenSide = WhiteCanCastleQueenSide;
        clone.BlackCanCastleKingSide = BlackCanCastleKingSide;
        clone.BlackCanCastleQueenSide = BlackCanCastleQueenSide;

        return clone;
    }

    public void ApplyMove(Move move)
    {
        var movingPiece = GetPiece(move.From);
        if (!movingPiece.HasValue)
            return;

        var capturedSquare = move.GetCaptureSquare();
        var capturedPiece = move.IsCapture ? GetPiece(capturedSquare) : null;

        UpdateCastlingRightsForMove(move.From, movingPiece.Value);
        if (capturedPiece.HasValue)
            UpdateCastlingRightsForCapture(capturedSquare, capturedPiece.Value);

        if (move.IsEnPassant)
            SetPiece(capturedSquare, null);

        SetPiece(move.To, movingPiece);
        SetPiece(move.From, null);

        if (move.IsCastleKingSide || move.IsCastleQueenSide)
            MoveCastleRook(move, movingPiece.Value.Side);

        // Auto-promote pawns to queens for simplicity.
        var piece = GetPiece(move.To);
        if (piece.HasValue && piece.Value.Type == PieceType.Pawn)
        {
            if ((piece.Value.Side == PieceSide.White && move.To.y == 7) ||
                (piece.Value.Side == PieceSide.Black && move.To.y == 0))
            {
                SetPiece(move.To, new Piece(PieceType.Queen, piece.Value.Side));
            }
        }

        if (movingPiece.Value.Type == PieceType.Pawn && Mathf.Abs(move.To.y - move.From.y) == 2)
        {
            EnPassantTarget = new PieceCoord(move.From.x, (move.From.y + move.To.y) / 2);
        }
        else
        {
            EnPassantTarget = null;
        }

        SideToMove = SideToMove == PieceSide.White ? PieceSide.Black : PieceSide.White;
    }

    public PieceCoord? FindKing(PieceSide side)
    {
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                var piece = squares[x, y];
                if (piece.HasValue && piece.Value.Side == side && piece.Value.Type == PieceType.King)
                    return new PieceCoord(x, y);
            }
        }

        return null;
    }

    public bool CanCastleKingSide(PieceSide side) =>
        side == PieceSide.White ? WhiteCanCastleKingSide : BlackCanCastleKingSide;

    public bool CanCastleQueenSide(PieceSide side) =>
        side == PieceSide.White ? WhiteCanCastleQueenSide : BlackCanCastleQueenSide;

    private void MoveCastleRook(Move move, PieceSide side)
    {
        int rank = side == PieceSide.White ? 0 : 7;
        PieceCoord rookFrom;
        PieceCoord rookTo;

        if (move.IsCastleKingSide)
        {
            rookFrom = new PieceCoord(7, rank);
            rookTo = new PieceCoord(5, rank);
        }
        else
        {
            rookFrom = new PieceCoord(0, rank);
            rookTo = new PieceCoord(3, rank);
        }

        var rook = GetPiece(rookFrom);
        SetPiece(rookTo, rook);
        SetPiece(rookFrom, null);
    }

    private void UpdateCastlingRightsForMove(PieceCoord from, Piece piece)
    {
        if (piece.Type == PieceType.King)
        {
            if (piece.Side == PieceSide.White)
            {
                WhiteCanCastleKingSide = false;
                WhiteCanCastleQueenSide = false;
            }
            else
            {
                BlackCanCastleKingSide = false;
                BlackCanCastleQueenSide = false;
            }
        }

        if (piece.Type != PieceType.Rook)
            return;

        if (piece.Side == PieceSide.White)
        {
            if (from.x == 0 && from.y == 0) WhiteCanCastleQueenSide = false;
            if (from.x == 7 && from.y == 0) WhiteCanCastleKingSide = false;
        }
        else
        {
            if (from.x == 0 && from.y == 7) BlackCanCastleQueenSide = false;
            if (from.x == 7 && from.y == 7) BlackCanCastleKingSide = false;
        }
    }

    private void UpdateCastlingRightsForCapture(PieceCoord captureSquare, Piece capturedPiece)
    {
        if (capturedPiece.Type != PieceType.Rook)
            return;

        if (capturedPiece.Side == PieceSide.White)
        {
            if (captureSquare.x == 0 && captureSquare.y == 0) WhiteCanCastleQueenSide = false;
            if (captureSquare.x == 7 && captureSquare.y == 0) WhiteCanCastleKingSide = false;
        }
        else
        {
            if (captureSquare.x == 0 && captureSquare.y == 7) BlackCanCastleQueenSide = false;
            if (captureSquare.x == 7 && captureSquare.y == 7) BlackCanCastleKingSide = false;
        }
    }

    public static bool InBounds(PieceCoord c) => c.x >= 0 && c.x < 8 && c.y >= 0 && c.y < 8;
}

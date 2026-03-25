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

    public Move(PieceCoord from, PieceCoord to)
    {
        From = from;
        To = to;
    }
}

public class BoardState
{
    private readonly Piece?[,] squares = new Piece?[8, 8];
    public PieceSide SideToMove { get; private set; } = PieceSide.White;

    public Piece? GetPiece(PieceCoord coord) => InBounds(coord) ? squares[coord.x, coord.y] : null;
    public void SetPiece(PieceCoord coord, Piece? piece) { if (InBounds(coord)) squares[coord.x, coord.y] = piece; }

    public void SetupStartingPosition()
    {
        Array.Clear(squares, 0, squares.Length);
        SideToMove = PieceSide.White;

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

    public void ApplyMove(Move move)
    {
        var movingPiece = GetPiece(move.From);
        SetPiece(move.To, movingPiece);
        SetPiece(move.From, null);

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

        SideToMove = SideToMove == PieceSide.White ? PieceSide.Black : PieceSide.White;
    }

    public static bool InBounds(PieceCoord c) => c.x >= 0 && c.x < 8 && c.y >= 0 && c.y < 8;
}

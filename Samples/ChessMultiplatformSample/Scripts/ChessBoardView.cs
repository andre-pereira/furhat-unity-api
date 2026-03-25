using System;
using System.Collections.Generic;
using UnityEngine;

public class ChessBoardView : MonoBehaviour
{
    [SerializeField] private RectTransform boardRoot;
    [SerializeField] private GameObject squarePrefab;

    [Header("Board Colors")]
    [SerializeField] private Color lightSquare = new Color(0.94f, 0.85f, 0.71f);
    [SerializeField] private Color darkSquare = new Color(0.71f, 0.53f, 0.39f);

    [Header("Piece Sprites")]
    [SerializeField] private Sprite whiteKing;
    [SerializeField] private Sprite whiteQueen;
    [SerializeField] private Sprite whiteRook;
    [SerializeField] private Sprite whiteBishop;
    [SerializeField] private Sprite whiteKnight;
    [SerializeField] private Sprite whitePawn;
    [SerializeField] private Sprite blackKing;
    [SerializeField] private Sprite blackQueen;
    [SerializeField] private Sprite blackRook;
    [SerializeField] private Sprite blackBishop;
    [SerializeField] private Sprite blackKnight;
    [SerializeField] private Sprite blackPawn;

    [Header("Highlight Sprites")]
    [SerializeField] private Sprite selectedSprite;
    [SerializeField] private Sprite moveSprite;
    [SerializeField] private Sprite captureSprite;

    private readonly Dictionary<(int, int), ChessSquareView> squares = new();

    public void BuildBoard(BoardState boardState, Action<PieceCoord> onClick)
    {
        for (int y = 7; y >= 0; y--)
        {
            for (int x = 0; x < 8; x++)
            {
                var go = Instantiate(squarePrefab, boardRoot);
                var view = go.GetComponent<ChessSquareView>();
                var coord = new PieceCoord(x, y);

                view.Initialize(coord, () => onClick?.Invoke(coord));

                bool isLight = (x + y) % 2 == 0;
                view.SetBackgroundColor(isLight ? lightSquare : darkSquare);

                squares[(x, y)] = view;
            }
        }

        Refresh(boardState);
    }

    public void Refresh(BoardState boardState)
    {
        foreach (var kvp in squares)
        {
            var coord = new PieceCoord(kvp.Key.Item1, kvp.Key.Item2);
            kvp.Value.SetPieceSprite(GetPieceSprite(boardState.GetPiece(coord)));
            kvp.Value.ClearHighlight();
        }
    }

    public void ClearHighlights()
    {
        foreach (var view in squares.Values)
            view.ClearHighlight();
    }

    public void ShowSelected(PieceCoord coord)
    {
        if (squares.TryGetValue((coord.x, coord.y), out var view))
            view.SetHighlight(selectedSprite);
    }

    public void ShowMove(PieceCoord coord, bool isCapture)
    {
        if (squares.TryGetValue((coord.x, coord.y), out var view))
            view.SetHighlight(isCapture ? captureSprite : moveSprite);
    }

    private Sprite GetPieceSprite(Piece? piece)
    {
        if (!piece.HasValue) return null;

        return (piece.Value.Side, piece.Value.Type) switch
        {
            (PieceSide.White, PieceType.King) => whiteKing,
            (PieceSide.White, PieceType.Queen) => whiteQueen,
            (PieceSide.White, PieceType.Rook) => whiteRook,
            (PieceSide.White, PieceType.Bishop) => whiteBishop,
            (PieceSide.White, PieceType.Knight) => whiteKnight,
            (PieceSide.White, PieceType.Pawn) => whitePawn,
            (PieceSide.Black, PieceType.King) => blackKing,
            (PieceSide.Black, PieceType.Queen) => blackQueen,
            (PieceSide.Black, PieceType.Rook) => blackRook,
            (PieceSide.Black, PieceType.Bishop) => blackBishop,
            (PieceSide.Black, PieceType.Knight) => blackKnight,
            (PieceSide.Black, PieceType.Pawn) => blackPawn,
            _ => null
        };
    }
}
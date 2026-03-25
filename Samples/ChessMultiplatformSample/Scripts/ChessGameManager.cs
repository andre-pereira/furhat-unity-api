using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChessGameManager : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private ChessBoardView boardView;
    [SerializeField] private ChessUIController uiController;

    private BoardState boardState;
    private MoveGenerator moveGenerator;
    private RandomAiPlayer randomAi;

    private PieceCoord? selectedSquare;
    private bool isBusy;

    private void Start()
    {
        boardState = new BoardState();
        moveGenerator = new MoveGenerator();
        randomAi = new RandomAiPlayer();

        boardState.SetupStartingPosition();
        boardView.BuildBoard(boardState, OnSquareClicked);
        uiController.SetStatus("Your turn (White)");
    }

    private void OnSquareClicked(PieceCoord square)
    {
        if (isBusy || boardState.SideToMove != PieceSide.White)
            return;

        var piece = boardState.GetPiece(square);

        if (!selectedSquare.HasValue)
        {
            if (piece != null && piece.Value.Side == PieceSide.White)
            {
                selectedSquare = square;
                ShowSelection(square);
            }
            return;
        }

        var from = selectedSquare.Value;
        var legalMoves = moveGenerator.GenerateLegalMoves(boardState, PieceSide.White);
        foreach (var move in legalMoves)
        {
            if (move.From.Equals(from) && move.To.Equals(square))
            {
                StartCoroutine(PlayHumanMoveThenAi(move));
                selectedSquare = null;
                boardView.ClearHighlights();
                return;
            }
        }

        if (piece != null && piece.Value.Side == PieceSide.White)
        {
            selectedSquare = square;
            ShowSelection(square);
        }
        else
        {
            selectedSquare = null;
            boardView.ClearHighlights();
        }
    }

    private void ShowSelection(PieceCoord from)
    {
        boardView.ClearHighlights();
        boardView.ShowSelected(from);

        var legalMoves = moveGenerator.GenerateLegalMoves(boardState, PieceSide.White);
        foreach (var move in legalMoves)
        {
            if (move.From.Equals(from))
            {
                boardView.ShowMove(move.To, boardState.GetPiece(move.To).HasValue);
            }
        }
    }

    private IEnumerator PlayHumanMoveThenAi(Move humanMove)
    {
        isBusy = true;

        boardState.ApplyMove(humanMove);
        boardView.Refresh(boardState);
        yield return new WaitForSeconds(0.15f);

        if (CheckGameEnd())
        {
            isBusy = false;
            yield break;
        }

        uiController.SetStatus("AI thinking randomly...");
        yield return new WaitForSeconds(0.35f);

        var aiMove = randomAi.ChooseMove(boardState, moveGenerator, PieceSide.Black);
        if (aiMove.HasValue)
        {
            boardState.ApplyMove(aiMove.Value);
            boardView.Refresh(boardState);
        }

        if (!CheckGameEnd())
        {
            uiController.SetStatus("Your turn (White)");
        }

        isBusy = false;
    }

    private bool CheckGameEnd()
    {
        var whiteMoves = moveGenerator.GenerateLegalMoves(boardState, PieceSide.White);
        var blackMoves = moveGenerator.GenerateLegalMoves(boardState, PieceSide.Black);

        if (whiteMoves.Count == 0 || blackMoves.Count == 0)
        {
            uiController.SetStatus("Game over");
            return true;
        }

        return false;
    }
}

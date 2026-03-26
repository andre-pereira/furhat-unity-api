using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChessGameManager : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private ChessBoardView boardView;
    [SerializeField] private ChessUIController uiController;
    [SerializeField] private AudioManager audioManager;

    private BoardState boardState;
    private MoveGenerator moveGenerator;
    private RandomAiPlayer randomAi;

    private PieceCoord? selectedSquare;
    private bool isBusy;
    private Move? lastAiMove;

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
                audioManager?.PlaySelect();
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
                boardView.ClearAllMarkers();
                return;
            }
        }

        if (piece != null && piece.Value.Side == PieceSide.White)
        {
            selectedSquare = square;
            ShowSelection(square);
            audioManager?.PlaySelect();
        }
        else
        {
            selectedSquare = null;
            boardView.ClearAllMarkers();
            boardView.ClearAllSelectionVisuals();
        }
    }

    private void ShowSelection(PieceCoord from)
    {
        boardView.ClearAllMarkers();
        boardView.ClearAllSquareOverlays();
        boardView.ClearAllSelectionVisuals();

        if (lastAiMove.HasValue)
            boardView.ShowLastMove(lastAiMove.Value.From, lastAiMove.Value.To);

        boardView.ShowSelected(from);
        boardView.SetSelectedVisual(from, true);

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

        bool humanIsCapture = boardState.GetPiece(humanMove.To).HasValue;
        boardState.ApplyMove(humanMove);
        boardView.Refresh(boardState);
        if (humanIsCapture) audioManager?.PlayCapture();
        else audioManager?.PlayMove();

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
            bool aiIsCapture = boardState.GetPiece(aiMove.Value.To).HasValue;
            boardState.ApplyMove(aiMove.Value);
            lastAiMove = aiMove.Value;
            boardView.Refresh(boardState);

            if (aiIsCapture) audioManager?.PlayCapture();
            else audioManager?.PlayMove();

        }

        if (lastAiMove.HasValue)
        {
            boardView.ClearAllSquareOverlays();
            boardView.ShowLastMove(lastAiMove.Value.From, lastAiMove.Value.To);
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

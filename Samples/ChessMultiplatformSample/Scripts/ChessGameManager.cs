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
        uiController.SetStatus(GetTurnStatus(PieceSide.White));
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
                boardView.ShowMove(move.To, move.IsCapture);
            }
        }
    }

    private IEnumerator PlayHumanMoveThenAi(Move humanMove)
    {
        isBusy = true;

        bool humanIsCapture = humanMove.IsCapture;
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

        uiController.SetStatus(GetAiThinkingStatus());
        yield return new WaitForSeconds(0.35f);

        var aiMove = randomAi.ChooseMove(boardState, moveGenerator, PieceSide.Black);
        if (aiMove.HasValue)
        {
            bool aiIsCapture = aiMove.Value.IsCapture;
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
            uiController.SetStatus(GetTurnStatus(PieceSide.White));
        }

        isBusy = false;
    }

    private bool CheckGameEnd()
    {
        var sideToMove = boardState.SideToMove;
        var legalMoves = moveGenerator.GenerateLegalMoves(boardState, sideToMove);
        if (legalMoves.Count == 0)
        {
            bool inCheck = moveGenerator.IsKingInCheck(boardState, sideToMove);
            if (inCheck)
            {
                var winner = sideToMove == PieceSide.White ? "Black" : "White";
                uiController.SetStatus($"Checkmate. {winner} wins.");
            }
            else
            {
                uiController.SetStatus("Stalemate.");
            }

            return true;
        }

        return false;
    }

    private string GetTurnStatus(PieceSide side)
    {
        string player = side == PieceSide.White ? "Your turn (White)" : "Black to move";
        return moveGenerator.IsKingInCheck(boardState, side) ? $"{player} - Check!" : player;
    }

    private string GetAiThinkingStatus()
    {
        return moveGenerator.IsKingInCheck(boardState, PieceSide.Black)
            ? "AI thinking... Black is in check."
            : "AI thinking randomly...";
    }
}

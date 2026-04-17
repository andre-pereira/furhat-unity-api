using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChessGameManager : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private ChessBoardView boardView;
    [SerializeField] private ChessUIController uiController;
    [SerializeField] private AudioManager audioManager;
    [SerializeField] private Button gameControlButton;
    [SerializeField] private TMP_Text gameControlButtonText;

    [Header("UI Text")]
    [SerializeField] private string startGameLabel = "Start Game";
    [SerializeField] private string clearBoardLabel = "Clear Board";
    [SerializeField] private string idleStatus = "Board cleared. Press Start Game to begin.";

    [Header("Chess Clock")]
    [SerializeField] private ChessClockController chessClock;
    [SerializeField] private TMP_InputField timeLimitInput;
    [Min(1)] [SerializeField] private int defaultTimeLimitMinutes = 5;

    [Header("AI Settings")]
    [SerializeField] private MinimaxAiSettings aiSettings = new MinimaxAiSettings();

    private BoardState boardState;
    private MoveGenerator moveGenerator;
    private MinimaxAiPlayer minimaxAi;

    private PieceCoord? selectedSquare;
    private bool isBusy;
    private bool isGameActive;
    private Move? lastAiMove;

    private void Start()
    {
        boardState = new BoardState();
        moveGenerator = new MoveGenerator();
        minimaxAi = new MinimaxAiPlayer();

        boardView.BuildBoard(boardState, OnSquareClicked);
        WireControlButton();
        WireClockControls();
        ResetToIdleBoard();
    }

    private void OnDestroy()
    {
        if (chessClock != null)
            chessClock.OnTimeExpired -= HandleTimeExpired;

        if (timeLimitInput != null)
            timeLimitInput.onEndEdit.RemoveListener(HandleTimeLimitEdited);
    }

    public void ToggleGameButton()
    {
        if (isGameActive)
        {
            ResetToIdleBoard();
            return;
        }

        StartNewGame();
    }

    public void StartNewGame()
    {
        StopAllCoroutines();
        isBusy = false;
        isGameActive = true;
        selectedSquare = null;
        lastAiMove = null;

        boardState.SetupStartingPosition();
        boardView.Refresh(boardState);
        boardView.ClearAllMarkers();
        boardView.ClearAllSquareOverlays();
        boardView.ClearAllSelectionVisuals();

        float startingSeconds = GetSelectedTimeLimitMinutes() * 60f;
        chessClock?.Initialize(startingSeconds);
        chessClock?.StartTicking(PieceSide.White);

        UpdateControlButtonLabel(clearBoardLabel);
        uiController.SetStatus(GetTurnStatus(PieceSide.White));
    }

    public void ResetToIdleBoard()
    {
        StopAllCoroutines();
        isBusy = false;
        isGameActive = false;
        selectedSquare = null;
        lastAiMove = null;

        boardState = new BoardState();
        boardView.Refresh(boardState);
        boardView.ClearAllMarkers();
        boardView.ClearAllSquareOverlays();
        boardView.ClearAllSelectionVisuals();

        chessClock?.StopAll();
        chessClock?.Initialize(GetSelectedTimeLimitMinutes() * 60f);

        UpdateControlButtonLabel(startGameLabel);
        uiController.SetStatus(idleStatus);
    }

    private void WireControlButton()
    {
        if (gameControlButton == null)
            return;

        gameControlButton.onClick.RemoveListener(ToggleGameButton);
        gameControlButton.onClick.AddListener(ToggleGameButton);

        if (gameControlButtonText == null)
            gameControlButtonText = gameControlButton.GetComponentInChildren<TMP_Text>(true);
    }

    private void WireClockControls()
    {
        if (timeLimitInput != null)
        {
            timeLimitInput.onEndEdit.RemoveListener(HandleTimeLimitEdited);
            timeLimitInput.onEndEdit.AddListener(HandleTimeLimitEdited);
            timeLimitInput.text = defaultTimeLimitMinutes.ToString();
        }

        if (chessClock != null)
            chessClock.OnTimeExpired += HandleTimeExpired;
    }

    private void UpdateControlButtonLabel(string label)
    {
        if (gameControlButtonText != null)
            gameControlButtonText.text = label;
    }

    private void OnSquareClicked(PieceCoord square)
    {
        if (!isGameActive || isBusy || boardState.SideToMove != PieceSide.White)
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

        chessClock?.SwitchTo(PieceSide.Black);
        chessClock?.StartTicking(PieceSide.Black);

        yield return new WaitForSeconds(0.15f);

        if (!isGameActive)
        {
            isBusy = false;
            yield break;
        }

        if (CheckGameEnd())
        {
            isBusy = false;
            yield break;
        }

        uiController.SetStatus(GetAiThinkingStatus());

        if (!isGameActive)
        {
            isBusy = false;
            yield break;
        }

        var aiResult = minimaxAi.ChooseMove(boardState, moveGenerator, PieceSide.Black, aiSettings);
        float extraDelaySeconds = Mathf.Max(0f, (aiResult.TargetThinkTimeMilliseconds - aiResult.SearchElapsedMilliseconds) / 1000f);
        if (extraDelaySeconds > 0f)
            yield return new WaitForSeconds(extraDelaySeconds);

        if (!isGameActive)
        {
            isBusy = false;
            yield break;
        }

        if (aiResult.BestMove.HasValue)
        {
            bool aiIsCapture = aiResult.BestMove.Value.IsCapture;
            boardState.ApplyMove(aiResult.BestMove.Value);
            lastAiMove = aiResult.BestMove.Value;
            boardView.Refresh(boardState);

            if (aiIsCapture) audioManager?.PlayCapture();
            else audioManager?.PlayMove();
        }

        chessClock?.SwitchTo(PieceSide.White);
        chessClock?.StartTicking(PieceSide.White);

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
            isBusy = true;
            chessClock?.StopAll();

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

    private void HandleTimeExpired(PieceSide expiredSide)
    {
        if (!isGameActive)
            return;

        StopAllCoroutines();
        isBusy = true;
        isGameActive = true;
        selectedSquare = null;
        boardView.ClearAllMarkers();
        boardView.ClearAllSelectionVisuals();
        boardView.ClearAllSquareOverlays();

        string winner = expiredSide == PieceSide.White ? "Black" : "White";
        uiController.SetStatus($"Time out. {winner} wins.");
    }

    private void HandleTimeLimitEdited(string rawValue)
    {
        int minutes = ParseMinutesOrDefault(rawValue);
        if (timeLimitInput != null && timeLimitInput.text != minutes.ToString())
            timeLimitInput.text = minutes.ToString();

        if (!isGameActive)
            chessClock?.Initialize(minutes * 60f);
    }

    private int GetSelectedTimeLimitMinutes()
    {
        int minutes = ParseMinutesOrDefault(timeLimitInput != null ? timeLimitInput.text : defaultTimeLimitMinutes.ToString());

        if (timeLimitInput != null && timeLimitInput.text != minutes.ToString())
            timeLimitInput.text = minutes.ToString();

        return minutes;
    }

    private int ParseMinutesOrDefault(string rawValue)
    {
        if (!int.TryParse(rawValue, out int minutes))
            minutes = defaultTimeLimitMinutes;

        minutes = Mathf.Max(1, minutes);
        return minutes;
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
            : "AI thinking...";
    }
}

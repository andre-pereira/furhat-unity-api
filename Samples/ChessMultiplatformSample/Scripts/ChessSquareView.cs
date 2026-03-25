using UnityEngine;
using UnityEngine.UI;

public class ChessSquareView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image pieceImage;
    [SerializeField] private Image highlightImage;

    private PieceCoord coord;

    public void Initialize(PieceCoord pieceCoord, UnityEngine.Events.UnityAction onClick)
    {
        coord = pieceCoord;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(onClick);
        ClearHighlight();
    }

    public void SetBackgroundColor(Color color)
    {
        if (backgroundImage != null)
            backgroundImage.color = color;
    }

    public void SetPieceSprite(Sprite sprite)
    {
        pieceImage.sprite = sprite;
        pieceImage.enabled = sprite != null;
    }

    public void SetHighlight(Sprite sprite)
    {
        highlightImage.sprite = sprite;
        highlightImage.enabled = sprite != null;
    }

    public void ClearHighlight()
    {
        highlightImage.sprite = null;
        highlightImage.enabled = false;
    }
}
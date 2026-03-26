using UnityEngine;
using UnityEngine.UI;

public class ChessSquareView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image squareOverlayImage;
    [SerializeField] private Image pieceImage;
    [SerializeField] private Image markerOverlayImage;

    private Color baseColor = Color.white;

    public void Initialize(PieceCoord pieceCoord, UnityEngine.Events.UnityAction onClick)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(onClick);
        ClearAllOverlays();
        SetSelectedVisual(false);
    }

    public void SetBackgroundColor(Color color)
    {
        baseColor = color;
        if (backgroundImage != null)
            backgroundImage.color = color;
    }

    public void SetPieceSprite(Sprite sprite)
    {
        pieceImage.sprite = sprite;
        pieceImage.enabled = sprite != null;
    }

    public void SetSquareOverlay(Sprite sprite)
    {
        if (squareOverlayImage == null) return;
        squareOverlayImage.sprite = sprite;
        squareOverlayImage.color = Color.white;
        squareOverlayImage.enabled = sprite != null;
    }

    public void SetMarkerOverlay(Sprite sprite)
    {
        if (markerOverlayImage == null) return;
        markerOverlayImage.sprite = sprite;
        markerOverlayImage.color = Color.white;
        markerOverlayImage.enabled = sprite != null;
    }

    public void ClearSquareOverlay()
    {
        if (squareOverlayImage == null) return;
        squareOverlayImage.sprite = null;
        squareOverlayImage.enabled = false;
    }

    public void ClearMarkerOverlay()
    {
        if (markerOverlayImage == null) return;
        markerOverlayImage.sprite = null;
        markerOverlayImage.enabled = false;
    }

    public void ClearAllOverlays()
    {
        ClearSquareOverlay();
        ClearMarkerOverlay();
    }

    public void SetSelectedVisual(bool selected)
    {
        if (pieceImage == null) return;
        pieceImage.transform.localScale = selected ? Vector3.one * 1.12f : Vector3.one;
    }
}
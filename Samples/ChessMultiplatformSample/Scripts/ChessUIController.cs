using TMPro; 
using UnityEngine;

public class ChessUIController : MonoBehaviour
{
    [SerializeField] private TMP_Text statusText;

    public void SetStatus(string message)
    {
        statusText.text = message;
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PieceAnimator : MonoBehaviour
{
    public float moveDuration = 0.15f;

    public IEnumerator MoveTo(RectTransform piece, Vector3 targetPos)
    {
        Vector3 start = piece.position;
        float t = 0;

        while (t < 1)
        {
            t += Time.deltaTime / moveDuration;
            piece.position = Vector3.Lerp(start, targetPos, t);
            yield return null;
        }

        piece.position = targetPos;
    }
}
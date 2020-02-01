using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class TileVisorUI : MonoBehaviour
{
    public void SpawnFX(GameObject pFXPrefab)
    {
        GameObject fxGO = Instantiate(pFXPrefab, transform);
        RectTransform fxRectTransform = fxGO.GetComponent<RectTransform>();

        fxRectTransform.localPosition = Vector3.zero;
        fxRectTransform.localRotation = Quaternion.identity;
        fxRectTransform.localScale = Vector3.one;

        TileVisorFX fx = fxGO.GetComponent<TileVisorFX>();
        fx.PlayFX();
    }
}

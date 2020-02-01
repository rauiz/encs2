using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class TileVisorUI : MonoBehaviour
{
    private Animator m_animator;
    public void SpawnFX(GameObject pFXPrefab)
    {
        GetAnimator().Play("Visor_Hurt_Animation");

        GameObject fxGO = Instantiate(pFXPrefab, transform);
        RectTransform fxRectTransform = fxGO.GetComponent<RectTransform>();

        fxRectTransform.localPosition = Vector3.zero;
        fxRectTransform.localRotation = Quaternion.identity;
        fxRectTransform.localScale = Vector3.one;

        TileVisorFX fx = fxGO.GetComponent<TileVisorFX>();
        fx.PlayFX();
    }

    public Animator GetAnimator()
    {
        if (m_animator == null)
            m_animator = GetComponent<Animator>();

        return m_animator;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TileVisorFX : MonoBehaviour
{
    private Animator m_animator;

    public bool m_autoDestroy;
    public float m_lifeTime = 2f;

    public void PlayFX()
    {
        if (m_autoDestroy)
            Destroy(gameObject, m_lifeTime);
    }
}

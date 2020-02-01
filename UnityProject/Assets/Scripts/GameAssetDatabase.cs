using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameAssetDatabase : MonoBehaviour
{
    public string[] m_gameTexts;
    public GameObject[] m_gamePrefabs;

    public GameObject GetPrefab(int pIndex)
    {
        return m_gamePrefabs[pIndex];
    }
}

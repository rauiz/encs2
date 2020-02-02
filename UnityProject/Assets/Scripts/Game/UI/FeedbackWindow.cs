using UnityEngine;
using TMPro;

public class FeedbackWindow : MonoBehaviour
{
    private GameAssetDatabase m_assetDatabase;

    public GameObject m_footStepFX_prefab;
    public TileVisorUI[] m_tileVisorsArray;

    public void ShowFeedback(int pTileIndex, int pPrefabIndex)
    {
        m_tileVisorsArray[pTileIndex].SpawnFX(GetAssetDatabase().GetPrefab(pPrefabIndex));
    }

    public void OnGUI()
    {
        for (int i = 0; i < m_tileVisorsArray.Length; i++)
        {
            if(GUILayout.Button("Play"))
            {
                ShowFeedback(i, Random.Range(0, 2));
            }
        } 
    }

    public GameAssetDatabase GetAssetDatabase()
    {
        if (m_assetDatabase == null)
            m_assetDatabase = FindObjectOfType<GameAssetDatabase>();

        return m_assetDatabase;
    }
}

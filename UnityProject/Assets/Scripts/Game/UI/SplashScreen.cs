using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SplashScreen : MonoBehaviour
{
    public Image Title;
    public Image TitleReinforcement;    
    public TextMeshProUGUI TextUI;

    public float DisplayTime;

    private bool b_FilledTitle = false;

    private float m_TimeFromStart;    
    private float m_TotalTime;

    private float m_CurrentTime;
    private int m_CurrentTextIndex;
    private string m_ShowingText;

    void Start()
    {
        Title.fillAmount = 0;
        TitleReinforcement.fillAmount = 0;

        m_ShowingText = TextUI.text;
        TextUI.SetText("");

        m_TotalTime = DisplayTime * m_ShowingText.Length + 2;        
    }

    // Update is called once per frame
    void Update()
    {
        if (!b_FilledTitle)
        {
            m_TimeFromStart += Time.deltaTime;
            Title.fillAmount = (m_TimeFromStart - 1.0f) / DisplayTime;
            TitleReinforcement.fillAmount = (m_TimeFromStart - 1.0f) * 0.75f / DisplayTime;

            if(TitleReinforcement.fillAmount >= 1)
            {
                b_FilledTitle = true;
                m_TimeFromStart = 0.0f;
            }
        }
        else
        {
            m_TimeFromStart += Time.deltaTime;
            m_CurrentTime += Time.deltaTime;
            if (m_TimeFromStart >= m_TotalTime)
            {
                SceneManager.LoadScene("Boot_UI", LoadSceneMode.Single);
                return;
            }

            if (m_CurrentTime >= DisplayTime)
            {                
                m_CurrentTime -= DisplayTime;
                m_CurrentTextIndex += 1;
                TextUI.SetText(m_ShowingText.Substring(0, m_CurrentTextIndex));
            }
        }
    }
}

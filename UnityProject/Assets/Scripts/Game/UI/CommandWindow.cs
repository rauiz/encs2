using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CommandWindow : MonoBehaviour
{
    public Button m_sendButton;
    public TMP_InputField m_commandField;
    public TextMeshProUGUI m_logText;

    public ScrollRect m_logScrollRect;

    public string m_invalidCommandMessage = "Invalid Command";

    public delegate string onMessageSentDelegate(string sMessage);
    public onMessageSentDelegate onMessageSentCallback;

    private void Awake()
    {
        m_sendButton.onClick.RemoveAllListeners();
        m_sendButton.onClick.AddListener(SendCommand);
        
    }

    private void Update()
    {
        m_commandField.Select();
        m_commandField.ActivateInputField();

        if(Input.GetKeyDown(KeyCode.Return))
        {
            if(!string.IsNullOrEmpty(m_commandField.text))
                SendCommand();
        }
    }

    public void SendCommand()
    {
        string commandText = m_commandField.text;
        string result = onMessageSentCallback?.Invoke(commandText);

        if (string.IsNullOrEmpty(result))
            RegisterLog("LALALA");
        else
            RegisterLog(result);

        m_commandField.text = string.Empty;
    }

    public void RegisterLog(string pMessage)
    {
        m_logText.text += pMessage;
        m_logText.text += "\n";

        m_logScrollRect.verticalNormalizedPosition = 0.0f;        
    }
}

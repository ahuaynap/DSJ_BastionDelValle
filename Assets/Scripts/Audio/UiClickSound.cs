using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UiClickSound : MonoBehaviour
{
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(Play);
        button.onClick.AddListener(Play);
    }

    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(Play);
        }
    }

    private void Play()
    {
        MatchAudio.NotifyInterfaceClick();
    }
}

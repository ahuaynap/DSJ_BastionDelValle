using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class CodexEntryView : MonoBehaviour
{
    [SerializeField]
    private Button button;

    [SerializeField]
    private TMP_Text label;

    private UnityAction consultAction;

    public GameObject SelectableObject =>
        button != null ? button.gameObject : null;

    public void Bind(
        string text,
        bool canConsult,
        UnityAction onConsult)
    {
        Clear();

        if (label != null)
        {
            label.text = text;
        }

        if (button == null)
        {
            return;
        }

        button.interactable = canConsult;

        if (!canConsult || onConsult == null)
        {
            return;
        }

        consultAction = onConsult;
        button.onClick.AddListener(consultAction);
    }

    public void Clear()
    {
        if (button != null && consultAction != null)
        {
            button.onClick.RemoveListener(consultAction);
        }

        consultAction = null;
    }

    private void OnDestroy()
    {
        Clear();
    }
}

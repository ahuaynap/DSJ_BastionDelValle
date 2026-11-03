using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class BuildChoiceButton : MonoBehaviour
{
    [SerializeField] private BuildSelection selection;
    [SerializeField] private int index;

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

        button.onClick.RemoveListener(Choose);
        button.onClick.AddListener(Choose);
    }

    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(Choose);
        }
    }

    private void Choose()
    {
        if (selection != null)
        {
            selection.Select(index);
        }
    }
}

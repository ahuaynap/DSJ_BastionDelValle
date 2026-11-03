using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildChoiceView : MonoBehaviour
{
    [SerializeField] private BuildSelection selection;
    [SerializeField] private Button[] buttons;
    [SerializeField] private TMP_Text[] labels;
    [SerializeField] private TMP_Text description;
    [SerializeField] private GameObject detailRoot;
    [SerializeField] private Button detailButton;
    [SerializeField] private Color selectedColor = new(0.22f, 0.62f, 0.58f, 1f);
    [SerializeField] private Color idleColor = new(0.25f, 0.36f, 0.32f, 1f);

    private bool detailOpen;

    private void OnEnable()
    {
        if (selection != null)
        {
            selection.Changed += Refresh;
        }

        if (detailButton != null)
        {
            detailButton.onClick.RemoveListener(ToggleDetail);
            detailButton.onClick.AddListener(ToggleDetail);
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (selection != null)
        {
            selection.Changed -= Refresh;
        }

        if (detailButton != null)
        {
            detailButton.onClick.RemoveListener(ToggleDetail);
        }
    }

    public void ToggleDetail()
    {
        detailOpen = !detailOpen;
        Refresh();
    }

    private void Refresh()
    {
        if (selection == null)
        {
            ApplyDetail();
            return;
        }

        int count = selection.Count;

        for (int i = 0; i < count; i++)
        {
            TowerController defense = selection.DefenseAt(i);
            TowerData data = defense == null ? null : defense.Data;

            if (labels != null && i < labels.Length && labels[i] != null)
            {
                labels[i].text = data == null
                    ? "Defensa"
                    : ShortName(data.DisplayName) + "\n" + data.BuildCost;
                labels[i].enableAutoSizing = false;
                labels[i].enableWordWrapping = true;
                labels[i].alignment = TextAlignmentOptions.Center;
                labels[i].overflowMode = TextOverflowModes.Ellipsis;
                labels[i].raycastTarget = false;

                if (labels[i].fontSize < 18f)
                {
                    labels[i].fontSize = 22f;
                }
            }

            if (buttons != null && i < buttons.Length && buttons[i] != null)
            {
                Image image = buttons[i].targetGraphic as Image;

                if (image != null)
                {
                    image.color = i == selection.SelectedIndex
                        ? selectedColor
                        : idleColor;
                }
            }
        }

        if (description == null)
        {
            ApplyDetail();
            return;
        }

        description.raycastTarget = false;
        TowerData selected = selection.Selected == null
            ? null
            : selection.Selected.Data;
        description.text = selected == null || !detailOpen
            ? string.Empty
            : selected.DisplayName + "\n" +
              selected.Description + "\n" +
              "Alcance " + selected.AttackRange.ToString("0.0") +
              "  Intervalo " + selected.AttackInterval.ToString("0.00") +
              "  Daño " + selected.ProjectileDamage;
        ApplyDetail();
    }

    private void ApplyDetail()
    {
        GameObject root = detailRoot != null
            ? detailRoot
            : description != null
                ? description.gameObject
                : null;

        if (root != null)
        {
            root.SetActive(detailOpen);
        }
    }

    private static string ShortName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "Defensa";
        }

        if (name.StartsWith("Guardian") || name.StartsWith("Guardián"))
        {
            return "Guardián";
        }

        if (name.StartsWith("Canal"))
        {
            return "Canal";
        }

        if (name.StartsWith("Terraza"))
        {
            return "Raíces";
        }

        if (name.StartsWith("Refugio"))
        {
            return "Refugio";
        }

        if (name.StartsWith("Vig"))
        {
            return "Vigía";
        }

        int space = name.IndexOf(' ');
        return space > 0 ? name.Substring(0, space) : name;
    }
}

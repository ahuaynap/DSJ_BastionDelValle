using System;
using UnityEngine;

public class BuildSelection : MonoBehaviour
{
    [SerializeField] private TowerController[] defenses;
    [SerializeField] private int selectedIndex;

    public event Action Changed;

    public int SelectedIndex => selectedIndex;

    public int Count => defenses == null ? 0 : defenses.Length;

    public TowerController Selected => DefenseAt(selectedIndex);

    public TowerController DefenseAt(int index)
    {
        if (defenses == null || index < 0 || index >= defenses.Length)
        {
            return null;
        }

        return defenses[index];
    }

    public bool Select(int index)
    {
        if (DefenseAt(index) == null)
        {
            return false;
        }

        selectedIndex = index;
        Changed?.Invoke();
        return true;
    }

    private void OnEnable()
    {
        Changed?.Invoke();
    }
}

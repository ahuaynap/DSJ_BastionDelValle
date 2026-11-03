using UnityEngine;

public class PassageMarker : MonoBehaviour
{
    [SerializeField] private MeshRenderer passageTile;
    [SerializeField] private Material openMaterial;
    [SerializeField] private Material closedMaterial;

    public void Show(bool open)
    {
        if (passageTile == null)
        {
            return;
        }

        Material material = open ? openMaterial : closedMaterial;

        if (material == null)
        {
            return;
        }

        passageTile.sharedMaterial = material;
    }
}

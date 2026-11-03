using UnityEngine;

public class ValleyFinishSettings : MonoBehaviour
{
    [HideInInspector]
    [SerializeField]
    private bool terrainApplied;

    [HideInInspector]
    [SerializeField]
    private bool pathTintApplied;

    [HideInInspector]
    [SerializeField]
    private bool interfaceApplied;

    [HideInInspector]
    [SerializeField]
    private bool temporarySurfaceRetired;

    public bool TerrainApplied => terrainApplied;

    public bool PathTintApplied => pathTintApplied;

    public bool InterfaceApplied => interfaceApplied;

    public bool TemporarySurfaceRetired => temporarySurfaceRetired;
}

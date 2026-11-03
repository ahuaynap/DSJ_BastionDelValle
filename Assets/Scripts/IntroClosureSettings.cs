using UnityEngine;

public class IntroClosureSettings : MonoBehaviour
{
    [SerializeField] private bool hudArranged;
    [SerializeField] private bool pausePrepared;
    [SerializeField] private bool resultPrepared;
    [SerializeField] private bool approachChecked;

    public bool HudArranged => hudArranged;

    public bool PausePrepared => pausePrepared;

    public bool ResultPrepared => resultPrepared;

    public bool ApproachChecked => approachChecked;
}

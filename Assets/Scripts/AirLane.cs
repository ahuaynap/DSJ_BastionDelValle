using UnityEngine;

public class AirLane : MonoBehaviour
{
    [SerializeField] private Transform[] points;

    public int Count => points == null ? 0 : points.Length;

    public bool IsValid
    {
        get
        {
            if (points == null || points.Length < 2)
            {
                return false;
            }

            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] == null)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public Vector3[] Copy()
    {
        if (!IsValid)
        {
            return null;
        }

        var copy = new Vector3[points.Length];

        for (int i = 0; i < points.Length; i++)
        {
            copy[i] = points[i].position;
        }

        return copy;
    }

    private void OnDrawGizmos()
    {
        if (points == null || points.Length < 2)
        {
            return;
        }

        Gizmos.color = new Color(0.65f, 0.82f, 0.95f, 0.9f);

        for (int i = 1; i < points.Length; i++)
        {
            if (points[i - 1] == null || points[i] == null)
            {
                continue;
            }

            Gizmos.DrawLine(points[i - 1].position, points[i].position);
        }
    }
}

using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class WanderingArea2D : MonoBehaviour
{
    [SerializeField, Min(0.05f)]
    private float boundaryMargin = 0.75f;

    private BoxCollider2D areaCollider;

    public float BoundaryMargin =>
        boundaryMargin;

    private void Awake()
    {
        CacheCollider();
    }

    public Vector2 GetRandomPoint()
    {
        CacheCollider();

        Vector2 halfSize =
            areaCollider.size * 0.5f;

        Vector2 localPoint =
            areaCollider.offset +
            new Vector2(
                Random.Range(
                    -halfSize.x,
                    halfSize.x
                ),
                Random.Range(
                    -halfSize.y,
                    halfSize.y
                )
            );

        return transform.TransformPoint(localPoint);
    }

    public Vector2 ClampPoint(Vector2 worldPoint)
    {
        CacheCollider();

        Vector2 localPoint =
            transform.InverseTransformPoint(
                worldPoint
            );

        Vector2 halfSize =
            areaCollider.size * 0.5f;

        Vector2 minimum =
            areaCollider.offset - halfSize;

        Vector2 maximum =
            areaCollider.offset + halfSize;

        localPoint.x = Mathf.Clamp(
            localPoint.x,
            minimum.x,
            maximum.x
        );

        localPoint.y = Mathf.Clamp(
            localPoint.y,
            minimum.y,
            maximum.y
        );

        return transform.TransformPoint(localPoint);
    }

    public Vector2 GetBoundaryDirection(
        Vector2 worldPoint)
    {
        CacheCollider();

        Vector2 localPoint =
            transform.InverseTransformPoint(
                worldPoint
            );

        Vector2 halfSize =
            areaCollider.size * 0.5f;

        Vector2 minimum =
            areaCollider.offset - halfSize;

        Vector2 maximum =
            areaCollider.offset + halfSize;

        Vector2 correction =
            Vector2.zero;

        float leftDistance =
            localPoint.x - minimum.x;

        float rightDistance =
            maximum.x - localPoint.x;

        float bottomDistance =
            localPoint.y - minimum.y;

        float topDistance =
            maximum.y - localPoint.y;

        if (leftDistance < boundaryMargin)
        {
            correction.x +=
                1f -
                Mathf.Clamp01(
                    leftDistance /
                    boundaryMargin
                );
        }

        if (rightDistance < boundaryMargin)
        {
            correction.x -=
                1f -
                Mathf.Clamp01(
                    rightDistance /
                    boundaryMargin
                );
        }

        if (bottomDistance < boundaryMargin)
        {
            correction.y +=
                1f -
                Mathf.Clamp01(
                    bottomDistance /
                    boundaryMargin
                );
        }

        if (topDistance < boundaryMargin)
        {
            correction.y -=
                1f -
                Mathf.Clamp01(
                    topDistance /
                    boundaryMargin
                );
        }

        if (correction.sqrMagnitude <= 0f)
        {
            return Vector2.zero;
        }

        Vector2 worldCorrection =
            transform.TransformDirection(
                correction.normalized
            );

        return worldCorrection.normalized;
    }

    private void CacheCollider()
    {
        if (areaCollider == null)
        {
            areaCollider =
                GetComponent<BoxCollider2D>();
        }
    }

    private void OnValidate()
    {
        if (boundaryMargin < 0.05f)
        {
            boundaryMargin = 0.05f;
        }

        CacheCollider();

        if (areaCollider != null)
        {
            areaCollider.isTrigger = true;
        }
    }

    private void OnDrawGizmosSelected()
    {
        CacheCollider();

        if (areaCollider == null)
        {
            return;
        }

        Gizmos.color =
            new Color(0.2f, 0.9f, 0.7f, 0.8f);

        Matrix4x4 previousMatrix =
            Gizmos.matrix;

        Gizmos.matrix =
            transform.localToWorldMatrix;

        Gizmos.DrawWireCube(
            areaCollider.offset,
            areaCollider.size
        );

        Gizmos.color =
            new Color(1f, 0.8f, 0.2f, 0.5f);

        Vector2 innerSize =
            areaCollider.size -
            Vector2.one *
            boundaryMargin *
            2f;

        innerSize.x =
            Mathf.Max(0.01f, innerSize.x);

        innerSize.y =
            Mathf.Max(0.01f, innerSize.y);

        Gizmos.DrawWireCube(
            areaCollider.offset,
            innerSize
        );

        Gizmos.matrix = previousMatrix;
    }
}
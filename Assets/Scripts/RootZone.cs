using UnityEngine;

public class RootZone : MonoBehaviour
{
    private static int nextSource = 1;
    private static EnemyHealth[] scan = new EnemyHealth[32];

    private Vector2 center;
    private float radius;
    private float duration;
    private float pulseDelay;
    private int damage;
    private float slowMultiplier;
    private TravelMask targets = TravelMask.Ground;
    private int sourceId;
    private float elapsed;
    private int pulsesFired;
    private bool closing;
    private bool released;
    private float fade;
    private GameManager gameManager;
    private SpriteRenderer graphic;
    private EnemyHealth[] occupants = new EnemyHealth[8];
    private int occupantCount;

    public static RootZone Create(
        Vector3 worldCenter,
        float zoneRadius,
        float zoneDuration,
        float secondPulse,
        int pulseDamage,
        float presenceSlow,
        GameManager manager,
        TravelMask targets = TravelMask.Ground)
    {
        if (manager == null || !manager.IsPlaying || Time.timeScale <= 0f)
        {
            return null;
        }

        if (zoneRadius <= 0f || zoneDuration <= 0f || pulseDamage <= 0)
        {
            return null;
        }

        var host = new GameObject("RootZone");
        host.transform.position = new Vector3(worldCenter.x, worldCenter.y, 0f);
        RootZone zone = host.AddComponent<RootZone>();
        zone.center = worldCenter;
        zone.radius = zoneRadius;
        zone.duration = zoneDuration;
        zone.pulseDelay = secondPulse;
        zone.damage = pulseDamage;
        zone.slowMultiplier = presenceSlow;
        zone.targets = TargetRules.Normalize((int)targets);
        zone.sourceId = nextSource++;
        zone.gameManager = manager;
        zone.BuildVisual();
        manager.OnGameStateChanged += zone.HandleState;
        zone.RefreshPresence();
        zone.FirePulse();
        zone.pulsesFired = 1;
        return zone;
    }

    private void Update()
    {
        float delta = Time.deltaTime;

        if (closing)
        {
            Fade(delta);
            return;
        }

        if (gameManager != null && !gameManager.IsPlaying)
        {
            EndImmediately();
            return;
        }

        if (!ZoneSchedule.Tick(elapsed, delta, duration, out float next, out bool finished))
        {
            return;
        }

        elapsed = next;
        RefreshPresence();

        if (ZoneSchedule.AllowPulse(true, false, false))
        {
            int due = ZoneSchedule.DuePulses(elapsed, pulseDelay);

            while (pulsesFired < due && pulsesFired < 2)
            {
                FirePulse();
                pulsesFired++;
            }
        }

        if (finished)
        {
            BeginFade();
            return;
        }

        Paint(false);
    }

    private void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.OnGameStateChanged -= HandleState;
        }

        ReleaseAll();
    }

    private void HandleState(GameState state)
    {
        if (state == GameState.Playing)
        {
            return;
        }

        EndImmediately();
    }

    private void EndImmediately()
    {
        ReleaseAll();
        Destroy(gameObject);
    }

    private void BeginFade()
    {
        if (closing)
        {
            return;
        }

        closing = true;
        fade = 0.25f;
        ReleaseAll();
        Paint(true);
    }

    private void Fade(float delta)
    {
        if (delta <= 0f)
        {
            return;
        }

        fade -= delta;
        Paint(true);

        if (fade <= 0f)
        {
            Destroy(gameObject);
        }
    }

    private void RefreshPresence()
    {
        int found = Scan();

        for (int i = occupantCount - 1; i >= 0; i--)
        {
            if (Listed(scan, found, occupants[i]))
            {
                continue;
            }

            RemoveSlow(occupants[i]);
            occupants[i] = occupants[occupantCount - 1];
            occupants[occupantCount - 1] = null;
            occupantCount--;
        }

        for (int i = 0; i < found; i++)
        {
            EnemyHealth health = scan[i];
            scan[i] = null;

            if (health == null || Listed(occupants, occupantCount, health))
            {
                continue;
            }

            AddSlow(health);

            if (occupantCount >= occupants.Length)
            {
                System.Array.Resize(ref occupants, occupants.Length * 2);
            }

            occupants[occupantCount] = health;
            occupantCount++;
        }
    }

    private void FirePulse()
    {
        int found = Scan();

        for (int i = 0; i < found; i++)
        {
            EnemyHealth health = scan[i];

            if (health == null || health.IsDead)
            {
                continue;
            }

            health.TakeDamage(damage);
        }

        for (int i = 0; i < found; i++)
        {
            EnemyHealth health = scan[i];
            scan[i] = null;

            if (health == null || health.IsDead)
            {
                continue;
            }

            EnemyMovement movement = health.GetComponent<EnemyMovement>();

            if (movement != null)
            {
                movement.ReceiveScatter(center);
            }
        }
    }

    private int Scan()
    {
        ChannelNavigator roster = ChannelNavigator.Current;

        if (roster == null || roster.ActiveAgents == null)
        {
            return 0;
        }

        var agents = roster.ActiveAgents;
        EnsureScan(agents.Count);
        int found = 0;
        float radiusSq = radius * radius;

        for (int i = 0; i < agents.Count; i++)
        {
            EnemyMovement agent = agents[i];

            if (agent == null || !agent.IsReady || agent.Health == null)
            {
                continue;
            }

            EnemyHealth health = agent.Health;

            EnemyTravel travel = health.Data == null
                ? EnemyTravel.Ground
                : health.Data.Travel;

            if (!health.IsInitialized ||
                health.IsDead ||
                !TargetRules.Allows(targets, travel))
            {
                continue;
            }

            float dx = agent.transform.position.x - center.x;
            float dy = agent.transform.position.y - center.y;

            if (dx * dx + dy * dy > radiusSq)
            {
                continue;
            }

            scan[found] = health;
            found++;
        }

        return found;
    }

    private void AddSlow(EnemyHealth health)
    {
        if (slowMultiplier >= 1f || health == null)
        {
            return;
        }

        EnemySlow slow = health.GetComponent<EnemySlow>();

        if (slow == null)
        {
            return;
        }

        slow.SetPresence(sourceId, slowMultiplier);
    }

    private void RemoveSlow(EnemyHealth health)
    {
        if (health == null)
        {
            return;
        }

        EnemySlow slow = health.GetComponent<EnemySlow>();

        if (slow != null)
        {
            slow.RemovePresence(sourceId);
        }
    }

    private void ReleaseAll()
    {
        if (released)
        {
            return;
        }

        released = true;

        for (int i = 0; i < occupantCount; i++)
        {
            RemoveSlow(occupants[i]);
            occupants[i] = null;
        }

        occupantCount = 0;
    }

    private void BuildVisual()
    {
        graphic = gameObject.AddComponent<SpriteRenderer>();
        graphic.sprite = Ring();
        graphic.sortingOrder = 2;
        float width = graphic.sprite.bounds.size.x;

        if (width < 0.01f)
        {
            width = 1f;
        }

        float scale = radius * 2f / width;
        transform.localScale = new Vector3(scale, scale, 1f);
        Paint(false);
    }

    private void Paint(bool fading)
    {
        if (graphic == null)
        {
            return;
        }

        float alpha = 0.32f;

        if (!fading)
        {
            float remain = duration - elapsed;

            if (remain < 0.4f)
            {
                alpha *= Mathf.Clamp01(remain / 0.4f);
            }
        }
        else
        {
            alpha *= Mathf.Clamp01(fade / 0.25f);
        }

        graphic.color = new Color(0.55f, 0.36f, 0.16f, alpha);
    }

    private static void EnsureScan(int count)
    {
        if (scan.Length >= count)
        {
            return;
        }

        scan = new EnemyHealth[count];
    }

    private static bool Listed(EnemyHealth[] buffer, int count, EnemyHealth health)
    {
        for (int i = 0; i < count; i++)
        {
            if (buffer[i] == health)
            {
                return true;
            }
        }

        return false;
    }

    private static Sprite ring;

    private static Sprite Ring()
    {
        if (ring != null)
        {
            return ring;
        }

        const int size = 48;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float middle = (size - 1) * 0.5f;
        float outer = size * 0.46f;
        float inner = outer * 0.78f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - middle;
                float dy = y - middle;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = distance <= outer && distance >= inner ? 1f : 0f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        ring = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size
        );
        return ring;
    }
}

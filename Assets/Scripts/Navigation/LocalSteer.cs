using System;

public struct MotionLimits
{
    public float MaxSpeed;
    public float Acceleration;
    public float ArrivalRadius;
    public float SeparationSpeed;
    public float CohesionSpeed;
    public float AlignmentSpeed;
    public float FlockRadius;
}

public struct NeighborSample
{
    public float X;
    public float Y;
    public int Identity;
    public int GroupId;
    public float VelocityX;
    public float VelocityY;
}

public struct SteerResult
{
    public float X;
    public float Y;
    public float VelocityX;
    public float VelocityY;
    public float SeparationX;
    public float SeparationY;
    public bool Moved;
}

public static class LocalSteer
{
    public static void Accelerate(
        float velocityX,
        float velocityY,
        float desiredX,
        float desiredY,
        float maxSpeed,
        float acceleration,
        float deltaTime,
        out float nextX,
        out float nextY)
    {
        if (!IsFinite(velocityX) || !IsFinite(velocityY))
        {
            velocityX = 0f;
            velocityY = 0f;
        }

        if (deltaTime <= 0f || !IsFinite(deltaTime))
        {
            Clamp(velocityX, velocityY, maxSpeed, out nextX, out nextY);
            return;
        }

        if (!IsFinite(desiredX) || !IsFinite(desiredY))
        {
            desiredX = 0f;
            desiredY = 0f;
        }

        float changeX = desiredX - velocityX;
        float changeY = desiredY - velocityY;
        float change = (float)Math.Sqrt(changeX * changeX + changeY * changeY);
        float maxChange = Math.Max(0f, acceleration) * deltaTime;

        if (change > maxChange && change > 0.000001f)
        {
            float scale = maxChange / change;
            changeX *= scale;
            changeY *= scale;
        }

        Clamp(
            velocityX + changeX,
            velocityY + changeY,
            maxSpeed,
            out nextX,
            out nextY);
    }

    public static void Clamp(
        float velocityX,
        float velocityY,
        float maxSpeed,
        out float nextX,
        out float nextY)
    {
        if (!IsFinite(velocityX) || !IsFinite(velocityY))
        {
            nextX = 0f;
            nextY = 0f;
            return;
        }

        if (maxSpeed <= 0f || !IsFinite(maxSpeed))
        {
            nextX = 0f;
            nextY = 0f;
            return;
        }

        float speed = (float)Math.Sqrt(velocityX * velocityX + velocityY * velocityY);

        if (speed <= maxSpeed || speed <= 0.000001f)
        {
            nextX = velocityX;
            nextY = velocityY;
            return;
        }

        float scale = maxSpeed / speed;
        nextX = velocityX * scale;
        nextY = velocityY * scale;
    }

    public static void Separate(
        float x,
        float y,
        int identity,
        NeighborSample[] neighbors,
        int neighborCount,
        float radius,
        float maxSpeed,
        out float pushX,
        out float pushY)
    {
        pushX = 0f;
        pushY = 0f;

        if (neighbors == null || neighborCount <= 0 || radius <= 0f)
        {
            return;
        }

        float radiusSq = radius * radius;
        int count = neighborCount;

        if (count > neighbors.Length)
        {
            count = neighbors.Length;
        }

        for (int i = 0; i < count; i++)
        {
            NeighborSample neighbor = neighbors[i];

            if (!IsFinite(neighbor.X) || !IsFinite(neighbor.Y))
            {
                continue;
            }

            float offsetX = x - neighbor.X;
            float offsetY = y - neighbor.Y;
            float distanceSq = offsetX * offsetX + offsetY * offsetY;

            if (distanceSq > radiusSq)
            {
                continue;
            }

            float distance;
            float directionX;
            float directionY;

            if (distanceSq <= 0.0000001f)
            {
                StableDirection(identity, neighbor.Identity, out directionX, out directionY);
                distance = 0.001f;
            }
            else
            {
                distance = (float)Math.Sqrt(distanceSq);
                directionX = offsetX / distance;
                directionY = offsetY / distance;
            }

            float strength = (radius - distance) / radius;

            if (strength < 0f)
            {
                strength = 0f;
            }

            if (identity > neighbor.Identity)
            {
                strength *= 0.35f;
            }

            pushX += directionX * strength;
            pushY += directionY * strength;
        }

        Clamp(pushX, pushY, maxSpeed, out pushX, out pushY);
    }

    public static SteerResult Step(
        NavigationGrid grid,
        float originX,
        float originY,
        float cellSize,
        float x,
        float y,
        float velocityX,
        float velocityY,
        int identity,
        float targetX,
        float targetY,
        bool finalLeg,
        float deltaTime,
        float agentRadius,
        MotionLimits limits,
        NeighborSample[] neighbors,
        int neighborCount,
        float separationScale,
        int groupId = 0,
        float cohesionScale = 0f,
        float scatterX = 0f,
        float scatterY = 0f,
        bool openAir = false)
    {
        var result = new SteerResult
        {
            X = x,
            Y = y,
            VelocityX = 0f,
            VelocityY = 0f
        };

        if (!IsFinite(x) || !IsFinite(y))
        {
            return result;
        }

        float desiredX = 0f;
        float desiredY = 0f;
        float offsetX = targetX - x;
        float offsetY = targetY - y;
        float distance = (float)Math.Sqrt(offsetX * offsetX + offsetY * offsetY);

        if (distance > 0.0001f && limits.MaxSpeed > 0f)
        {
            float desiredSpeed = limits.MaxSpeed;

            if (finalLeg &&
                limits.ArrivalRadius > 0f &&
                distance < limits.ArrivalRadius)
            {
                desiredSpeed *= distance / limits.ArrivalRadius;
            }

            desiredX = offsetX / distance * desiredSpeed;
            desiredY = offsetY / distance * desiredSpeed;
        }

        float scale = separationScale;

        if (!IsFinite(scale) || scale < 0f)
        {
            scale = 0f;
        }

        if (scale > 1f)
        {
            scale = 1f;
        }

        Separate(
            x,
            y,
            identity,
            neighbors,
            neighborCount,
            agentRadius > 0f ? agentRadius * 2.5f : 0f,
            limits.SeparationSpeed * scale,
            out float pushX,
            out float pushY);
        result.SeparationX = pushX;
        result.SeparationY = pushY;
        float flockScale = cohesionScale;

        if (!IsFinite(flockScale) || flockScale < 0f)
        {
            flockScale = 0f;
        }

        if (flockScale > 1f)
        {
            flockScale = 1f;
        }

        Flock(
            x,
            y,
            velocityX,
            velocityY,
            groupId,
            neighbors,
            neighborCount,
            limits.FlockRadius,
            limits.CohesionSpeed * flockScale,
            limits.AlignmentSpeed * flockScale,
            out float cohesionX,
            out float cohesionY,
            out float alignX,
            out float alignY);

        if (!IsFinite(scatterX) || !IsFinite(scatterY))
        {
            scatterX = 0f;
            scatterY = 0f;
        }

        float extraX = cohesionX + alignX + scatterX;
        float extraY = cohesionY + alignY + scatterY;
        float scatterSpeed = (float)Math.Sqrt(scatterX * scatterX + scatterY * scatterY);
        float pushBudget =
            limits.SeparationSpeed * scale +
            limits.CohesionSpeed * flockScale +
            limits.AlignmentSpeed * flockScale +
            scatterSpeed;

        float fallbackVelocityX = 0f;
        float fallbackVelocityY = 0f;

        for (int attempt = 0; attempt < 3; attempt++)
        {
            float lateral = attempt == 0 ? 1f : attempt == 1 ? 0.35f : 0f;
            Blend(
                desiredX,
                desiredY,
                pushX + extraX,
                pushY + extraY,
                limits.MaxSpeed,
                pushBudget * lateral,
                out float blendedX,
                out float blendedY);
            Accelerate(
                velocityX,
                velocityY,
                blendedX,
                blendedY,
                limits.MaxSpeed,
                limits.Acceleration,
                deltaTime,
                out float nextVelocityX,
                out float nextVelocityY);

            if (deltaTime <= 0f)
            {
                result.VelocityX = nextVelocityX;
                result.VelocityY = nextVelocityY;
                return result;
            }

            float nextX = x + nextVelocityX * deltaTime;
            float nextY = y + nextVelocityY * deltaTime;

            bool allowed = openAir
                ? IsFinite(nextX) && IsFinite(nextY)
                : CorridorMotion.SegmentAllowed(
                    grid,
                    originX,
                    originY,
                    cellSize,
                    x,
                    y,
                    nextX,
                    nextY,
                    agentRadius);

            fallbackVelocityX = nextVelocityX;
            fallbackVelocityY = nextVelocityY;

            if (allowed)
            {
                result.X = nextX;
                result.Y = nextY;
                result.VelocityX = nextVelocityX;
                result.VelocityY = nextVelocityY;
                result.Moved =
                    Math.Abs(nextX - x) + Math.Abs(nextY - y) > 0.0001f;
                return result;
            }
        }

        for (int shrink = 1; shrink <= 4; shrink++)
        {
            float fraction = 1f / (1 << shrink);
            float nextX = x + fallbackVelocityX * deltaTime * fraction;
            float nextY = y + fallbackVelocityY * deltaTime * fraction;
            bool allowed = openAir
                ? IsFinite(nextX) && IsFinite(nextY)
                : CorridorMotion.SegmentAllowed(
                    grid,
                    originX,
                    originY,
                    cellSize,
                    x,
                    y,
                    nextX,
                    nextY,
                    agentRadius);

            if (!allowed)
            {
                continue;
            }

            result.X = nextX;
            result.Y = nextY;
            result.VelocityX = fallbackVelocityX * fraction;
            result.VelocityY = fallbackVelocityY * fraction;
            result.Moved =
                Math.Abs(nextX - x) + Math.Abs(nextY - y) > 0.0001f;
            return result;
        }

        Clamp(velocityX, velocityY, limits.MaxSpeed, out result.VelocityX, out result.VelocityY);
        return result;
    }

    public static void Flock(
        float x,
        float y,
        float velocityX,
        float velocityY,
        int groupId,
        NeighborSample[] neighbors,
        int neighborCount,
        float radius,
        float cohesionLimit,
        float alignmentLimit,
        out float cohesionX,
        out float cohesionY,
        out float alignX,
        out float alignY)
    {
        cohesionX = 0f;
        cohesionY = 0f;
        alignX = 0f;
        alignY = 0f;

        if (groupId == 0 ||
            neighbors == null ||
            neighborCount <= 0 ||
            radius <= 0f ||
            !IsFinite(x) ||
            !IsFinite(y))
        {
            return;
        }

        float radiusSq = radius * radius;
        int count = 0;
        float sumX = 0f;
        float sumY = 0f;
        float sumVelocityX = 0f;
        float sumVelocityY = 0f;
        int limit = neighborCount < neighbors.Length ? neighborCount : neighbors.Length;

        for (int i = 0; i < limit; i++)
        {
            NeighborSample neighbor = neighbors[i];

            if (neighbor.GroupId != groupId)
            {
                continue;
            }

            float offsetX = neighbor.X - x;
            float offsetY = neighbor.Y - y;
            float distanceSq = offsetX * offsetX + offsetY * offsetY;

            if (distanceSq > radiusSq || !IsFinite(distanceSq))
            {
                continue;
            }

            if (!IsFinite(neighbor.VelocityX) || !IsFinite(neighbor.VelocityY))
            {
                continue;
            }

            sumX += neighbor.X;
            sumY += neighbor.Y;
            sumVelocityX += neighbor.VelocityX;
            sumVelocityY += neighbor.VelocityY;
            count++;
        }

        if (count == 0)
        {
            return;
        }

        float inverse = 1f / count;
        float centerX = sumX * inverse;
        float centerY = sumY * inverse;
        float towardX = centerX - x;
        float towardY = centerY - y;
        float distance = (float)Math.Sqrt(towardX * towardX + towardY * towardY);

        if (distance > 0.0001f && cohesionLimit > 0f)
        {
            float strength = distance / radius;

            if (strength > 1f)
            {
                strength = 1f;
            }

            cohesionX = towardX / distance * cohesionLimit * strength;
            cohesionY = towardY / distance * cohesionLimit * strength;
        }

        Clamp(
            sumVelocityX * inverse - velocityX,
            sumVelocityY * inverse - velocityY,
            alignmentLimit,
            out alignX,
            out alignY);
    }

    public static void AdvanceFlockResponse(
        float scatterTime,
        float cohesion,
        float deltaTime,
        float recoverDuration,
        out float nextTime,
        out float nextCohesion)
    {
        if (!IsFinite(scatterTime) || scatterTime < 0f)
        {
            scatterTime = 0f;
        }

        if (!IsFinite(cohesion))
        {
            cohesion = 1f;
        }

        if (cohesion < 0f)
        {
            cohesion = 0f;
        }

        if (cohesion > 1f)
        {
            cohesion = 1f;
        }

        if (deltaTime <= 0f || !IsFinite(deltaTime))
        {
            nextTime = scatterTime;
            nextCohesion = cohesion;
            return;
        }

        nextTime = scatterTime - deltaTime;

        if (nextTime < 0f)
        {
            nextTime = 0f;
        }

        float recover = recoverDuration > 0.0001f ? recoverDuration : 0.0001f;
        nextCohesion = cohesion + deltaTime / recover;

        if (nextCohesion > 1f)
        {
            nextCohesion = 1f;
        }
    }

    public static void Blend(
        float desiredX,
        float desiredY,
        float pushX,
        float pushY,
        float maxSpeed,
        float pushLimit,
        out float blendedX,
        out float blendedY)
    {
        Clamp(pushX, pushY, pushLimit, out pushX, out pushY);
        float desiredSpeed = (float)Math.Sqrt(desiredX * desiredX + desiredY * desiredY);

        if (desiredSpeed <= 0.0001f)
        {
            Clamp(pushX, pushY, maxSpeed, out blendedX, out blendedY);
            return;
        }

        float directionX = desiredX / desiredSpeed;
        float directionY = desiredY / desiredSpeed;
        float along = pushX * directionX + pushY * directionY;

        if (along < 0f)
        {
            pushX -= directionX * along;
            pushY -= directionY * along;
        }

        blendedX = desiredX + pushX;
        blendedY = desiredY + pushY;
        float minimumForward = desiredSpeed * 0.8f;
        float forward = blendedX * directionX + blendedY * directionY;

        if (forward < minimumForward)
        {
            float restore = minimumForward - forward;
            blendedX += directionX * restore;
            blendedY += directionY * restore;
        }

        Clamp(blendedX, blendedY, maxSpeed, out blendedX, out blendedY);
    }

    public static void StableDirection(
        int identity,
        int other,
        out float x,
        out float y)
    {
        int salt = identity * 73856093 ^ other * 19349663;
        float angle = (salt & 4095) * (6.2831853f / 4096f);
        x = (float)Math.Cos(angle);
        y = (float)Math.Sin(angle);

        if (!IsFinite(x) || !IsFinite(y))
        {
            x = 1f;
            y = 0f;
        }
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

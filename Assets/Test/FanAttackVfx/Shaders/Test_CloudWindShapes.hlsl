#ifndef CHEON_TEST_CLOUD_WIND_SHAPES_INCLUDED
#define CHEON_TEST_CLOUD_WIND_SHAPES_INCLUDED

float CloudWindSegmentDistance(float2 p, float2 a, float2 b)
{
    float2 direction = b - a;
    float progress = saturate(dot(p - a, direction) / max(dot(direction, direction), 0.000001));
    return length(p - a - direction * progress);
}

float CloudWindBodyDistance(float2 p)
{
    float shape = length(p - float2(-0.055, 0.025)) - 0.105;
    shape = min(shape, length(p - float2(0.015, 0.095)) - 0.105);
    shape = min(shape, length(p - float2(0.105, 0.065)) - 0.082);
    shape = min(shape, length(p - float2(0.105, -0.035)) - 0.078);
    shape = min(shape, length(p - float2(0.015, -0.065)) - 0.096);
    shape = min(shape, length(p - float2(-0.095, -0.055)) - 0.06);
    return shape;
}

float CloudWindSpiralDistance(float2 p)
{
    // 소용돌이에서 먼 픽셀은 선분 계산을 생략한다.
    [branch]
    if (dot(p, p) > 0.065) return 1;
    float distanceToSpiral = 1;
    float2 previous = float2(-0.165, -0.105);
    [unroll]
    for (int index = 0; index <= 20; index++)
    {
        float progress = index / 20.0;
        float angle = -2.55 - progress * 8.15;
        float radius = lerp(0.155, 0.008, progress);
        float2 next = float2(cos(angle), sin(angle)) * radius;
        distanceToSpiral = min(distanceToSpiral, CloudWindSegmentDistance(p, previous, next));
        previous = next;
    }
    return distanceToSpiral;
}

float CloudWindTailDistance(float2 p, float center, float width, float phase, float start, float end)
{
    float progress = saturate((p.x - start) / (end - start));
    float taper = pow(saturate(sin(progress * PI)), 0.65);
    float wave = sin(progress * PI) * sin(progress * 4 + phase) * 0.07;
    return max(abs(p.y - center - wave) - width * taper, max(start - p.x, p.x - end));
}

#endif

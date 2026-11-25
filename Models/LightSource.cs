namespace Models;

using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Reprezentuje punktowe źródło światła poruszające się po zamkniętej krzywej Béziera
/// na poziomej płaszczyźnie z = HeightZ.
/// </summary>
public class LightSource {
    private const float DefaultHeightFactor = 1.35f;
    
    // Część okrążenia na sekundę
    private const float DefaultSpeed = 0.15f;

    /// <summary>
    /// Domyślna, gładka pętla punktów kontrolnych wyrażona jako ułamek BoundingRadius sceny.
    /// </summary>
    private static readonly Vector2[] DefaultNormalizedPoints = {
        new( 0.55f,  0.00f),
        new( 0.55f,  0.85f),
        new(-0.85f,  0.80f),
        new(-0.85f, -0.80f),
        new( 0.55f, -0.85f)
    };

    private readonly List<Vector2> _controlPoints = new();
    private float _referenceRadius;
    private float _t;

    /// <summary>
    /// Aktualna pozycja źródła światła w przestrzeni 3D.
    /// </summary>
    public Vector3 Position { get; private set; }

    /// <summary>
    /// Wysokość płaszczyzny (z = const), po której porusza się światło.
    /// </summary>
    public float HeightZ { get; private set; }

    /// <summary>
    /// Czy animacja światła jest aktualnie odtwarzana.
    /// </summary>
    public bool IsAnimating { get; private set; }

    /// <summary>
    /// Punkty kontrolne zamkniętej krzywej Béziera na płaszczyźnie XY.
    /// </summary>
    public IReadOnlyList<Vector2> ControlPoints => _controlPoints;
    
    /// <summary>
    /// Inicjalizuje źródło światła z domyślną pętlą dopasowaną do podanego promienia sceny.
    /// </summary>
    public LightSource(float boundingRadius) {
        ResetTrajectory(boundingRadius);
    }

    /// <summary>
    /// Odtwarza domyślną trajektorię i wysokość światła dla podanego promienia ograniczającego sceny.
    /// </summary>
    internal void ResetTrajectory(float boundingRadius) {
        _referenceRadius = MathF.Max(1f, boundingRadius);
        _controlPoints.Clear();

        foreach (var pt in DefaultNormalizedPoints) {
            _controlPoints.Add(pt * _referenceRadius);
        }

        HeightZ = _referenceRadius * DefaultHeightFactor;
        _t = 0f;
        UpdatePosition();
    }

    /// <summary>
    /// Proporcjonalnie przeskalowuje bieżącą trajektorię oraz wysokość Z do nowego promienia ograniczającego sceny.
    /// </summary>
    internal void ScaleToRadius(float newBoundingRadius) {
        float targetRadius = MathF.Max(1f, newBoundingRadius);
        if (MathF.Abs(targetRadius - _referenceRadius) < 1e-5f)
            return;

        float factor = targetRadius / _referenceRadius;
        for (int i = 0; i < _controlPoints.Count; i++) {
            _controlPoints[i] *= factor;
        }

        HeightZ *= factor;
        _referenceRadius = targetRadius;
        UpdatePosition();
    }

    /// <summary>
    /// Wyznacza punkt na zamkniętej krzywej Béziera dla parametru t z zakresu [0, 1]
    /// </summary>
    public Vector2 EvaluateTrajectory(float t) {
        int count = _controlPoints.Count;
        if (count == 0)
            return Vector2.Zero;
        if (count == 1)
            return _controlPoints[0];

        t = Math.Clamp(t, 0f, 1f);

        // Dodanie P0 jako ostatni punkt
        int m = count + 1;
        Span<Vector2> pts = m <= 32 ? stackalloc Vector2[m] : new Vector2[m];

        for (int i = 0; i < count; i++) {
            pts[i] = _controlPoints[i];
        }
        pts[count] = _controlPoints[0];

        // Algorytm de Casteljau
        for (int level = 1; level < m; level++) {
            int limit = m - level;
            for (int i = 0; i < limit; i++) {
                pts[i] = Vector2.Lerp(pts[i], pts[i + 1], t);
            }
        }

        return pts[0];
    }

    internal void SetHeight(float z) {
        HeightZ = z;
        Position = new Vector3(Position.X, Position.Y, HeightZ);
    }

    internal void SetAnimating(bool isPlaying) {
        IsAnimating = isPlaying;
    }

    internal void Update(float deltaTime) {
        if (!IsAnimating || deltaTime <= 0f)
            return;

        _t += deltaTime * DefaultSpeed;
        if (_t >= 1f)
            _t -= MathF.Floor(_t);

        UpdatePosition();
    }

    internal int TryPickControlPoint(float x, float y, float hitRadius) {
        if (hitRadius <= 0f)
            return -1;

        Vector2 target = new(x, y);
        float bestDistSq = hitRadius * hitRadius;
        int bestIndex = -1;

        for (int i = 0; i < _controlPoints.Count; i++) {
            float distSq = Vector2.DistanceSquared(_controlPoints[i], target);
            if (distSq <= bestDistSq) {
                bestDistSq = distSq;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    internal void MoveControlPoint(int index, float x, float y) {
        if ((uint)index >= (uint)_controlPoints.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _controlPoints[index] = new Vector2(x, y);
        UpdatePosition();
    }

    private void UpdatePosition() {
        Vector2 xy = EvaluateTrajectory(_t);
        Position = new Vector3(xy.X, xy.Y, HeightZ);
    }
}
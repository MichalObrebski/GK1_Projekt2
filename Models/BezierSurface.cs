namespace Models;

using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Reprezentuje dwusześcienny płat Béziera zdefiniowany przez siatkę 4x4 punktów kontrolnych.
/// </summary>
public class BezierSurface {
    private readonly Vector3[,] _baseControlPoints = new Vector3[4, 4];
    private Vertex[] _vertices = Array.Empty<Vertex>();
    private Matrix4x4 _currentRotation = Matrix4x4.Identity;

    /// <summary>
    /// Punkty kontrolne (4x4) po zastosowaniu bieżącego obrotu sceny.
    /// </summary>
    public Vector3[,] ControlPoints { get; } = new Vector3[4, 4];

    /// <summary>
    /// Lista trójkątów wygenerowanych dla aktualnej gęstości podziału N.
    /// </summary>
    public List<Triangle> Triangles { get; } = new();

    public BezierSurface(Vector3[,] controlPoints) {
        if (controlPoints == null || controlPoints.GetLength(0) != 4 || controlPoints.GetLength(1) != 4)
            throw new ArgumentException("Bezier surface requires a 4x4 grid of control points.", nameof(controlPoints));

        for (int i = 0; i < 4; i++) {
            for (int j = 0; j < 4; j++) {
                _baseControlPoints[i, j] = controlPoints[i, j];
                ControlPoints[i, j] = controlPoints[i, j];
            }
        }
    }

    /// <summary>
    /// Generuje siatkę (N+1)x(N+1) wierzchołków oraz 2*N*N trójkątów za pomocą wielomianów Bernsteina,
    /// a następnie nakłada na nią bieżący obrót sceny.
    /// </summary>
    /// <param name="n">Liczba podziałów wzdłuż osi u oraz v (np. od 1 do 50).</param>
    public void Triangulate(int n) {
        if (n < 1)
            throw new ArgumentOutOfRangeException(nameof(n), "Triangulation resolution must be at least 1.");

        int gridSize = n + 1;
        _vertices = new Vertex[gridSize * gridSize];

        Span<float> bu3 = stackalloc float[4];
        Span<float> bv3 = stackalloc float[4];
        Span<float> bu2 = stackalloc float[3];
        Span<float> bv2 = stackalloc float[3];

        float step = 1f / n;

        // Próbkowanie powierzchni w punktach (u, v)
        for (int row = 0; row < gridSize; row++) {
            float u = row == n ? 1f : row * step;
            EvalBernstein3(u, bu3);
            EvalBernstein2(u, bu2);

            for (int col = 0; col < gridSize; col++) {
                float v = col == n ? 1f : col * step;
                EvalBernstein3(v, bv3);
                EvalBernstein2(v, bv2);

                Vector3 pos = EvaluatePosition(bu3, bv3);
                Vector3 pu = EvaluateDerivativeU(bu2, bv3);
                Vector3 pv = EvaluateDerivativeV(bu3, bv2);

                Vector3 puNorm = pu.LengthSquared() > 1e-8f ? Vector3.Normalize(pu) : Vector3.UnitX;
                Vector3 pvNorm = pv.LengthSquared() > 1e-8f ? Vector3.Normalize(pv) : Vector3.UnitY;

                Vector3 normal = Vector3.Cross(pu, pv);
                normal = normal.LengthSquared() > 1e-8f ? Vector3.Normalize(normal) : Vector3.UnitZ;

                int idx = row * gridSize + col;
                _vertices[idx] = new Vertex(
                    u: u,
                    v: v,
                    position: pos,
                    normal: normal,
                    pu: puNorm,
                    pv: pvNorm
                );
            }
        }

        //Budowa listy 2*N*N trójkątów
        Triangles.Clear();
        Triangles.Capacity = 2 * n * n;

        for (int row = 0; row < n; row++) {
            for (int col = 0; col < n; col++) {
                int i00 = row * gridSize + col;
                int i10 = (row + 1) * gridSize + col;
                int i01 = row * gridSize + (col + 1);
                int i11 = (row + 1) * gridSize + (col + 1);

                Triangles.Add(new Triangle(_vertices[i00], _vertices[i10], _vertices[i11]));
                Triangles.Add(new Triangle(_vertices[i00], _vertices[i11], _vertices[i01]));
            }
        }

        ApplyRotation(_currentRotation);
    }

    /// <summary>
    /// Obraca wygenerowane wierzchołki (pozycję, normalną, styczne) oraz punkty kontrolne
    /// bez ponownego przeliczania wielomianów Bernsteina.
    /// </summary>
    public void ApplyRotation(Matrix4x4 rotation) {
        _currentRotation = rotation;

        for (int i = 0; i < 4; i++) {
            for (int j = 0; j < 4; j++) {
                ControlPoints[i, j] = Vector3.Transform(_baseControlPoints[i, j], rotation);
            }
        }

        for (int i = 0; i < _vertices.Length; i++) {
            Vertex v = _vertices[i];

            v.Position = Vector3.Transform(v.BasePosition, rotation);

            Vector3 normal = Vector3.TransformNormal(v.BaseNormal, rotation);
            v.Normal = normal.LengthSquared() > 1e-8f ? Vector3.Normalize(normal) : Vector3.UnitZ;

            Vector3 pu = Vector3.TransformNormal(v.BasePu, rotation);
            v.Pu = pu.LengthSquared() > 1e-8f ? Vector3.Normalize(pu) : Vector3.UnitX;

            Vector3 pv = Vector3.TransformNormal(v.BasePv, rotation);
            v.Pv = pv.LengthSquared() > 1e-8f ? Vector3.Normalize(pv) : Vector3.UnitY;
        }
    }

    // ==========================================
    // MATEMATYKA WIELOMIANÓW BERNSTEINA
    // ==========================================

    private static void EvalBernstein3(float t, Span<float> b) {
        float it = 1f - t;
        float it2 = it * it;
        float t2 = t * t;

        b[0] = it2 * it;
        b[1] = 3f * t * it2;
        b[2] = 3f * t2 * it;
        b[3] = t2 * t;
    }

    private static void EvalBernstein2(float t, Span<float> b) {
        float it = 1f - t;
        b[0] = it * it;
        b[1] = 2f * t * it;
        b[2] = t * t;
    }

    private Vector3 EvaluatePosition(ReadOnlySpan<float> bu3, ReadOnlySpan<float> bv3) {
        Vector3 sum = Vector3.Zero;
        for (int i = 0; i < 4; i++) {
            Vector3 rowSum = Vector3.Zero;
            for (int j = 0; j < 4; j++) {
                rowSum += _baseControlPoints[i, j] * bv3[j];
            }
            sum += rowSum * bu3[i];
        }
        return sum;
    }

    private Vector3 EvaluateDerivativeU(ReadOnlySpan<float> bu2, ReadOnlySpan<float> bv3) {
        Vector3 sum = Vector3.Zero;
        for (int i = 0; i < 3; i++) {
            Vector3 rowSum = Vector3.Zero;
            for (int j = 0; j < 4; j++) {
                rowSum += (_baseControlPoints[i + 1, j] - _baseControlPoints[i, j]) * bv3[j];
            }
            sum += rowSum * bu2[i];
        }
        return 3f * sum;
    }

    private Vector3 EvaluateDerivativeV(ReadOnlySpan<float> bu3, ReadOnlySpan<float> bv2) {
        Vector3 sum = Vector3.Zero;
        for (int i = 0; i < 4; i++) {
            Vector3 rowSum = Vector3.Zero;
            for (int j = 0; j < 3; j++) {
                rowSum += (_baseControlPoints[i, j + 1] - _baseControlPoints[i, j]) * bv2[j];
            }
            sum += rowSum * bu3[i];
        }
        return 3f * sum;
    }
}
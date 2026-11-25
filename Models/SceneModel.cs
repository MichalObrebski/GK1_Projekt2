namespace Models;

using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Główny model sceny 3D przechowujący powierzchnie Béziera, orientację sceny
/// oraz źródło światła wraz z jego trajektorią.
/// </summary>
public class SceneModel {
    // ==========================================
    // STAN WEWNĘTRZNY
    // ==========================================

    private readonly List<BezierSurface> _surfaces = new();
    private int _gridResolution;
    private float _boundingRadius;
    private Quaternion _orientation = Quaternion.Identity;
    private Matrix4x4 _rotationMatrix = Matrix4x4.Identity;

    // ==========================================
    // DANE WYJŚCIOWE
    // ==========================================

    /// <summary>
    /// Aktualna gęstość podziału siatki (N).
    /// </summary>
    public int GridResolution => _gridResolution;

    /// <summary>
    /// Maksymalna odległość od początku układu współrzędnych do dowolnego punktu kontrolnego sceny.
    /// </summary>
    public float BoundingRadius => _boundingRadius;

    /// <summary>
    /// Lista wczytanych powierzchni Béziera.
    /// </summary>
    public IReadOnlyList<BezierSurface> Surfaces => _surfaces;

    /// <summary>
    /// Obiekt źródła światła (pozycja 3D i punkty kontrolne jego trajektorii).
    /// </summary>
    public LightSource Light { get; }

    // ==========================================
    // KONSTRUKTOR
    // ==========================================

    /// <summary>
    /// Inicjalizuje pustą scenę z domyślną trajektorią światła.
    /// </summary>
    public SceneModel() {
        Light = new LightSource(_boundingRadius);
    }

    // ==========================================
    // ZARZĄDZANIE POWIERZCHNIAMI I SIATKĄ
    // ==========================================

    /// <summary>
    /// Dodaje nową powierzchnię Béziera na podstawie macierzy 4x4 punktów kontrolnych
    /// i przeprowadza jej triangulację oraz obrót.
    /// </summary>
    /// <param name="controlPoints">Tablica 4x4 punktów kontrolnych (x, y, z).</param>
    public void AddSurface(Vector3[,] controlPoints) {
        var surface = new BezierSurface(controlPoints);
        surface.Triangulate(_gridResolution);
        surface.ApplyRotation(_rotationMatrix);

        float surfaceRadius = CalculateBoundingRadius(controlPoints);
        _boundingRadius = MathF.Max(_boundingRadius, surfaceRadius);

        _surfaces.Add(surface);
        Light.ScaleToRadius(_boundingRadius);
    }

    /// <summary>
    /// Podmienia wszystkie powierzchnie na scenie nowym zestawem.
    /// </summary>
    /// <param name="surfacesControlPoints">Kolekcja tablic 4x4 punktów kontrolnych.</param>
    public void SetSurfaces(IEnumerable<Vector3[,]> surfacesControlPoints) {
        ArgumentNullException.ThrowIfNull(surfacesControlPoints);

        _surfaces.Clear();
        _boundingRadius = 0f;

        foreach (var controlPoints in surfacesControlPoints) {
            AddSurface(controlPoints);
        }

        Light.ResetTrajectory(_boundingRadius);
    }

    /// <summary>
    /// Zmienia gęstość podziału siatki i przelicza triangulację wszystkich powierzchni.
    /// </summary>
    /// <param name="resolution">Liczba podziałów 1-50.</param>
    /// <exception cref="ArgumentOutOfRangeException">Jeśli resolution nie mieści się w zakresie 1-50.</exception>
    public void SetGridResolution(int resolution) {
        if (resolution < 1 || resolution > 50)
            throw new ArgumentOutOfRangeException(nameof(resolution), "Grid resolution must be between 1 and 50.");

        if (resolution == _gridResolution)
            return;

        _gridResolution = resolution;

        foreach (var surface in _surfaces) {
            surface.Triangulate(_gridResolution);
        }
    }

    // ==========================================
    // TRANSFORMACJE SCENY
    // ==========================================

    /// <summary>
    /// Składa przyrost obrotu z aktualną orientacją sceny
    /// i przelicza obrócone współrzędne wszystkich powierzchni.
    /// </summary>
    /// <param name="deltaRotation">Macierz przyrostu obrotu.</param>
    public void ApplyRotation(Matrix4x4 deltaRotation) {
        Quaternion deltaOrientation = Quaternion.CreateFromRotationMatrix(deltaRotation);
        _orientation = Quaternion.Normalize(Quaternion.Concatenate(_orientation, deltaOrientation));
        _rotationMatrix = Matrix4x4.CreateFromQuaternion(_orientation);

        foreach (var surface in _surfaces) {
            surface.ApplyRotation(_rotationMatrix);
        }
    }

    /// <summary>
    /// Resetuje obrót sceny do macierzy jednostkowej.
    /// </summary>
    public void ResetRotation() {
        _orientation = Quaternion.Identity;
        _rotationMatrix = Matrix4x4.Identity;

        foreach (var surface in _surfaces) {
            surface.ApplyRotation(_rotationMatrix);
        }
    }

    // ==========================================
    // ŚWIATŁO I JEGO TRAJEKTORIA
    // ==========================================

    /// <summary>
    /// Ustawia wysokość płaszczyzny, po której porusza się światło.
    /// </summary>
    /// <param name="z">Współrzędna Z płaszczyzny światła.</param>
    public void SetLightHeight(float z) {
        Light.SetHeight(z);
    }

    /// <summary>
    /// Włącza lub wstrzymuje animację źródła światła.
    /// </summary>
    /// <param name="isPlaying">True, jeśli animacja ma być aktywna.</param>
    public void ToggleLightAnimation(bool isPlaying) {
        Light.SetAnimating(isPlaying);
    }

    /// <summary>
    /// Przesuwa światło wzdłuż krzywej Béziera o krok czasowy.
    /// </summary>
    /// <param name="deltaTime">Czas od ostatniej klatki w sekundach.</param>
    public void UpdateLightAnimation(float deltaTime) {
        Light.Update(deltaTime);
    }

    /// <summary>
    /// Sprawdza, czy podany punkt na płaszczyźnie XY trafia w punkt kontrolny krzywej światła.
    /// </summary>
    /// <param name="x">Współrzędna X w przestrzeni sceny.</param>
    /// <param name="y">Współrzędna Y w przestrzeni sceny.</param>
    /// <param name="hitRadius">Promień trafienia.</param>
    /// <returns>Indeks punktu kontrolnego lub -1, jeśli nie trafiono.</returns>
    public int TryPickLightControlPoint(float x, float y, float hitRadius) {
        return Light.TryPickControlPoint(x, y, hitRadius);
    }

    /// <summary>
    /// Przesuwa wybrany punkt kontrolny zamkniętej krzywej Béziera źródła światła.
    /// </summary>
    /// <param name="index">Indeks punktu kontrolnego.</param>
    /// <param name="x">Nowa współrzędna X.</param>
    /// <param name="y">Nowa współrzędna Y.</param>
    public void MoveLightControlPoint(int index, float x, float y) {
        Light.MoveControlPoint(index, x, y);
    }

    // ==========================================
    // METODY POMOCNICZE
    // ==========================================

    private static float CalculateBoundingRadius(Vector3[,] controlPoints) {
        float maxDistSq = 0f;
        int rows = controlPoints.GetLength(0);
        int cols = controlPoints.GetLength(1);

        for (int i = 0; i < rows; i++) {
            for (int j = 0; j < cols; j++) {
                float distSq = controlPoints[i, j].LengthSquared();
                if (distSq > maxDistSq)
                    maxDistSq = distSq;
            }
        }

        return MathF.Sqrt(maxDistSq);
    }
}
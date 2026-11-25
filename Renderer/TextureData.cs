namespace Renderer;

using System;
using System.Numerics;

/// <summary>
/// Przechowuje wczytaną teksturę lub mapę wektorów normalnych w formacie BGRA32
/// oraz udostępnia bezpieczne próbkowanie dla współrzędnych (u, v) z zakresu [0, 1].
/// </summary>
public class TextureData {
    public int Width { get; }
    public int Height { get; }
    private readonly byte[] _pixels;

    /// <summary>
    /// Tworzy obiekt tekstury z podanymi wymiarami i buforem pikseli w formacie BGRA32.
    /// </summary>
    /// <param name="width">Szerokość tekstury w pikselach.</param> 
    /// <param name="height">Wysokość tekstury w pikselach.</param>
    /// <param name="bgraPixels">Bufor pikseli w formacie BGRA32 (B, G, R, A) o rozmiarze width * height * 4.</param>
    /// <exception cref="ArgumentOutOfRangeException">Jeśli width lub height są mniejsze lub równe 0.</exception>
    /// <exception cref="ArgumentException">Jeśli bgraPixels jest null lub ma nieprawidłowy rozmiar.</exception>
    public TextureData(int width, int height, byte[] bgraPixels) {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Width of the texture must be positive.");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Height of the texture must be positive.");

        if (bgraPixels == null || bgraPixels.Length != width * height * 4)
            throw new ArgumentException("Wrong size of the BGRA32 pixel buffer.", nameof(bgraPixels));

        Width = width;
        Height = height;
        _pixels = bgraPixels;
    }

    /// <summary>
    /// Pobiera kolor z tekstury jako Vector3 (R, G, B) w zakresie [0, 1].
    /// </summary>
    public Vector3 SampleColor(float u, float v) {
        int idx = GetPixelIndex(u, v);
        float b = _pixels[idx] / 255f;
        float g = _pixels[idx + 1] / 255f;
        float r = _pixels[idx + 2] / 255f;
        return new Vector3(r, g, b);
    }

    /// <summary>
    /// Pobiera wektor normalny z mapy normalnych (przestrzeń stycznych),
    /// mapując bajty [0, 255] na zakres [-1, 1].
    /// </summary>
    public Vector3 SampleNormal(float u, float v) {
        int idx = GetPixelIndex(u, v);
        float nx = _pixels[idx + 2] / 255f * 2f - 1f; // R -> X
        float ny = _pixels[idx + 1] / 255f * 2f - 1f; // G -> Y
        float nz = _pixels[idx] / 255f * 2f - 1f;     // B -> Z

        Vector3 n = new(nx, ny, nz);
        return n.LengthSquared() > 1e-8f ? Vector3.Normalize(n) : Vector3.UnitZ;
    }

    private int GetPixelIndex(float u, float v) {
        u = Math.Clamp(u, 0f, 1f);
        v = Math.Clamp(v, 0f, 1f);

        int x = (int)(u * (Width - 1));
        int y = (int)(v * (Height - 1));
        return (y * Width + x) * 4;
    }
}
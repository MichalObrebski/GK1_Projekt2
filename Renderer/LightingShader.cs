namespace Renderer;

using System;
using System.Numerics;

/// <summary>
/// Shader wyliczający kolor piksela na podstawie modelu oświetlenia Phonga
/// (składowa rozproszona Lamberta + składowa zwierciadlana).
/// </summary>
internal static class LightingShader {
    /// <summary>
    /// Stały wektor skierowany w stronę obserwatora (dla rzutu równoległego).
    /// </summary>
    private static readonly Vector3 ViewDir = Vector3.UnitZ;

    /// <summary>
    /// Oblicza finalny kolor piksela (R, G, B w zakresie 0-255) dla punktu na powierzchni.
    /// </summary>
    /// <param name="worldPos">Zinterpolowana pozycja punktu w przestrzeni 3D.</param>
    /// <param name="normal">Zinterpolowany i znormalizowany wektor normalny powierzchni.</param>
    /// <param name="tangentU">Zinterpolowany i znormalizowany wektor styczny U powierzchni.</param>
    /// <param name="tangentV">Zinterpolowany i znormalizowany wektor styczny V powierzchni.</param>
    /// <param name="u">Zinterpolowana współrzędna tekstury U (0-1).</param>
    /// <param name="v">Zinterpolowana współrzędna tekstury V (0-1).</param>
    /// <param name="lightPos">Pozycja punktowego źródła światła w przestrzeni 3D.</param>
    /// <param name="attr">Atrybuty materiału i światła (kolory, Kd, Ks, M).</param>
    /// <returns>Krotka bajtów (r, g, b) gotowa do zapisania w buforze pikseli.</returns>
    public static (byte r, byte g, byte b) CalculateColor(
        Vector3 worldPos,
        Vector3 normal,
        Vector3 tangentU,
        Vector3 tangentV,
        float u,
        float v,
        Vector3 lightPos,
        RenderAttributes attr) {

        bool isBackFacing = normal.Z < 0f;

        // Opcjonalne zaburzenie wektora normalnego z mapy normalnych (macierz M = [Pu, Pv, N])
        if (attr.UseNormalMap && attr.NormalMap != null) {
            Vector3 nTex = attr.NormalMap.SampleNormal(u, v);
            Vector3 perturbed = tangentU * nTex.X + tangentV * nTex.Y + normal * nTex.Z;
            if (perturbed.LengthSquared() > 1e-8f)
                normal = Vector3.Normalize(perturbed);
        }

        // Dwustronne oświetlenie dla otwartych płatów
        if (isBackFacing)
            normal = -normal;

        // Wektor jednostkowy od punktu na powierzchni do źródła światła (L)
        Vector3 toLight = lightPos - worldPos;
        float distSq = toLight.LengthSquared();
        if (distSq < 1e-8f)
            return (0, 0, 0);

        Vector3 lightDir = toLight / MathF.Sqrt(distSq);

        // cos(N, L)
        float cosNL = Vector3.Dot(normal, lightDir);
        if (cosNL <= 0f)
            return (0, 0, 0);

        // Wektor odbity: R = 2 * (N · L) * N - L oraz cos(V, R)^m
        Vector3 reflectDir = 2f * cosNL * normal - lightDir;
        float cosVR = Vector3.Dot(ViewDir, reflectDir);
        float specularTerm = cosVR > 0f ? MathF.Pow(cosVR, Math.Max(1, attr.M)) : 0f;

        // Kolor obiektu (jednolity lub z tekstury) i złożenie modelu Phonga
        Vector3 objectColor = (attr.UseTexture && attr.Texture != null)
            ? attr.Texture.SampleColor(u, v)
            : attr.ObjectColor;

        Vector3 baseColor = objectColor * attr.LightColor;
        Vector3 finalColor = (attr.Kd * cosNL + attr.Ks * specularTerm) * baseColor;
        finalColor = Vector3.Clamp(finalColor, Vector3.Zero, Vector3.One);

        return (
            (byte)(finalColor.X * 255f),
            (byte)(finalColor.Y * 255f),
            (byte)(finalColor.Z * 255f)
        );
    }
}
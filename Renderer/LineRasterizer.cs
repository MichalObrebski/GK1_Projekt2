namespace Renderer;

using System;

/// <summary>
/// Narzędzie do rasteryzacji odcinków 2D na buforze pikseli za pomocą algorytmu Bresenhama.
/// </summary>
internal static class LineRasterizer {
    /// <summary>
    /// Rysuje odcinek między dwoma punktami na ekranie w podanym kolorze.
    /// </summary>
    /// <param name="ctx">Kontekst bieżącej klatki.</param>
    /// <param name="x0">Współrzędna X punktu początkowego.</param>
    /// <param name="y0">Współrzędna Y punktu początkowego.</param>
    /// <param name="x1">Współrzędna X punktu końcowego.</param>
    /// <param name="y1">Współrzędna Y punktu końcowego.</param>
    /// <param name="r">Składowa czerwona koloru linii (0-255).</param>
    /// <param name="g">Składowa zielona koloru linii (0-255).</param>
    /// <param name="b">Składowa niebieska koloru linii (0-255).</param>
    public static void DrawLine(RenderContext ctx, int x0, int y0, int x1, int y1, byte r, byte g, byte b) {
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true) {
            ctx.SetPixel(x0, y0, r, g, b);
            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * err;
            if (e2 > -dy) {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx) {
                err += dx;
                y0 += sy;
            }
        }
    }

    /// <summary>
    /// Rysuje odcinek między dwoma punktami na ekranie z liniową interpolacją głębokości Z
    /// oraz testem Z-buffera.
    /// </summary>
    /// <param name="ctx">Kontekst bieżącej klatki.</param>
    /// <param name="x0">Współrzędna X punktu początkowego.</param>
    /// <param name="y0">Współrzędna Y punktu początkowego.</param>
    /// <param name="z0">Głębokość Z punktu początkowego (w przestrzeni sceny).</param>
    /// <param name="x1">Współrzędna X punktu końcowego.</param>
    /// <param name="y1">Współrzędna Y punktu końcowego.</param>
    /// <param name="z1">Głębokość Z punktu końcowego (w przestrzeni sceny).</param>
    /// <param name="r">Składowa czerwona koloru linii (0-255).</param>
    /// <param name="g">Składowa zielona koloru linii (0-255).</param>
    /// <param name="b">Składowa niebieska koloru linii (0-255).</param>
    public static void DrawLineWithZ(
        RenderContext ctx,
        int x0, int y0, float z0,
        int x1, int y1, float z1,
        byte r, byte g, byte b) {

        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        int steps = Math.Max(dx, dy);
        float z = steps == 0 ? MathF.Max(z0, z1) : z0;
        float dz = steps > 0 ? (z1 - z0) / steps : 0f;

        while (true) {
            ctx.TrySetPixelWithZ(x0, y0, z, r, g, b);
            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * err;
            if (e2 > -dy) {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx) {
                err += dx;
                y0 += sy;
            }

            z += dz;
        }
    }
}
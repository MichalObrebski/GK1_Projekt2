namespace Renderer;

using System;
using System.Numerics;

/// <summary>
/// Kontekst renderowania tworzony na czas generowania jednej klatki.
/// Enkapsuluje dostęp do bufora pikseli (BGRA32), Z-buffera oraz rzutowanie 3D na 2D.
/// </summary>
internal class RenderContext {
    /// <summary>
    /// Szerokość obszaru renderowania w pikselach.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Wysokość obszaru renderowania w pikselach.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Współczynnik skali przeliczający jednostki sceny na piksele ekranu.
    /// </summary>
    public float Scale { get; }

    /// <summary>
    /// Surowy bufor pikseli w formacie BGRA32 (4 bajty na piksel).
    /// </summary>
    public byte[] PixelBuffer { get; }

    /// <summary>
    /// Bufor głębokości przechowujący najwyższą (najbliższą kamerze) współrzędną Z dla każdego piksela.
    /// </summary>
    public float[] ZBuffer { get; }

    /// <summary>
    /// Inicjalizuje kontekst dla pojedynczej klatki.
    /// </summary>
    /// <param name="width">Szerokość płótna w pikselach.</param>
    /// <param name="height">Wysokość płótna w pikselach.</param>
    /// <param name="scale">Skala dopasowania sceny do rozmiaru okna.</param>
    /// <param name="pixelBuffer">Docelowy bufor pikseli (BGRA32).</param>
    /// <param name="zBuffer">Współdzielony bufor głębokości.</param>
    public RenderContext(int width, int height, float scale, byte[] pixelBuffer, float[] zBuffer) {
        Width = width;
        Height = height;
        Scale = scale;
        PixelBuffer = pixelBuffer;
        ZBuffer = zBuffer;
    }

    /// <summary>
    /// Wypełnia cały bufor pikseli jednolitym kolorem tła i resetuje Z-buffer.
    /// </summary>
    /// <param name="r">Składowa czerwona tła (0-255).</param>
    /// <param name="g">Składowa zielona tła (0-255).</param>
    /// <param name="b">Składowa niebieska tła (0-255).</param>
    public void Clear(byte r, byte g, byte b) {
        Array.Fill(ZBuffer, float.NegativeInfinity);

        for (int i = 0; i < PixelBuffer.Length; i += 4) {
            PixelBuffer[i] = b;
            PixelBuffer[i + 1] = g;
            PixelBuffer[i + 2] = r;
            PixelBuffer[i + 3] = 255;
        }
    }

    /// <summary>
    /// Zapisuje piksel w buforze bez testu głębokości (z bezpiecznym przycinaniem do granic ekranu).
    /// </summary>
    /// <param name="x">Współrzędna X na ekranie.</param>
    /// <param name="y">Współrzędna Y na ekranie.</param>
    /// <param name="r">Składowa czerwona (0-255).</param>
    /// <param name="g">Składowa zielona (0-255).</param>
    /// <param name="b">Składowa niebieska (0-255).</param>
    public void SetPixel(int x, int y, byte r, byte g, byte b) {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            return;

        int idx = (y * Width + x) * 4;
        PixelBuffer[idx] = b;
        PixelBuffer[idx + 1] = g;
        PixelBuffer[idx + 2] = r;
        PixelBuffer[idx + 3] = 255;
    }

    /// <summary>
    /// Zapisuje piksel tylko wtedy, gdy znajduje się bliżej obserwatora (większe Z) niż obecny wpis w Z-bufferze.
    /// </summary>
    /// <param name="x">Współrzędna X na ekranie.</param>
    /// <param name="y">Współrzędna Y na ekranie.</param>
    /// <param name="z">Głębokość punktu w przestrzeni sceny.</param>
    /// <param name="r">Składowa czerwona (0-255).</param>
    /// <param name="g">Składowa zielona (0-255).</param>
    /// <param name="b">Składowa niebieska (0-255).</param>
    /// <returns>True, jeśli piksel przeszedł test głębokości i został zapisany; w przeciwnym razie false.</returns>
    public bool TrySetPixelWithZ(int x, int y, float z, byte r, byte g, byte b) {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            return false;

        int zIdx = y * Width + x;
        if (z <= ZBuffer[zIdx])
            return false;

        ZBuffer[zIdx] = z;
        int pIdx = zIdx * 4;
        PixelBuffer[pIdx] = b;
        PixelBuffer[pIdx + 1] = g;
        PixelBuffer[pIdx + 2] = r;
        PixelBuffer[pIdx + 3] = 255;
        return true;
    }

    /// <summary>
    /// Rysuje wypełniony kwadratowy znacznik wyśrodkowany w (cx, cy) z testem Z-buffera.
    /// </summary>
    /// <param name="cx">Współrzędna X środka znacznika.</param>
    /// <param name="cy">Współrzędna Y środka znacznika.</param>
    /// <param name="z">Głębokość punktu w przestrzeni sceny.</param>
    /// <param name="radius">Połowa szerokości znacznika w pikselach (np. 3 daje kwadrat 7x7).</param>
    /// <param name="r">Składowa czerwona (0-255).</param>
    /// <param name="g">Składowa zielona (0-255).</param>
    /// <param name="b">Składowa niebieska (0-255).</param>
    public void DrawMarkerWithZ(int cx, int cy, float z, int radius, byte r, byte g, byte b) {
        for (int dy = -radius; dy <= radius; dy++) {
            for (int dx = -radius; dx <= radius; dx++) {
                TrySetPixelWithZ(cx + dx, cy + dy, z, r, g, b);
            }
        }
    }

    /// <summary>
    /// Sprawdza, czy punkt (x, y) o głębokości z znajduje się bliżej kamery niż obecny wpis w Z-bufferze.
    /// Jeśli tak, aktualizuje Z-buffer i zwraca true.
    /// </summary>
    public bool TryUpdateDepth(int x, int y, float z) {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            return false;

        int zIdx = y * Width + x;
        if (z <= ZBuffer[zIdx])
            return false;

        ZBuffer[zIdx] = z;
        return true;
    }

    /// <summary>
    /// Przelicza współrzędne ekranowe (piksele myszy) na współrzędne płaszczyzny XY sceny.
    /// </summary>
    /// <param name="screenPoint">Punkt na ekranie w pikselach.</param>
    /// <param name="width">Szerokość obszaru renderowania w pikselach.</param>
    /// <param name="height">Wysokość obszaru renderowania w pikselach.</param>
    /// <param name="scale">Skala przeliczenia jednostek sceny na piksele ekranu.</param>
    /// <returns>Współrzędne punktu w przestrzeni sceny.</returns>
    internal static Vector2 ScreenToScene(Vector2 screenPoint, int width, int height, float scale) {
        return new Vector2(
            (screenPoint.X - width * 0.5f) / scale,
            (height * 0.5f - screenPoint.Y) / scale
        );
    }

    /// <summary>
    /// Rzutuje punkt 2D z przestrzeni sceny na współrzędne 2D ekranu
    /// </summary>
    /// <param name="scenePoint">Punkt w przestrzeni sceny 2D.</param>
    /// <param name="width">Szerokość obszaru renderowania w pikselach.</param>
    /// <param name="height">Wysokość obszaru renderowania w pikselach.</param>
    /// <param name="scale">Skala przeliczenia jednostek sceny na piksele ekranu.</param>
    /// <returns>Pozycja punktu w pikselach ekranu.</returns>
    internal static Vector2 SceneToScreen(Vector2 scenePoint, int width, int height, float scale) {
        return new Vector2(
            scenePoint.X * scale + width * 0.5f,
            height * 0.5f - scenePoint.Y * scale
        );
    }

    /// <summary>
    /// Rzutuje punkt 2D z przestrzeni sceny na współrzędne 2D ekranu (rzut równoległy ze skalowaniem i odwróceniem osi Y).
    /// Wywołuje przeciążenie metody <see cref="SceneToScreen(Vector2, int, int, float)"/> z aktualnymi wymiarami i skalą kontekstu.
    /// </summary>
    /// <param name="p">Punkt w przestrzeni sceny 2D.</param>
    /// <returns>Pozycja punktu w pikselach ekranu.</returns>
    public Vector2 SceneToScreen(Vector2 p) => SceneToScreen(p, Width, Height, Scale);

    /// <summary>
    /// Rzutuje punkt 3D z przestrzeni sceny na współrzędne 2D ekranu (przeciążenie metody <see cref="SceneToScreen(Vector2)"/>, współrzędna Z jest ignorowana).
    /// </summary>
    /// <param name="p">Punkt w przestrzeni sceny 3D.</param>
    /// <returns>Pozycja punktu w pikselach ekranu.</returns>
    public Vector2 SceneToScreen(Vector3 p) => SceneToScreen(new Vector2(p.X, p.Y));

    /// <summary>
    /// Rysuje wypełniony kwadratowy znacznik wyśrodkowany w (cx, cy).
    /// </summary>
    /// <param name="cx">Współrzędna X środka znacznika.</param>
    /// <param name="cy">Współrzędna Y środka znacznika.</param>
    /// <param name="radius">Połowa szerokości znacznika w pikselach (np. 3 daje kwadrat 7x7).</param>
    /// <param name="r">Składowa czerwona (0-255).</param>
    /// <param name="g">Składowa zielona (0-255).</param>
    /// <param name="b">Składowa niebieska (0-255).</param>
    public void DrawMarker(int cx, int cy, int radius, byte r, byte g, byte b) {
        for (int dy = -radius; dy <= radius; dy++) {
            for (int dx = -radius; dx <= radius; dx++) {
                SetPixel(cx + dx, cy + dy, r, g, b);
            }
        }
    }
}
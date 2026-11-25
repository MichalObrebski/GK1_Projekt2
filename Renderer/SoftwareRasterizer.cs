namespace Renderer;

using System;
using System.Collections.Generic;
using System.Numerics;
using Models;

public class SoftwareRasterizer {
    private int _width;
    private int _height;
    private float[] _zBuffer = Array.Empty<float>();
    private readonly List<IRenderPass> _pipeline = new() {
        new ClearPass(),
        new FillTrianglesPass(),
        new GridPass(),
        new ControlPolygonPass(),
        new LightPass(),
    };

    public SoftwareRasterizer(int width, int height) {
        Resize(width, height);
    }

    /// <summary>
    /// Zmienia rozmiar wewnętrznego Z-buffera przy zmianie rozmiaru okna.
    /// </summary>
    public void Resize(int width, int height) {
        if (width <= 0 || height <= 0) return;

        _width = width;
        _height = height;
        _zBuffer = new float[width * height];
    }

    /// <summary>
    /// Renderuje pełną klatkę sceny do przekazanego bufora pikseli (BGRA32).
    /// </summary>
    /// <param name="scene">Model sceny z powierzchniami Béziera i światłem.</param>
    /// <param name="attributes">Atrybuty renderowania (kolory, tekstury, oświetlenie).</param>
    /// <param name="pixelBuffer">Bufor pikseli o rozmiarze width * height * 4 bajty (BGRA32).</param>
    /// <exception cref="ArgumentException">Jeśli rozmiar pixelBuffer nie zgadza się z width * height * 4.</exception>
    public void Render(SceneModel scene, RenderAttributes attributes, byte[] pixelBuffer) {
        if (_width <= 0 || _height <= 0)
            return;

        if (pixelBuffer.Length != _width * _height * 4)
            throw new ArgumentException($"Wrong pixel buffer size. Expected: {_width * _height * 4}, Actual: {pixelBuffer.Length}", nameof(pixelBuffer));

        float scale = CalculateScale(scene);
        var context = new RenderContext(_width, _height, scale, pixelBuffer, _zBuffer);

        foreach (var pass in _pipeline) {
            pass.Execute(context, scene, attributes);
        }
    }

    /// <summary>
    /// Oblicza skalę sceny w pikselach na jednostkę sceny
    /// </summary>
    private float CalculateScale(SceneModel scene) {
        if (_width <= 0 || _height <= 0 || scene.BoundingRadius <= 0f)
            return 1f;

        return (Math.Min(_width, _height) * 0.4f) / scene.BoundingRadius;
    }

    /// <summary>
    /// Przelicza współrzędne ekranowe (piksele myszy) na współrzędne płaszczyzny XY sceny.
    /// </summary>
    /// <param name="screenX">Współrzędna X w pikselach (0..width-1).</param>
    /// <param name="screenY">Współrzędna Y w pikselach (0..height-1).</param>
    /// <param name="scene">Model sceny (do obliczenia skali).</param>
    public Vector2 ScreenToScene(float screenX, float screenY, SceneModel scene){
        return RenderContext.ScreenToScene(new Vector2(screenX, screenY), _width, _height, CalculateScale(scene));
    }

    /// <summary>
    /// Przelicza współrzędne X i Y sceny (2D) na współrzędne ekranowe (piksele).
    /// </summary>
    /// <param name="scenePos">Współrzędne X i Y sceny (2D).</param>
    /// <param name="scene">Model sceny (do obliczenia skali).</param>
    public Vector2 SceneToScreen(Vector2 scenePos, SceneModel scene) {
        return RenderContext.SceneToScreen(scenePos, _width, _height, CalculateScale(scene));
    }
}
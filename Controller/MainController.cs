namespace Controller;

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Models;

using Renderer;

public class MainController {
    // ==========================================
    // PRYWATNE DANE KONTROLERA
    // ==========================================

    private const float LightPickRadiusPx = 10f;
    private const float MaxAnimationDeltaTime = 0.1f;

    private readonly SceneModel _model;
    private readonly SoftwareRasterizer _renderer;
    private readonly RenderAttributes _attributes;
    private int? _lightControlPointIndex = null;
    private (float x, float y)? _lastMousePosition = null;

    // ==========================================
    // DANE WYJŚCIOWE DLA WIDOKU
    // ==========================================

    /// <summary>
    /// Szerokość sceny (w pikselach).
    /// </summary>
    public int Width { get; private set; }

    /// <summary>
    /// Wysokość sceny (w pikselach).
    /// </summary>
    public int Height { get; private set; }

    /// <summary>
    /// Surowy bufor pikseli (BGRA), który Widok rysuje na ekranie.
    /// </summary>
    public byte[] PixelBuffer { get; private set; }

    /// <summary>
    /// Kolor wypełnienia obiektu (składowe 0.0-1.0).
    /// </summary>
    public Vector3 ObjectColor => _attributes.ObjectColor;

    /// <summary>
    /// Kolor padającego światła (składowe 0.0-1.0).
    /// </summary>
    public Vector3 LightColor => _attributes.LightColor;
    /// <summary>
    /// Czy światło jest animowane.
    /// </summary>
    public bool IsLightAnimating => _model.Light.IsAnimating;

    /// <summary>
    /// Zdarzenie wywoływane po wyrenderowaniu nowej klatki do PixelBuffer.
    /// </summary>
    public event Action? FrameUpdated;

    /// <summary>
    /// Inicjalizacja kontrolera i wczytanie powierzchni Béziera z pliku tekstowego.
    /// </summary>
    /// <param name="initialWidth">Początkowa szerokość sceny.</param>
    /// <param name="initialHeight">Początkowa wysokość sceny.</param>
    /// <param name="surfaceDataFilePath">Ścieżka do pliku z danymi powierzchni.</param>
    public MainController(int initialWidth, int initialHeight, string surfaceDataFilePath) {
        Width = initialWidth;
        Height = initialHeight;
        PixelBuffer = new byte[Math.Max(0, Width * Height * 4)];

        _model = new SceneModel();
        _renderer = new SoftwareRasterizer(Width, Height);
        _attributes = new RenderAttributes();

        _attributes.SetObjectColor(0xC8, 0xC8, 0xC8);
        _attributes.SetLightColor(0xFF, 0xFF, 0x80);

        LoadSurfacesFromFile(surfaceDataFilePath);
    }

    // ==========================================
    // INTERAKCJA MYSZĄ (OBSZAR RYSOWANIA)
    // ==========================================

    /// <summary>
    /// Rozpoczyna przeciąganie punktu kontrolnego światła albo obracanie sceny.
    /// </summary>
    /// <param name="x">Pozycja X kursora (w pikselach ekranu).</param>
    /// <param name="y">Pozycja Y kursora (w pikselach ekranu).</param>
    public void StartDrag(float x, float y) {
        if (Width <= 0 || Height <= 0)
            return;

        Vector2 scenePos = _renderer.ScreenToScene(x, y, _model);
        float hitRadius = MathF.Abs(
            _renderer.ScreenToScene(LightPickRadiusPx, 0f, _model).X -
            _renderer.ScreenToScene(0f, 0f, _model).X);

        int lightPointIndex = _model.TryPickLightControlPoint(scenePos.X, scenePos.Y, hitRadius);
        _lightControlPointIndex = lightPointIndex >= 0 ? lightPointIndex : null;

        if (!_lightControlPointIndex.HasValue) {
            _lastMousePosition = (x, y);
        }
    }

    /// <summary>
    /// Przesuwa wybrany punkt kontrolny światła lub obraca scenę zgodnie z ruchem kursora.
    /// </summary>
    /// <param name="x">Pozycja X kursora (w pikselach ekranu).</param>
    /// <param name="y">Pozycja Y kursora (w pikselach ekranu).</param>
    public void Drag(float x, float y) {
        if (_lightControlPointIndex.HasValue) {
            Vector2 scenePos = _renderer.ScreenToScene(x, y, _model);
            _model.MoveLightControlPoint(_lightControlPointIndex.Value, scenePos.X, scenePos.Y);
            RequestRender();
        }
        else if (_lastMousePosition.HasValue) {
            float deltaX = x - _lastMousePosition.Value.x;
            float deltaY = y - _lastMousePosition.Value.y;

            Matrix4x4 deltaRotation =
                Matrix4x4.CreateRotationY(deltaX * 0.01f) *
                Matrix4x4.CreateRotationX(deltaY * 0.01f);
            _model.ApplyRotation(deltaRotation);

            _lastMousePosition = (x, y);
            RequestRender();
        }
    }

    /// <summary>
    /// Kończy przeciąganie sceny lub punktu światła.
    /// </summary>
    public void EndDrag() {
        _lightControlPointIndex = null;
        _lastMousePosition = null;
    }

    /// <summary>
    /// Resetuje rotację sceny.
    /// </summary>
    public void ResetRotation() {
        _model.ResetRotation();
        RequestRender();
    }

    // ==========================================
    // MENU GŁÓWNE (PLIKI I KOLORY)
    // ==========================================

    /// <summary>
    /// Wczytuje teksturę z pliku obrazu (BGRA32) i przekazuje ją do atrybutów renderera.
    /// </summary>
    /// <param name="filePath">Ścieżka do pliku z teksturą.</param>
    public void LoadTexture(string filePath) {
        TextureData? Texture = LoadTextureData(filePath);

        if (Texture == null)
            return;
        
        _attributes.Texture = Texture;

        if (_attributes.ShowFill && _attributes.UseTexture)
            RequestRender();
    }

    /// <summary>
    /// Wczytuje mapę wektorów normalnych z pliku obrazu (BGRA32) i przekazuje ją do atrybutów renderera.
    /// </summary>
    /// <param name="filePath">Ścieżka do pliku z mapą wektorów normalnych.</param>
    public void LoadNormalMap(string filePath) {
        TextureData? NormalMap = LoadTextureData(filePath);

        if (NormalMap == null)
            return;

        _attributes.NormalMap = NormalMap;

        if (_attributes.ShowFill && _attributes.UseNormalMap)
            RequestRender();
    }

    /// <summary>
    /// Ustawia jednolity kolor wypełnienia trójkątów.
    /// </summary>
    /// <param name="r">Natężenie czerwonego (0-255).</param>
    /// <param name="g">Natężenie zielonego (0-255).</param>
    /// <param name="b">Natężenie niebieskiego (0-255).</param>
    public void SetObjectColor(byte r, byte g, byte b) {
        _attributes.SetObjectColor(r, g, b);

        if (_attributes.ShowFill && !_attributes.UseTexture)
            RequestRender();
    }

    /// <summary>
    /// Ustawia kolor padającego światła.
    /// </summary>
    /// <param name="r">Natężenie czerwonego (0-255).</param>
    /// <param name="g">Natężenie zielonego (0-255).</param>
    /// <param name="b">Natężenie niebieskiego (0-255).</param>
    public void SetLightColor(byte r, byte g, byte b) {
        _attributes.SetLightColor(r, g, b);
        RequestRender();
    }

    // ==========================================
    // SEKCJA 1: RYSOWANIE I WIDOCZNOŚĆ
    // ==========================================

    /// <summary>
    /// Ustawia widoczność wieloboków kontrolnych Béziera.
    /// </summary>
    public void SetPolygonVisibility(bool showPolygons) {
        if (showPolygons == _attributes.ShowControlPolygons)
            return;

        _attributes.ShowControlPolygons = showPolygons;
        RequestRender();
    }

    /// <summary>
    /// Ustawia widoczność siatki trójkątów.
    /// </summary>
    public void SetGridVisibility(bool showGrid) {
        if (showGrid == _attributes.ShowGrid)
            return;

        _attributes.ShowGrid = showGrid;
        RequestRender();
    }

    /// <summary>
    /// Ustawia widoczność wypełnienia trójkątów.
    /// </summary>
    public void SetFillVisibility(bool showFilled) {
        if (showFilled == _attributes.ShowFill)
            return;

        _attributes.ShowFill = showFilled;
        RequestRender();
    }

    /// <summary>
    /// Ustawia dokładność triangulacji (gęstość podziału powierzchni).
    /// </summary>
    /// <param name="n">Liczba podziałów powierzchni 1-50.</param>
    public void SetGridResolution(int n) {
        n = Math.Clamp(n, 1, 50);

        if (n == _model.GridResolution)
            return;

        _model.SetGridResolution(n);
        RequestRender();
    }

    // ==========================================
    // SEKCJA 2: WYPEŁNIENIE I MATERIAŁ
    // ==========================================

    /// <summary>
    /// Ustawia tryb wypełnienia (jednolity kolor lub tekstura).
    /// </summary>
    /// <param name="useTexture">True, jeśli ma być używana tekstura.</param>
    public void SetFillMode(bool useTexture) {
        if (useTexture == _attributes.UseTexture)
            return;

        _attributes.UseTexture = useTexture;

        if (_attributes.ShowFill)
            RequestRender();
    }

    /// <summary>
    /// Ustawia, czy ma być używana mapa wektorów normalnych.
    /// </summary>
    public void SetNormalVectorsMapUsage(bool useNormalVectorsMap) {
        if (useNormalVectorsMap == _attributes.UseNormalMap)
            return;

        _attributes.UseNormalMap = useNormalVectorsMap;

        if (_attributes.ShowFill)
            RequestRender();
    }

    // ==========================================
    // SEKCJA 3: OŚWIETLENIE I ANIMACJA
    // ==========================================

    /// <summary>
    /// Ustawia współczynnik rozproszenia (diffuse).
    /// </summary>
    /// <param name="kd">Współczynnik rozproszenia 0.0-1.0.</param>
    public void SetKd(float kd) {
        if (kd < 0f || kd > 1f)
            throw new ArgumentOutOfRangeException(nameof(kd), "Kd must be between 0.0 and 1.0.");

        if (kd == _attributes.Kd)
            return;

        _attributes.Kd = kd;

        if (_attributes.ShowFill)
            RequestRender();
    }

    /// <summary>
    /// Ustawia współczynnik zwierciadła (specular).
    /// </summary>
    /// <param name="ks">Współczynnik zwierciadła 0.0-1.0.</param>
    public void SetKs(float ks) {
        if (ks < 0f || ks > 1f)
            throw new ArgumentOutOfRangeException(nameof(ks), "Ks must be between 0.0 and 1.0.");

        if (ks == _attributes.Ks)
            return;

        _attributes.Ks = ks;

        if (_attributes.ShowFill)
            RequestRender();
    }

    /// <summary>
    /// Ustawia współczynnik połysku (shininess).
    /// </summary>
    /// <param name="m">Współczynnik połysku 1-100.</param>
    public void SetM(int m) {
        if (m < 1 || m > 100)
            throw new ArgumentOutOfRangeException(nameof(m), "M must be between 1 and 100.");

        if (m == _attributes.M)
            return;

        _attributes.M = m;

        if (_attributes.ShowFill)
            RequestRender();
    }

    /// <summary>
    /// Ustawia znormalizowaną wysokość płaszczyzny światła (jako mnożnik BoundingRadius sceny).
    /// </summary>
    /// <param name="zFactor">Mnożnik wysokości z zakresu -3.0 do 3.0.</param>
    public void SetLightHeight(float zFactor) {
        zFactor = Math.Clamp(zFactor, -3f, 3f);

        _model.SetLightHeight(zFactor * _model.BoundingRadius);
        RequestRender();
    }

    /// <summary>
    /// Włącza lub zatrzymuje animację ruchu źródła światła po zamkniętej krzywej Béziera.
    /// </summary>
    /// <param name="isPlaying">True, jeśli animacja ma być odtwarzana.</param>
    public void ToggleLightAnimation(bool isPlaying) {
        if (isPlaying == _model.Light.IsAnimating)
            return;

        _model.ToggleLightAnimation(isPlaying);
    }

    // ==========================================
    // RENDEROWANIE, ANIMACJA I ROZMIAR
    // ==========================================

    /// <summary>
    /// Wywołuje renderera do przeliczenia nowej klatki i powiadamia Widok.
    /// </summary>
    public void RequestRender() {
        if (Width <= 0 || Height <= 0)
            return;

        _renderer.Render(_model, _attributes, PixelBuffer);
        FrameUpdated?.Invoke();
    }

    /// <summary>
    /// Aktualizuje animację źródła światła (jeśli jest włączona) i odświeża klatkę.
    /// </summary>
    /// <param name="deltaTime">Czas, który upłynął od ostatniej klatki w sekundach.</param>
    public void UpdateAnimation(float deltaTime) {
        if (!_model.Light.IsAnimating || deltaTime <= 0f)
            return;

        float clampedDt = MathF.Min(deltaTime, MaxAnimationDeltaTime);
        _model.UpdateLightAnimation(clampedDt);
        RequestRender();
    }

    /// <summary>
    /// Zmienia rozmiar sceny i bufora pikseli, a następnie renderuje nową klatkę.
    /// </summary>
    /// <param name="newWidth">Nowa szerokość sceny.</param>
    /// <param name="newHeight">Nowa wysokość sceny.</param>
    public void ResizeScene(int newWidth, int newHeight) {
        if (newWidth <= 0 || newHeight <= 0)
            return;
        if (newWidth == Width && newHeight == Height)
            return;

        Width = newWidth;
        Height = newHeight;

        PixelBuffer = new byte[Width * Height * 4];
        _renderer.Resize(Width, Height);
        RequestRender();
    }

    // ==========================================
    // METODY POMOCNICZE (WEJŚCIE PLIKOWE)
    // ==========================================

    private void LoadSurfacesFromFile(string surfaceDataFilePath) {
        try {
            if (!File.Exists(surfaceDataFilePath))
                throw new FileNotFoundException("Surface data file not found.", surfaceDataFilePath);

            using var surfaceData = new StreamReader(surfaceDataFilePath);

            Vector3[] controlPoints = surfaceData.ReadToEnd()
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => {
                    var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 3)
                        throw new FormatException($"Invalid control point format: {line}");

                    return new Vector3(
                        float.Parse(parts[0], CultureInfo.InvariantCulture),
                        float.Parse(parts[1], CultureInfo.InvariantCulture),
                        float.Parse(parts[2], CultureInfo.InvariantCulture)
                    );
                })
                .ToArray();

            if (controlPoints.Length == 0 || controlPoints.Length % 16 != 0)
                throw new FormatException("Each surface must have exactly 16 control points.");

            foreach (var surfacePoints in controlPoints.Chunk(16)) {
                Vector3[,] controlMatrix = new Vector3[4, 4];
                for (int i = 0; i < 4; i++) {
                    for (int j = 0; j < 4; j++) {
                        controlMatrix[i, j] = surfacePoints[i * 4 + j];
                    }
                }

                if(_model.GridResolution < 1)
                    _model.SetGridResolution(1);
                _model.AddSurface(controlMatrix);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or OverflowException or ArgumentException) {
            MessageBox.Show(
                $"Nie udało się wczytać pliku sceny:\n{surfaceDataFilePath}\n\nSzczegóły: {ex.Message}",
                "Błąd wczytywania sceny",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static TextureData? LoadTextureData(string filePath) {
        try {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Image file not found.", filePath);

            var frame = BitmapFrame.Create(
                new Uri(Path.GetFullPath(filePath), UriKind.Absolute),
                BitmapCreateOptions.IgnoreImageCache,
                BitmapCacheOption.OnLoad);

            BitmapSource bgraBitmap = frame.Format == PixelFormats.Bgra32
                ? frame
                : new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);

            int width = bgraBitmap.PixelWidth;
            int height = bgraBitmap.PixelHeight;
            byte[] pixels = new byte[width * height * 4];
            bgraBitmap.CopyPixels(pixels, width * 4, 0);

            return new TextureData(width, height, pixels);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FormatException or ArgumentException or COMException) {
            MessageBox.Show(
                $"Nie udało się wczytać pliku obrazu:\n{filePath}\n\nSzczegóły: {ex.Message}",
                "Błąd wczytywania obrazu",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return null;
        }
    }
}
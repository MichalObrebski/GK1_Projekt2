using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Controller;

namespace View;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window {
    //==========================================
    //  ZMIENNE
    //==========================================

    private bool _isDragging = false;
    private readonly MainController _controller;
    private WriteableBitmap? _bitmap = null;
    private Color _objectColor => Color.FromRgb((byte)(_controller.ObjectColor.X * 255), (byte)(_controller.ObjectColor.Y * 255), (byte)(_controller.ObjectColor.Z * 255));
    private Color _lightColor => Color.FromRgb((byte)(_controller.LightColor.X * 255), (byte)(_controller.LightColor.Y * 255), (byte)(_controller.LightColor.Z * 255));
    private readonly Stopwatch _frameTimer = Stopwatch.StartNew();
    private double _lastFrameTimeSeconds = 0.0;

    //==========================================
    // KONSTRUKTOR
    //==========================================

    /// <summary>
    /// Konstruktor okna głównego.
    /// </summary>
    public MainWindow() {
        // Inicjalizacja kontrolera
        string[] args = Environment.GetCommandLineArgs();
        if (args.Length < 2) {
            MessageBox.Show(
                "Nie podano ścieżki do pliku ze sceną (.txt) jako argumentu uruchomienia.",
                "Błąd uruchamiania",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            _controller = null!;
            Environment.Exit(1);
            return;
        }

        try {
            _controller = new MainController(0, 0, args[1]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or OverflowException or ArgumentException) {
            MessageBox.Show(
                $"Nie udało się wczytać pliku sceny:\n{args[1]}\n\nSzczegóły: {ex.Message}",
                "Błąd wczytywania sceny",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            _controller = null!;
            Environment.Exit(1);
            return;
        }
        InitializeComponent();
        CompositionTarget.Rendering += OnRendering;
        Closed += (_, _) => CompositionTarget.Rendering -= OnRendering;
        ScreenGrid.SizeChanged += ScreenGrid_SizeChanged;

        _controller.FrameUpdated += () => _bitmap?.WritePixels(
                new Int32Rect(0, 0, _controller.Width, _controller.Height),
                _controller.PixelBuffer, _controller.Width * 4, 0
            );

        // ustawienie wartości domyślnych
        _controller.SetLightColor(0xFF, 0xFF, 0xFF);
        string texturePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "bricks.png");
        _controller.LoadTexture(texturePath);

        // Obsługa skrótów klawiszowych
        KeyDown += (_, e) => {
            switch (e.Key) {
                case Key.Space:
                    ToggleLightAnimationCheckBox.IsChecked = !ToggleLightAnimationCheckBox.IsChecked;
                    e.Handled = true;
                    break;
                case Key.R:
                    _controller.ResetRotation();
                    break;
                case Key.D1:
                    VisibilitySectionRadio.IsChecked = true;
                    break;
                case Key.D2:
                    MaterialSectionRadio.IsChecked = true;
                    break;
                case Key.D3:
                    LightingSectionRadio.IsChecked = true;
                    break;
            }
        };
    }


    //==========================================
    // ZDARZENIA OKNA I MYSZY
    //==========================================

    /// <summary>
    /// Obsługuje zdarzenie zmiany rozmiaru okna.
    /// </summary>
    /// <param name="sender">Obiekt wysyłający zdarzenie.</param>
    /// <param name="e">Dane zdarzenia zmiany rozmiaru.</param>
    private void ScreenGrid_SizeChanged(object? sender, SizeChangedEventArgs e) {
        int newWidth = (int)ScreenGrid.ActualWidth;
        int newHeight = (int)ScreenGrid.ActualHeight;

        if (newWidth <= 0 || newHeight <= 0)
            return;

        if (newWidth == _controller.Width && newHeight == _controller.Height)
            return;

        // Tworzenie nowej bitmapy o nowym rozmiarze
        _bitmap = new WriteableBitmap(newWidth, newHeight, 96, 96, PixelFormats.Bgra32, null);
        ScreenImage.Source = _bitmap;

        // Zmiana rozmiaru sceny w kontrolerze
        _controller.ResizeScene(newWidth, newHeight);

    }

    /// <summary>
    /// Obsługuje zdarzenie naciśnięcia przycisku myszy.
    /// </summary>
    /// <param name="sender">Obiekt wysyłający zdarzenie.</param>
    /// <param name="e">Dane zdarzenia naciśnięcia przycisku myszy.</param>
    private void ScreenImage_MouseDown(object? sender, MouseButtonEventArgs e) {
        // Rozpoczęcie przeciągania
        if (e.LeftButton == MouseButtonState.Pressed) {
            ScreenImage.CaptureMouse();
            Point position = e.GetPosition(ScreenImage);
            _isDragging = true;
            _controller.StartDrag((float)position.X, (float)position.Y);
        }
    }

    /// <summary>
    /// Obsługuje zdarzenie ruchu myszy.
    /// </summary>
    /// <param name="sender">Obiekt wysyłający zdarzenie.</param>
    /// <param name="e">Dane zdarzenia ruchu myszy.</param>
    private void ScreenImage_MouseMove(object? sender, MouseEventArgs e) {
        if (!_isDragging)
            return;

        // Aktualizacja pozycji przeciągania
        Point position = e.GetPosition(ScreenImage);
        _controller.Drag((float)position.X, (float)position.Y);
    }

    /// <summary>
    /// Obsługuje zdarzenie zwolnienia przycisku myszy.
    /// </summary>
    /// <param name="sender">Obiekt wysyłający zdarzenie.</param>
    /// <param name="e">Dane zdarzenia zwolnienia przycisku myszy.</param>
    private void ScreenImage_MouseUp(object? sender, MouseButtonEventArgs e) {
        // Zakończenie przeciągania
        if (!_isDragging)
            return;

        ScreenImage.ReleaseMouseCapture();
        _isDragging = false;
        _controller.EndDrag();
    }

    /// <summary>
    /// Obsługuje zdarzenie renderowania.
    /// </summary>
    /// <param name="sender">Obiekt wysyłający zdarzenie.</param>
    /// <param name="e">Dane zdarzenia renderowania.</param>
    private void OnRendering(object? sender, EventArgs e) {
        double now = _frameTimer.Elapsed.TotalSeconds;
        float deltaTime = (float)(now - _lastFrameTimeSeconds);
        _lastFrameTimeSeconds = now;

        _controller.UpdateAnimation(deltaTime);
    }


    //==========================================
    // OBSŁUGA MENU GŁÓWNEGO
    //==========================================

    /// <summary>
    /// Obsługuje zdarzenie kliknięcia przycisku "Plik -> Wczytaj teksturę z pliku" w menu głównym.
    /// </summary>
    /// <param name="sender">Obiekt wysyłający zdarzenie.</param>
    /// <param name="e">Dane zdarzenia kliknięcia przycisku.</param>
    private void LoadTextureFromFile(object sender, RoutedEventArgs e) {
        // Okienko dialogowe
        Microsoft.Win32.OpenFileDialog openFileDialog = new() {
            Filter = "Pliki obrazu (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
        };

        if (openFileDialog.ShowDialog() == true) {
            _controller.LoadTexture(openFileDialog.FileName);
        }
    }

    /// <summary>
    /// Obsługuje zdarzenie kliknięcia przycisku "Plik -> Wczytaj mapę wektorów normalnych" w menu głównym.
    /// </summary>
    /// <param name="sender">Obiekt wysyłający zdarzenie.</param>
    /// <param name="e">Dane zdarzenia kliknięcia przycisku.</param>
    private void LoadNormalVectorsMapFromFile(object sender, RoutedEventArgs e) {
        // Okienko dialogowe
        Microsoft.Win32.OpenFileDialog openFileDialog = new() {
            Filter = "Pliki obrazów (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
        };

        if (openFileDialog.ShowDialog() == true) {
            _controller.LoadNormalMap(openFileDialog.FileName);
        }
    }

    /// <summary>
    /// Obsługuje zdarzenie kliknięcia przycisku "Kolory -> Wybierz jednolity kolor obiektu" w menu głównym.
    /// </summary>
    private void SelectObjectColor(object sender, RoutedEventArgs e) {
        var dialog = new ColorPickerDialog(_objectColor, "Wybierz jednolity kolor obiektu") {
            Owner = this
        };

        if (dialog.ShowDialog() == true) {
            Color newColor = dialog.SelectedColor;
            _controller.SetObjectColor(newColor.R, newColor.G, newColor.B);
        }
    }

    /// <summary>
    /// Obsługuje zdarzenie kliknięcia przycisku "Kolory -> Wybierz kolor padającego światła" w menu głównym.
    /// </summary>
    private void SelectLightColor(object sender, RoutedEventArgs e) {
        var dialog = new ColorPickerDialog(_lightColor, "Wybierz kolor padającego światła") {
            Owner = this
        };

        if (dialog.ShowDialog() == true) {
            Color newColor = dialog.SelectedColor;
            _controller.SetLightColor(newColor.R, newColor.G, newColor.B);
        }
    }

    /// <summary>
    /// Obsługuje zdarzenie kliknięcia przycisku "Pomoc -> Instrukcja obsługi" w menu głównym.
    /// </summary>
    /// <param name="sender">Obiekt wysyłający zdarzenie.</param>
    /// <param name="e">Dane zdarzenia kliknięcia przycisku.</param>
    private void ShowInstructions(object sender, RoutedEventArgs e) {
        // Wyświetlanie instrukcji obsługi
    }

    //==========================================
    // OBSŁUGA PANELU STEROWANIA SEKCJA 1
    //==========================================

    /// <summary>
    /// Obsługuje zdarzenie zaznaczenia opcji "Pokaż wielokąty kontrolne Beziera" w panelu sterowania.
    /// </summary>
    /// <param name="sender">Obiekt wysyłający zdarzenie.</param>
    /// <param name="e">Dane zdarzenia zaznaczenia opcji.</param>
    private void ShowBezierPolygonChecked(object sender, RoutedEventArgs e) => _controller.SetPolygonVisibility(true);

    /// <summary>
    /// Obsługuje zdarzenie odznaczenia opcji "Pokaż wielokąty kontrolne Beziera" w panelu sterowania.
    /// </summary>
    /// <param name="sender">Obiekt wysyłający zdarzenie.</param>
    /// <param name="e">Dane zdarzenia odznaczenia opcji.</param>
    private void ShowBezierPolygonUnchecked(object sender, RoutedEventArgs e) => _controller.SetPolygonVisibility(false);

    /// <summary>
    /// Obsługuje zdarzenie zaznaczenia opcji "Pokaż siatkę trójkątów" w panelu sterowania.
    /// </summary>
    /// <param name="sender">Obiekt wysyłający zdarzenie.</param>
    /// <param name="e">Dane zdarzenia zaznaczenia opcji.</param>
    private void ShowGridChecked(object sender, RoutedEventArgs e) => _controller.SetGridVisibility(true);

    /// <summary>
    /// Obsługuje zdarzenie odznaczenia opcji "Pokaż siatkę trójkątów" w panelu sterowania.
    /// </summary>
    /// <param name="sender">Obiekt wysyłający zdarzenie.</param>
    /// <param name="e">Dane zdarzenia odznaczenia opcji.</param>
    private void ShowGridUnchecked(object sender, RoutedEventArgs e) => _controller.SetGridVisibility(false);

    /// <summary>
    /// Obsługuje zdarzenie zaznaczenia opcji "Pokaż wypełnienie trójkątów" w panelu sterowania.
    /// </summary>
    /// <param name="sender">Obiekt wysyłający zdarzenie.</param>
    /// <param name="e">Dane zdarzenia zaznaczenia opcji.</param>
    private void FillTrianglesChecked(object sender, RoutedEventArgs e) => _controller.SetFillVisibility(true);

    /// <summary>
    /// Obsługuje zdarzenie odznaczenia opcji "Pokaż wypełnienie trójkątów" w panelu sterowania.
    /// </summary>
    /// <param name="sender">Obiekt wysyłający zdarzenie.</param>
    /// <param name="e">Dane zdarzenia odznaczenia opcji.</param>
    private void FillTrianglesUnchecked(object sender, RoutedEventArgs e) => _controller.SetFillVisibility(false);

    /// <summary>
    /// Obsługuje zdarzenie zmiany wartości suwaka "Dokładność podziału siatki" w panelu sterowania.
    /// </summary>
    /// <param name="sender">Obiekt wysyłający zdarzenie.</param>
    /// <param name="e">Dane zdarzenia zmiany wartości suwaka.</param>
    private void GridResolutionChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => _controller.SetGridResolution((int)e.NewValue);


    //==========================================
    // OBSŁUGA PANELU STEROWANIA SEKCJA 2
    //==========================================

    /// <summary>
    /// Obsługuje zdarzenie ustawienia trybu wypełniania w panelu sterowania na "Jednolity kolor".
    /// </summary>
    /// <param name="sender">Obiekt wysyłający zdarzenie.</param>
    /// <param name="e">Dane zdarzenia zmiany trybu wypełniania.</param>
    private void SetFillModeSolidColor(object sender, RoutedEventArgs e) => _controller.SetFillMode(false);

    /// <summary>
    /// Obsługuje zdarzenie ustawienia trybu wypełniania w panelu sterowania na "Tekstura z pliku obrazu".
    /// </summary>
    /// <param name="sender">Obiekt wysyłający zdarzenie.</param>
    /// <param name="e">Dane zdarzenia zmiany trybu wypełniania.</param>
    private void SetFillModeTexture(object sender, RoutedEventArgs e) => _controller.SetFillMode(true);

    /// <summary>
    /// Obsługuje zdarzenie zaznaczenia opcji "Użyj mapy wektorów normalnych" w panelu sterowania.
    /// </summary>
    /// <param name="sender">Obiekt wysyłający zdarzenie.</param>
    /// <param name="e">Dane zdarzenia zaznaczenia opcji.</param>
    private void SetNormalVectorsMapUsageChecked(object sender, RoutedEventArgs e) => _controller.SetNormalVectorsMapUsage(true);

    /// <summary>
    /// Obsługuje zdarzenie odznaczenia opcji "Użyj mapy wektorów normalnych" w panelu sterowania.
    /// </summary>
    /// <param name="sender">Obiekt wysyłający zdarzenie.</param>
    /// <param name="e">Dane zdarzenia odznaczenia opcji.</param>
    private void SetNormalVectorsMapUsageUnchecked(object sender, RoutedEventArgs e) => _controller.SetNormalVectorsMapUsage(false);


    //==========================================
    // OBSŁUGA PANELU STEROWANIA SEKCJA 3
    //==========================================

    /// <summary>
    /// Obsługuje zdarzenie zmiany wartości suwaka "Rozproszenie" w panelu sterowania.
    /// </summary>
    /// <param name="sender">Obiekt wysyłający zdarzenie.</param>
    /// <param name="e">Dane zdarzenia zmiany wartości suwaka.</param>
    private void SetKdValue(object sender, RoutedPropertyChangedEventArgs<double> e) => _controller.SetKd((float)e.NewValue);

    /// <summary>
    /// Obsługuje zdarzenie zmiany wartości suwaka "Odbicie" w panelu sterowania.
    /// </summary>
    /// <param name="sender">Obiekt wysyłający zdarzenie.</param>
    /// <param name="e">Dane zdarzenia zmiany wartości suwaka.</param>
    private void SetKsValue(object sender, RoutedPropertyChangedEventArgs<double> e) => _controller.SetKs((float)e.NewValue);

    /// <summary>
    /// Obsługuje zdarzenie zmiany wartości suwaka "Połysk" w panelu sterowania.
    /// </summary>
    /// <param name="sender">Obiekt wysyłający zdarzenie.</param>
    /// <param name="e">Dane zdarzenia zmiany wartości suwaka.</param>
    private void SetMValue(object sender, RoutedPropertyChangedEventArgs<double> e) => _controller.SetM((int)e.NewValue);

    /// <summary>
    /// Obsługuje zdarzenie zmiany wartości suwaka "Wysokość światła" w panelu sterowania.
    /// </summary>
    /// <param name="sender">Obiekt wysyłający zdarzenie.</param>
    /// <param name="e">Dane zdarzenia zmiany wartości suwaka.</param>
    private void SetLightHeightValue(object sender, RoutedPropertyChangedEventArgs<double> e) => _controller.SetLightHeight((float)e.NewValue);

    /// <summary>
    /// Obsługuje zdarzenie zaznaczenia opcji "Odtwarzaj animację światła" w panelu sterowania.
    /// </summary>
    /// <param name="sender">Obiekt wysyłający zdarzenie.</param>
    /// <param name="e">Dane zdarzenia zaznaczenia opcji.</param>
    private void ToggleLightAnimationChecked(object sender, RoutedEventArgs e) => _controller.ToggleLightAnimation(true);

    /// <summary>
    /// Obsługuje zdarzenie odznaczenia opcji "Odtwarzaj animację światła" w panelu sterowania.
    /// </summary>
    /// <param name="sender">Obiekt wysyłający zdarzenie.</param>
    /// <param name="e">Dane zdarzenia odznaczenia opcji.</param>
    private void ToggleLightAnimationUnchecked(object sender, RoutedEventArgs e) => _controller.ToggleLightAnimation(false);
}

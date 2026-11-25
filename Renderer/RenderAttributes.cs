namespace Renderer;

using System.Numerics;

/// <summary>
/// Atrybuty renderowania ustawiające parametry widoczności, wyglądu i oświetlenia sceny.
/// </summary>
public class RenderAttributes {
    // ==========================================
    // 1. WIDOCZNOŚĆ (SEKCJA 1)
    // ==========================================

    public bool ShowControlPolygons { get; set; }
    public bool ShowGrid { get; set; }
    public bool ShowFill { get; set; }

    // ==========================================
    // 2. MATERIAŁ I TEKSTURY (SEKCJA 2)
    // ==========================================

    public bool UseTexture { get; set; }
    public bool UseNormalMap { get; set; }

    public Vector3 ObjectColor { get; private set; }
    public TextureData? Texture { get; set; }
    public TextureData? NormalMap { get; set; }

    // ==========================================
    // 3. OŚWIETLENIE (SEKCJA 3)
    // ==========================================

    public float Kd { get; set; }
    public float Ks { get; set; }
    public int M { get; set; }
    public Vector3 LightColor { get; private set; }

    // ==========================================
    // METODY POMOCNICZE DLA KONTROLERA
    // ==========================================

    /// <summary>
    /// Ustawia jednolity kolor wypełnienia z wartości 0-255 (normalizuje do 0.0-1.0).
    /// </summary>
    public void SetObjectColor(byte r, byte g, byte b) {
        ObjectColor = new Vector3(r / 255f, g / 255f, b / 255f);
    }

    /// <summary>
    /// Ustawia kolor światła z wartości 0-255 (normalizuje do 0.0-1.0).
    /// </summary>
    public void SetLightColor(byte r, byte g, byte b) {
        LightColor = new Vector3(r / 255f, g / 255f, b / 255f);
    }
}
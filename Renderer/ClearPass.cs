namespace Renderer;

using Models;

/// <summary>
/// Pierwszy etap łańcucha renderowania, odpowiedzialny za wyczyszczenie tła i Z-buffera.
/// </summary>
internal class ClearPass : IRenderPass {
    private readonly byte _r;
    private readonly byte _g;
    private readonly byte _b;

    /// <summary>
    /// Inicjalizuje etap czyszczenia z wybranym kolorem tła.
    /// </summary>
    /// <param name="r">Składowa czerwona tła (0-255). Domyślnie 0xE2.</param>
    /// <param name="g">Składowa zielona tła (0-255). Domyślnie 0xE2.</param>
    /// <param name="b">Składowa niebieska tła (0-255). Domyślnie 0xE2.</param>
    public ClearPass(byte r = 0xE2, byte g = 0xE2, byte b = 0xE2) {
        _r = r;
        _g = g;
        _b = b;
    }

    /// <summary>
    /// Czyści bufor pikseli kolorem tła oraz resetuje Z-buffer.
    /// </summary>
    /// <param name="ctx">Kontekst bieżącej klatki.</param>
    /// <param name="scene">Model sceny zawierający geometrię i światło.</param>
    /// <param name="attr">Atrybuty renderera sterujące widocznością, wyglądem i oświetleniem.</param>
    public void Execute(RenderContext ctx, SceneModel scene, RenderAttributes attr) {
        ctx.Clear(_r, _g, _b);
    }
}
namespace Renderer;

using Models;

/// <summary>
/// Wspólny interfejs dla pojedynczego etapu w łańcuchu renderowania.
/// </summary>
internal interface IRenderPass {
    /// <summary>
    /// Wykonuje etap renderowania, modyfikując bufory w przekazanym kontekście.
    /// </summary>
    /// <param name="ctx">Kontekst bieżącej klatki.</param>
    /// <param name="scene">Model sceny zawierający geometrię i światło.</param>
    /// <param name="attr">Atrybuty renderera sterujące widocznością, wyglądem i oświetleniem.</param>
    void Execute(RenderContext ctx, SceneModel scene, RenderAttributes attr);
}
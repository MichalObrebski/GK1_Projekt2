namespace Renderer;

using System;
using System.Numerics;
using Models;

/// <summary>
/// Etap łańcucha renderowania rysujący wielobok (siatkę 4x4) punktów kontrolnych dla każdej powierzchni Béziera na scenie.
/// </summary>
internal class ControlPolygonPass : IRenderPass {
    private readonly byte _lineR, _lineG, _lineB;
    private readonly byte _ptR, _ptG, _ptB;

    /// <summary>
    /// Inicjalizuje etap rysowania siatki kontrolnej z wybraną kolorystyką linii i punktów.
    /// </summary>
    public ControlPolygonPass(
        byte lineR = 0x50, byte lineG = 0x8C, byte lineB = 0xFF,
        byte ptR = 0xCC, byte ptG = 0xE0, byte ptB = 0xFF) {
        _lineR = lineR;
        _lineG = lineG;
        _lineB = lineB;
        _ptR = ptR;
        _ptG = ptG;
        _ptB = ptB;
    }

    /// <summary>
    /// Rysuje krawędzie i punkty siatki kontrolnej z testem Z-buffera, jeśli odpowiednia flaga w atrybutach jest włączona.
    /// </summary>
    /// <param name="ctx">Kontekst bieżącej klatki.</param>
    /// <param name="scene">Model sceny zawierający geometrię i światło.</param>
    /// <param name="attr">Atrybuty renderera sterujące widocznością, wyglądem i oświetleniem.</param>
    public void Execute(RenderContext ctx, SceneModel scene, RenderAttributes attr) {
        if (!attr.ShowControlPolygons)
            return;

        float lineZBias = ctx.Scale > 1e-5f ? 3f / ctx.Scale : 1.5e-3f;
        float markerZBias = ctx.Scale > 1e-5f ? 4f / ctx.Scale : 2e-3f;

        foreach (var surface in scene.Surfaces) {
            var cp = surface.ControlPoints;
            int rows = cp.GetLength(0);
            int cols = cp.GetLength(1);

            // Krawędzie wieloboku kontrolnego
            for (int i = 0; i < rows; i++) {
                for (int j = 0; j < cols; j++) {
                    Vector3 v = cp[i, j];
                    Vector2 p = ctx.SceneToScreen(v);
                    int x = (int)MathF.Round(p.X);
                    int y = (int)MathF.Round(p.Y);
                    float z = v.Z + lineZBias;

                    if (j + 1 < cols) {
                        Vector3 vRight = cp[i, j + 1];
                        Vector2 pRight = ctx.SceneToScreen(vRight);
                        LineRasterizer.DrawLineWithZ(
                            ctx,
                            x, y, z,
                            (int)MathF.Round(pRight.X), (int)MathF.Round(pRight.Y), vRight.Z + lineZBias,
                            _lineR, _lineG, _lineB);
                    }

                    if (i + 1 < rows) {
                        Vector3 vDown = cp[i + 1, j];
                        Vector2 pDown = ctx.SceneToScreen(vDown);
                        LineRasterizer.DrawLineWithZ(
                            ctx,
                            x, y, z,
                            (int)MathF.Round(pDown.X), (int)MathF.Round(pDown.Y), vDown.Z + lineZBias,
                            _lineR, _lineG, _lineB);
                    }
                }
            }

            // Znaczniki punktów kontrolnych
            for (int i = 0; i < rows; i++) {
                for (int j = 0; j < cols; j++) {
                    Vector3 v = cp[i, j];
                    Vector2 p = ctx.SceneToScreen(v);
                    ctx.DrawMarkerWithZ(
                        (int)MathF.Round(p.X),
                        (int)MathF.Round(p.Y),
                        v.Z + markerZBias,
                        radius: 3,
                        _ptR, _ptG, _ptB);
                }
            }
        }
    }
}
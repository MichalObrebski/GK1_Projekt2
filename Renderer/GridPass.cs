namespace Renderer;

using System;
using Models;

/// <summary>
/// Etap łańcucha renderowania rysujący krawędzie siatki trójkątów dla wszystkich powierzchni na scenie.
/// </summary>
internal class GridPass : IRenderPass {
    private readonly byte _r;
    private readonly byte _g;
    private readonly byte _b;

    /// <summary>
    /// Inicjalizuje etap rysowania siatki z wybranym kolorem linii.
    /// </summary>
    /// <param name="r">Składowa czerwona linii (0-255). Domyślnie 0x34.</param>
    /// <param name="g">Składowa zielona linii (0-255). Domyślnie 0xE0.</param>
    /// <param name="b">Składowa niebieska linii (0-255). Domyślnie 0x34.</param>
    public GridPass(byte r = 0x34, byte g = 0xE0, byte b = 0x34) {
        _r = r;
        _g = g;
        _b = b;
    }

    /// <summary>
    /// Rysuje krawędzie wszystkich trójkątów z testem Z-buffera, jeśli w atrybutach włączona jest widoczność siatki.
    /// </summary>
    /// <param name="ctx">Kontekst bieżącej klatki.</param>
    /// <param name="scene">Model sceny zawierający geometrię i światło.</param>
    /// <param name="attr">Atrybuty renderera sterujące widocznością, wyglądem i oświetleniem.</param>
    public void Execute(RenderContext ctx, SceneModel scene, RenderAttributes attr) {
        if (!attr.ShowGrid)
            return;

        // Niewielkie przesunięcie w stronę kamery (+Z), aby uniknąć Z-fightingu z wypełnionymi trójkątami
        float zBias = ctx.Scale > 1e-5f ? 2f / ctx.Scale : 1e-3f;

        foreach (var surface in scene.Surfaces) {
            foreach (var tri in surface.Triangles) {
                var p0 = ctx.SceneToScreen(tri.V0.Position);
                var p1 = ctx.SceneToScreen(tri.V1.Position);
                var p2 = ctx.SceneToScreen(tri.V2.Position);

                int x0 = (int)MathF.Round(p0.X);
                int y0 = (int)MathF.Round(p0.Y);
                int x1 = (int)MathF.Round(p1.X);
                int y1 = (int)MathF.Round(p1.Y);
                int x2 = (int)MathF.Round(p2.X);
                int y2 = (int)MathF.Round(p2.Y);

                float z0 = tri.V0.Position.Z + zBias;
                float z1 = tri.V1.Position.Z + zBias;
                float z2 = tri.V2.Position.Z + zBias;

                LineRasterizer.DrawLineWithZ(ctx, x0, y0, z0, x1, y1, z1, _r, _g, _b);
                LineRasterizer.DrawLineWithZ(ctx, x1, y1, z1, x2, y2, z2, _r, _g, _b);
                LineRasterizer.DrawLineWithZ(ctx, x2, y2, z2, x0, y0, z0, _r, _g, _b);
            }
        }
    }
}
namespace Renderer;

using System;
using System.Numerics;
using Models;

/// <summary>
/// Etap łańcucha renderowania rysujący zamkniętą trajektorię źródła światła,
/// jej wielobok kontrolny oraz aktualną pozycję światła.
/// </summary>
internal class LightPass : IRenderPass {
    private const int CurveSegments = 48;

    private readonly byte _polygonR, _polygonG, _polygonB;
    private readonly byte _curveR, _curveG, _curveB;
    private readonly byte _pointR, _pointG, _pointB;

    /// <summary>
    /// Inicjalizuje etap rysowania światła z wybraną paletą kolorów trajektorii.
    /// </summary>
    public LightPass(
        byte polygonR = 0xA0, byte polygonG = 0x8C, byte polygonB = 0x3C,
        byte curveR = 0xD8, byte curveG = 0x7A, byte curveB = 0x00,
        byte pointR = 0x40, byte pointG = 0x40, byte pointB = 0x50) {
        _polygonR = polygonR;
        _polygonG = polygonG;
        _polygonB = polygonB;
        _curveR = curveR;
        _curveG = curveG;
        _curveB = curveB;
        _pointR = pointR;
        _pointG = pointG;
        _pointB = pointB;
    }

    /// <summary>
    /// Rysuje zamknięty wielobok kontrolny, krzywą Béziera trajektorii oraz znacznik źródła światła.
    /// </summary>
    /// <param name="ctx">Kontekst bieżącej klatki.</param>
    /// <param name="scene">Model sceny zawierający geometrię i światło.</param>
    /// <param name="attr">Atrybuty renderera sterujące widocznością, wyglądem i oświetleniem.</param>
    public void Execute(RenderContext ctx, SceneModel scene, RenderAttributes attr) {
        var controlPoints = scene.Light.ControlPoints;
        int count = controlPoints.Count;

        if (count >= 2) {
            // Zamknięty wielobok kontrolny trajektorii światła
            for (int i = 0; i < count; i++) {
                Vector2 p0 = ctx.SceneToScreen(controlPoints[i]);
                Vector2 p1 = ctx.SceneToScreen(controlPoints[(i + 1) % count]);

                LineRasterizer.DrawLine(
                    ctx,
                    (int)MathF.Round(p0.X), (int)MathF.Round(p0.Y),
                    (int)MathF.Round(p1.X), (int)MathF.Round(p1.Y),
                    _polygonR, _polygonG, _polygonB);
            }

            // Zamknięta krzywa Béziera ruchu światła
            Vector2 prevScreen = ctx.SceneToScreen(scene.Light.EvaluateTrajectory(0f));
            int prevX = (int)MathF.Round(prevScreen.X);
            int prevY = (int)MathF.Round(prevScreen.Y);

            for (int s = 1; s <= CurveSegments; s++) {
                float t = (float)s / CurveSegments;
                Vector2 currScreen = ctx.SceneToScreen(scene.Light.EvaluateTrajectory(t));
                int currX = (int)MathF.Round(currScreen.X);
                int currY = (int)MathF.Round(currScreen.Y);

                LineRasterizer.DrawLine(ctx, prevX, prevY, currX, currY, _curveR, _curveG, _curveB);
                prevX = currX;
                prevY = currY;
            }
        }

        // Znaczniki punktów kontrolnych trajektorii
        for (int i = 0; i < count; i++) {
            Vector2 p = ctx.SceneToScreen(controlPoints[i]);
            ctx.DrawMarker((int)MathF.Round(p.X), (int)MathF.Round(p.Y), radius: 4, _pointR, _pointG, _pointB);
        }

        // Znacznik aktualnej pozycji źródła światła
        Vector2 lightScreen = ctx.SceneToScreen(scene.Light.Position);
        int lx = (int)MathF.Round(lightScreen.X);
        int ly = (int)MathF.Round(lightScreen.Y);

        Vector3 lightCol = Vector3.Clamp(attr.LightColor, Vector3.Zero, Vector3.One);
        byte lr = (byte)MathF.Round(lightCol.X * 255f);
        byte lg = (byte)MathF.Round(lightCol.Y * 255f);
        byte lb = (byte)MathF.Round(lightCol.Z * 255f);

        ctx.DrawMarker(lx, ly, radius: 7, 0x00, 0x00, 0x00);
        ctx.DrawMarker(lx, ly, radius: 6, lr, lg, lb);
        ctx.DrawMarker(lx, ly, radius: 2, 0xFF, 0xFF, 0xFF);
    }
}
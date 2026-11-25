namespace Renderer;

using System;
using System.Numerics;
using Models;

/// <summary>
/// Etap łańcucha renderowania wypełniający trójkąty w oparciu o algorytm scan-linii z interpolacją wag barycentrycznych i głębokości Z.
/// </summary>
internal class FillTrianglesPass : IRenderPass {
    /// <summary>
    /// Dane pomocnicze pojedynczego trójkąta używane podczas wypełniania scan-linii.
    /// </summary>
    private readonly struct TriangleData {
        public readonly Vertex V0;
        public readonly Vertex V1;
        public readonly Vertex V2;
        public readonly Vector2 P0;
        public readonly Vector2 P1;
        public readonly Vector2 P2;
        public readonly float InvTotalArea;
        public readonly float DAlphaDx;
        public readonly float DBetaDx;
        public readonly float DzDx;

        public TriangleData(Vertex v0, Vertex v1, Vertex v2, Vector2 p0, Vector2 p1, Vector2 p2, float invTotalArea) {
            V0 = v0;
            V1 = v1;
            V2 = v2;
            P0 = p0;
            P1 = p1;
            P2 = p2;
            InvTotalArea = invTotalArea;

            // Stałe pochodne wag barycentrycznych i głębokości Z przy kroku x -> x + 1
            DAlphaDx = (p2.Y - p1.Y) * invTotalArea;
            DBetaDx = (p0.Y - p2.Y) * invTotalArea;
            DzDx = DAlphaDx * (v0.Position.Z - v2.Position.Z) + DBetaDx * (v1.Position.Z - v2.Position.Z);
        }
    }

    /// <summary>
    /// Wypełnia wszystkie trójkąty na scenie, jeśli w atrybutach włączona jest widoczność wypełnienia.
    /// </summary>
    public void Execute(RenderContext ctx, SceneModel scene, RenderAttributes attr) {
        if (!attr.ShowFill)
            return;

        Vector3 lightPos = scene.Light?.Position ?? Vector3.Zero;

        foreach (var surface in scene.Surfaces) {
            foreach (var tri in surface.Triangles) {
                FillTriangle(ctx, tri, lightPos, attr);
            }
        }
    }

    /// <summary>
    /// Rasteryzuje pojedynczy trójkąt, dzieląc go poziomo na wysokości środkowego wierzchołka (p1.Y)
    /// na górną i dolną połówkę.
    /// </summary>
    private static void FillTriangle(RenderContext ctx, Triangle tri, Vector3 lightPos, RenderAttributes attr) {
        Vertex v0 = tri.V0;
        Vertex v1 = tri.V1;
        Vertex v2 = tri.V2;

        Vector2 p0 = ctx.SceneToScreen(v0.Position);
        Vector2 p1 = ctx.SceneToScreen(v1.Position);
        Vector2 p2 = ctx.SceneToScreen(v2.Position);

        // Sortowanie 3 wierzchołków rosnąco po współrzędnej Y (p0.Y <= p1.Y <= p2.Y)
        if (p0.Y > p1.Y) { (v0, v1) = (v1, v0); (p0, p1) = (p1, p0); }
        if (p0.Y > p2.Y) { (v0, v2) = (v2, v0); (p0, p2) = (p2, p0); }
        if (p1.Y > p2.Y) { (v1, v2) = (v2, v1); (p1, p2) = (p2, p1); }

        float totalHeight = p2.Y - p0.Y;
        if (totalHeight < 1e-5f)
            return;

        // Podwójne pole trójkąta ze znakiem (do współrzędnych barycentrycznych)
        float totalArea = EdgeFunction(p0, p1, p2);
        if (MathF.Abs(totalArea) < 1e-5f)
            return;

        var triData = new TriangleData(v0, v1, v2, p0, p1, p2, 1f / totalArea);

        // Współczynnik 1/m (dx/dy) dla krawędzi długiej P0 -> P2
        float invSlopeLong = (p2.X - p0.X) / totalHeight;

        // Górna połówka trójkąta: krawędź długa (P0 -> P2) oraz górna krótka (P0 -> P1)
        float upperHeight = p1.Y - p0.Y;
        if (upperHeight > 1e-5f) {
            int yStart = Math.Max(0, (int)MathF.Ceiling(p0.Y));
            int yEnd = Math.Min(ctx.Height - 1, (int)MathF.Ceiling(p1.Y) - 1);

            if (yStart <= yEnd) {
                float invSlopeShort = (p1.X - p0.X) / upperHeight;
                float xLong = p0.X + (yStart - p0.Y) * invSlopeLong;
                float xShort = p0.X + (yStart - p0.Y) * invSlopeShort;

                for (int y = yStart; y <= yEnd; y++) {
                    FillScanline(ctx, y, xLong, xShort, in triData, lightPos, attr);
                    xLong += invSlopeLong;
                    xShort += invSlopeShort;
                }
            }
        }

        // Dolna połówka trójkąta: krawędź długa (P0 -> P2) oraz dolna krótka (P1 -> P2)
        float lowerHeight = p2.Y - p1.Y;
        if (lowerHeight > 1e-5f) {
            int yStart = Math.Max(0, (int)MathF.Ceiling(p1.Y));
            int yEnd = Math.Min(ctx.Height - 1, (int)MathF.Ceiling(p2.Y) - 1);

            if (yStart <= yEnd) {
                float invSlopeShort = (p2.X - p1.X) / lowerHeight;
                float xLong = p0.X + (yStart - p0.Y) * invSlopeLong;
                float xShort = p1.X + (yStart - p1.Y) * invSlopeShort;

                for (int y = yStart; y <= yEnd; y++) {
                    FillScanline(ctx, y, xLong, xShort, in triData, lightPos, attr);
                    xLong += invSlopeLong;
                    xShort += invSlopeShort;
                }
            }
        }
    }

    /// <summary>
    /// Wypełnia pojedynczy poziomy odcinek (scan-linię) między dwoma aktywnymi krawędziami,
    /// używając przyrostowej interpolacji wag barycentrycznych i głębokości Z.
    /// </summary>
    private static void FillScanline(
        RenderContext ctx, int y, float xA, float xB,
        in TriangleData tri, Vector3 lightPos, RenderAttributes attr) {

        if (xA > xB)
            (xA, xB) = (xB, xA);

        int xStart = Math.Max(0, (int)MathF.Ceiling(xA));
        int xEnd = Math.Min(ctx.Width - 1, (int)MathF.Ceiling(xB) - 1);

        if (xStart > xEnd)
            return;

        // Jednorazowe wyznaczenie wag barycentrycznych i głębokości Z na początku scan-linii
        Vector2 startP = new(xStart, y);
        float alpha = EdgeFunction(tri.P1, tri.P2, startP) * tri.InvTotalArea;
        float beta = EdgeFunction(tri.P2, tri.P0, startP) * tri.InvTotalArea;
        float z = alpha * tri.V0.Position.Z + beta * tri.V1.Position.Z + (1f - alpha - beta) * tri.V2.Position.Z;

        float dAlphaDx = tri.DAlphaDx;
        float dBetaDx = tri.DBetaDx;
        float dzDx = tri.DzDx;

        bool useNormalMap = attr.UseNormalMap && attr.NormalMap != null;
        bool useUV = (attr.UseTexture && attr.Texture != null) || useNormalMap;

        for (int x = xStart; x <= xEnd; x++, alpha += dAlphaDx, beta += dBetaDx, z += dzDx) {
            if (!ctx.TryUpdateDepth(x, y, z))
                continue;

            float gamma = 1f - alpha - beta;

            // Interpolacja pozycji 3D i wektora normalnego
            Vector3 worldPos = alpha * tri.V0.Position + beta * tri.V1.Position + gamma * tri.V2.Position;
            Vector3 normal = alpha * tri.V0.Normal + beta * tri.V1.Normal + gamma * tri.V2.Normal;
            if (normal.LengthSquared() > 1e-8f)
                normal = Vector3.Normalize(normal);

            float u = 0f, v = 0f;
            if (useUV) {
                u = alpha * tri.V0.U + beta * tri.V1.U + gamma * tri.V2.U;
                v = alpha * tri.V0.V + beta * tri.V1.V + gamma * tri.V2.V;
            }

            Vector3 tangentU = Vector3.Zero;
            Vector3 tangentV = Vector3.Zero;
            if (useNormalMap) {
                tangentU = alpha * tri.V0.Pu + beta * tri.V1.Pu + gamma * tri.V2.Pu;
                tangentV = alpha * tri.V0.Pv + beta * tri.V1.Pv + gamma * tri.V2.Pv;
                if (tangentU.LengthSquared() > 1e-8f) tangentU = Vector3.Normalize(tangentU);
                if (tangentV.LengthSquared() > 1e-8f) tangentV = Vector3.Normalize(tangentV);
            }

            var (r, g, b) = LightingShader.CalculateColor(
                worldPos, normal, tangentU, tangentV, u, v, lightPos, attr);
            ctx.SetPixel(x, y, r, g, b);
        }
    }

    /// <summary>
    /// Wyznacznik 2D (podwójne pole ze znakiem trójkąta ABC) do obliczania współrzędnych barycentrycznych.
    /// </summary>
    private static float EdgeFunction(Vector2 a, Vector2 b, Vector2 c) => (c.X - a.X) * (b.Y - a.Y) - (c.Y - a.Y) * (b.X - a.X);
}
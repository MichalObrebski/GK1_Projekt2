namespace Models;

using System.Numerics;

public class Vertex {
    // Parametry powierzchni Béziera (0..1)
    public float U { get; }
    public float V { get; }

    // Geometria bazowa (przed obrotem)
    public Vector3 BasePosition { get; }
    public Vector3 BasePu { get; }
    public Vector3 BasePv { get; }
    public Vector3 BaseNormal { get; }

    // Geometria po obrocie
    public Vector3 Position { get; set; }
    public Vector3 Pu { get; set; }
    public Vector3 Pv { get; set; }
    public Vector3 Normal { get; set; }

    public Vertex(float u, float v, Vector3 position, Vector3 pu, Vector3 pv, Vector3 normal) {
        U = u;
        V = v;
        BasePosition = Position = position;
        BasePu = Pu = pu;
        BasePv = Pv = pv;
        BaseNormal = Normal = normal;
    }
}
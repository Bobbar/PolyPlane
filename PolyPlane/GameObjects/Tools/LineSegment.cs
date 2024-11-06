namespace PolyPlane.GameObjects.Tools
{
    public struct LineSegment
    {
        public D2DPoint A;
        public D2DPoint B;

        public LineSegment(D2DPoint a, D2DPoint b)
        {
            A = a;
            B = b;
        }
    }
}

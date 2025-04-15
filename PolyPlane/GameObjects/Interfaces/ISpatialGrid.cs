using PolyPlane.Helpers;

namespace PolyPlane.GameObjects.Interfaces
{
    public interface ISpatialGrid
    {
        D2DPoint Position { get; }
        int GridHash { get; }
        SpatialGridGameObject SpatialGridRef { get; set; }
    }
}

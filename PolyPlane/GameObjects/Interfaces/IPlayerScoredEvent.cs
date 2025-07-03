using PolyPlane.Rendering;

namespace PolyPlane.GameObjects.Interfaces
{
    public interface IPlayerScoredEvent
    {
        event EventHandler<PlayerScoredEventArgs> PlayerScoredEvent;
    }
}

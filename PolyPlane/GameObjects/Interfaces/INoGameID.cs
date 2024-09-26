namespace PolyPlane.GameObjects.Interfaces
{
    /// <summary>
    /// Marker interface for objects which will never need to be looked up or compared by ID.
    /// 
    /// Objects which implement this will not increment the global object ID.
    /// </summary>
    public interface INoGameID
    { }
}

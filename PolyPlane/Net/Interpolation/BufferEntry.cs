namespace PolyPlane.Net.Interpolation
{
    public struct BufferEntry<T>
    {
        public T State;
        public double UpdatedAt;

        public BufferEntry(T state, double updatedAt)
        {
            State = state;
            UpdatedAt = updatedAt;
        }
    }
}

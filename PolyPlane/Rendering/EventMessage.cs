namespace PolyPlane.Rendering
{
    public class EventMessage
    {
        public string Message;
        public EventType Type;

        public EventMessage() { }

        public EventMessage(string message, EventType type)
        {
            Message = message;
            Type = type;
        }
    }

    public enum EventType
    {
        Hit,
        Kill,
        Net,
        Chat
    }
}

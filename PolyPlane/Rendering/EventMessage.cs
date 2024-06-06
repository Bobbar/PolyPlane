using PolyPlane.GameObjects;

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

    public class PlayerScoredEventArgs
    {
        public FighterPlane Player;
        public FighterPlane Target;

        public PlayerScoredEventArgs(FighterPlane player, FighterPlane target)
        {
            Player = player;
            Target = target;
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

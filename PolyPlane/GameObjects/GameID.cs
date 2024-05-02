using NetStack.Serialization;

namespace PolyPlane.GameObjects
{
    [Serializable]
    public struct GameID : IEquatable<GameID>
    {
        public int PlayerID;
        public long ObjectID;

        public GameID() { }

        public GameID(int playerID, long objectID)
        {
            PlayerID = playerID;
            ObjectID = objectID;
        }

        public GameID(uint playerID)
        {
            PlayerID = (int)playerID;
            ObjectID = -1;
        }

        public void Serialize(BitBuffer data)
        {
            data.AddInt(PlayerID)
            .AddLong(ObjectID);
        }

        public void Deserialize(BitBuffer data)
        {
            PlayerID = data.ReadInt();
            ObjectID = data.ReadLong();
        }

        public bool Equals(GameID other)
        {
            if (this.PlayerID != other.PlayerID)
                return false;

            if (this.ObjectID != other.ObjectID)
                return false;

            return true;
        }

        public override string ToString()
        {
            return $"PlrID: {PlayerID}  ObjID: {ObjectID}";
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PlayerID, ObjectID);
        }

    }
}

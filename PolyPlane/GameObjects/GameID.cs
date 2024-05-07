namespace PolyPlane.GameObjects
{
    public struct GameID : IEquatable<GameID>
    {
        public int PlayerID;
        public int ObjectID;

        public GameID() { }

        public GameID(int playerID, int objectID)
        {
            PlayerID = playerID;
            ObjectID = objectID;
        }

        public GameID(uint playerID)
        {
            PlayerID = (int)playerID;
            ObjectID = -1;
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

namespace PolyPlane.GameObjects
{
    public struct GameID : IEquatable<GameID>
    {
        //public int PlayerID;
        //public int ObjectID;

        public int PlayerID 
        {
            get {  return _playerID; }
            
            set 
            { 
                if (_playerID != value) 
                { 
                    _playerID = value;
                    _hashCode = HashCode.Combine(_playerID, _objectID);
                }
            }
        }

        public int ObjectID
        {
            get { return _objectID; }

            set
            {
                if (_objectID != value)
                {
                    _objectID = value;
                    _hashCode = HashCode.Combine(_playerID, _objectID);
                }
            }
        }


        private int _playerID;
        private int _objectID;

        private int _hashCode = -1;

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
            return _hashCode;
            //return HashCode.Combine(PlayerID, ObjectID);
        }

    }
}

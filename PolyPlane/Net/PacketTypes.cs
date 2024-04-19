namespace PolyPlane.Net
{
    public enum PacketTypes
    {
        PlaneUpdate,
        MissileUpdate,
        Impact,
        NewPlayer,
        NewBullet,
        NewMissile,
        NewDecoy,
        SetID,
        GetNextID,
        ChatMessage,
        GetOtherPlanes,
        ExpiredObjects,
        PlayerDisconnect,
        PlayerReset,
        ServerSync,
        Discovery,
        KickPlayer

    }
}

namespace PolyPlane.Net
{
    public enum PacketTypes
    {
        PlaneUpdate,
        PlaneListUpdate,
        MissileUpdateList,
        MissileUpdate,
        Impact,
        NewPlayer,
        NewBullet,
        NewMissile,
        NewDecoy,
        SetID,
        ChatMessage,
        GetOtherPlanes,
        ExpiredObjects,
        PlayerDisconnect,
        PlayerReset,
        ServerSync,
        Discovery,
        KickPlayer,
        ImpactList,
        BulletList,
        DecoyList,
        PlayerEvent

    }
}

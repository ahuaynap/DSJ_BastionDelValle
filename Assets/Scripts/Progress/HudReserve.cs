public static class HudReserve
{
    public const float ReferenceHeight = 1080f;
    public const float TopBar = 64f;
    public const float Habitat = 44f;
    public const float Tray = 110f;
    public const float Preparation = 72f;

    public static float TopFraction(bool habitat)
    {
        return (TopBar + (habitat ? Habitat : 0f)) / ReferenceHeight;
    }

    public static float BottomFraction(bool tray)
    {
        return (Preparation + (tray ? Tray : 0f)) / ReferenceHeight;
    }
}

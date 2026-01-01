namespace TabletDeck;

internal static class TokenUtil
{
    public static string NewToken() => Convert.ToHexString(Guid.NewGuid().ToByteArray());
}
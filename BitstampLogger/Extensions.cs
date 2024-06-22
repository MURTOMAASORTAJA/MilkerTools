namespace BitstampLogger;

public static class Extensions
{
    public static long ToUnixTimestamp(this DateTime dateTime) => new DateTimeOffset(dateTime).ToUnixTimeMilliseconds();
}

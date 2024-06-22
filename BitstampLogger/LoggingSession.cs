namespace BitstampLogger;

public class LoggingSession
{
    public DateTime? LastLoggingTime { get; set; }

    public DateTime? TimestampOfNewestItemInDb { get; set; }
}

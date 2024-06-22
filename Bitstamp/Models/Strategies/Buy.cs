namespace MilkerTools.Models.Strategies;

public class Buy
{
    public string MarketSymbol { get; set; }

    
}

public class OhlcHistoryContextOptions
{
    // Timeframe in seconds
    public int Step { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public (DateTime StartDate, DateTime EndDate)[] GetRequestRanges()
    {
        List<(DateTime StartDate, DateTime EndDate)> ranges = [];

        DateTime currentStart = StartDate;
        int maxItems = 1000;
        TimeSpan interval = TimeSpan.FromSeconds(Step * maxItems);

        while (currentStart < EndDate)
        {
            DateTime currentEnd = currentStart.Add(interval);
            if (currentEnd > EndDate)
            {
                currentEnd = EndDate;
            }
            ranges.Add((currentStart, currentEnd));
            currentStart = currentEnd;
        }

        return [.. ranges];
    }
}
namespace MilkerTools.Models.Strategies;

internal class PumpProject
{
    /// <summary>
    /// The pump project stops when this profit ratio has been reached.
    /// </summary>
    public decimal? ProfitTargetRatio { get; set; }

    /// <summary>
    /// The pump project stops when this loss ratio has been reached.
    /// </summary>
    public decimal? StopLossRatio { get; set; }
}

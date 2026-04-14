namespace LedgerFlow.Domain.Entities;

public class DocumentNumberSequence
{
    public int Id { get; set; }
    public string SeriesPrefix { get; set; } = string.Empty;
    public int Year { get; set; }
    public int LastNumber { get; set; }
}

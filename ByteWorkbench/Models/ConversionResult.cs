namespace ByteWorkbench.Models;

public sealed class ConversionResult
{
    public required IReadOnlyList<RepresentationRow> Representations { get; init; }
    public required IReadOnlyList<EndianRow> EndianRows { get; init; }
    public required string DetectedFormat { get; init; }
    public required string Summary { get; init; }
}

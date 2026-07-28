namespace Blex.Generators;

/// <summary>
/// Location captured as plain values so pipeline outputs stay equatable (a raw
/// <see cref="Microsoft.CodeAnalysis.Location"/> roots syntax trees and defeats incremental caching).
/// </summary>
internal sealed record LocationInfoBlex(string FilePath, int SpanStart, int SpanLength, int StartLine, int StartCharacter, int EndLine, int EndCharacter)
    : System.IEquatable<LocationInfoBlex>
{
    public static LocationInfoBlex? From(Microsoft.CodeAnalysis.Location? location)
    {
        if (location is null || location.SourceTree is null)
            return null;

        var span = location.GetLineSpan();
        return new LocationInfoBlex(
            location.SourceTree.FilePath,
            location.SourceSpan.Start,
            location.SourceSpan.Length,
            span.StartLinePosition.Line,
            span.StartLinePosition.Character,
            span.EndLinePosition.Line,
            span.EndLinePosition.Character);
    }

    public Microsoft.CodeAnalysis.Location ToLocation()
        => Microsoft.CodeAnalysis.Location.Create(
            FilePath,
            new Microsoft.CodeAnalysis.Text.TextSpan(SpanStart, SpanLength),
            new Microsoft.CodeAnalysis.Text.LinePositionSpan(
                new Microsoft.CodeAnalysis.Text.LinePosition(StartLine, StartCharacter),
                new Microsoft.CodeAnalysis.Text.LinePosition(EndLine, EndCharacter)));
}

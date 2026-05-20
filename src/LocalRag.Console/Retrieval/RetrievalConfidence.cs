namespace LocalRag;

internal sealed record RetrievalConfidence(string Label, string Explanation)
{
    public static RetrievalConfidence From(IReadOnlyList<ScoredChunk> chunks)
    {
        if (chunks.Count == 0)
        {
            return new RetrievalConfidence("None", "no matching context was found");
        }

        var topScore = chunks.Max(chunk => chunk.Score);
        return topScore switch
        {
            >= 0.55f => new RetrievalConfidence("High", $"top match score {topScore:0.000}"),
            >= 0.40f => new RetrievalConfidence("Medium", $"top match score {topScore:0.000}"),
            >= 0.25f => new RetrievalConfidence("Low", $"top match score {topScore:0.000}"),
            _ => new RetrievalConfidence("Very low", $"top match score {topScore:0.000}")
        };
    }
}

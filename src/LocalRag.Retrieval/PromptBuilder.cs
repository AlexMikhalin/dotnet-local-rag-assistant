using System.Text;

namespace LocalRag.Retrieval;

public static class PromptBuilder
{
    public static string Build(string question, IReadOnlyList<ScoredChunk> chunks)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Use the context below to answer the question.");
        builder.AppendLine("Cite sources using their source labels when useful.");
        builder.AppendLine();
        builder.AppendLine("Context:");

        foreach (var chunk in chunks)
        {
            builder.AppendLine($"--- Source: {chunk.Source} #chunk-{chunk.ChunkIndex}, score {chunk.Score:0.000}");
            builder.AppendLine(chunk.Text);
            builder.AppendLine();
        }

        builder.AppendLine("Question:");
        builder.AppendLine(question);
        return builder.ToString();
    }
}

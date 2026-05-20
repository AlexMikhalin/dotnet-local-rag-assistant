namespace LocalRag.Retrieval;

public sealed record ScoredChunk(float Score, string Source, int ChunkIndex, string Text);

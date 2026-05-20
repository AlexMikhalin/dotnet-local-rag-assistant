namespace LocalRag;

internal sealed record ScoredChunk(float Score, string Source, int ChunkIndex, string Text);

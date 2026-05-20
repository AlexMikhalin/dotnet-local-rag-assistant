namespace LocalRag;

internal sealed record OllamaTagsResponse(OllamaModel[] Models);

internal sealed record OllamaModel(string Name);

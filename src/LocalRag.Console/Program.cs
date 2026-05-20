using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var settings = RagSettings.FromEnvironment();
var app = new RagConsoleApp(settings);

await app.RunAsync(args);

internal sealed class RagConsoleApp(RagSettings settings)
{
    private readonly OllamaClient _ollama = new(settings.OllamaUrl);
    private readonly QdrantClient _qdrant = new(settings.QdrantUrl, settings.CollectionName);

    public async Task RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            await RunInteractiveAsync();
            return;
        }

        await RunCommandAsync(args);
    }

    private async Task RunInteractiveAsync()
    {
        Console.WriteLine("LocalRag.Console interactive mode");
        Console.WriteLine("Type `help` to see commands or `exit` to close.");
        Console.WriteLine();

        while (true)
        {
            Console.Write("rag> ");
            var line = Console.ReadLine();

            if (line is null)
            {
                return;
            }

            var args = ParseCommandLine(line);
            if (args.Length == 0)
            {
                continue;
            }

            var command = args[0].ToLowerInvariant();
            if (command is "exit" or "quit")
            {
                return;
            }

            if (command is "clear" or "cls")
            {
                Console.Clear();
                continue;
            }

            await RunCommandAsync(args);
            Console.WriteLine();
        }
    }

    private async Task RunCommandAsync(string[] args)
    {
        var command = args[0].ToLowerInvariant();
        var commandArgs = args.Skip(1).ToArray();

        try
        {
            switch (command)
            {
                case "status":
                    await PrintStatusAsync();
                    break;
                case "ingest":
                    await IngestAsync(commandArgs);
                    break;
                case "search":
                    await SearchAsync(commandArgs);
                    break;
                case "ask":
                    await AskAsync(commandArgs);
                    break;
                case "help":
                case "--help":
                case "-h":
                    PrintHelp();
                    break;
                default:
                    Console.WriteLine($"Unknown command: {command}");
                    PrintHelp();
                    break;
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine("A local service did not respond correctly.");
            Console.WriteLine(ex.Message);
            Console.WriteLine();
            Console.WriteLine("Check that Qdrant is running with `docker compose up -d` and Ollama is running.");
        }
    }

    private static string[] ParseCommandLine(string line)
    {
        var args = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var character in line)
        {
            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                AddCurrentArg();
                continue;
            }

            current.Append(character);
        }

        AddCurrentArg();
        return args.ToArray();

        void AddCurrentArg()
        {
            if (current.Length == 0)
            {
                return;
            }

            args.Add(current.ToString());
            current.Clear();
        }
    }

    private async Task PrintStatusAsync()
    {
        Console.WriteLine("Local RAG status");
        Console.WriteLine($"Ollama:  {settings.OllamaUrl}");
        Console.WriteLine($"Qdrant:  {settings.QdrantUrl}");
        Console.WriteLine($"Index:   {settings.CollectionName}");
        Console.WriteLine();

        var ollamaReady = await _ollama.IsReadyAsync();
        var qdrantReady = await _qdrant.IsReadyAsync();

        Console.WriteLine($"Ollama reachable: {YesNo(ollamaReady)}");
        Console.WriteLine($"Qdrant reachable: {YesNo(qdrantReady)}");

        if (ollamaReady)
        {
            var models = await _ollama.ListModelsAsync();
            Console.WriteLine("Ollama models:");
            foreach (var model in models.DefaultIfEmpty("(none pulled yet)"))
            {
                Console.WriteLine($"  - {model}");
            }
        }
    }

    private async Task IngestAsync(string[] args)
    {
        var inputPath = args.Length > 0 ? args[0] : "sample-docs";
        var fullPath = Path.GetFullPath(inputPath);

        if (!Directory.Exists(fullPath) && !File.Exists(fullPath))
        {
            Console.WriteLine($"Path not found: {fullPath}");
            return;
        }

        var files = FindTextFiles(fullPath).ToArray();
        if (files.Length == 0)
        {
            Console.WriteLine("No .txt or .md files found.");
            return;
        }

        Console.WriteLine($"Embedding model: {settings.EmbeddingModel}");
        Console.WriteLine($"Files found: {files.Length}");

        var firstEmbedding = await _ollama.EmbedAsync(settings.EmbeddingModel, "dimension probe");
        await _qdrant.EnsureCollectionAsync(firstEmbedding.Length);

        var totalChunks = 0;
        foreach (var file in files)
        {
            var text = await File.ReadAllTextAsync(file);
            var chunks = TextChunker.Split(text, settings.ChunkSize, settings.ChunkOverlap).ToArray();

            Console.WriteLine($"Indexing {Path.GetRelativePath(Environment.CurrentDirectory, file)} ({chunks.Length} chunks)");

            for (var i = 0; i < chunks.Length; i++)
            {
                var chunk = chunks[i];
                var vector = await _ollama.EmbedAsync(settings.EmbeddingModel, chunk);
                var point = QdrantPoint.FromChunk(file, i, chunk, vector);
                await _qdrant.UpsertAsync(point);
                totalChunks++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Done. Indexed chunks: {totalChunks}");
    }

    private async Task SearchAsync(string[] args)
    {
        var question = ReadQuestion(args);
        if (string.IsNullOrWhiteSpace(question))
        {
            Console.WriteLine("Usage: dotnet run -- search \"your question\"");
            return;
        }

        var chunks = await RetrieveAsync(question);
        PrintConfidence(chunks);
        PrintSources(chunks);
    }

    private async Task AskAsync(string[] args)
    {
        var question = ReadQuestion(args);
        if (string.IsNullOrWhiteSpace(question))
        {
            Console.WriteLine("Usage: dotnet run -- ask \"your question\"");
            return;
        }

        var chunks = await RetrieveAsync(question);
        if (chunks.Count == 0)
        {
            Console.WriteLine("No context found. Run `dotnet run -- ingest sample-docs` first.");
            return;
        }

        var prompt = PromptBuilder.Build(question, chunks);

        Console.WriteLine("Answer");
        Console.WriteLine("------");
        var answer = await _ollama.ChatAsync(settings.ChatModel, prompt);
        Console.WriteLine(answer.Trim());
        Console.WriteLine();

        PrintConfidence(chunks);
        PrintSources(chunks);
    }

    private async Task<IReadOnlyList<ScoredChunk>> RetrieveAsync(string question)
    {
        var vector = await _ollama.EmbedAsync(settings.EmbeddingModel, question);
        return await _qdrant.SearchAsync(vector, settings.TopK);
    }

    private static string ReadQuestion(string[] args) => string.Join(' ', args).Trim();

    private static IEnumerable<string> FindTextFiles(string path)
    {
        if (File.Exists(path))
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension is ".txt" or ".md")
            {
                yield return path;
            }

            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(file).ToLowerInvariant();
            if (extension is ".txt" or ".md")
            {
                yield return file;
            }
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("LocalRag.Console");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  status                         Check Ollama and Qdrant");
        Console.WriteLine("  ingest [path]                  Index .txt/.md documents, default: sample-docs");
        Console.WriteLine("  search \"question\"              Show retrieved chunks");
        Console.WriteLine("  ask \"question\"                 Answer using retrieved context");
        Console.WriteLine("  clear                          Clear the interactive console");
        Console.WriteLine("  exit                           Close interactive mode");
        Console.WriteLine();
        Console.WriteLine("Environment overrides:");
        Console.WriteLine("  OLLAMA_URL=http://localhost:11434");
        Console.WriteLine("  QDRANT_URL=http://localhost:6333");
        Console.WriteLine("  RAG_EMBED_MODEL=nomic-embed-text");
        Console.WriteLine("  RAG_CHAT_MODEL=llama3.2:3b");
        Console.WriteLine("  RAG_COLLECTION=local_rag_documents");
    }

    private static void PrintSources(IReadOnlyList<ScoredChunk> chunks)
    {
        Console.WriteLine("Sources");
        Console.WriteLine("-------");

        if (chunks.Count == 0)
        {
            Console.WriteLine("No results.");
            return;
        }

        foreach (var chunk in chunks)
        {
            Console.WriteLine($"[{chunk.Score:0.000}] {chunk.Source} #chunk-{chunk.ChunkIndex}");
            Console.WriteLine(TrimForConsole(chunk.Text));
            Console.WriteLine();
        }
    }

    private static void PrintConfidence(IReadOnlyList<ScoredChunk> chunks)
    {
        var confidence = RetrievalConfidence.From(chunks);
        Console.WriteLine($"Retrieval confidence: {confidence.Label} ({confidence.Explanation})");
        Console.WriteLine();
    }

    private static string TrimForConsole(string text)
    {
        const int maxLength = 420;
        var compact = text.ReplaceLineEndings(" ").Trim();
        return compact.Length <= maxLength ? compact : compact[..maxLength] + "...";
    }

    private static string YesNo(bool value) => value ? "yes" : "no";
}

internal sealed record RagSettings(
    Uri OllamaUrl,
    Uri QdrantUrl,
    string CollectionName,
    string EmbeddingModel,
    string ChatModel,
    int ChunkSize,
    int ChunkOverlap,
    int TopK)
{
    public static RagSettings FromEnvironment() => new(
        OllamaUrl: ReadUri("OLLAMA_URL", "http://localhost:11434"),
        QdrantUrl: ReadUri("QDRANT_URL", "http://localhost:6333"),
        CollectionName: Read("RAG_COLLECTION", "local_rag_documents"),
        EmbeddingModel: Read("RAG_EMBED_MODEL", "nomic-embed-text"),
        ChatModel: Read("RAG_CHAT_MODEL", "llama3.2:3b"),
        ChunkSize: ReadInt("RAG_CHUNK_SIZE", 900),
        ChunkOverlap: ReadInt("RAG_CHUNK_OVERLAP", 120),
        TopK: ReadInt("RAG_TOP_K", 5));

    private static string Read(string key, string fallback) =>
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key))
            ? fallback
            : Environment.GetEnvironmentVariable(key)!;

    private static Uri ReadUri(string key, string fallback) => new(Read(key, fallback));

    private static int ReadInt(string key, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(key), out var value) ? value : fallback;
}

internal sealed class OllamaClient(Uri baseUrl)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http = new() { BaseAddress = baseUrl };

    public async Task<bool> IsReadyAsync()
    {
        try
        {
            using var response = await _http.GetAsync("/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<string>> ListModelsAsync()
    {
        var response = await _http.GetFromJsonAsync<OllamaTagsResponse>("/api/tags", JsonOptions);
        return response?.Models.Select(model => model.Name).Order().ToArray() ?? [];
    }

    public async Task<float[]> EmbedAsync(string model, string input)
    {
        using var response = await _http.PostAsJsonAsync("/api/embed", new { model, input }, JsonOptions);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var embeddings = document.RootElement.GetProperty("embeddings");
        var first = embeddings[0];

        var vector = new float[first.GetArrayLength()];
        var index = 0;
        foreach (var value in first.EnumerateArray())
        {
            vector[index++] = value.GetSingle();
        }

        return vector;
    }

    public async Task<string> ChatAsync(string model, string prompt)
    {
        var request = new
        {
            model,
            stream = false,
            messages = new[]
            {
                new { role = "system", content = "You are a precise RAG assistant. Answer only from the provided context. If the context is insufficient, say what is missing." },
                new { role = "user", content = prompt }
            }
        };

        using var response = await _http.PostAsJsonAsync("/api/chat", request, JsonOptions);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }
}

internal sealed class QdrantClient(Uri baseUrl, string collectionName)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http = new() { BaseAddress = baseUrl };

    public async Task<bool> IsReadyAsync()
    {
        try
        {
            using var response = await _http.GetAsync("/collections");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task EnsureCollectionAsync(int vectorSize)
    {
        using var existing = await _http.GetAsync($"/collections/{collectionName}");
        if (existing.IsSuccessStatusCode)
        {
            return;
        }

        if (existing.StatusCode != HttpStatusCode.NotFound)
        {
            existing.EnsureSuccessStatusCode();
        }

        var createRequest = new
        {
            vectors = new
            {
                size = vectorSize,
                distance = "Cosine"
            }
        };

        using var created = await _http.PutAsJsonAsync($"/collections/{collectionName}", createRequest, JsonOptions);
        created.EnsureSuccessStatusCode();
    }

    public async Task UpsertAsync(QdrantPoint point)
    {
        var request = new { points = new[] { point } };
        using var response = await _http.PutAsJsonAsync($"/collections/{collectionName}/points?wait=true", request, JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<ScoredChunk>> SearchAsync(float[] vector, int limit)
    {
        var request = new
        {
            vector,
            limit,
            with_payload = true
        };

        using var response = await _http.PostAsJsonAsync($"/collections/{collectionName}/points/search", request, JsonOptions);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var result = document.RootElement.GetProperty("result");
        var chunks = new List<ScoredChunk>();

        foreach (var item in result.EnumerateArray())
        {
            var payload = item.GetProperty("payload");
            chunks.Add(new ScoredChunk(
                Score: item.GetProperty("score").GetSingle(),
                Source: payload.GetProperty("source").GetString() ?? "(unknown)",
                ChunkIndex: payload.GetProperty("chunk_index").GetInt32(),
                Text: payload.GetProperty("text").GetString() ?? string.Empty));
        }

        return chunks;
    }
}

internal sealed record QdrantPoint(
    string Id,
    float[] Vector,
    Dictionary<string, object> Payload)
{
    public static QdrantPoint FromChunk(string source, int chunkIndex, string text, float[] vector)
    {
        var relativeSource = Path.GetRelativePath(Environment.CurrentDirectory, source);
        return new QdrantPoint(
            Id: CreateStableGuid($"{relativeSource}:{chunkIndex}:{text}").ToString(),
            Vector: vector,
            Payload: new Dictionary<string, object>
            {
                ["source"] = relativeSource,
                ["chunk_index"] = chunkIndex,
                ["text"] = text
            });
    }

    private static Guid CreateStableGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash[..16]);
    }
}

internal sealed record ScoredChunk(float Score, string Source, int ChunkIndex, string Text);

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

internal static class TextChunker
{
    public static IEnumerable<string> Split(string text, int chunkSize, int overlap)
    {
        var normalized = text.ReplaceLineEndings("\n").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            yield break;
        }

        var start = 0;
        while (start < normalized.Length)
        {
            var length = Math.Min(chunkSize, normalized.Length - start);
            var end = start + length;

            if (end < normalized.Length)
            {
                var lastBreak = normalized.LastIndexOf('\n', end - 1, length);
                if (lastBreak > start + chunkSize / 2)
                {
                    end = lastBreak;
                }
            }

            var chunk = normalized[start..end].Trim();
            if (!string.IsNullOrWhiteSpace(chunk))
            {
                yield return chunk;
            }

            if (end >= normalized.Length)
            {
                break;
            }

            start = Math.Max(0, end - overlap);
        }
    }
}

internal static class PromptBuilder
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

internal sealed record OllamaTagsResponse(OllamaModel[] Models);

internal sealed record OllamaModel(string Name);

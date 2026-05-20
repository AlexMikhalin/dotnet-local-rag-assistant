using System.Net.Http;
using System.Text;
using LocalRag.Application;
using LocalRag.Retrieval;

namespace LocalRag.Cli;

internal sealed class RagConsoleApp(RagApplication application, RagSettings settings)
{
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
        Console.WriteLine("LocalRag.Cli interactive mode");
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
        var status = await application.GetStatusAsync();

        Console.WriteLine("Local RAG status");
        Console.WriteLine($"Ollama:  {settings.OllamaUrl}");
        Console.WriteLine($"Qdrant:  {settings.QdrantUrl}");
        Console.WriteLine($"Index:   {settings.CollectionName}");
        Console.WriteLine();
        Console.WriteLine($"Ollama reachable: {YesNo(status.OllamaReady)}");
        Console.WriteLine($"Qdrant reachable: {YesNo(status.QdrantReady)}");

        if (status.OllamaReady)
        {
            Console.WriteLine("Ollama models:");
            foreach (var model in status.OllamaModels.DefaultIfEmpty("(none pulled yet)"))
            {
                Console.WriteLine($"  - {model}");
            }
        }
    }

    private async Task IngestAsync(string[] args)
    {
        var inputPath = args.Length > 0 ? args[0] : "sample-docs";
        var fullPath = Path.GetFullPath(inputPath);

        Console.WriteLine($"Embedding model: {settings.EmbeddingModel}");

        var result = await application.IngestAsync(fullPath);
        if (!result.Success)
        {
            Console.WriteLine(result.Message);
            return;
        }

        Console.WriteLine($"Files found: {result.FilesIndexed}");
        foreach (var file in result.IndexedFiles)
        {
            Console.WriteLine($"Indexing {file.Source} ({file.ChunkCount} chunks)");
        }

        Console.WriteLine();
        Console.WriteLine($"Done. Indexed chunks: {result.ChunksIndexed}");
    }

    private async Task SearchAsync(string[] args)
    {
        var question = ReadQuestion(args);
        if (string.IsNullOrWhiteSpace(question))
        {
            Console.WriteLine("Usage: dotnet run -- search \"your question\"");
            return;
        }

        var result = await application.SearchAsync(question);
        PrintConfidence(result.Confidence);
        PrintSources(result.Chunks);
    }

    private async Task AskAsync(string[] args)
    {
        var question = ReadQuestion(args);
        if (string.IsNullOrWhiteSpace(question))
        {
            Console.WriteLine("Usage: dotnet run -- ask \"your question\"");
            return;
        }

        var result = await application.AskAsync(question);
        if (!result.HasContext)
        {
            Console.WriteLine("No context found. Run `dotnet run -- ingest sample-docs` first.");
            return;
        }

        Console.WriteLine("Answer");
        Console.WriteLine("------");
        Console.WriteLine(result.Answer.Trim());
        Console.WriteLine();

        PrintConfidence(result.Confidence);
        PrintSources(result.Chunks);
    }

    private static string ReadQuestion(string[] args) => string.Join(' ', args).Trim();

    private static void PrintHelp()
    {
        Console.WriteLine("LocalRag.Cli");
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

    private static void PrintConfidence(RetrievalConfidence confidence)
    {
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

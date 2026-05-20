using LocalRag.Application;
using LocalRag.Cli;
using LocalRag.Infrastructure.Ollama;
using LocalRag.Infrastructure.Qdrant;

var settings = RagSettings.FromEnvironment();
var ai = new OllamaClient(settings.OllamaUrl);
var vectorStore = new QdrantClient(settings.QdrantUrl, settings.CollectionName);
var application = new RagApplication(settings, ai, vectorStore);
var app = new RagConsoleApp(application, settings);

await app.RunAsync(args);

using LocalRag;

var settings = RagSettings.FromEnvironment();
var app = new RagConsoleApp(settings);

await app.RunAsync(args);

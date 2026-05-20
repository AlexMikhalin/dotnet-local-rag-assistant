using LocalRag.Application;
using LocalRag.Cli;

var settings = RagSettings.FromEnvironment();
var application = new RagApplication(settings);
var app = new RagConsoleApp(application, settings);

await app.RunAsync(args);

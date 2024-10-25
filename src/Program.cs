using ScriptLauncher;
using Spectre.Console.Cli;

var app = new CommandApp<RootCommand>();
app.Configure(x =>
{
    x.SetApplicationName("scrl");
});

return app.Run(args);

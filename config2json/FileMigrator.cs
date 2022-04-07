using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.ConfigFile;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Config2Json;

public class FileMigrator
{
    public IEnumerable<string> FilesToSquash { get; }
    public string Prefix { get; }
    public bool Raw { get; }
    public IConsole Console { get; }

    public FileMigrator(IEnumerable<string> filesToSquash, IConsole console, string prefix, bool raw)
    {
        FilesToSquash = filesToSquash;
        Console = console;
        Prefix = prefix;
        Raw = raw;
    }

    public async Task MigrateFiles()
    {
        // migrate all sequentially

        foreach (var file in FilesToSquash.Where(file => Constants.SupportedExtensions.Contains(Path.GetExtension(file))))
        {
            await MigrateFile(file);
        }
    }

    private async Task MigrateFile(string file)
    {
        var fileName = Path.GetFileName(file);
        try
        {
            //based on https://github.com/aspnet/Entropy/tree/7c027069b715a4b2ffd126f58def04c6111925c3/samples/Config.CustomConfigurationProviders.Sample
            Console.WriteLine($"Migrating {fileName}...");

            var config = new ConfigurationBuilder()
                .Add(new ConfigFileConfigurationProvider(file, true, false, Console,
                    new KeyValueParser(logger: Console),
                    new KeyValueParser("name", "connectionString", Console),
                    new KeyValueParser("name", logger: Console)))
                .Build();

            if (Raw)
            {
                foreach (var key in ((ConfigFileConfigurationProvider)config.Providers.First()).Keys)
                {
                    Console.Write(key);
                    Console.Write(" = ");
                    Console.WriteLine(config[key]);
                }
            }

            var jsonObject = GetConfigAsJObject(config);

            if (!string.IsNullOrEmpty(Prefix))
            {
                jsonObject = new JsonObject { { Prefix, jsonObject } };
            }

            //write to file
            await using var fs = File.Open(Path.ChangeExtension(file, "json"), FileMode.Create);

            await JsonSerializer.SerializeAsync(fs, jsonObject, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            Console.WriteLine($"Migration of {fileName} to {Path.GetFileName(fs.Name)} complete");
        }
        catch (System.Exception ex)
        {
            Console.WriteLine($"An error occurred migrating {fileName}: ");
            Console.WriteLine(ex);
        }
    }

    private static JsonNode GetConfigAsJObject(IConfiguration config)
    {
        var children = config.GetChildren().ToArray();
        if (children.All(c => int.TryParse(c.Key, out _)))
        {
            var root = new JsonArray();

            foreach (var child in children.OrderBy(c => int.Parse(c.Key)))
            {
                //not strictly correct, but we'll go with it.
                if (child.GetChildren().Any())
                {
                    root.Add(GetConfigAsJObject(child));
                }
                else
                {
                    root.Add(child.Value);
                }
            }

            return root;
        }
        else
        {
            var root = new JsonObject();

            foreach (var child in children)
            {
                //not strictly correct, but we'll go with it.
                root.Add(child.Key, child.GetChildren().Any() ? GetConfigAsJObject(child) : child.Value);
            }

            return root;
        }
    }
}
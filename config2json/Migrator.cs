﻿using McMaster.Extensions.CommandLineUtils;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

namespace Config2Json;

[Command(Name = "config2json",
    Description = "Converts a web.config/app.config file to an appsettings.json file",
    ExtendedHelpText = Constants.ExtendedHelpText)]
[HelpOption]
public partial class Migrator
{
    [Required(ErrorMessage = "You must specify the path to a directory or file to migrate")]
    [Argument(0, Name = "path", Description = "Path to the file or directory to migrate")]
    [FileOrDirectoryExists]
    public string Path { get; }

    [Argument(1, Name = "prefix", Description = "If provided, an additional namespace to prefix on generated keys")]
    public string Prefix { get; }

    [Option("-r|--raw", Description = "Show parsed raw key/value.")]
    public bool Raw { get; }

    public async Task<int> OnExecute(CommandLineApplication app, IConsole console)
    {
        var filesToMigrate = GetFilesToMigrate(console, Path);
        
        var optimiser = new FileMigrator(filesToMigrate, console, Prefix, Raw);

        await optimiser.MigrateFiles();

        console.WriteLine($"Migration complete.");

        return Program.OK;
    }

    static string[] GetFilesToMigrate(IConsole console, string path)
    {
        console.WriteLine($"Checking '{path}'...");
        
        if (File.GetAttributes(path).HasFlag(FileAttributes.Directory))
        {
            console.WriteLine($"Path '{path}' is a directory, migrating all config");
            
            return Directory.GetFiles(path);
        }

        console.WriteLine($"Path '{path}' is a file");
        
        return new[] { path };
    }
}
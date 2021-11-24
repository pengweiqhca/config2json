## Installation

The latest release of config2json requires the [6.0.100](https://dotnet.microsoft.com/download/dotnet/6.0) .NET Core SDK or newer.
Once installed, run this command:

```
dotnet tool install -g config2json
```

## Usage

```
Usage: config2json [arguments] [options]

Arguments:
  path          Path to the file or directory to migrate
  prefix        If provided, an additional namespace to prefix on generated keys

Options:
  -?|-h|--help  Show help information
  -r|--raw  Show parsed raw key/value.

Performs basic migration of an xml .config file to
a JSON file. Uses the 'key' value as the key, and the
'value' as the value. Can optionally replace a given
character with the section marker (':').

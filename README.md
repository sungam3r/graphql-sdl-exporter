# graphql-sdl-exporter

[![NuGet](https://img.shields.io/nuget/v/dotnet-sdlexport)](https://www.nuget.org/packages/dotnet-sdlexport)
[![Nuget](https://img.shields.io/nuget/dt/dotnet-sdlexport)](https://www.nuget.org/packages/dotnet-sdlexport)

![Activity](https://img.shields.io/github/commit-activity/w/sungam3r/graphql-sdl-exporter)
![Activity](https://img.shields.io/github/commit-activity/m/sungam3r/graphql-sdl-exporter)
![Activity](https://img.shields.io/github/commit-activity/y/sungam3r/graphql-sdl-exporter)

![Size](https://img.shields.io/github/repo-size/sungam3r/graphql-sdl-exporter)

.NET Core Global Tool for generating SDL from url or executable file.

## Installation

You can install the latest version via [NuGet](https://www.nuget.org/packages/dotnet-sdlexport).

```
> dotnet tool install -g dotnet-sdlexport
```

## What is it ?

This tool generates a text SDL (Schema Definition Language) file from the given URL or path to the
executable ASP.NET Core assembly. In the case of using URL, the tool immediately sends introspection
request to that URL. If the path to the file is specified as the source, then the tool starts the
temporary process and stops it after receiving the introspection response. The tool will be able
to send requests only when it starts the process on the URL it knows. This parameter is passed
through the `--url` command line switch and has default value `http://localhost:8088`. This
parameter is then passed as the `--server.urls` command line switch to the process being launched.

It should be noted that with this method, the service should not (although it may) perform any side work
at startup, such as contacting external systems, databases, etc. as this may slow down the process of
obtaining the schema. The tool passes `API_ONLY_RESTRICTED_ENVIRONMENT` command line key to the
process being launched. By the presence of this key, it is possible to disable the execution of code
that is not required to obtain a schema:
```C#
public static Task Main(string[] args) => CreateBuilder(args)
    .Build()
    .RunAsync();

private static IWebHostBuilder CreateBuilder(string[] args)
    => args.Contains("API_ONLY_RESTRICTED_ENVIRONMENT")
    ? WebHost.CreateDefaultBuilder<StartupApiOnly>(args)
    : WebHost.CreateDefaultBuilder<Startup>(args);
```

## Why do I need it ?

SDL is easier to work with than with raw JSON introspection response. SDL is easier to read and understand.
SDL is also much more compact in comparison with raw JSON - in this case it's easier to track changes. SDL
file, like any code file, can be stored as an artifact in the version control system and act as a service 
public API. 

## Experimental features

The tool can get information about directives if the server [supports](https://github.com/sungam3r/graphql-introspection-model/blob/master/src/GraphQL.IntrospectionModel/IntrospectionQuery.cs#L102) this feature.
If the server does not support directive return (in this case server returns a request validation error),
then the tool uses the classic introspection request. The [official specification](https://graphql.github.io/graphql-spec/June2018/#)
does not describe such a possibility, although [discussions](https://github.com/graphql/graphql-spec/issues/300) are underway to expand the specification to add this feature.

## Usage

1. [GitHub GraphQL API v4](https://developer.github.com/v4/) [[Generated SDL](samples/github.graphql)]

```sh
sdlexport --source https://api.github.com/graphql --auth bearer|<YOUR_TOKEN> --out samples/github.graphql
```

2. [SWAPI](http://graphql.org/swapi-graphql/) - A GraphQL schema and server wrapping Star Wars API. [[Generated SDL](samples/swapi.graphql)]

```sh
sdlexport --source https://swapi-graphql.netlify.com/.netlify/functions/index --out samples/swapi.graphql
```

3. [HIVDB](https://hivdb.stanford.edu/page/graphiql/) - A curated database to represent, store and analyze HIV drug resistance data. [[Generated SDL](samples/hivdb.graphql)]

```sh
sdlexport --source https://hivdb.stanford.edu/graphql --out samples/hivdb.graphql
```

4. [Countries](https://countries.trevorblades.com/) - Information about countries, continents, and languages, based on [Countries List](https://annexare.github.io/Countries/). [[Generated SDL](samples/countries.graphql)]

```sh
sdlexport --source https://countries.trevorblades.com --out samples/countries.graphql
```

5. You can export the schema directly from your published ASP.NET Core app

```sh
> sdlexport --source C:\MyWebHost.dll --out samples/myschema.graphql
```

To see the full list of available options:
```sh
> sdlexport
``` 

## See also

- [Step-by-step introduction to GraphQL](https://graphql.org/learn/)
- [Type System and SDL specification](http://spec.graphql.org/June2018/#sec-Type-System)
- [Introspection specification](http://spec.graphql.org/June2018/#sec-Introspection)
- [GraphQL for .NET](https://github.com/graphql-dotnet/graphql-dotnet)
- [Types for GraphQL introspection model](https://github.com/sungam3r/graphql-introspection-model)

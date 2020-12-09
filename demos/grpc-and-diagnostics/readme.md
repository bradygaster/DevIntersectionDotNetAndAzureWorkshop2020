In today's example, we're building a blazor application to track the score of a basketball game. We will then expose this information via a gRPC server-streaming endpoint. Finally, we'll generate a gRPC client to call the endpoint exposed by this service.

## Creating a server

Let's start by building the server

- Open Visual Studio 2019 and Create a new Blazor App -> Blazor Server App

> Make sure to chose .NET 5.0 from the .NET version selector drop down

- Create new `protos` directory

- Right-click on the `protos` directory and choose *Add new Item* -> *Protocol Buffer File* -> `basketball.proto`.

We will now define our data model and service contract. 

In our simple example, we will track the Home team's score, Away team's score, and elapsed time on the game clock. While you can expand the data model to track other components of the box score, the skills learned in the building this application will be broadly applicable in building any gRPC-based RPC solution.

> Links: [Protobuf developer's guide](https://developers.google.com/protocol-buffers/docs/proto3)

- Copy the contents of this snippet into your protobuf file

```proto
syntax = "proto3";

import "google/protobuf/duration.proto";

option csharp_namespace = "Basketball";

message Game {
	string game_id = 1;
}

message Score {
	int32 home_score = 1;
	int32 away_score = 2;
	google.protobuf.Duration game_clock = 3;
}
```

Now that we've defined our data model, let's add our service contract.

- Add the following snippet to the same file

```proto
service Scorer {
  rpc GetScore (Game) returns (stream Score);
}
```

Let's use the Service Reference tooling in Visual Studio to generate our message types and and service stubs.

- Right-click on your Project in Solution Explorer. Select *Add* -> *Service Reference*
- Now click the **Green plus icon ➕** to Add a new service reference.
- Select **gRPC** and hit next
- Navigate to the previously created `protos` directory in the **File** radio button. For the *Select the type of class to be generated* dropdown, you should choose **Server**.

Visual Studio will now configure your project by adding the right NuGet packages to your project and adding the proto file you authored to the Protobuf Item Group in MSBuild.

- Modify your `Startup.cs` file

```diff
        public void ConfigureServices(IServiceCollection services)
        {
+            services.AddGrpc();
            services.AddRazorPages();
            services.AddServerSideBlazor();
            services.AddSingleton<WeatherForecastService>();
        }
```

- Create a new `Services` directory
- Create a new `ScorerService.cs` file and make `ScorerService` inherit from `Scorer.ScorerBase` (defined in our proto file)



Now go back to your Startup class and explicity register the use of this service. Unlike MVC, there is discovery of gRPC services. You need to register every service you are using.

- Modify your `Startup.cs` file
```diff
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
           // ...
            app.UseEndpoints(endpoints =>
            {
+                endpoints.MapGrpcService<ScorerService>();
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });
```

- Let's copy over some files from the solution that implement the score keeping before continuing with gRPC

- Create a `ScoreKeeper.cs` file

```csharp
using Basketball;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace BasketballServer
{
    public class ScoreKeeper : IDisposable
    {
        private readonly Stopwatch _stopwatch;
        private int _homeScore = 0;
        private int _awayScore = 0;

        private static IDictionary<string, ScoreKeeper> _gameDictionary
            = new ConcurrentDictionary<string, ScoreKeeper>();

        public string GameId { get; private set; }

        public static ScoreKeeper CreateGame(string gameId)
        {
            var scoreKeeper = new ScoreKeeper(gameId);
            if(!_gameDictionary.TryAdd(gameId, scoreKeeper))
            {
                throw new ArgumentException("Game with specified gameId already exists");
            }
            return scoreKeeper;
        }

        public static ScoreKeeper GetGame(string gameId)
        {
            if(!_gameDictionary.TryGetValue(gameId, out var scoreKeeper))
            {
                throw new ArgumentException("Game with specified gameId doesn't exist");
            }
            return scoreKeeper;
        }

        public Score CurrentScore()
        {
            return new Score()
            {
                HomeScore = _homeScore,
                AwayScore = _awayScore,
                GameClock = Duration.FromTimeSpan(_stopwatch.Elapsed)
            };
        }

        private ScoreKeeper(string gameId)
        {
            GameId = gameId;
            _stopwatch = new Stopwatch();
        }

        public void Start()
        {
            _stopwatch.Start();
        }

        public void ShotMade(Team team, int points)
        {
            if (team == Team.Home)
            {
                _homeScore += points;
            }
            if (team == Team.Away)
            {
                _awayScore += points;
            }
            EventHandler<ScoreUpdateEventArgs> handler = ScoreUpdated;
            if (handler != null)
            {
                ScoreUpdateEventArgs eventArgs = new ScoreUpdateEventArgs()
                {
                    Score = CurrentScore()
                };
                handler(this, eventArgs);
            }
        }

        public void Dispose()
        {
            _gameDictionary.Remove(GameId);
        }

        public event EventHandler<ScoreUpdateEventArgs> ScoreUpdated;

    }

    public class ScoreUpdateEventArgs : EventArgs
    {
        public Score Score { get; set; }
    }

    public enum Team
    {
        Home,
        Away
    }
}
```

- Let's also replace the contents on `Index.razor` with some components that allow us update and view the current score of the basketball game

```razor
@page "/"
@using Basketball

<div class="jumbotron">
  <h1 class="display-4">Game @scoreKeeper.GameId</h1>
  <hr class="my-4">
  <h1>@gameClock.ToString("c")</h1>
  <h1>Home @homeScore : Away @awayScore</h1>
</div>

<div class="container">
  <div class="row">
    <div class="col-sm">
        <div class="row">
            Home team
        </div>
        <div class="row">
            <button @onclick="(e => scoreKeeper.ShotMade(Team.Home, 1))">Free throw</button>
        </div>
        <div class="row">
            <button @onclick="(e => scoreKeeper.ShotMade(Team.Home, 2))">2 pointer</button>
        </div>
        <div class="row">
            <button @onclick="(e => scoreKeeper.ShotMade(Team.Home, 3))">3 pointer</button>
        </div>
    </div>
    <div class="col-sm">
        <div class="row">
            Away team
        </div>
        <div class="row">
            <button @onclick="(e => scoreKeeper.ShotMade(Team.Away, 1))">Free throw</button>
        </div>
        <div class="row">
            <button @onclick="(e => scoreKeeper.ShotMade(Team.Away, 2))">2 pointer</button>
        </div>
        <div class="row">
            <button @onclick="(e => scoreKeeper.ShotMade(Team.Away, 3))">3 pointer</button>
        </div>
    </div>
  </div>
</div>

@code {
    private static int id = 0;
    private ScoreKeeper scoreKeeper;
    private int homeScore = 0;
    private int awayScore = 0;
    private TimeSpan gameClock = TimeSpan.Zero;

    protected override Task OnInitializedAsync()
    {
        scoreKeeper= ScoreKeeper.CreateGame(id.ToString());
        id++;
        scoreKeeper.ScoreUpdated += (_, e) =>
        {
            Update(e.Score);
        };
        scoreKeeper.Start();
        return Task.CompletedTask;
    }

    private void Update(Score score)
    {
        homeScore = score.HomeScore;
        awayScore = score.AwayScore;
        gameClock = score.GameClock.ToTimeSpan();
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        scoreKeeper.Dispose();
    }
}
```

You should now be able to run your Blazor application, start a new basketball game, update the score, and see that reflected in the UI.

Now let's implement the gRPC service that allows us to reply to requests with the score of a current game.

- Update the `ScorerService.cs` file

```csharp
    public class ScorerService : Scorer.ScorerBase
    {
        public override async Task GetScore(Game request, IServerStreamWriter<Score> responseStream, ServerCallContext context)
        {
            var scoreKeeper = ScoreKeeper.GetGame(request.GameId);
            while (!context.CancellationToken.IsCancellationRequested)
            {
                await responseStream.WriteAsync(scoreKeeper.CurrentScore());
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
    }
```

## Create a client

At this point we have a gRPC service, so let's create a client to access this service.

- Right-click on the solution and Add a new Project. Create a new C# Console Application

Let's now the Service Reference tooling in Visual Studio to generate our client.

- Right-click on your Project in Solution Explorer. Select *Add* -> *Service Reference*
- Now click the **Green plus icon ➕** to Add a new service reference.
- Select **gRPC** and hit next
- Navigate to the previously created `protos` directory in the **File** radio button. For the *Select the type of class to be generated* dropdown, you should choose **Client**.

```csharp
        private static readonly string serverAddress = "https://localhost:5001";
        private static readonly string gameId = "1";
        static async Task Main(string[] args)
        {
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            using var channel = GrpcChannel.ForAddress(serverAddress);
            var client = new Scorer.ScorerClient(channel);

            using var call = client.GetScore(new Game() { GameId = gameId });
            try
            {
                await foreach (var message in call.ResponseStream.ReadAllAsync(cts.Token))
                {
                    Console.WriteLine($"Home: {message.HomeScore}");
                    Console.WriteLine($"Away: {message.AwayScore}");
                    Console.WriteLine($"Time: {message.GameClock?.ToTimeSpan().ToString("c")}\n");
                };
            }
            catch (RpcException) { /* Swallow exception */ }
        }
    }
```

- To communicate between the server and client, we'll need both applications running. To do this I recommend running one of the projects from the command line while debugging/running the other from Visual Studio.

## Add support for gRPC-web in the server

A known limitation of Azure App Service is that doesn't support gRPC. So now let's convert this application to use an alternative protocol `gRPC-web` which is supported in App Service. We'll still enjoy the benefits of productivity benefits of gRPC.

- Modify your `Startup.cs`

```csharp
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<ScorerService>()
                         .EnableGrpcWeb();
            });
```

> Note: We will be calling this gRPC-web endpoint from a .NET Console Client. If you want to use a Blazor-based client (or any browser-based client), you will need to configure CORS.

- You can now publish this application to Azure App Service.

## Add support for gRPC-web in the client

Let's modify our client to make use of gRPC-web. I've also modified my server address to point to my deployed App Service instance. You can try running this both locally and on Azure.

```csharp
    class Program
    {
        private static readonly string serverAddress = "https://basketballserver20201209023806.azurewebsites.net";
        private static readonly string gameId = "1";
        static async Task Main(string[] args)
        {
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            using var channel = GrpcChannel.ForAddress(serverAddress, new GrpcChannelOptions
            {
                HttpHandler = new GrpcWebHandler(new HttpClientHandler())
            });
            var client = new Scorer.ScorerClient(channel);

            using var call = client.GetScore(new Game() { GameId = gameId });
            try
            {
                await foreach (var message in call.ResponseStream.ReadAllAsync(cts.Token))
                {
                    Console.WriteLine($"Home: {message.HomeScore}");
                    Console.WriteLine($"Away: {message.AwayScore}");
                    Console.WriteLine($"Time: {message.GameClock?.ToTimeSpan().ToString("c")}\n");
                };
            }
            catch (RpcException) { /* Swallow exception */ }
        }
    }
```




using Basketball;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace BasketballClientConsole
{
    class Program
    {
        private static readonly bool grpcWebEnabled = true;
        //private static readonly string serverAddress = "https://localhost:5001";
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

            HttpMessageHandler handler;
            if (grpcWebEnabled)
            {
                handler = new GrpcWebHandler(new HttpClientHandler());
            }
            else
            {
                handler = new HttpClientHandler();
            }

            using var channel = GrpcChannel.ForAddress(serverAddress, new GrpcChannelOptions
            {
                HttpHandler = handler
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
}

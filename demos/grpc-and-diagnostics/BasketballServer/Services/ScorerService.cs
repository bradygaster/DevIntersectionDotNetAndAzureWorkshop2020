using Basketball;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BasketballServer.Services
{
    public class ScorerService : Scorer.ScorerBase
    {
        private static Stopwatch _stopwatch = new Stopwatch();

        static ScorerService()
        {
            _stopwatch.Start();
        }

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
}

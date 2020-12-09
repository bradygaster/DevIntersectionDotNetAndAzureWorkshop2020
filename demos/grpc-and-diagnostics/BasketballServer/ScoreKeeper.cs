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
using System.Collections.Concurrent;
using Grpc.Core;
using RockPaperScissorsApi.gRPC;

namespace RockPaperScissorsApi.Services
{
    public class MatchState
    {
        public string Player1Id { get; set; }
        public string Player2Id { get; set; }
        public List<IServerStreamWriter<JoinGameResponse>> JoinStreams { get; } = new();
        public string Player1Move { get; set; }
        public string Player2Move { get; set; }
        public List<IServerStreamWriter<MoveResponse>> MoveStreams { get; } = new();
        public bool IsReady => !string.IsNullOrEmpty(Player1Id) && !string.IsNullOrEmpty(Player2Id);
        public bool ResultSent { get; set; }

        public bool BalancesUpdated { get; set; }

        public TaskCompletionSource<MoveResponse> ResultCompletion { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    public class MatchManager
    {
        private readonly ConcurrentDictionary<string, MatchState> _matches = new();

        public MatchState GetOrCreateMatch(string matchId)
        {
            return _matches.GetOrAdd(matchId, _ => new MatchState());
        }

        public void AddJoinStream(string matchId, string userId, IServerStreamWriter<JoinGameResponse> stream)
        {
            var state = GetOrCreateMatch(matchId);
            lock (state)
            {
                if (string.IsNullOrEmpty(state.Player1Id))
                    state.Player1Id = userId;
                else if (string.IsNullOrEmpty(state.Player2Id))
                    state.Player2Id = userId;

                state.JoinStreams.Add(stream);
            }
        }

        public void AddMoveStream(string matchId, string userId, IServerStreamWriter<MoveResponse> stream, string move)
        {
            var state = GetOrCreateMatch(matchId);
            lock (state)
            {
                if (userId == state.Player1Id)
                    state.Player1Move = move;
                else if (userId == state.Player2Id)
                    state.Player2Move = move;

                state.MoveStreams.Add(stream);
            }
        }

       

        public bool IsMatchReady(string matchId)
        {
            var state = GetOrCreateMatch(matchId);
            lock (state)
            {
                return state.Player1Id != null && state.Player2Id != null;
            }
            
        }

        public async Task NotifyPlayersAsync(string matchId, JoinGameResponse response)
        {
            var state = GetOrCreateMatch(matchId);
            
                foreach (var stream in state.JoinStreams)
                {
                    await stream.WriteAsync(response);
                }
                //_matches.TryRemove(matchId, out _);
            
        }

        public bool AddMove(string matchId, string userId, string move, IServerStreamWriter<MoveResponse> stream)
    {
        var state = GetOrCreateMatch(matchId);
        lock (state)
        {
            
            if (userId == state.Player1Id)
                state.Player1Move = move;
            else if (userId == state.Player2Id)
                state.Player2Move = move;
            else
                return false;

            state.MoveStreams.Add(stream);
            return state.Player1Move != null && state.Player2Move != null;
        }
    }

    public MatchState GetMatchState(string matchId)
    {
        if (_matches.TryGetValue(matchId, out var state))
        {
            
            lock (state)
            {
                return new MatchState
                {
                    Player1Id = state.Player1Id,
                    Player2Id = state.Player2Id,
                    Player1Move = state.Player1Move,
                    Player2Move = state.Player2Move
                };
            }
        }
        return null;
    }

    public bool AreMovesReady(string matchId)
    {
        if (_matches.TryGetValue(matchId, out var state))
        {
            lock (state)
            {
                return state.Player1Move != null && state.Player2Move != null;
            }
        }
        return false;
    }

    public async Task BroadcastResultAsync(string matchId, MoveResponse response)
    {
        if (_matches.TryGetValue(matchId, out var state))
        {
            lock (state)
            {
                if (state.ResultSent) return;
                state.ResultSent = true;
            }

            foreach (var stream in state.MoveStreams)
            {
                try
                {
                    await stream.WriteAsync(response);
                }
                catch (Exception ex)
                {
                    
                }
            }
            _matches.TryRemove(matchId, out _);
        }
    }

        
    }
}
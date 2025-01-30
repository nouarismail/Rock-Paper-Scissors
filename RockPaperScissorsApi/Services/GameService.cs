using Grpc.Core;
using RockPaperScissorsApi.Data;
using RockPaperScissorsApi.Models;
using Microsoft.AspNetCore.Identity;
using RockPaperScissorsApi.gRPC;
using static RockPaperScissorsApi.Models.MatchHistory;
using RockPaperScissorsApi.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace RockPaperScissorsApi
{
    public class GameService : RockPaperScissorsApi.gRPC.GameService.GameServiceBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly MatchManager _matchManager;

        public GameService(AppDbContext context, UserManager<ApplicationUser> userManager, MatchManager matchManager)
        {
            _context = context;
            _userManager = userManager;
            _matchManager = matchManager;
        }



        
        public override async Task<GameListResponse> ListGames(Empty request, ServerCallContext context)
        {
            var games = _context.MatchHistories
                .Where(m => m.Result == GameResult.Pending)
                .Select(m => new GameInfo
                {
                    MatchId = m.Id.ToString(),
                    BetAmount = (double)m.BetAmount,
                    HasWaitingPlayer = string.IsNullOrEmpty(m.Player2Id)
                }).ToList();

            var response = new GameListResponse();
            response.Games.AddRange(games);
            return response;
        }



        public override async Task JoinGame(
        JoinGameRequest request,
        IServerStreamWriter<JoinGameResponse> responseStream,
        ServerCallContext context)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var match = await _context.MatchHistories
                    .FirstOrDefaultAsync(m => m.Id == Guid.Parse(request.MatchId));

                var user = await _userManager.FindByIdAsync(request.UserId);

                if (match == null || user == null)
                {
                    await responseStream.WriteAsync(new JoinGameResponse
                    {
                        Status = JoinGameResponse.Types.Status.Error,
                        Message = "Invalid match or user."
                    });
                    return;
                }

                if (match.Result == GameResult.Created)
                {
                    match.Player1Id = user.Id;
                    match.Result = GameResult.Pending;

                }
                else if (match.Result == GameResult.Pending)
                {
                    match.Player2Id = user.Id;
                }
                else
                {
                    await responseStream.WriteAsync(new JoinGameResponse
                    {
                        Status = JoinGameResponse.Types.Status.Error,
                        Message = "Match is either full or over."
                    });
                    return;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();


                _matchManager.AddJoinStream(request.MatchId, request.UserId, responseStream);


                if (_matchManager.IsMatchReady(request.MatchId))
                {
                    await _matchManager.NotifyPlayersAsync(request.MatchId, new JoinGameResponse
                    {
                        Status = JoinGameResponse.Types.Status.Ready,
                        Message = "Game is ready. Submit your move!"
                    });
                }
                else
                {
                    await responseStream.WriteAsync(new JoinGameResponse
                    {
                        Status = JoinGameResponse.Types.Status.Waiting,
                        Message = "Waiting for another player..."
                    });
                }

                
                while (!context.CancellationToken.IsCancellationRequested &&
                      !_matchManager.IsMatchReady(request.MatchId))
                {
                    await Task.Delay(1000);
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                await responseStream.WriteAsync(new JoinGameResponse
                {
                    Status = JoinGameResponse.Types.Status.Error,
                    Message = "Conflict: Match was modified by another player."
                });
            }
        }



        public override async Task SubmitMove(
    MoveRequest request,
    IServerStreamWriter<MoveResponse> responseStream,
    ServerCallContext context)
        {
            try
            {
                
                var match = await _context.MatchHistories.FindAsync(Guid.Parse(request.MatchId));
                var user = await _userManager.FindByIdAsync(request.UserId);
                if (match == null || user == null)
                {
                    await responseStream.WriteAsync(new MoveResponse
                    {
                        Status = MoveResponse.Types.Status.Error,
                        Details = "Invalid match or user."
                    });
                    return;
                }

                
                bool movesReady = _matchManager.AddMove(request.MatchId, request.UserId, request.Move, responseStream);

                if (movesReady)
                {
                    
                    var (result, details) = ComputeResult(request.MatchId);
                    match.Result = result;
                    await _matchManager.BroadcastResultAsync(request.MatchId, new MoveResponse
                    {
                        Status = MoveResponse.Types.Status.Result,
                        Details = details
                    });

                    
                    match.Result = result;



                    await _context.SaveChangesAsync();
                }
                else
                {
                    
                    while (!context.CancellationToken.IsCancellationRequested &&
                           !_matchManager.AreMovesReady(request.MatchId))
                    {
                        await responseStream.WriteAsync(new MoveResponse
                        {
                            Status = MoveResponse.Types.Status.Pending,
                            Details = "Waiting for the other player..."
                        });
                        await Task.Delay(2000);
                    }
                    var (result, details) = ComputeResult(request.MatchId);
                    await _matchManager.BroadcastResultAsync(request.MatchId, new MoveResponse
                    {
                        Status = MoveResponse.Types.Status.Result,
                        Details = details
                    });

                    await UpdateBalances(match, result);

                }
            }
            catch (Exception ex)
            {
                await responseStream.WriteAsync(new MoveResponse
                {
                    Status = MoveResponse.Types.Status.Error,
                    Details = ex.Message
                });
            }
        }

        private (GameResult Result, string Details) ComputeResult(string matchId)
        {


            var state = _matchManager.GetMatchState(matchId);
            var match = _context.MatchHistories
                    .FirstOrDefaultAsync(m => m.Id == Guid.Parse(matchId));

            if (state.Player1Move == state.Player2Move)
            {
                return (GameResult.Draw, "It's a draw!");


            }

            if ((state.Player1Move == "Rock" && state.Player2Move == "Scissors") ||
                (state.Player1Move == "Paper" && state.Player2Move == "Rock") ||
                (state.Player1Move == "Scissors" && state.Player2Move == "Paper"))

                return (GameResult.Win, "Player 1 wins!");
            return (GameResult.Loss, "Player 2 wins!");
        }



        private async Task UpdateBalances(MatchHistory match, MatchHistory.GameResult result)
        {
            
            var player1 = await _userManager.FindByIdAsync(match.Player1Id);
            var player2 = await _userManager.FindByIdAsync(match.Player2Id);

            if (player1 == null || player2 == null) return;

            decimal betAmount = match.BetAmount;

            if (result == MatchHistory.GameResult.Win)
            {
                
                player1.Balance += betAmount;
                player2.Balance -= betAmount;

                
                var transaction = new GameTransactions
                {
                    Id = Guid.NewGuid(),
                    SenderId = player2.Id,
                    ReceiverId = player1.Id,
                    Amount = betAmount,
                    TransactionDate = DateTime.UtcNow
                };
                _context.GameTransactions.Add(transaction);
            }
            else if (result == MatchHistory.GameResult.Loss)
            {
                
                player2.Balance += betAmount;
                player1.Balance -= betAmount;

                
                var transaction = new GameTransactions
                {
                    Id = Guid.NewGuid(),
                    SenderId = player1.Id,
                    ReceiverId = player2.Id,
                    Amount = betAmount,
                    TransactionDate = DateTime.UtcNow
                };
                _context.GameTransactions.Add(transaction);
            }
            else if (result == MatchHistory.GameResult.Draw)
            {
                
            }

            
            await _userManager.UpdateAsync(player1);
            await _userManager.UpdateAsync(player2);
            await _context.SaveChangesAsync();
        }



    }

    internal class PlayerConnection
    {
        public string UserId { get; set; }
        public IServerStreamWriter<JoinGameResponse> Stream { get; set; }
    }
}
using System;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using RockPaperScissorsApi.gRPC;

namespace RockPaperScissorsClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using var channel = GrpcChannel.ForAddress("http://localhost:5001");
            var grpcClient = new GameService.GameServiceClient(channel);
            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri("http://localhost:5000/");

            Console.WriteLine("Enter your User ID:");
            var userId = Console.ReadLine();

            while (true)
            {
                Console.WriteLine("\nCommands:");
                Console.WriteLine("1. Create Game");
                Console.WriteLine("2. View Balance");
                Console.WriteLine("3. List Games");
                Console.WriteLine("4. Join Game");
                Console.WriteLine("5. Transfer credit to another player");
                Console.WriteLine("5. Exit");
                var choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        await CreateAndJoinMatch(userId, grpcClient, httpClient);
                        break;
                    case "2":
                        var balance = await grpcClient.GetBalanceAsync(new UserRequest { UserId = userId });
                        Console.WriteLine($"Your balance: {balance.Balance}");
                        break;

                    case "3":
                        var games = await grpcClient.ListGamesAsync(new Empty());
                        Console.WriteLine("Available Games:");
                        foreach (var game in games.Games)
                        {
                            string waiting = game.HasWaitingPlayer? "Waiting a player to join" : "Finished";
                            Console.WriteLine($"Match ID: {game.MatchId}, Bet: {game.BetAmount}, " + waiting);
                        }
                        break;

                    case "4":
                        Console.WriteLine("Enter Match ID:");
                        var matchId = Console.ReadLine();
                        await HandleJoinGame(grpcClient, userId, matchId);
                        break;

                    case "5":
                        await TransferBalance(userId, httpClient);
                        break;

                    case "6":
                        return;

                    default:
                        Console.WriteLine("Invalid command.");
                        break;

                }
            }
        }

        private static async Task TransferBalance(string userId, HttpClient httpClient)
        {
            Console.WriteLine("Enter recipient's User ID:");
            var recipientId = Console.ReadLine();

            Console.WriteLine("Enter amount to transfer:");
            if (!decimal.TryParse(Console.ReadLine(), out var amount) || amount <= 0)
            {
                Console.WriteLine("Invalid amount. Please enter a positive number.");
                return;
            }

            try
            {
                var response = await httpClient.PostAsJsonAsync("api/transactions/create", new
                {
                    SenderId = userId,
                    ReceiverId = recipientId,
                    Amount = amount
                });

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<TransactionResult>();
                    Console.WriteLine($"Transfer successful! New balance: {result.NewBalance}");
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Transfer failed: {error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error transferring balance: {ex.Message}");
            }
        }

        private static async Task CreateAndJoinMatch(string userId, GameService.GameServiceClient gameClient, HttpClient httpClient)
        {
            Console.WriteLine("Enter bet amount:");
            if (!decimal.TryParse(Console.ReadLine(), out var betAmount))
            {
                Console.WriteLine("Invalid amount.");
                return;
            }

            try
            {
                
                var createResponse = await httpClient.PostAsJsonAsync("api/matches/create", new
                {
                    PlayerId = userId,
                    BetAmount = betAmount
                });

                if (!createResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to create match: {createResponse.ReasonPhrase}");
                    return;
                }

                var createdMatch = await createResponse.Content.ReadFromJsonAsync<CreateMatchResponse>();
                Console.WriteLine($"Match created! ID: {createdMatch.MatchId}");

                
                await HandleJoinGame(gameClient, userId, createdMatch.MatchId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private static async Task HandleJoinGame(
        GameService.GameServiceClient grpcClient,
        string userId,
        string matchId)
        {
            var joinRequest = new JoinGameRequest { UserId = userId, MatchId = matchId };
            using var call = grpcClient.JoinGame(joinRequest);

            try
            {
                await foreach (var response in call.ResponseStream.ReadAllAsync())
                {
                    switch (response.Status)
                    {
                        case JoinGameResponse.Types.Status.Waiting:
                            Console.WriteLine($"[Server] {response.Message}");
                            break;

                        case JoinGameResponse.Types.Status.Ready:
                            Console.WriteLine($"[Server] {response.Message}");
                            await SubmitMove(grpcClient, userId, matchId);
                            return;

                        case JoinGameResponse.Types.Status.Error:
                            Console.WriteLine($"[Error] {response.Message}");
                            return;
                    }
                }
            }
            catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Cancelled)
            {
                Console.WriteLine("Connection closed by the server.");
            }
        }

        private static async Task SubmitMove(
            GameService.GameServiceClient grpcClient,
            string userId,
            string matchId)
        {
            Console.WriteLine("Enter your move (Rock/Paper/Scissors):");
            var move = Console.ReadLine();

            var moveRequest = new MoveRequest
            {
                UserId = userId,
                MatchId = matchId,
                Move = move
            };

            using var moveCall = grpcClient.SubmitMove(moveRequest);
            await foreach (var response in moveCall.ResponseStream.ReadAllAsync())
            {
                Console.WriteLine($"[Status: {response.Status}] {response.Details}");
                if (response.Status == MoveResponse.Types.Status.Result)
                    break;
            }
        }
    }

    internal class CreateMatchResponse
    {
        public string MatchId { get; set; }
    }

    internal class TransactionResult
    {
        public decimal NewBalance { get; set; }
    }
}
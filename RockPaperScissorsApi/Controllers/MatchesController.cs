using DTOs.Requests;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RockPaperScissorsApi.Data;
using RockPaperScissorsApi.Models;
using static RockPaperScissorsApi.Models.MatchHistory;

[ApiController]
[Route("api/[controller]")]
public class MatchesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public MatchesController(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateMatch([FromBody] CreateMatchRequest request)
    {
        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            try
            {
                
                var player = await _userManager.FindByIdAsync(request.PlayerId);
                if (player == null) return NotFound("Player not found.");

                
                if (player.Balance < request.BetAmount)
                    return BadRequest("Insufficient balance for the bet.");

                
                player.Balance -= request.BetAmount;
                await _userManager.UpdateAsync(player);

                
                var match = new MatchHistory
                {
                    Player1Id = player.Id,
                    BetAmount = request.BetAmount,
                    MatchDate = DateTime.UtcNow,
                    Result = GameResult.Created,
                    Player1Move = GameMove.None,
                    Player2Move = GameMove.None
                };
                _context.MatchHistories.Add(match);

                
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { MatchId = match.Id });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, "Failed to create match.");
            }
        }
    }
}
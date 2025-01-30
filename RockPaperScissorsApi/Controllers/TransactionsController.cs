
using DTOs.Requests;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RockPaperScissorsApi.Data;
using RockPaperScissorsApi.Models;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public TransactionsController(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateTransaction([FromBody] TransactionRequest request)
    {
        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            try
            {
                
                var sender = await _userManager.FindByIdAsync(request.SenderId);
                var receiver = await _userManager.FindByIdAsync(request.ReceiverId);
                if (sender == null || receiver == null) return NotFound("User not found.");

                
                if (sender.Balance < request.Amount)
                    return BadRequest("Insufficient balance.");

                
                sender.Balance -= request.Amount;
                receiver.Balance += request.Amount;

                
                var gameTransaction = new GameTransactions
                {
                    SenderId = sender.Id,
                    ReceiverId = receiver.Id,
                    Amount = request.Amount,
                    TransactionDate = DateTime.UtcNow
                };
                _context.GameTransactions.Add(gameTransaction);

                
                await _userManager.UpdateAsync(sender);
                await _userManager.UpdateAsync(receiver);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new {NewBalance=sender.Balance});
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, "Transaction failed.");
            }
        }
    }
}
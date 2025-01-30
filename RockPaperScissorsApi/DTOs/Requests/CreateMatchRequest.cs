using System.ComponentModel.DataAnnotations;

namespace DTOs.Requests
{
    public class CreateMatchRequest
    {
        public CreateMatchRequest()
        {
            
        }

        [Required]
        public string PlayerId { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Bet amount must be positive.")]
        public decimal BetAmount { get; set; }
    }
}
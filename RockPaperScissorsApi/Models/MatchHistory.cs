using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RockPaperScissorsApi.Models
{
    public class MatchHistory
    {
        public MatchHistory()
        {
            
        }

        [Key]
        public Guid Id { get; set; }

        [Required]
        public string Player1Id { get; set; } 


        public string? Player2Id { get; set; } 

        [ConcurrencyCheck] 
        public byte[]? RowVersion { get; set; }

        [Required]
        public decimal BetAmount { get; set; }

        [Required]
        public DateTime MatchDate { get; set; }

        [Required]
        public GameResult Result { get; set; } 

        [Required]
        public GameMove Player1Move { get; set; } 

        [Required]
        public GameMove Player2Move { get; set; }

        
        [ForeignKey(nameof(Player1Id))]
        public virtual ApplicationUser Player1 { get; set; }

        [ForeignKey(nameof(Player2Id))]
        public virtual ApplicationUser Player2 { get; set; }


        public enum GameResult { Created, Pending, Win, Loss, Draw }
        public enum GameMove { Rock, Paper, Scissors, None }

        
    }
}
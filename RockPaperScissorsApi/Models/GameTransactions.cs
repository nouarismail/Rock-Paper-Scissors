using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RockPaperScissorsApi.Models
{
    public class GameTransactions
    {
        public GameTransactions()
        {
            
        }

        [Key]
        public Guid Id { get; set; }

        [Required]
        public string SenderId { get; set; } 

        [Required]
        public string ReceiverId { get; set; } 

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        public DateTime TransactionDate { get; set; }

        
        [ForeignKey(nameof(SenderId))]
        public virtual ApplicationUser Sender { get; set; }

        [ForeignKey(nameof(ReceiverId))]
        public virtual ApplicationUser Receiver { get; set; }
    }
}
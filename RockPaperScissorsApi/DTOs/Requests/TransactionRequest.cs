using System.ComponentModel.DataAnnotations;

namespace DTOs.Requests
{
    public class TransactionRequest
    {
        public TransactionRequest()
        {
           
        }

        [Required]
        public string SenderId { get; set; }

        [Required]
        public string ReceiverId { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be positive.")]
        public decimal Amount { get; set; }
    }
}
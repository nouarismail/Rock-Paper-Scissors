using Microsoft.AspNetCore.Identity;

namespace RockPaperScissorsApi.Models
{
    public class ApplicationUser :IdentityUser
    {
        public decimal Balance { get; set; } = 1000;
        public string DisplayName { get; set; }
        public ApplicationUser()
        {
            
        }

        
    }
}
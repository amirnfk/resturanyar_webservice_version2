using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace resturanyar.Models.AuthorizationModels
{
  
 
        public class RefreshToken
        {
            [Key]
            public int Id { get; set; }
            public string Token { get; set; }
            public DateTime ExpiryTime { get; set; }

            public int OwnerId { get; set; }

            [ForeignKey("OwnerId")]
            public Owner Owner { get; set; }
        }
    }
 
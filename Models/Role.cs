using Resturanyar.Data;
using resturanyar.Models;
using System.ComponentModel.DataAnnotations;

public class Role
{
    [Key]
    public int role_id { get; set; }

    [Required]
    [MaxLength(50)]
    public string role_name { get; set; }

    // Navigation Properties
    public ICollection<User> Users { get; set; }
    public ICollection<Owner> Owners { get; set; }
}

using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace resturanyar.Models
{
    internal class CreateOrderApiResponse  
    {
        
            public bool Success { get; set; }
            public string? Message { get; set; }
            public object? Data { get; set; }
        }

     
}
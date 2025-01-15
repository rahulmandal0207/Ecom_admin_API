using System.Dynamic;

namespace Ecommerce_API_2.Models
{
    public class ApiResponse<T> 
    {
        public string? Message { get; set; }
        public T? Data { get; set; }
    }
}

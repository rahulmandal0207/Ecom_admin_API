using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce_API_2.Models;

[Table("Product")]
public partial class Product
{
    [Key]
    public int ProductId { get; set; }

    [StringLength(255)]
    public string ProductName { get; set; } = null!;

    [Column(TypeName = "decimal(18, 0)")]
    public decimal CurrentPrice { get; set; }

    public int StockQuantity { get; set; }

    public string? ProductImageUrl { get; set; }

    [InverseProperty("Product")]
    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}

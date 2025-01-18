using Ecommerce_API_2.Data;
using Ecommerce_API_2.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Dynamic;

namespace Ecommerce_API_2.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly EcommerceContext _context;

        public OrderController(EcommerceContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult GetOrders()
        {
            dynamic response = new ExpandoObject();
            try
            {
                var data = (from o in _context.Orders
                            select new
                            {
                                o.OrderId,
                                o.OrderDate,
                                o.TotalAmount,
                                o.OrderStatus,
                                OrderDetails = (from od in _context.OrderDetails
                                                where od.OrderId == o.OrderId
                                                select new
                                                {
                                                    od.OrderDetailId,
                                                    od.ProductId,
                                                    od.Quantity,
                                                    od.Price,
                                                    Products = (from p in _context.Products
                                                                where p.ProductId == od.ProductId
                                                                select new
                                                                {
                                                                    p.ProductName,
                                                                    p.CurrentPrice,
                                                                    p.StockQuantity,
                                                                }).ToList()
                                                }).ToList()
                            }).ToList();


                response.Message = "Success";
                response.Data = data;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return Ok(response);
        }

        [HttpGet("{id}")]
        public IActionResult GetOrder(int id)
        {
            dynamic response = new ExpandoObject();
            try
            {
                var data = (from o in _context.Orders
                            where o.OrderId == id
                            select new
                            {
                                o.OrderId,
                                o.OrderDate,
                                o.TotalAmount,
                                o.OrderStatus,
                                OrderDetails = (from od in _context.OrderDetails
                                                where od.OrderId == o.OrderId
                                                select new
                                                {
                                                    od.OrderDetailId,
                                                    od.ProductId,
                                                    od.Quantity,
                                                    od.Price,
                                                    Products = (from p in _context.Products
                                                                where p.ProductId == od.ProductId
                                                                select new
                                                                {
                                                                    p.ProductName,
                                                                    p.CurrentPrice,
                                                                    p.StockQuantity,
                                                                }).ToList()
                                                }).ToList()
                            }).FirstOrDefault();




                response.Message = "Success";
                response.Data = data;
            }
            catch (Exception ex)
            {
                response.Message = ex.Message;
            }
            return Ok(response);
        }

        [HttpPost]
        public IActionResult PostOrder(OrderRequest dto)
        {
            dynamic response = new ExpandoObject();

            using var transaction = _context.Database.BeginTransaction();
            try
            {
                // Validate user
                if (!_context.Users.Any(u => u.UserId == dto.UserId))
                {
                    response.Message = "User not found";
                    return BadRequest(response);
                }

                // Fetch products in a single query
                var productIds = dto.OrderDetails.Select(od => od.ProductId).ToList();
                var products = _context.Products.Where(p => productIds.Contains(p.ProductId)).ToList();

                decimal totalAmount = 0;

                foreach (var od in dto.OrderDetails)
                {
                    var product = products.FirstOrDefault(p => p.ProductId == od.ProductId);

                    if (product == null)
                    {
                        response.Message = $"Product with ID {od.ProductId} not found";
                        return BadRequest(response);
                    }

                    if (product.StockQuantity < od.Quantity)
                    {
                        response.Message = $"Insufficient stock for product ID {od.ProductId}";
                        return BadRequest(response);
                    }

                    product.StockQuantity -= od.Quantity;
                    od.Price = product.CurrentPrice;

                    totalAmount += od.Quantity * od.Price;
                }

                _context.Products.UpdateRange(products);

                // Create and save order
                Order order = new Order
                {
                    UserId = dto.UserId,
                    TotalAmount = totalAmount,
                    OrderDate = DateTime.Now,
                    OrderStatus = 1,
                    OrderDetails = dto.OrderDetails.Select(od => new OrderDetail
                    {
                        ProductId = od.ProductId,
                        Quantity = od.Quantity,
                        Price = od.Price
                    }).ToList()
                };

                _context.Orders.Add(order);
                _context.SaveChanges();

                transaction.Commit();

                response.Message = "Success";
                response.Data = order;

                return Ok(response);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                response.Message = ex.Message;
                return StatusCode(500, response);
            }
        }


    }
    
    public class OrderRequest
    {
        public int UserId { get; set; }
        public List<OrderDetailRequest> OrderDetails  { get; set; }
    }

    public class OrderDetailRequest
    {
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

}

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
        public async Task<IActionResult> GetOrders()
        {
            dynamic response = new ExpandoObject();
            try
            {
                var data = await (from o in _context.Orders
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
                                  }).ToListAsync();


                response.Success = true;
                response.Data = data;
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.Success = false; 
                response.Message = ex.Message;
                return StatusCode(500, response);
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetOrder(int id)
        {
            dynamic response = new ExpandoObject();
            try
            {
                var data = await (from o in _context.Orders
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
                                  }).FirstOrDefaultAsync();

                response.Success = true;
                response.Data = data;
            }
            catch (Exception ex)
            {
                response.Success = false; response.Message = ex.Message;
            }
            return Ok(response);
        }

        [HttpPost]
        public async Task<IActionResult> PlaceOrder(OrderRequest dto)
        {
            dynamic response = new ExpandoObject();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validate user
                if (!await _context.Users.AnyAsync(u => u.UserId == dto.UserId))
                {
                    response.Message = "User not found";
                    return BadRequest(response);
                }

                // Fetch products in a single query
                var productIds = dto.OrderDetails.Select(od => od.ProductId).ToList();
                var products = await _context.Products.Where(p => productIds.Contains(p.ProductId)).ToListAsync();

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
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                response.Success = true;
                response.Data = order;

                return Ok(response);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                response.Success = false; response.Message = ex.Message;
                return StatusCode(500, response);
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateOrder(int id, OrderRequest request)
        {
            dynamic response = new ExpandoObject();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (id != request.OrderId)
                {
                    response.Message = "Invalid Order ID";
                    return BadRequest(response);
                }


                var existingOrder = await _context.Orders.Include(o => o.OrderDetails).FirstOrDefaultAsync(o => o.OrderId == id);

                if (existingOrder == null)
                {
                    response.Message = "Order not found";
                    return NotFound(response);
                }

                existingOrder.UserId = request.UserId;
                existingOrder.OrderDate = DateTime.Now;

                decimal totalAmount = 0;

                foreach (var detail in existingOrder.OrderDetails)
                {
                    Product? product = await (from p in _context.Products
                                              where p.ProductId == detail.ProductId
                                              select p).FirstOrDefaultAsync();

                    if (product != null)
                    {
                        // Restoring the stock
                        product.StockQuantity += detail.Quantity;
                        _context.Products.Update(product);
                    }
                }

                _context.OrderDetails.RemoveRange(existingOrder.OrderDetails);

                foreach (var detail in request.OrderDetails)
                {
                    Product? product = await (from p in _context.Products
                                              where p.ProductId == detail.ProductId
                                              select p).FirstOrDefaultAsync();

                    if (product == null)
                    {
                        response.Message = $"Product with ID {detail.ProductId} not found";
                        return BadRequest(response);
                    }

                    if (product.StockQuantity < detail.Quantity)
                    {
                        response.Message = $"Insufficient stock for product ID {detail.ProductId}";
                        return BadRequest(response);
                    }

                    product.StockQuantity -= detail.Quantity;
                    _context.Products.Update(product);

                    var newDetail = new OrderDetail
                    {
                        OrderId = id,
                        ProductId = detail.ProductId,
                        Quantity = detail.Quantity,
                        Price = product.CurrentPrice
                    };

                    totalAmount += newDetail.Quantity * newDetail.Price;
                    existingOrder.OrderDetails.Add(newDetail);
                }

                existingOrder.TotalAmount = totalAmount;
                _context.Orders.Update(existingOrder);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                response.Success = true;
                return Ok(response);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                response.Success = false; response.Message = ex.Message;
                return StatusCode(500, response);
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            dynamic response = new ExpandoObject();

            try
            {
                Order? order = await (from o in _context.Orders
                                      where o.OrderId == id
                                      select o).FirstOrDefaultAsync();

                if (order == null)
                {
                    response.Message = "Order not found";
                    return BadRequest(response);
                }

                _context.Orders.Remove(order);
                await _context.SaveChangesAsync();

                response.Success = true;
                return Ok(response);
            }
            catch (Exception e)
            {
                response.Success = false;
                response.Message = e.Message;
                return StatusCode(500, response);
            }
        }
    }

    public class OrderRequest
    {
        public int OrderId { get; set; }
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

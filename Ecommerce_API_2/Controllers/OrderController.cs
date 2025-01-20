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
        public IActionResult PlaceOrder(OrderRequest dto)
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



        // update
        [HttpPut("{id}")]
        public IActionResult UpdateOrder(int id, OrderRequest request)
        {
            dynamic response = new ExpandoObject();

            using var transaction = _context.Database.BeginTransaction();
            try
            {
                if (id != request.OrderId)
                {
                    response.Message = "Invalid Order ID";
                    return BadRequest(response);
                }

                
                var existingOrder = _context.Orders.Include(o => o.OrderDetails).FirstOrDefault(o => o.OrderId == id);

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
                    Product? product = (from p in _context.Products
                                        where p.ProductId == detail.ProductId
                                        select p).FirstOrDefault();

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
                    Product? product = (from p in _context.Products
                                        where p.ProductId == detail.ProductId
                                        select p).FirstOrDefault();

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
                _context.SaveChanges();
                transaction.Commit();

                response.Message = "Success";
                return Ok(response);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                response.Message = ex.Message;
                return StatusCode(500, response);
            }
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteOrder(int id)
        {
            dynamic response = new ExpandoObject();

            try
            {
                Order? order = (from o in _context.Orders
                                where o.OrderId == id
                                select o).FirstOrDefault();

                if (order == null)
                {
                    response.Message = "Order not found";
                    return BadRequest(response);
                }

                _context.Orders.Remove(order);
                _context.SaveChanges();

                return Ok(response);
            }
            catch(Exception e)
            {
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

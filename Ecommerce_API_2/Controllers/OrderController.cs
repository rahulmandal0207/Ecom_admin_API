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

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateOrder(int id, OrderRequest request)
        {
            dynamic response = new ExpandoObject();

            // Start a database transaction
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Check if the order exists
                var existingOrder = await _context.Orders
                    .Include(o => o.OrderDetails)
                    .FirstOrDefaultAsync(o => o.OrderId == id);

                if (existingOrder == null)
                {
                    response.Message = "Order not found";
                    return NotFound(response);
                }

                // Update the order's basic properties
                existingOrder.UserId = request.UserId;
                existingOrder.OrderDate = DateTime.Now;

                // Validate and update order details
                decimal totalAmount = 0;

                // Remove existing order details from the database
                foreach (var detail in existingOrder.OrderDetails)
                {
                    var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == detail.ProductId);
                    if (product != null)
                    {
                        product.StockQuantity += detail.Quantity; // Restore stock
                        _context.Products.Update(product);
                    }
                }
                _context.OrderDetails.RemoveRange(existingOrder.OrderDetails);

                // Add new order details from the request
                foreach (var detailRequest in request.OrderDetails)
                {
                    var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == detailRequest.ProductId);
                    if (product == null)
                    {
                        response.Message = $"Product with ID {detailRequest.ProductId} not found";
                        return BadRequest(response);
                    }

                    if (product.StockQuantity < detailRequest.Quantity)
                    {
                        response.Message = $"Insufficient stock for product ID {detailRequest.ProductId}";
                        return BadRequest(response);
                    }

                    product.StockQuantity -= detailRequest.Quantity;
                    _context.Products.Update(product);

                    var newDetail = new OrderDetail
                    {
                        OrderId = id,
                        ProductId = detailRequest.ProductId,
                        Quantity = detailRequest.Quantity,
                        Price = product.CurrentPrice
                    };

                    totalAmount += detailRequest.Quantity * product.CurrentPrice;
                    existingOrder.OrderDetails.Add(newDetail);
                }

                // Update the total amount
                existingOrder.TotalAmount = totalAmount;

                // Save changes to the database
                _context.Orders.Update(existingOrder);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                response.Message = "Order updated successfully";
                response.Data = new
                {
                    existingOrder.OrderId,
                    existingOrder.UserId,
                    existingOrder.TotalAmount,
                    existingOrder.OrderDate,
                    existingOrder.OrderStatus,
                    OrderDetails = existingOrder.OrderDetails.Select(d => new
                    {
                        d.ProductId,
                        d.Quantity,
                        d.Price
                    }).ToList()
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                response.Message = ex.Message;
                return StatusCode(500, response);
            }
        }


        //// update
        //[HttpPut("{id}")]
        //public IActionResult UpdateOrder(int id, OrderRequest request)
        //{
        //    dynamic response = new ExpandoObject();

        //    using var transaction = _context.Database.BeginTransaction();
        //    try
        //    {
        //        if (id != request.OrderId)
        //        {
        //            response.Message = "Invalid Order ID";
        //            return BadRequest(response);
        //        }

        //        //var existingOrder = (from o in _context.Orders
        //        //                     where o.OrderId == id
        //        //                     select new
        //        //                     {
        //        //                         o.OrderId,
        //        //                         o.UserId,
        //        //                         o.OrderDate,
        //        //                         o.TotalAmount,
        //        //                         o.OrderStatus,
        //        //                         OrderDetails = (from od in _context.OrderDetails
        //        //                                         where o.OrderId == od.OrderId
        //        //                                         select new
        //        //                                         {
        //        //                                             od.OrderDetailId,
        //        //                                             od.ProductId,
        //        //                                             od.Quantity,
        //        //                                             od.Price,
        //        //                                             Products = (from p in _context.Products
        //        //                                                         where od.ProductId == p.ProductId
        //        //                                                         select new
        //        //                                                         {
        //        //                                                             p.ProductName,
        //        //                                                             p.CurrentPrice,
        //        //                                                             p.StockQuantity,
        //        //                                                         }).ToList()
        //        //                                         }).ToList()
        //        //                     }).FirstOrDefault();

        //        var existingOrder = _context.Orders.Include(o => o.OrderDetails).FirstOrDefault(o => o.OrderId == id);

        //        if (existingOrder == null)
        //        {
        //            response.Message = "Order not found";
        //            return NotFound(response);
        //        }

        //        existingOrder.UserId = request.UserId;
        //        existingOrder.OrderDate = DateTime.Now;

        //        decimal totalAmount = 0;

        //        foreach (var detail in existingOrder.OrderDetails)
        //        {
        //            Product? product = (from p in _context.Products
        //                                where p.ProductId == detail.ProductId
        //                                select p).FirstOrDefault();

        //            if (product != null)
        //            {
        //                // Restoring the stock
        //                product.StockQuantity += detail.Quantity;
        //                _context.Products.Update(product);
        //            }
        //        }

        //        _context.OrderDetails.RemoveRange(existingOrder.OrderDetails);

        //        foreach (var detail in existingOrder.OrderDetails)
        //        {
        //            Product? product = (from p in _context.Products
        //                                where p.ProductId == detail.ProductId
        //                                select p).FirstOrDefault();

        //            if (product == null)
        //            {
        //                response.Message = $"Product with ID {detail.ProductId} not found";
        //                return BadRequest(response);
        //            }

        //            if (product.StockQuantity < detail.Quantity)
        //            {
        //                response.Message = $"Insufficient stock for product ID {detail.ProductId}";
        //                return BadRequest(response);
        //            }

        //            product.StockQuantity -= detail.Quantity;
        //            _context.Products.Update(product);

        //            var newDetail = new OrderDetail
        //            {
        //                OrderId = id,
        //                ProductId = detail.ProductId,
        //                Quantity = detail.Quantity,
        //                Price = product.CurrentPrice
        //            };

        //            totalAmount += detail.Quantity * detail.Price;
        //            existingOrder.OrderDetails.Add(newDetail);
        //        }

        //        existingOrder.TotalAmount = totalAmount;
        //        _context.Orders.Update(existingOrder);
        //        _context.SaveChanges();
        //        transaction.Commit();

        //        response.Message = "Success";
        //        return Ok(response);
        //    }
        //    catch (Exception ex)
        //    {
        //        transaction.Rollback();
        //        response.Message = ex.Message;
        //        return StatusCode(500, response);
        //    }
        //}




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

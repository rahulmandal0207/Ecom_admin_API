using Ecommerce_API_2.Data;
using Ecommerce_API_2.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Dynamic;
using System.Data;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Ecommerce_API_2.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly EcommerceContext _context;

        public UserController(EcommerceContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult GetUsers()
        {
            dynamic response = new ExpandoObject();
            try
            {
                var data = (from u in _context.Users
                            join o in _context.Orders on u.UserId equals o.UserId into userOrders
                            from o in userOrders.DefaultIfEmpty()
                            join od in _context.OrderDetails on o.OrderId equals od.OrderId into orderDetails
                            from od in orderDetails.DefaultIfEmpty()
                            join p in _context.Products on od.ProductId equals p.ProductId into product
                            from p in product.DefaultIfEmpty()
                            select new
                            {
                                u.UserId,
                                u.Email,
                                u.Password,
                                Orders = o == null ? null : new
                                {
                                    o.OrderId,
                                    o.OrderDate,
                                    o.TotalAmount,
                                    o.OrderStatus,

                                    OrderDetails = od == null ? null : new
                                    {
                                        od.OrderDetailId,
                                        od.ProductId,
                                        od.Quantity,
                                        od.Price,
                                        Product = p == null ? null : new
                                        {
                                            p.ProductName,
                                            p.CurrentPrice,
                                            p.StockQuantity
                                        }
                                    }
                                }
                            }).ToList();

                response.Data = data;
                response.Message = "Success";
            }
            catch(Exception e)
            {
                response.Message = e.Message;
            }

            return Ok(response);
        }

        [HttpPost]
        public IActionResult CreateUser(UserModel dto)
        {
            dynamic response = new ExpandoObject();
            try
            {
               var user = new User()
               {
                   Email = dto.Email,
                   Password = dto.Password,
                   Role = dto.Role
               };

                
                _context.Users.Add(user);
                _context.SaveChanges();

                response.Message = "Success";
                response.Data = user;
            }
            catch (Exception e)
            {
                response.Message = e.Message;
            }
            return Ok(response);
        }

        [HttpPut("{id}")]
        public IActionResult UpdateUser(int id, UserModel dto)
        {

            dynamic response = new ExpandoObject();
            if (id != dto.UserId)
            {
                response.Message = "Invalid User";
                return BadRequest(response);
            }

            try
            {
                User? user = (from u in _context.Users.Where(x => x.UserId == dto.UserId)
                             select u).FirstOrDefault();
                
                if (user == null)
                {
                    response.Message = "User not found";
                    return NotFound(response);
                }

                user.Email = dto.Email;
                user.Password = dto.Password;
                user.Role = dto.Role;
                

                _context.Users.Update(user);
                _context.SaveChanges();

                response.Message = "Success";
                response.Data = user;
            }
            catch(Exception e)
            {
                response.Message = e.Message;
            }
            return Ok(response);
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteUser(int id)
        {
            dynamic response = new ExpandoObject();
            
            try
            {
                User? user = (from u in _context.Users.Where(x => x.UserId == id)
                              select u).FirstOrDefault();

                if (user == null)
                {
                    response.Message = "User Not found";
                    return NotFound(response);
                }

                _context.Users.Remove(user);
                _context.SaveChanges();

                response.Message = "Deleted User";
                response.Data = user;
            }
            catch (Exception e)
            {
                response.Message = e.Message;
            }

            return Ok(response);
        }

 
    }

    public class UserModel 
    {
        [Key]
        public int UserId { get; set; }

        [StringLength(50)]
        public string Email { get; set; } = null!;

        [StringLength(50)]
        public string Password { get; set; } = null!;

        public byte Role { get; set; }

    }
}

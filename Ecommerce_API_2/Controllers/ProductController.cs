using Ecommerce_API_2.Data;
using Ecommerce_API_2.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Dynamic;

namespace Ecommerce_API_2.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly EcommerceContext _context;

        public ProductController(EcommerceContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult GetProducts()
        {
            dynamic response = new ExpandoObject();
            try
            {
                var data = (from p in _context.Products
                            select new
                            {
                                p.ProductId,
                                p.ProductName,
                                p.CurrentPrice,
                                p.StockQuantity,
                                p.ProductImageUrl,
                            }).ToList();

                response.Message = "Success";
                response.Data = data;
            }
            catch (System.Exception ex)
            {
                response.Message = ex.Message;
            }
            return Ok(response);
        }

        [HttpGet("{id}")]
        public IActionResult GetProduct(int id)
        {
            dynamic response = new ExpandoObject();
            try
            {
                var product = (from p in _context.Products
                               where p.ProductId == id
                               select new
                               {
                                   p.ProductId,
                                   p.ProductName,
                                   p.CurrentPrice,
                                   p.StockQuantity,
                                   p.ProductImageUrl,
                               }).FirstOrDefault();

                if (product == null)
                {
                    response.Message = "Product Not found";
                    return NotFound(response);
                }
                response.Message = "Success";
                response.Data = product;

                return Ok(product);
            }
            catch(Exception e)
            {
                response.Message = e.Message;
            }

            return response;
        }

        [HttpPost]
        public IActionResult SaveProduct(ProductModel dto)
        {
            dynamic response = new ExpandoObject();
            
            try
            {
                Product product = new Product()
                {
                    ProductName = dto.ProductName,
                    CurrentPrice = dto.CurrentPrice,
                    StockQuantity = dto.StockQuantity,
                    ProductImageUrl = dto.ProductImageUrl,
                };

                _context.Products.Add(product);
                _context.SaveChanges();

                response.Message = "Success";
                response.Data = product;

            }
            catch(Exception e)
            {
                response.Message = e.Message;
            }

            return Ok(response);
        }



        [HttpPut]
        public IActionResult UpdateProduct(ProductModel dto)
        {
            dynamic response = new ExpandoObject();

            try
            {
                Product? product = (from p in _context.Products
                                    where p.ProductId == dto.ProductId
                                    select p).FirstOrDefault();
                
                if (product == null)
                {
                    response.Message = "Product not found";
                    return NotFound(response);
                };

                product.ProductName = dto.ProductName;
                product.CurrentPrice = dto.CurrentPrice;
                product.StockQuantity = dto.StockQuantity;
                product.ProductImageUrl = dto.ProductImageUrl;

                _context.SaveChanges();

                response.Message = "Success";
                response.Data = product;

            }
            catch (Exception e)
            {
                response.Message = e.Message;
            }

            return Ok(response);
        }

        [HttpDelete("{id}")]
        public IActionResult DeleleProduct(int id)
        {
            dynamic response = new ExpandoObject();
            try
            {
                Product? product = (from p in _context.Products
                                    where p.ProductId == id
                                    select p).FirstOrDefault();

                if (product == null)
                {
                    response.Message = "Product does not exist.";
                    return NotFound(response);
                }

                _context.Products.Remove(product);
                _context.SaveChanges();

                response.Message = "Delete Success";
                response.Data = product;
            }
            catch(Exception e)
            {
                response.Message = e.Message;
            }

            return Ok(response);
        }
    }

    public class ProductModel
    {
        public int ProductId { get; set; }

        [StringLength(255)]
        public string ProductName { get; set; } = null!;

        [Column(TypeName = "decimal(18, 0)")]
        public decimal CurrentPrice { get; set; }

        public int StockQuantity { get; set; }

        public string? ProductImageUrl { get; set; }
    }
}

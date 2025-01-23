using Ecommerce_API_2.Data;
using Ecommerce_API_2.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Dynamic;

namespace Ecommerce_API_2.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EnumController : ControllerBase
    {
        private readonly EcommerceContext _context;

        public EnumController(EcommerceContext context)
        {
            _context = context;
        }

        [HttpGet("userroles")]
        public IActionResult GetUserRoles()
        {
            dynamic res = new ExpandoObject();
            try
            {
                var roles = Enum.GetValues(typeof(UserRole))
                                .Cast<UserRole>()
                                .Select(role => new StateModel
                                {
                                    Key = (int)role,
                                    Value = role.ToString()
                                })
                                .ToList();

                res.Success = true;
                res.Data = roles;

                return Ok(res);
            }
            catch (Exception e)
            {
                res.Success = false;
                res.Message = e.Message;
                return StatusCode(500, res);
            }
        }
    }

    public class StateModel
    {
        public int Key { get; set; }
        public string Value { get; set; }
    }
}

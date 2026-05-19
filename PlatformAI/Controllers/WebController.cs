using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace PlatFormAI.Controllers;

 [Route("[controller]")]
 [ApiController]
 [Authorize]
 public class WebController : ControllerBase
    {

        #region Header

        [HttpGet]
        [Route("Test")]
        public IActionResult Test()
        {
           // var tenants = BaseDataManager.GetListTenants(Constants.BTCCustomer.Code);
               return Ok(new { message = "Accesso riuscito, sei autenticato!" });
        }
        #endregion
    }
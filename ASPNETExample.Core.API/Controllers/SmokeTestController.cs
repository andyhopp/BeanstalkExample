using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ASPNETExample.Core.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SmokeTestController : ControllerBase
    {
        private readonly ILogger<SmokeTestController> _logger;

        public SmokeTestController(ILogger<SmokeTestController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public async Task<string> Get()
        {
            return System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList.Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).First().ToString();
        }
    }
}

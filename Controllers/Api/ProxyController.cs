using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Net.Http;
using System.Threading.Tasks;

namespace resturanyar.Controllers.Api
{
    [Authorize]
    public class ProxyController : Controller
    {
         
         
            private readonly IHttpClientFactory _httpClientFactory;
            private const string StaticToken = "stR@nG3_Stat1c_T0ken_Resturanyar_2025!#X9LpQ";
            private const string TargetBaseUrl = "https://resturanyar.ir/api/UserApi/";

            public ProxyController(IHttpClientFactory httpClientFactory)
            {
                _httpClientFactory = httpClientFactory;
            }

            [HttpGet]
            [Route("Proxy/GetSubscriptions")]
            public async Task<IActionResult> GetSubscriptions()
            {
                var client = _httpClientFactory.CreateClient();

                
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("Authorization", StaticToken);
                client.DefaultRequestHeaders.Add("Accept", "*/*");

                try
                {
                    var response = await client.GetAsync($"{TargetBaseUrl}/getallsubscriptions");

                    if (response.IsSuccessStatusCode)
                    {
                        var data = await response.Content.ReadAsStringAsync();
                        return Content(data, "application/json");
                    }

                    return StatusCode((int)response.StatusCode, "خطا در برقراری ارتباط با سرور اصلی");
                }
                catch (HttpRequestException ex)
                {
                    return StatusCode(500, $"خطای داخلی سرور: {ex.Message}");
                }
            }
        }
    }

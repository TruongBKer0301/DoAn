using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace LapTopBD.Controllers
{
    [Route("api/address")]
    [ApiController]
    public class AddressApiController : ControllerBase
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string OPEN_API_BASE = "https://provinces.open-api.vn/api/v2";

        [HttpGet("provinces")]
        public async Task<IActionResult> GetProvinces()
        {
            try
            {
                var res = await _httpClient.GetAsync($"{OPEN_API_BASE}/p");
                if (!res.IsSuccessStatusCode)
                {
                    return StatusCode((int)res.StatusCode, new { error = "Failed to fetch provinces" });
                }

                var content = await res.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to fetch provinces", detail = ex.Message });
            }
        }

        [HttpGet("provinces/{code}/communes")]
        public async Task<IActionResult> GetCommunes(int code)
        {
            try
            {
                var res = await _httpClient.GetAsync($"{OPEN_API_BASE}/p/{code}?depth=2");

                if (!res.IsSuccessStatusCode)
                {
                    return StatusCode((int)res.StatusCode, new { error = "Failed to fetch communes" });
                }

                var content = await res.Content.ReadAsStringAsync();
                var province = JsonSerializer.Deserialize<ProvinceResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var wards = province?.wards ?? new List<WardOption>();

                var result = wards
                    .Where(w => !string.IsNullOrWhiteSpace(w.name))
                    .Select(w => new
                    {
                        name = w.name,
                        code = w.code,
                        districtName = ""
                    })
                    .OrderBy(w => w.name)
                    .ToList();

                return new JsonResult(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to fetch communes", detail = ex.Message });
            }
        }

        public class WardOption
        {
            public string? name { get; set; }
            public int code { get; set; }
            public string? division_type { get; set; }
            public string? codename { get; set; }
            public int province_code { get; set; }
        }

        public class ProvinceResponse
        {
            public string? name { get; set; }
            public int code { get; set; }
            public string? division_type { get; set; }
            public string? codename { get; set; }
            public int phone_code { get; set; }
            public List<WardOption>? wards { get; set; }
        }
    }
}

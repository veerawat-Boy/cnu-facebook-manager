using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CnuFacebookAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CnuLineController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IMemoryCache _cache;
        private readonly IHttpClientFactory _httpFactory;

        private static readonly string LineSystemPrompt = CnuFacebookController.FirstPromp;

        public CnuLineController(IConfiguration config, IMemoryCache cache, IHttpClientFactory httpFactory)
        {
            _config = config;
            _cache = cache;
            _httpFactory = httpFactory;
        }

        // ─────────────────────────────────────────────────────────────
        // บันทึก LINE Official Account (Channel Access Token + Secret)
        // ─────────────────────────────────────────────────────────────
        [HttpPost("SaveLineAccount")]
        public async Task<IActionResult> SaveLineAccount([FromBody] SaveLineAccountRequest req)
        {
            if (req == null ||
                string.IsNullOrEmpty(req.ChannelAccessToken) ||
                string.IsNullOrEmpty(req.ChannelSecret) ||
                string.IsNullOrEmpty(req.CreateUserId))
                return BadRequest(new { message = "ข้อมูลไม่ครบ" });

            using var http = _httpFactory.CreateClient();
            var botReq = new HttpRequestMessage(HttpMethod.Get, "https://api.line.me/v2/bot/info");
            botReq.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", req.ChannelAccessToken);

            var botRes = await http.SendAsync(botReq);
            if (!botRes.IsSuccessStatusCode)
                return BadRequest(new { message = "Channel Access Token ไม่ถูกต้อง กรุณาตรวจสอบใหม่" });

            var botDoc = JsonDocument.Parse(await botRes.Content.ReadAsStringAsync());
            string botUserId    = botDoc.RootElement.TryGetProperty("userId",      out var uid) ? uid.GetString() ?? "" : "";
            string botName      = botDoc.RootElement.TryGetProperty("displayName", out var dn)  ? dn.GetString()  ?? "" : "";

            if (string.IsNullOrEmpty(botUserId))
                return BadRequest(new { message = "ไม่สามารถดึงข้อมูล LINE Official Account ได้" });

            string channelName = !string.IsNullOrWhiteSpace(req.ChannelName) ? req.ChannelName.Trim() : botName;

            string connStr = _config["ConnectionStrings:EMS"]!;
            using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO accesstokenline
                    (channelid, channelname, channelaccesstoken, channelsecret, createuserid, createdate, createtime)
                VALUES
                    (@cid, @cname, @token, @secret, @uid, CURRENT_DATE, TO_CHAR(NOW(), 'HH24:MI:SS'))
                ON CONFLICT (channelid, createuserid) DO UPDATE
                SET channelname        = @cname,
                    channelaccesstoken = @token,
                    channelsecret      = @secret,
                    updateuserid       = @uid,
                    updatedate         = CURRENT_DATE,
                    updatetime         = TO_CHAR(NOW(), 'HH24:MI:SS')", conn);

            cmd.Parameters.AddWithValue("@cid",    botUserId);
            cmd.Parameters.AddWithValue("@cname",  channelName);
            cmd.Parameters.AddWithValue("@token",  req.ChannelAccessToken);
            cmd.Parameters.AddWithValue("@secret", req.ChannelSecret);
            cmd.Parameters.AddWithValue("@uid",    req.CreateUserId);
            await cmd.ExecuteNonQueryAsync();

            return Ok(new { message = "บันทึกสำเร็จ", channelName, botUserId });
        }

        // ─────────────────────────────────────────────────────────────
        // ดึงรายการ LINE Official Accounts ของ user
        // ─────────────────────────────────────────────────────────────
        [HttpGet("GetLineAccounts")]
        public IActionResult GetLineAccounts([FromQuery] string lineUserId)
        {
            if (string.IsNullOrWhiteSpace(lineUserId))
                return BadRequest("ต้องระบุ lineUserId");

            string connStr = _config["ConnectionStrings:EMS"]!;
            var result = new List<object>();

            using var conn = new NpgsqlConnection(connStr);
            conn.Open();
            using var cmd = new NpgsqlCommand(@"
                SELECT id, channelid, channelname, openstatus, createdate, createtime
                FROM accesstokenline
                WHERE createuserid = @uid
                ORDER BY createdate DESC, createtime DESC", conn);
            cmd.Parameters.AddWithValue("@uid", lineUserId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new
                {
                    id          = reader["id"].ToString(),
                    channelId   = reader["channelid"].ToString(),
                    channelName = reader["channelname"].ToString(),
                    openStatus  = reader["openstatus"] == DBNull.Value ? "1" : reader["openstatus"].ToString(),
                    createDate  = reader["createdate"].ToString()
                });
            }

            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────────
        // เปิด/ปิดตอบอัตโนมัติ
        // ─────────────────────────────────────────────────────────────
        [HttpPatch("ToggleLineAccountStatus")]
        public IActionResult ToggleLineAccountStatus([FromBody] ToggleLineRequest req)
        {
            if (req == null || string.IsNullOrEmpty(req.ChannelId) || string.IsNullOrEmpty(req.LineUserId))
                return BadRequest("ข้อมูลไม่ครบ");

            string connStr = _config["ConnectionStrings:EMS"]!;
            using var conn = new NpgsqlConnection(connStr);
            conn.Open();
            using var cmd = new NpgsqlCommand(@"
                UPDATE accesstokenline
                SET openstatus = @status
                WHERE createuserid = @uid AND channelid = @cid", conn);
            cmd.Parameters.AddWithValue("@status", req.OpenStatus);
            cmd.Parameters.AddWithValue("@uid",    req.LineUserId);
            cmd.Parameters.AddWithValue("@cid",    req.ChannelId);

            int rows = cmd.ExecuteNonQuery();
            return rows > 0 ? Ok("อัปเดตสถานะสำเร็จ") : NotFound("ไม่พบข้อมูล");
        }

        // ─────────────────────────────────────────────────────────────
        // ยกเลิกการเชื่อมต่อ LINE Official Account
        // ─────────────────────────────────────────────────────────────
        [HttpPatch("DisconnectLineAccount")]
        public IActionResult DisconnectLineAccount([FromBody] DisconnectLineRequest req)
        {
            if (req == null || string.IsNullOrEmpty(req.ChannelId) || string.IsNullOrEmpty(req.LineUserId))
                return BadRequest("ข้อมูลไม่ครบ");

            string connStr = _config["ConnectionStrings:EMS"]!;
            using var conn = new NpgsqlConnection(connStr);
            conn.Open();
            using var cmd = new NpgsqlCommand(@"
                DELETE FROM accesstokenline
                WHERE createuserid = @uid AND channelid = @cid", conn);
            cmd.Parameters.AddWithValue("@uid", req.LineUserId);
            cmd.Parameters.AddWithValue("@cid", req.ChannelId);

            int rows = cmd.ExecuteNonQuery();
            return rows > 0 ? Ok("ยกเลิกการเชื่อมต่อสำเร็จ") : NotFound("ไม่พบข้อมูล");
        }

        // ─────────────────────────────────────────────────────────────
        // LINE Webhook — LINE ส่ง event มาที่นี่เมื่อมีข้อความเข้า
        // ─────────────────────────────────────────────────────────────
        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook()
        {
            string body;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                body = await reader.ReadToEndAsync();

            if (string.IsNullOrEmpty(body)) return Ok();

            JsonDocument doc;
            try { doc = JsonDocument.Parse(body); }
            catch { return Ok(); }

            string destination = doc.RootElement.TryGetProperty("destination", out var dest)
                ? dest.GetString() ?? "" : "";

            if (string.IsNullOrEmpty(destination)) return Ok();

            var (accessToken, channelSecret) = GetChannelCredentials(destination);
            if (string.IsNullOrEmpty(accessToken)) return Ok();

            string signature = Request.Headers["X-Line-Signature"].FirstOrDefault() ?? "";
            if (!VerifySignature(channelSecret, body, signature))
            {
                Console.WriteLine("[LINE Webhook] signature mismatch");
                return Unauthorized();
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    if (!doc.RootElement.TryGetProperty("events", out var events)) return;
                    foreach (var ev in events.EnumerateArray())
                    {
                        if (!ev.TryGetProperty("type", out var t) || t.GetString() != "message") continue;
                        if (!ev.TryGetProperty("message", out var msg)) continue;
                        if (!msg.TryGetProperty("type", out var mt) || mt.GetString() != "text") continue;
                        if (!msg.TryGetProperty("text", out var textEl)) continue;
                        if (!ev.TryGetProperty("replyToken", out var rtEl)) continue;
                        if (!ev.TryGetProperty("source", out var src)) continue;
                        if (!src.TryGetProperty("userId", out var senderEl)) continue;

                        string userText   = textEl.GetString() ?? "";
                        string replyToken = rtEl.GetString()   ?? "";
                        string senderId   = senderEl.GetString() ?? "";

                        if (string.IsNullOrWhiteSpace(userText) || string.IsNullOrWhiteSpace(replyToken)) continue;

                        await ProcessAndReplyAsync(senderId, destination, userText, replyToken, accessToken);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LINE Webhook] background error: {ex.Message}");
                }
            });

            return Ok();
        }

        private (string accessToken, string secret) GetChannelCredentials(string channelId)
        {
            try
            {
                string connStr = _config["ConnectionStrings:EMS"]!;
                using var conn = new NpgsqlConnection(connStr);
                conn.Open();
                using var cmd = new NpgsqlCommand(@"
                    SELECT channelaccesstoken, channelsecret
                    FROM accesstokenline
                    WHERE channelid = @cid AND openstatus = '1'
                    LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@cid", channelId);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                    return (reader["channelaccesstoken"].ToString() ?? "",
                            reader["channelsecret"].ToString() ?? "");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LINE] GetChannelCredentials error: {ex.Message}");
            }
            return ("", "");
        }

        private static bool VerifySignature(string channelSecret, string body, string signature)
        {
            if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(channelSecret)) return false;
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(channelSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
            return Convert.ToBase64String(hash) == signature;
        }

        private async Task ProcessAndReplyAsync(
            string senderId, string channelId, string userText, string replyToken, string accessToken)
        {
            try
            {
                string aiToken = _config["AI:GeminiToken"] ?? "";
                string model   = "gemini-2.5-flash";
                string cacheKey = $"line_conv_{channelId}_{senderId}";

                if (!_cache.TryGetValue(cacheKey, out List<ConvMsg>? history) || history == null)
                    history = new List<ConvMsg>();

                history.Add(new ConvMsg("user", userText));
                if (history.Count > 20)
                    history = history.GetRange(history.Count - 20, 20);

                var geminiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={aiToken}";
                var geminiRequest = new
                {
                    system_instruction = new { parts = new[] { new { text = LineSystemPrompt } } },
                    contents = history.Select(m => new
                    {
                        role  = m.Role,
                        parts = new[] { new { text = m.Text } }
                    }).ToArray()
                };

                Console.WriteLine($"[LINE Gemini] model={model} history={history.Count}");

                using var http = _httpFactory.CreateClient();
                var aiRes = await http.PostAsync(geminiUrl,
                    new StringContent(
                        JsonConvert.SerializeObject(geminiRequest,
                            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }),
                        Encoding.UTF8, "application/json"));

                if (!aiRes.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[LINE Gemini] Error {(int)aiRes.StatusCode}");
                    return;
                }

                var aiDoc = JsonDocument.Parse(await aiRes.Content.ReadAsStringAsync());
                string? replyText = aiDoc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                if (string.IsNullOrEmpty(replyText)) return;

                history.Add(new ConvMsg("model", replyText));
                _cache.Set(cacheKey, history, new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromMinutes(30)
                });

                // LINE reply — max 5 messages, 5000 chars each
                const int limit = 5000;
                var messages = new List<object>();
                for (int i = 0; i < replyText.Length && messages.Count < 5; i += limit)
                    messages.Add(new { type = "text", text = replyText.Substring(i, Math.Min(limit, replyText.Length - i)) });

                var linePayload = new { replyToken, messages };
                var lineReq = new HttpRequestMessage(HttpMethod.Post, "https://api.line.me/v2/bot/message/reply");
                lineReq.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                lineReq.Content = new StringContent(
                    JsonConvert.SerializeObject(linePayload), Encoding.UTF8, "application/json");

                var lineRes = await http.SendAsync(lineReq);
                Console.WriteLine($"[LINE Reply] {(int)lineRes.StatusCode}: {await lineRes.Content.ReadAsStringAsync()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LINE ProcessAndReply] error: {ex.Message}");
            }
        }

        private record ConvMsg(string Role, string Text);

        public class SaveLineAccountRequest
        {
            public string ChannelName        { get; set; } = "";
            public string ChannelAccessToken { get; set; } = "";
            public string ChannelSecret      { get; set; } = "";
            public string CreateUserId       { get; set; } = "";
        }

        public class ToggleLineRequest
        {
            public string LineUserId  { get; set; } = "";
            public string ChannelId   { get; set; } = "";
            public string OpenStatus  { get; set; } = "0";
        }

        public class DisconnectLineRequest
        {
            public string LineUserId { get; set; } = "";
            public string ChannelId  { get; set; } = "";
        }
    }
}

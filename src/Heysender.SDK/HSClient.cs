using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Heysender.SDK
{
    /// <summary>
    /// Heysender C# SDK
    ///
    /// A comprehensive SDK for interacting with the Heysender API
    /// Version: 0.9
    /// </summary>

    #region Exceptions

    /// <summary>
    /// Custom exception for Heysender API errors
    /// </summary>
    public class HeysenderException : Exception
    {
        public int? StatusCode { get; }
        public string ResponseBody { get; }

        public HeysenderException(string message) : base(message)
        {
        }

        public HeysenderException(string message, int statusCode, string responseBody)
            : base(message)
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }
    }

    #endregion

    #region Main Client

    /// <summary>
    /// Main Heysender API Client
    /// </summary>
    public class HeysenderClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly JsonSerializerOptions _jsonOptions;

        /// <summary>
        /// Create a new Heysender client
        /// </summary>
        /// <param name="apiKey">Your Heysender API key</param>
        /// <param name="apiSecret">Your Heysender API secret</param>
        /// <param name="baseUrl">Base URL for the API</param>
        public HeysenderClient(string apiKey, string apiSecret, string baseUrl = "https://app.heysender.com")
        {
            _baseUrl = baseUrl;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:{apiSecret}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HS-csharp-sdk/0.9");

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        private async Task<T> RequestAsync<T>(HttpMethod method, string endpoint, object body = null)
        {
            var url = _baseUrl + endpoint;
            var request = new HttpRequestMessage(method, url);

            if (body != null)
            {
                var json = JsonSerializer.Serialize(body, _jsonOptions);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HeysenderException(
                    $"API Error ({(int)response.StatusCode}): {responseBody}",
                    (int)response.StatusCode,
                    responseBody
                );
            }

            if (string.IsNullOrEmpty(responseBody))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(responseBody, _jsonOptions);
        }

        private async Task RequestAsync(HttpMethod method, string endpoint, object body = null)
        {
            var url = _baseUrl + endpoint;
            var request = new HttpRequestMessage(method, url);

            if (body != null)
            {
                var json = JsonSerializer.Serialize(body, _jsonOptions);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                throw new HeysenderException(
                    $"API Error ({(int)response.StatusCode}): {responseBody}",
                    (int)response.StatusCode,
                    responseBody
                );
            }
        }

        // ==================== DOMAIN METHODS ====================

        /// <summary>
        /// Get list of domains
        /// </summary>
        public async Task<List<Domain>> GetDomainsAsync()
        {
            return await RequestAsync<List<Domain>>(HttpMethod.Get, "/api/domains");
        }

        /// <summary>
        /// Create a new domain
        /// </summary>
        public async Task<Domain> CreateDomainAsync(CreateDomainRequest request)
        {
            return await RequestAsync<Domain>(HttpMethod.Post, "/api/domains", request);
        }

        /// <summary>
        /// Update domain with new DKIM key
        /// </summary>
        public async Task<string> UpdateDomainAsync(string domain, string dkimKey)
        {
            return await RequestAsync<string>(HttpMethod.Put, $"/api/domains/{domain}", new { dkim_key = dkimKey });
        }

        /// <summary>
        /// Delete a domain
        /// </summary>
        public async Task DeleteDomainAsync(string domain)
        {
            await RequestAsync(HttpMethod.Delete, $"/api/domains/{domain}");
        }

        /// <summary>
        /// Validate domain SPF and DKIM
        /// </summary>
        public async Task<DomainValidation> ValidateDomainAsync(string domain)
        {
            return await RequestAsync<DomainValidation>(HttpMethod.Get, $"/api/domains/{domain}/validate");
        }

        // ==================== SMTP USER METHODS ====================

        /// <summary>
        /// Get SMTP users for a domain
        /// </summary>
        public async Task<List<SmtpUser>> GetSmtpUsersAsync(int domainId)
        {
            var wrapper = await RequestAsync<SmtpUsersResponse>(HttpMethod.Get, $"/api/smtp/{domainId}");
            List<SmtpUser> users = wrapper?.Data ?? new List<SmtpUser>();
            foreach (var user in users)
            {
                if (user.AnonymizeOptions == null || !user.AnonymizeOptions.Any())
                {
                    user.AnonymizeOptions = BuildAnonymizeOptions(user);
                }
            }
            return users;
        }

        private List<string> BuildAnonymizeOptions(SmtpUser user)
        {
            var options = new List<string>();

            if (user.AnonymizeAll == true)  // Compare with true
            {
                return new List<string> { "all" };
            }

            if (user.AnonymizeNone == true)
            {
                return new List<string> { "none" };
            }

            if (user.AnonymizeRecipient == true) options.Add("recipient");
            if (user.AnonymizeSubject == true) options.Add("subject");
            if (user.AnonymizeContent == true) options.Add("content");

            return options.Count > 0 ? options : new List<string> { "none" };
        }

        /// <summary>
        /// Create SMTP user
        /// </summary>
        public async Task<SmtpUser> CreateSmtpUserAsync(int domainId, CreateSmtpUserRequest request)
        {
            return await RequestAsync<SmtpUser>(HttpMethod.Post, $"/api/smtp/{domainId}", request);
        }

        /// <summary>
        /// Delete SMTP user
        /// </summary>
        public async Task DeleteSmtpUserAsync(int domainId, int userId)
        {
            await RequestAsync(HttpMethod.Delete, $"/api/smtp/{domainId}/{userId}");
        }

        /// <summary>
        /// Generate new password for SMTP user
        /// </summary>
        public async Task<List<SmtpUser>> ResetSmtpPasswordAsync(int domainId, int userId)
        {
            return await RequestAsync<List<SmtpUser>>(HttpMethod.Get, $"/api/smtp/{domainId}/{userId}/newpassword");
        }

        // ==================== WEBHOOK METHODS ====================

        /// <summary>
        /// Get webhooks for a domain
        /// </summary>
        public async Task<List<Webhook>> GetWebhooksAsync(string domain)
        {
            return await RequestAsync<List<Webhook>>(HttpMethod.Get, $"/api/webhooks/{domain}");
        }

        /// <summary>
        /// Get specific webhook
        /// </summary>
        public async Task<Webhook> GetWebhookAsync(string domain, int webhookId)
        {
            return await RequestAsync<Webhook>(HttpMethod.Get, $"/api/webhooks/{domain}/{webhookId}");
        }

        /// <summary>
        /// Create webhook
        /// </summary>
        public async Task<WebhookCreatedResponse> CreateWebhookAsync(string domain, WebhookRequest request)
        {
            return await RequestAsync<WebhookCreatedResponse>(HttpMethod.Post, $"/api/webhooks/{domain}", request);
        }

        /// <summary>
        /// Update webhook
        /// </summary>
        public async Task UpdateWebhookAsync(string domain, int webhookId, WebhookRequest request)
        {
            await RequestAsync(HttpMethod.Put, $"/api/webhooks/{domain}/{webhookId}", request);
        }

        /// <summary>
        /// Delete webhook
        /// </summary>
        public async Task DeleteWebhookAsync(string domain, int webhookId)
        {
            await RequestAsync(HttpMethod.Delete, $"/api/webhooks/{domain}/{webhookId}");
        }

        // ==================== MESSAGE METHODS ====================

        /// <summary>
        /// Send an email message
        /// </summary>
        public async Task<List<MessageResponse>> SendMessageAsync(Message message)
        {
            return await RequestAsync<List<MessageResponse>>(HttpMethod.Post, "/api/message", message);
        }

        /// <summary>
        /// Get message information
        /// </summary>
        public async Task<MessageInfo> GetMessageAsync(string messageId)
        {
            return await RequestAsync<MessageInfo>(HttpMethod.Get, $"/api/message/{messageId}");
        }

        /// <summary>
        /// Get message information for specific recipient
        /// </summary>
        public async Task<MessageInfo> GetMessageByRecipientAsync(string messageId, string recipient)
        {
            return await RequestAsync<MessageInfo>(HttpMethod.Get, $"/api/message/{messageId}/{recipient}");
        }

        // ==================== SUPPRESSION METHODS ====================

        /// <summary>
        /// Get suppressions by domain and type
        /// </summary>
        public async Task<SuppressionList> GetSuppressionsAsync(string domain, string type)
        {
            return await RequestAsync<SuppressionList>(HttpMethod.Get, $"/api/suppressions/{domain}/{type}");
        }

        /// <summary>
        /// Remove email from bounce suppressions
        /// </summary>
        public async Task RemoveBounceAsync(string domain, string email)
        {
            await RequestAsync(HttpMethod.Delete, $"/api/suppressions/{domain}/bounce/{email}");
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    #endregion

    #region Data Models

    public class Domain
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("validated")]
        [JsonConverter(typeof(BoolOrIntConverter))]
        public bool Validated { get; set; }
    }

    public class CreateDomainRequest
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("custom_selector")]
        public string CustomSelector { get; set; }

        [JsonPropertyName("dkim_key")]
        public string DkimKey { get; set; }
    }

    public class DomainValidation
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("validated")]
        public int Validated { get; set; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public string UpdatedAt { get; set; }

        [JsonPropertyName("dkim")]
        public DkimRecord Dkim { get; set; }

        [JsonPropertyName("spf")]
        public string Spf { get; set; }

        [JsonPropertyName("spfValid")]
        public bool SpfValid { get; set; }

        [JsonPropertyName("dkimValid")]
        public bool DkimValid { get; set; }
    }

    public class DkimRecord
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }
    }

    public class SmtpUser
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("domain_id")]
        public int DomainId { get; set; }

        [JsonPropertyName("smtp_email")]
        public string SmtpEmail { get; set; }

        [JsonPropertyName("smtp_password")]
        public string SmtpPassword { get; set; }

        [JsonPropertyName("anonymize_options")]
        public List<string> AnonymizeOptions { get; set; }

        [JsonPropertyName("anonymize_none")]
        [JsonConverter(typeof(BoolOrIntConverter))]
        public bool? AnonymizeNone { get; set; }

        [JsonPropertyName("anonymize_all")]
        [JsonConverter(typeof(BoolOrIntConverter))]
        public bool? AnonymizeAll { get; set; }

        [JsonPropertyName("anonymize_subject")]
        [JsonConverter(typeof(BoolOrIntConverter))]
        public bool? AnonymizeSubject { get; set; }

        [JsonPropertyName("anonymize_content")]
        [JsonConverter(typeof(BoolOrIntConverter))]
        public bool? AnonymizeContent { get; set; }

        [JsonPropertyName("anonymize_recipient")]
        [JsonConverter(typeof(BoolOrIntConverter))]
        public bool? AnonymizeRecipient { get; set; }

    }

    public class SmtpUsersResponse
    {
        [JsonPropertyName("data")]
        public List<SmtpUser> Data { get; set; }
    }

    public class CreateSmtpUserRequest
    {
        [JsonPropertyName("smtp_email")]
        public string SmtpEmail { get; set; }

        [JsonPropertyName("anonymize_options")]
        public List<string> AnonymizeOptions { get; set; } = new List<string> { "none" };

        public CreateSmtpUserRequest SetAnonymizeOptions(List<string> options)
        {
            AnonymizeOptions = options;
            return this;
        }

        public CreateSmtpUserRequest SetAnonymizeOptions(List<AnonymizeOption> options)
        {
            AnonymizeOptions = options.Select(opt => opt.ToApiString()).ToList();
            return this;
        }

        public CreateSmtpUserRequest SetAnonymizeOptions(params AnonymizeOption[] options)
        {
            AnonymizeOptions = options.Select(opt => opt.ToApiString()).ToList();
            return this;
        }
    }

    public class Webhook
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("domain_id")]
        public int DomainId { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("latest_code")]
        public int LatestCode { get; set; }

        [JsonPropertyName("last_success")]
        public string LastSuccess { get; set; }

        [JsonPropertyName("queued")]
        public bool Queued { get; set; }

        [JsonPropertyName("sent")]
        public bool Sent { get; set; }

        [JsonPropertyName("attempt")]
        public bool Attempt { get; set; }

        [JsonPropertyName("soft_bounce")]
        public bool SoftBounce { get; set; }

        [JsonPropertyName("hard_bounce")]
        public bool HardBounce { get; set; }

        [JsonPropertyName("complaint")]
        public bool Complaint { get; set; }

        [JsonPropertyName("unsubscribe")]
        public bool Unsubscribe { get; set; }

        [JsonPropertyName("open")]
        public bool Open { get; set; }

        [JsonPropertyName("click")]
        public bool Click { get; set; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public string UpdatedAt { get; set; }
    }

    public class WebhookRequest
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("queued")]
        public bool? Queued { get; set; }

        [JsonPropertyName("sent")]
        public bool? Sent { get; set; }

        [JsonPropertyName("attempt")]
        public bool? Attempt { get; set; }

        [JsonPropertyName("soft_bounce")]
        public bool? SoftBounce { get; set; }

        [JsonPropertyName("hard_bounce")]
        public bool? HardBounce { get; set; }

        [JsonPropertyName("complaint")]
        public bool? Complaint { get; set; }

        [JsonPropertyName("unsubscribe")]
        public bool? Unsubscribe { get; set; }

        [JsonPropertyName("open")]
        public bool? Open { get; set; }

        [JsonPropertyName("click")]
        public bool? Click { get; set; }

        public void SetEvents(List<string> events)
        {
            Queued = events.Contains("queued");
            Sent = events.Contains("sent");
            Attempt = events.Contains("attempt");
            SoftBounce = events.Contains("soft_bounce");
            HardBounce = events.Contains("hard_bounce");
            Complaint = events.Contains("complaint");
            Unsubscribe = events.Contains("unsubscribe");
            Open = events.Contains("open");
            Click = events.Contains("click");
        }
    }

    public class WebhookCreatedResponse
    {
        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("id")]
        public int Id { get; set; }
    }

    public class Recipient
    {
        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        public Recipient() { }

        public Recipient(string email, string name = null)
        {
            Email = email;
            Name = name;
        }
    }

    public class Attachment
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        public Attachment() { }

        public Attachment(string name, string base64Content)
        {
            Name = name;
            Content = base64Content;
        }
    }

    public class Tag
    {
        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }

        public Tag() { }

        public Tag(string key, string value)
        {
            Key = key;
            Value = value;
        }
    }

    public class Header
    {
        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }

        public Header() { }

        public Header(string key, string value)
        {
            Key = key;
            Value = value;
        }
    }

    public class Message
    {
        [JsonPropertyName("from_email")]
        public string FromEmail { get; set; }

        [JsonPropertyName("from_name")]
        public string FromName { get; set; }

        [JsonPropertyName("html")]
        public string Html { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("subject")]
        public string Subject { get; set; }

        [JsonPropertyName("to")]
        public List<Recipient> To { get; set; } = new List<Recipient>();

        [JsonPropertyName("cc")]
        public List<Recipient> Cc { get; set; }

        [JsonPropertyName("bcc")]
        public List<string> Bcc { get; set; }

        [JsonPropertyName("reply_to")]
        public object ReplyTo { get; set; }

        [JsonPropertyName("custom_content")]
        public Dictionary<string, Dictionary<string, string>> CustomContent { get; set; }

        [JsonPropertyName("attachments")]
        public List<Attachment> Attachments { get; set; }

        [JsonPropertyName("tags")]
        public List<Tag> Tags { get; set; }

        [JsonPropertyName("headers")]
        public List<Header> Headers { get; set; }

        [JsonPropertyName("retention_time")]
        public int? RetentionTime { get; set; }

        [JsonPropertyName("list_unsubscribe")]
        public bool? ListUnsubscribe { get; set; }

        [JsonPropertyName("tracking")]
        public bool? Tracking { get; set; }

        [JsonPropertyName("anonymize_options")]
        public List<string> AnonymizeOptions { get; set; }
    }

    public class MessageResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("message_id")]
        public string MessageId { get; set; }

        [JsonPropertyName("recipient")]
        public string Recipient { get; set; }

        [JsonPropertyName("error")]
        public string Error { get; set; }
    }

    public class MessageInfo
    {
        [JsonPropertyName("protocol")]
        public string Protocol { get; set; }

        [JsonPropertyName("from_email")]
        public string FromEmail { get; set; }

        [JsonPropertyName("subject")]
        public string Subject { get; set; }

        [JsonPropertyName("domain")]
        public string Domain { get; set; }

        [JsonPropertyName("smtp_user")]
        public int? SmtpUser { get; set; }

        [JsonPropertyName("message_id")]
        public string MessageId { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("delete_at")]
        public string DeleteAt { get; set; }

        [JsonPropertyName("html_content")]
        public string HtmlContent { get; set; }

        [JsonPropertyName("text_content")]
        public string TextContent { get; set; }

        [JsonPropertyName("recipient_type")]
        public string RecipientType { get; set; }

        [JsonPropertyName("to_email")]
        public string ToEmail { get; set; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; }
    }

    public class SuppressionList
    {
        [JsonPropertyName("current_page")]
        public int CurrentPage { get; set; }

        [JsonPropertyName("data")]
        public List<Suppression> Data { get; set; }

        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("per_page")]
        public int PerPage { get; set; }
    }

    public class Suppression
    {
        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("error")]
        public string Error { get; set; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }
    }

    #endregion

    #region Message Builder

    /// <summary>
    /// Message builder helper class for message construction
    /// </summary>
    public class MessageBuilder
    {
        private readonly Message _message;

        public MessageBuilder(string fromEmail, string fromName, string subject, string html)
        {
            _message = new Message
            {
                FromEmail = fromEmail,
                FromName = fromName,
                Subject = subject,
                Html = html
            };
        }

        public MessageBuilder SetText(string text)
        {
            _message.Text = text;
            return this;
        }

        public MessageBuilder AddTo(string email, string name = null)
        {
            _message.To.Add(new Recipient(email, name));
            return this;
        }

        public MessageBuilder AddCc(string email, string name = null)
        {
            _message.Cc ??= new List<Recipient>();
            _message.Cc.Add(new Recipient(email, name));
            return this;
        }

        public MessageBuilder AddBcc(string email)
        {
            _message.Bcc ??= new List<string>();
            _message.Bcc.Add(email);
            return this;
        }

        public MessageBuilder SetReplyTo(string email)
        {
            _message.ReplyTo = email;
            return this;
        }

        public MessageBuilder AddAttachment(string name, string base64Content)
        {
            _message.Attachments ??= new List<Attachment>();
            _message.Attachments.Add(new Attachment(name, base64Content));
            return this;
        }

        public MessageBuilder AddTag(string key, string value)
        {
            _message.Tags ??= new List<Tag>();
            _message.Tags.Add(new Tag(key, value));
            return this;
        }

        public MessageBuilder AddHeader(string key, string value)
        {
            _message.Headers ??= new List<Header>();
            _message.Headers.Add(new Header(key, value));
            return this;
        }

        public MessageBuilder SetCustomContent(Dictionary<string, Dictionary<string, string>> content)
        {
            _message.CustomContent = content;
            return this;
        }

        public MessageBuilder SetTracking(bool enabled)
        {
            _message.Tracking = enabled;
            return this;
        }

        public MessageBuilder SetListUnsubscribe(bool enabled)
        {
            _message.ListUnsubscribe = enabled;
            return this;
        }

        public MessageBuilder SetRetentionTime(int days)
        {
            _message.RetentionTime = days;
            return this;
        }

        public MessageBuilder SetAnonymizeOptions(List<string> options)
        {
            _message.AnonymizeOptions = options;
            return this;
        }

        public MessageBuilder SetAnonymizeOptions(List<AnonymizeOption> options)
        {
            _message.AnonymizeOptions = options.Select(opt => opt.ToApiString()).ToList();
            return this;
        }

        public MessageBuilder SetAnonymizeOptions(params AnonymizeOption[] options)
        {
            _message.AnonymizeOptions = options.Select(opt => opt.ToApiString()).ToList();
            return this;
        }

        public Message Build()
        {
            return _message;
        }
    }

    #endregion

    #region Enumerations

    /// <summary>
    /// Anonymization options for email privacy compliance (e.g., GDPR)
    /// </summary>
    public enum AnonymizeOption
    {
        /// <summary>No anonymization</summary>
        None,
        /// <summary>Anonymize all fields</summary>
        All,
        /// <summary>Anonymize recipient information</summary>
        Recipient,
        /// <summary>Anonymize email subject</summary>
        Subject,
        /// <summary>Anonymize email content</summary>
        Content
    }

    /// <summary>
    /// Webhook event types for email tracking
    /// </summary>
    public enum EventType
    {
        /// <summary>Email has been queued for sending</summary>
        Queued,
        /// <summary>Email has been sent successfully</summary>
        Sent,
        /// <summary>Delivery attempt was made</summary>
        Attempt,
        /// <summary>Temporary delivery failure (e.g., mailbox full)</summary>
        SoftBounce,
        /// <summary>Permanent delivery failure (e.g., invalid address)</summary>
        HardBounce,
        /// <summary>Email marked as spam by recipient</summary>
        Complaint,
        /// <summary>Recipient unsubscribed from emails</summary>
        Unsubscribe,
        /// <summary>Email was opened by recipient</summary>
        Open,
        /// <summary>Link in email was clicked by recipient</summary>
        Click
    }

    /// <summary>
    /// Suppression list types for managing blocked email addresses
    /// </summary>
    public enum SuppressionType
    {
        /// <summary>Bounced email addresses</summary>
        Bounce,
        /// <summary>Unsubscribed email addresses</summary>
        Unsubscribe,
        /// <summary>Complaint email addresses</summary>
        Complaint
    }

    /// <summary>
    /// Extension methods for enumerations
    /// </summary>
    public static class EnumExtensions
    {
        /// <summary>
        /// Convert AnonymizeOption enum to string value
        /// </summary>
        public static string ToApiString(this AnonymizeOption option)
        {
            return option switch
            {
                AnonymizeOption.None => "none",
                AnonymizeOption.All => "all",
                AnonymizeOption.Recipient => "recipient",
                AnonymizeOption.Subject => "subject",
                AnonymizeOption.Content => "content",
                _ => throw new ArgumentOutOfRangeException(nameof(option))
            };
        }

        /// <summary>
        /// Convert EventType enum to string value
        /// </summary>
        public static string ToApiString(this EventType eventType)
        {
            return eventType switch
            {
                EventType.Queued => "queued",
                EventType.Sent => "sent",
                EventType.Attempt => "attempt",
                EventType.SoftBounce => "soft_bounce",
                EventType.HardBounce => "hard_bounce",
                EventType.Complaint => "complaint",
                EventType.Unsubscribe => "unsubscribe",
                EventType.Open => "open",
                EventType.Click => "click",
                _ => throw new ArgumentOutOfRangeException(nameof(eventType))
            };
        }

        /// <summary>
        /// Convert SuppressionType enum to string value
        /// </summary>
        public static string ToApiString(this SuppressionType suppressionType)
        {
            return suppressionType switch
            {
                SuppressionType.Bounce => "bounce",
                SuppressionType.Unsubscribe => "unsubscribe",
                SuppressionType.Complaint => "complaint",
                _ => throw new ArgumentOutOfRangeException(nameof(suppressionType))
            };
        }
    }

    #endregion

    #region Converters

    /// <summary>
    /// Converts both boolean and integer (0/1) values to boolean
    /// Handles: true/false and 0/1
    /// </summary>
    public class BoolOrIntConverter : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.True:
                    return true;

                case JsonTokenType.False:
                    return false;

                case JsonTokenType.Number:
                    return reader.GetInt32() != 0;

                default:
                    throw new JsonException($"Cannot convert {reader.TokenType} to boolean.");
            }
        }

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        {
            writer.WriteBooleanValue(value);
        }
    }

    #endregion
}

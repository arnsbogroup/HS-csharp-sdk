using System;
using System.Linq;
using System.Threading.Tasks;
using Heysender.SDK;

namespace Heysender.SDK.Test
{
    /// <summary>
    /// Heysender C# SDK Usage Examples
    ///
    /// These examples demonstrate common use cases for the Heysender C# SDK
    /// </summary>
    class Examples
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("\n================================");
            Console.WriteLine("Heysender C# SDK Examples");
            Console.WriteLine("================================\n");

            var client = new HeysenderClient(
                Environment.GetEnvironmentVariable("HEYSENDER_API_KEY") ?? "your-api-key",
                Environment.GetEnvironmentVariable("HEYSENDER_API_SECRET") ?? "your-api-secret"
            );

            // Running all examples
            await SendMessageExample(client);
            await CreateDomainExample(client);
            await CreateSmtpUserExample(client);

            Console.WriteLine("================================");
            Console.WriteLine("All examples completed!");
            Console.WriteLine("================================\n");
        }

        static async Task SendMessageExample(HeysenderClient client)
        {
            Console.WriteLine("=== Example 1: Sending a Message ===\n");

            try
            {
                var message = new MessageBuilder(
                    fromEmail: "example@example.heysender.com",
                    fromName: "Your Company",
                    subject: "Welcome to Our Service",
                    html: "<h1>Welcome!</h1><p>Thanks for signing up. We're excited to have you on board.</p>"
                )
                .AddTo("example@heysender.com", "John Doe")
                .AddCc("example2@heysender.com", "Manager")
                .SetTracking(true)
                .AddTag("campaign", "welcome-series")
                .AddTag("segment", "new-users")
                .AddHeader("X-Campaign-ID", "welcome-001")
                .Build();

                var responses = await client.SendMessageAsync(message);

                foreach (var resp in responses)
                {
                    Console.WriteLine($"Status: {resp.Status}");
                    Console.WriteLine($"Message ID: {resp.MessageId}");
                    Console.WriteLine($"Recipient: {resp.Recipient}");
                }

                Console.WriteLine("Message sent\n");
            }
            catch (HeysenderException ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                if (ex.StatusCode.HasValue)
                {
                    Console.WriteLine($"Status Code: {ex.StatusCode}");
                }
                Console.WriteLine();
            }
        }

        static async Task CreateDomainExample(HeysenderClient client)
        {
            Console.WriteLine("=== Example 2: Creating a Domain and Getting All Domains ===\n");

            try
            {
                Console.WriteLine("Creating new domain...");
                var newDomainRequest = new CreateDomainRequest
                {
                    Url = $"example.heysender.com",
                    CustomSelector = "heysender"
                };

                var newDomain = await client.CreateDomainAsync(newDomainRequest);
                Console.WriteLine($"ID: {newDomain.Id}");
                Console.WriteLine($"Domain Url: {newDomain.Url}");
                Console.WriteLine($"DKIM Validated: {newDomain.Validated == true}\n");

                await client.DeleteDomainAsync(newDomain.Url);

                Console.WriteLine("Fetching all domains...");
                var domains = await client.GetDomainsAsync();

                foreach (var domain in domains.Take(5)) // Show first 5
                {
                    Console.WriteLine($"Domain: {domain.Url}");
                    Console.WriteLine($"ID: {domain.Id}");
                    Console.WriteLine($"Verified: {(domain.Validated == true)}\n");
                }
            }
            catch (HeysenderException ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                if (ex.StatusCode.HasValue)
                {
                    Console.WriteLine($"Status Code: {ex.StatusCode}");
                }
                Console.WriteLine();
            }
        }

        static async Task CreateSmtpUserExample(HeysenderClient client)
        {
            Console.WriteLine("=== Example 3: Create SMTP User, Get All SMTP Users ===\n");

            try
            {
                Console.WriteLine("Get domains to create smtp user for");
                var domains = await client.GetDomainsAsync();

                if (domains.Count == 0)
                {
                    Console.WriteLine("No domains found\n");
                    return;
                }

                var firstDomain = domains[0];

                Console.WriteLine("Create SMTP user");
                var smtpRequest = new CreateSmtpUserRequest
                {
                    SmtpEmail = $"example@{firstDomain.Url}"
                };
                smtpRequest.SetAnonymizeOptions(AnonymizeOption.Recipient, AnonymizeOption.Content);

                var smtpUser = await client.CreateSmtpUserAsync(firstDomain.Id, smtpRequest);

                Console.WriteLine($"SMTP user id: {smtpUser.Id}");
                Console.WriteLine($"Domain id: {smtpUser.DomainId}");
                Console.WriteLine($"Email: {smtpUser.SmtpEmail}");
                Console.WriteLine($"Password: {smtpUser.SmtpPassword}");
                Console.WriteLine($"Anonymize Options: {string.Join(", ", smtpUser.AnonymizeOptions)}\n");

                Console.WriteLine("Get all smtp users for domain");
                var allSmtpUsers = await client.GetSmtpUsersAsync(firstDomain.Id);

                foreach (var user in allSmtpUsers.Take(5))
                {
                    Console.WriteLine($"SMTP user Id: {user.Id}");
                    Console.WriteLine($"Email: {user.SmtpEmail}");
                    Console.WriteLine($"Anonymize Options: {string.Join(", ", user.AnonymizeOptions)}\n");
                }
            }
            catch (HeysenderException ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                if (ex.StatusCode.HasValue)
                {
                    Console.WriteLine($"Status Code: {ex.StatusCode}");
                }
            }
        }
    }
}

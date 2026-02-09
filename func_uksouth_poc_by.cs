using System;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace func_uksouth_poc_by;

public class func_uksouth_poc_by
{
    private readonly ILogger<func_uksouth_poc_by> _logger;

    public func_uksouth_poc_by(ILogger<func_uksouth_poc_by> logger)
    {
        _logger = logger;
    }

    [Function(nameof(func_uksouth_poc_by))]
    [ServiceBusOutput("sbq-uksouth-poc-by-outbound", Connection = "sbuklabs_SERVICEBUS")]
    public async Task<string> Run(
        [ServiceBusTrigger("sbq-uksouth-poc-by-inbound", Connection = "sbuklabs_SERVICEBUS")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        _logger.LogInformation("Message ID: {id}", message.MessageId);
        _logger.LogInformation("Message Body: {body}", message.Body);
        _logger.LogInformation("Message Content-Type: {contentType}", message.ContentType);
        
        // Parse JSON payload to extract parameters
        var body = message.Body.ToString();
        var payload = JsonSerializer.Deserialize<Dictionary<string, string>>(body) ?? [];

        // Extract parameters and build query string
        var query = string.Join(
            "&",
            payload.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}")
            );

        // Construct the full URL
        var baseUrl = Environment.GetEnvironmentVariable("ApiM_Backend_Url");
        var requestUrl = $"{baseUrl}?{query}";
        _logger.LogInformation("Request URL: {requestUrl}", requestUrl);

        // Get subscription key from environment variable
        var subscriptionKey = Environment.GetEnvironmentVariable("Ocp-Apim-Subscription-Key");

        // Make the HTTP GET request
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

        _logger.LogInformation("Making API request to: {requestUrl}", requestUrl);
        var response = await httpClient.GetAsync(requestUrl);
        response.EnsureSuccessStatusCode();
        var responseBody = await response.Content.ReadAsStringAsync();
        
        _logger.LogInformation("API Response: {responseBody}", responseBody);

        // Complete the message
        await messageActions.CompleteMessageAsync(message);

        // Return the response body to be sent to the output Service Bus queue
        return responseBody;
    }
}
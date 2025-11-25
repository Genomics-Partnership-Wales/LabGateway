using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LabResultsGateway.API;

public class FileProcessor
{
    private readonly ILogger<FileProcessor> _logger;

    public FileProcessor(ILogger<FileProcessor> logger)
    {
        _logger = logger;
    }

    [Function(nameof(FileProcessor))]
    public async Task Run([BlobTrigger("lab-results-gateway/{name}", Connection = "StorageConnection")] Stream stream, string name)
    {
        using var blobStreamReader = new StreamReader(stream);
        var content = await blobStreamReader.ReadToEndAsync();
        _logger.LogInformation("C# Blob trigger function Processed blob\n Name: {name} \n Data: {content}", name, content);
    }
}
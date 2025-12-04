using System.Threading.Tasks;
using LabResultsGateway.API.Domain.Entities;

namespace LabResultsGateway.API.Domain.Interfaces
{
    public interface IIdempotencyService
    {
        Task<bool> HasBeenProcessedAsync(string blobName, byte[] contentHash);
        Task MarkAsProcessedAsync(string blobName, byte[] contentHash, ProcessingOutcome outcome);
    }
}

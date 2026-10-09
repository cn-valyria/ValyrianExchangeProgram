using System.Threading.Tasks;
using Valyria.Core.DTOs;

namespace Valyria.Core.Interfaces;

public interface IMassMessagesRepository
{
    /// <summary>
    /// Get every mass message with today's recipients and whether it's been sent today
    /// </summary>
    Task<MassMessagesSummary> GetMassMessagesAsync();

    /// <summary>
    /// Mark a message as sent (or not sent) for today's CN date. Returns false if the message does not exist.
    /// </summary>
    Task<bool> SetMessageSentAsync(int messageId, bool sent, int sentByAccountId);
}

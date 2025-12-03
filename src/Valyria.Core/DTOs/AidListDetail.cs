using System.Collections.Generic;

namespace Valyria.Core.DTOs;

public class AidListDetail
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Subject { get; set; }
    public string Message { get; set; }
    public List<ListRecipientDetail> Recipients { get; set; }
    public DateTime? RecipientsLastUpdatedOn { get; set; }
    public DateTime? MessagesSentOn { get; set; }
    public SimplifiedAccount? MessagesSentBy { get; set; }
}

public class SimplifiedAccount
{
    public int AccountId { get; set; }
    public int NationId { get; set; }
    public string RulerName { get; set; }
    public string NationName { get; set; }
}
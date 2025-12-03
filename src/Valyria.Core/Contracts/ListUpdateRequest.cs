namespace Valyria.Core.Contracts;

public class ListUpdateRequest
{
    public DateTime? MessagesSentOn { get; set; }
    public int? MessagesSentByAccountId { get; set; }
}

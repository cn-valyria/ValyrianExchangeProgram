using System.Collections.Generic;

public class AidList
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Subject { get; set; }
    public string Message { get; set; }
    public List<ListRecipient> Recipients { get; set; }
}
using System;

namespace Valyria.Core.Entities;

public class DealActivity
{
    public int Id { get; set; }
    public int DealId { get; set; }
    public int? AidId { get; set; }
    public int ExpectedSenderNationId { get; set; }
    public int ExpectedReceiverNationId { get; set; }
    public bool AidSent { get; set; }
    public bool AidReceived { get; set; }
    public DateTime ActivityDate { get; set; }
    public DateTime CreatedOn { get; set; }
}

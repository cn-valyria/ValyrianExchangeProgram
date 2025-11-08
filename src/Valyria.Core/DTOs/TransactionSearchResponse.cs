using System.Collections.Generic;

namespace Valyria.Core.DTOs;

public class TransactionSearchResponse
{
    public (int TotalCount, IEnumerable<TransactionDetail> Results) AidBasedTransactions { get; set; }
    public (int TotalCount, IEnumerable<TransactionDetail> Results) ManualTransactions { get; set; }
}
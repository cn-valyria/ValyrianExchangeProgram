namespace Valyria.Core.DTOs;

public class AccountTransactionsResponse
{
    public DataCollection<TransactionDetail> CurrentPage { get; set; }
    public int TotalCreditTxns { get; set; }
    public int TotalDebtTxns { get; set; }
}
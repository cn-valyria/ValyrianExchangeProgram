namespace Valyria.Core.Contracts;

public class AccountTransactionSearchResults
{
    public TransactionCollection CurrentPage { get; set; }
    public int TotalCreditTxns { get; set; }
    public int TotalDebtTxns { get; set; }
}
using System;

namespace Valyria.Core.Entities;

public class TransactionCode
{
    public char? SendingRole { get; }
    public char? ReceivingRole { get; }

    public TransactionCode(char? sendingRole, char? receivingRole)
    {
        SendingRole = sendingRole;
        ReceivingRole = receivingRole;
    }
}
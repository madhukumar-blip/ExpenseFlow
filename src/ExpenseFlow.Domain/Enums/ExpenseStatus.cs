using System;
using System.Collections.Generic;
using System.Text;

namespace ExpenseFlow.Domain.Enums
{
    public enum ExpenseStatus
    {
        Draft = 1,
        Submitted = 2,
        Approved = 3,
        Rejected = 4,
        Reimbursed = 5
    }
}

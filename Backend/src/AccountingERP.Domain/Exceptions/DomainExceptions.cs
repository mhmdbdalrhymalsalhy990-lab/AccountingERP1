namespace AccountingERP.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

public class UnbalancedJournalEntryException : DomainException
{
    public UnbalancedJournalEntryException(decimal totalDebit, decimal totalCredit)
        : base($"Cannot post unbalanced journal entry. Total Debit: {totalDebit:F4}, Total Credit: {totalCredit:F4}.") { }
}

public class CannotModifyPostedEntryException : DomainException
{
    public CannotModifyPostedEntryException(string entryNumber)
        : base($"Posted journal entry '{entryNumber}' is immutable and cannot be modified or deleted. Create a reversal entry instead.") { }
}

public class EntryAlreadyReversedException : DomainException
{
    public EntryAlreadyReversedException(string entryNumber)
        : base($"Journal entry '{entryNumber}' has already been reversed and cannot be reversed again.") { }
}

public class ClosedFiscalPeriodException : DomainException
{
    public ClosedFiscalPeriodException(string periodName)
        : base($"Fiscal period '{periodName}' is closed or locked. No entries can be posted.") { }
}

public class HeaderAccountPostingException : DomainException
{
    public HeaderAccountPostingException(string accountCode)
        : base($"Account '{accountCode}' is a header/summary account (IsPosting = false) and cannot receive direct journal entries.") { }
}

public class NotFoundException : DomainException
{
    public NotFoundException(string entityName, object key)
        : base($"Entity '{entityName}' with key '{key}' was not found.") { }
}

public class BusinessRuleViolationException : DomainException
{
    public BusinessRuleViolationException(string message) : base(message) { }
}

public class UnauthorizedTenantAccessException : DomainException
{
    public UnauthorizedTenantAccessException(int companyId)
        : base($"User is not authorized to access company '{companyId}'.") { }
}

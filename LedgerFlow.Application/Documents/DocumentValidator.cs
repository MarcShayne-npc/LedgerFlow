using LedgerFlow.Application.Exceptions;
using LedgerFlow.Domain.Enums;

namespace LedgerFlow.Application.Documents;

public static class DocumentValidator
{
    public static void Validate(CreateFinancialDocumentRequest request, DocumentType resolvedType)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(request.BusinessPartnerName))
            errors.Add(new ValidationError("REQUIRED", nameof(request.BusinessPartnerName), "Business partner name is required."));

        if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Length != 3)
            errors.Add(new ValidationError("INVALID", nameof(request.Currency), "Currency must be a 3-letter ISO code."));

        if (request.Lines.Count == 0)
            errors.Add(new ValidationError("REQUIRED", nameof(request.Lines), "At least one line is required."));

        var lineNumbers = new HashSet<int>();
        for (var i = 0; i < request.Lines.Count; i++)
        {
            var line = request.Lines[i];
            var prefix = $"{nameof(request.Lines)}[{i}]";

            if (!lineNumbers.Add(line.LineNumber))
                errors.Add(new ValidationError("DUPLICATE", $"{prefix}.{nameof(line.LineNumber)}", $"Duplicate line number {line.LineNumber}."));

            if (string.IsNullOrWhiteSpace(line.Description))
                errors.Add(new ValidationError("REQUIRED", $"{prefix}.{nameof(line.Description)}", "Line description is required."));

            if (line.Quantity <= 0)
                errors.Add(new ValidationError("RULE", $"{prefix}.{nameof(line.Quantity)}", "Quantity must be greater than zero."));

            if (line.UnitPrice < 0)
                errors.Add(new ValidationError("RULE", $"{prefix}.{nameof(line.UnitPrice)}", "Unit price cannot be negative."));

            if (line.TaxRate < 0 || line.TaxRate > 100)
                errors.Add(new ValidationError("RULE", $"{prefix}.{nameof(line.TaxRate)}", "Tax rate must be between 0 and 100."));

            var net = line.Quantity * line.UnitPrice;
            var taxPart = Math.Round(net * (line.TaxRate / 100m), 2, MidpointRounding.AwayFromZero);
            var expectedLineTotal = Math.Round(net + taxPart, 2, MidpointRounding.AwayFromZero);
            if (Math.Abs(expectedLineTotal - line.LineTotal) > 0.02m)
                errors.Add(new ValidationError("TOTAL", $"{prefix}.{nameof(line.LineTotal)}", $"Line total must equal net + tax. Expected {expectedLineTotal}, got {line.LineTotal}."));
        }

        var expectedSubTotal = Math.Round(request.Lines.Sum(l => l.Quantity * l.UnitPrice), 2, MidpointRounding.AwayFromZero);
        if (Math.Abs(expectedSubTotal - request.SubTotal) > 0.05m)
            errors.Add(new ValidationError("TOTAL", nameof(request.SubTotal), $"SubTotal must equal sum of net amounts (qty × unit price). Expected {expectedSubTotal}, got {request.SubTotal}."));

        var expectedTax = request.Lines.Sum(l =>
        {
            var net = l.Quantity * l.UnitPrice;
            return Math.Round(net * (l.TaxRate / 100m), 2, MidpointRounding.AwayFromZero);
        });

        if (Math.Abs(expectedTax - request.TaxAmount) > 0.05m)
            errors.Add(new ValidationError("TOTAL", nameof(request.TaxAmount), $"Tax amount must match sum of per-line tax. Expected {expectedTax}, got {request.TaxAmount}."));

        var expectedTotal = Math.Round(request.Lines.Sum(l => l.LineTotal), 2, MidpointRounding.AwayFromZero);
        if (Math.Abs(expectedTotal - request.TotalAmount) > 0.05m)
            errors.Add(new ValidationError("TOTAL", nameof(request.TotalAmount), $"Total must equal sum of line totals. Expected {expectedTotal}, got {request.TotalAmount}."));

        if (Math.Abs(request.SubTotal + request.TaxAmount - request.TotalAmount) > 0.05m)
            errors.Add(new ValidationError("TOTAL", nameof(request.TotalAmount), "Total must approximately equal SubTotal + TaxAmount."));

        if (resolvedType == DocumentType.CreditMemo)
            errors.Add(new ValidationError("RULE", nameof(resolvedType), "Credit memos are created by the reversal engine, not via the public create API."));

        if (request.Status == DocumentStatus.Reversed)
            errors.Add(new ValidationError("RULE", nameof(request.Status), "Cannot create a document directly in Reversed status."));

        if (errors.Count > 0)
            throw new ValidationException(errors);
    }

    public static void EnsureStatusTransition(DocumentStatus current, DocumentStatus next)
    {
        var ok = (current, next) switch
        {
            (DocumentStatus.Open, DocumentStatus.Closed) => true,
            (DocumentStatus.Open, DocumentStatus.Cancelled) => true,
            (DocumentStatus.Closed, DocumentStatus.Cancelled) => true,
            _ => false
        };

        if (!ok)
            throw new ValidationException(new[]
            {
                new ValidationError("STATUS", nameof(next), $"Cannot transition from {current} to {next}.")
            });
    }
}

using LedgerFlow.Application.Documents;
using LedgerFlow.Application.Exceptions;
using LedgerFlow.Domain.Enums;

namespace LedgerFlow.UnitTests;

public sealed class DocumentValidatorTests
{
    [Fact]
    public void Validate_WhenLineTotalsMismatch_Throws()
    {
        var request = new CreateFinancialDocumentRequest
        {
            BusinessPartnerName = "Acme",
            Currency = "USD",
            SubTotal = 100m,
            TaxAmount = 10m,
            TotalAmount = 110m,
            Lines = new[]
            {
                new FinancialDocumentLineRequest
                {
                    LineNumber = 1,
                    Description = "Item",
                    Quantity = 1,
                    UnitPrice = 100m,
                    TaxRate = 10m,
                    LineTotal = 50m
                }
            }
        };

        var ex = Assert.Throws<ValidationException>(() =>
            DocumentValidator.Validate(request, DocumentType.SalesInvoice));

        Assert.Contains(ex.Errors, e => e.Code == "TOTAL");
    }

    [Fact]
    public void Validate_WhenValid_DoesNotThrow()
    {
        var request = new CreateFinancialDocumentRequest
        {
            BusinessPartnerName = "Acme",
            Currency = "USD",
            SubTotal = 100m,
            TaxAmount = 10m,
            TotalAmount = 110m,
            Lines = new[]
            {
                new FinancialDocumentLineRequest
                {
                    LineNumber = 1,
                    Description = "Item",
                    Quantity = 1,
                    UnitPrice = 100m,
                    TaxRate = 10m,
                    LineTotal = 110m
                }
            }
        };

        DocumentValidator.Validate(request, DocumentType.SalesInvoice);
    }
}

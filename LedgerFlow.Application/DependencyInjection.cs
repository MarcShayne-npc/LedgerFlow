using LedgerFlow.Application.Documents;
using LedgerFlow.Application.Reversals;
using LedgerFlow.Application.Reporting;
using Microsoft.Extensions.DependencyInjection;

namespace LedgerFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IReversalService, ReversalService>();
        services.AddScoped<IReportingService, ReportingService>();
        return services;
    }
}

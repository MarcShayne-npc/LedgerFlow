using Hangfire.Dashboard;
using LedgerFlow.Application.Common;
using Microsoft.AspNetCore.Hosting;

namespace LedgerFlow.Api.Hangfire;

public sealed class HangfireDashboardAuth(IWebHostEnvironment env) : IDashboardAuthorizationFilter
{
    private readonly IWebHostEnvironment _env = env;

    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        if (_env.IsDevelopment())
            return true;

        return http.User.IsInRole(Roles.Admin);
    }
}

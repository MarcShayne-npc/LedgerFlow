FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY LedgerFlow.Domain/LedgerFlow.Domain.csproj LedgerFlow.Domain/
COPY LedgerFlow.Application/LedgerFlow.Application.csproj LedgerFlow.Application/
COPY LedgerFlow.Infrastructure/LedgerFlow.Infrastructure.csproj LedgerFlow.Infrastructure/
COPY LedgerFlow.Api/LedgerFlow.Api.csproj LedgerFlow.Api/
RUN dotnet restore LedgerFlow.Api/LedgerFlow.Api.csproj
COPY . .
WORKDIR /src/LedgerFlow.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "LedgerFlow.Api.dll"]

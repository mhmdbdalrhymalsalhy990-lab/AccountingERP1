# Multi-stage Dockerfile for Accounting ERP Backend (.NET 10 Web API)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for caching restore layer
COPY ["src/AccountingERP.Domain/AccountingERP.Domain.csproj", "src/AccountingERP.Domain/"]
COPY ["src/AccountingERP.Application/AccountingERP.Application.csproj", "src/AccountingERP.Application/"]
COPY ["src/AccountingERP.Infrastructure/AccountingERP.Infrastructure.csproj", "src/AccountingERP.Infrastructure/"]
COPY ["src/AccountingERP.API/AccountingERP.API.csproj", "src/AccountingERP.API/"]

RUN dotnet restore "src/AccountingERP.API/AccountingERP.API.csproj"

# Copy full source and publish
COPY . .
WORKDIR "/src/src/AccountingERP.API"
RUN dotnet publish "AccountingERP.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime Image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 5000

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AccountingERP.API.dll"]

# Build context is the repository root (see docker-compose.yml), since the API builds with Core and Infrastructure.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from the project files only, so the package layer is cached until a dependency changes.
COPY global.json Directory.Build.props ./
COPY src/InvoiceProcessor.Core/InvoiceProcessor.Core.csproj src/InvoiceProcessor.Core/
COPY src/InvoiceProcessor.Infrastructure/InvoiceProcessor.Infrastructure.csproj src/InvoiceProcessor.Infrastructure/
COPY src/InvoiceProcessor.Api/InvoiceProcessor.Api.csproj src/InvoiceProcessor.Api/
RUN dotnet restore src/InvoiceProcessor.Api/InvoiceProcessor.Api.csproj

COPY src/ src/
RUN dotnet publish src/InvoiceProcessor.Api/InvoiceProcessor.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Mount points for the volumes, owned by the image's non-root user so the API can write to them.
RUN mkdir -p /app/uploads /app/keys && chown -R app:app /app/uploads /app/keys

COPY --from=build /app .

USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "InvoiceProcessor.Api.dll"]

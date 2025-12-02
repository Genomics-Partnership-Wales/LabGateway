# Azure Functions .NET 10 Isolated Dockerfile
# Uses the Azure Functions base image for proper Functions runtime support

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy Directory.Build.props and Directory.Packages.props for Central Package Management
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]

# Copy the API project and its dependencies
COPY ["src/API/LabResultsGateway.API.csproj", "src/API/"]
COPY ["src/ServiceDefaults/LabResultsGateway.ServiceDefaults.csproj", "src/ServiceDefaults/"]

# Restore dependencies
RUN dotnet restore "src/API/LabResultsGateway.API.csproj"

# Copy all source code
COPY . .

# Build the project
WORKDIR "/src/src/API"
RUN dotnet build "LabResultsGateway.API.csproj" -c Release -o /app/build

# Publish the project
FROM build AS publish
RUN dotnet publish "LabResultsGateway.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Use Azure Functions runtime image for .NET 8 isolated (closest stable to .NET 10)
# Note: Azure Functions doesn't have an official .NET 10 image yet
# For .NET 10 preview, we need to use the base image and install the runtime
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0 AS final
WORKDIR /home/site/wwwroot
COPY --from=publish /app/publish .

# Azure Functions configuration
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
    FUNCTIONS_WORKER_RUNTIME=dotnet-isolated

EXPOSE 80

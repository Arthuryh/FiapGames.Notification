# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/2-Notification.Infrastructure/2-Notification.Infrastructure.csproj", "src/2-Notification.Infrastructure/"]

RUN dotnet restore "src/2-Notification.Infrastructure/2-Notification.Infrastructure.csproj"

COPY . .

WORKDIR "/src/src/2-Notification.Infrastructure"

RUN dotnet publish "2-Notification.Infrastructure.csproj" \
    -c Release \
    -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "2-Notification.Infrastructure.dll"]

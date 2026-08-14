# Ephemeral database migration image. This image is not deployed as a service.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/2-Notification.Infrastructure/2-Notification.Infrastructure.csproj", "src/2-Notification.Infrastructure/"]
COPY ["src/1-Notification.Application/1-Notification.Application.csproj", "src/1-Notification.Application/"]
COPY ["src/3-Notificacao.Domain/3-Notificacao.Domain.csproj", "src/3-Notificacao.Domain/"]

RUN dotnet restore "src/2-Notification.Infrastructure/2-Notification.Infrastructure.csproj"

COPY . .

WORKDIR "/src/src/2-Notification.Infrastructure"

RUN dotnet publish "2-Notification.Infrastructure.csproj" \
    -c Release \
    -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "2-Notification.Infrastructure.dll"]

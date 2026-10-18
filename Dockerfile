# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution và các file .csproj để tối ưu cache layer
COPY Server_WFS.sln ./
COPY src/Server_WFS.Domain/Server_WFS.Domain.csproj src/Server_WFS.Domain/
COPY src/Server_WFS.Application/Server_WFS.Application.csproj src/Server_WFS.Application/
COPY src/Server_WFS.Infrastructure/Server_WFS.Infrastructure.csproj src/Server_WFS.Infrastructure/
COPY src/Server_WFS.WebApi/Server_WFS.WebApi.csproj src/Server_WFS.WebApi/

RUN dotnet restore

# Copy toàn bộ mã nguồn và build Release
COPY . .
WORKDIR /src/src/Server_WFS.WebApi
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime image tối ưu kích thước siêu nhẹ
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Server_WFS.WebApi.dll"]

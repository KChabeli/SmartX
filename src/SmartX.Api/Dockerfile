FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY SmartX.slnx ./
COPY src/SmartX.Domain/SmartX.Domain.csproj src/SmartX.Domain/
COPY src/SmartX.Api/SmartX.Api.csproj src/SmartX.Api/
RUN dotnet restore src/SmartX.Api/SmartX.Api.csproj
COPY src/ src/
RUN dotnet publish src/SmartX.Api/SmartX.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "SmartX.Api.dll"]

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Restaurant.Api/Restaurant.Api.csproj Restaurant.Api/
COPY Restaurant.Api/packages.lock.json Restaurant.Api/
RUN dotnet restore Restaurant.Api/Restaurant.Api.csproj --locked-mode
COPY . .
RUN dotnet publish Restaurant.Api/Restaurant.Api.csproj -c Release -o /app --no-restore

FROM build AS tests
RUN dotnet restore Restaurant.Tests/Restaurant.Tests.csproj --locked-mode
RUN dotnet test Restaurant.Tests/Restaurant.Tests.csproj -c Release --no-restore --logger "console;verbosity=normal"

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Restaurant.Api.dll"]

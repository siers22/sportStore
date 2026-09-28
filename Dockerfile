FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY SportStore/SportStore.csproj SportStore/
RUN dotnet restore SportStore/SportStore.csproj
COPY SportStore/ SportStore/
RUN dotnet publish SportStore/SportStore.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SportStore.dll"]

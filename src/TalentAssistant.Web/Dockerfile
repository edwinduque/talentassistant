FROM mcr.microsoft.com/dotnet/sdk:10.0.402 AS build
WORKDIR /src

COPY global.json ./
COPY src/TalentAssistant.Web/TalentAssistant.Web.csproj src/TalentAssistant.Web/

RUN dotnet restore src/TalentAssistant.Web/TalentAssistant.Web.csproj

COPY src/TalentAssistant.Web/ src/TalentAssistant.Web/

# Razor components enable framework static web assets during publish.
RUN dotnet restore src/TalentAssistant.Web/TalentAssistant.Web.csproj

RUN dotnet publish src/TalentAssistant.Web/TalentAssistant.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.13 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5102
EXPOSE 5102

ENTRYPOINT ["dotnet", "TalentAssistant.Web.dll"]

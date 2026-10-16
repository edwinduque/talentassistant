FROM mcr.microsoft.com/dotnet/sdk:10.0.402 AS build
WORKDIR /src

COPY global.json ./
COPY src/TalentAssistant.Domain/TalentAssistant.Domain.csproj src/TalentAssistant.Domain/
COPY src/TalentAssistant.Application/TalentAssistant.Application.csproj src/TalentAssistant.Application/
COPY src/TalentAssistant.Infrastructure/TalentAssistant.Infrastructure.csproj src/TalentAssistant.Infrastructure/
COPY src/TalentAssistant.Api/TalentAssistant.Api.csproj src/TalentAssistant.Api/

RUN dotnet restore src/TalentAssistant.Api/TalentAssistant.Api.csproj

COPY src/TalentAssistant.Domain/ src/TalentAssistant.Domain/
COPY src/TalentAssistant.Application/ src/TalentAssistant.Application/
COPY src/TalentAssistant.Infrastructure/ src/TalentAssistant.Infrastructure/
COPY src/TalentAssistant.Api/ src/TalentAssistant.Api/

RUN dotnet publish src/TalentAssistant.Api/TalentAssistant.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.13 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5101
EXPOSE 5101

ENTRYPOINT ["dotnet", "TalentAssistant.Api.dll"]

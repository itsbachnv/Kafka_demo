FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY EmailSenderService/EmailSenderService.csproj EmailSenderService/
RUN dotnet restore EmailSenderService/EmailSenderService.csproj
COPY EmailSenderService/ EmailSenderService/
RUN dotnet publish EmailSenderService/EmailSenderService.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "EmailSenderService.dll"]

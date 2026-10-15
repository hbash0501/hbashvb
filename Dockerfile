# بوابة بصيرة المركزية. تُبنى من جذر المستودع: docker build -t basira-web .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY AIDocumentArchiveVB ./AIDocumentArchiveVB
COPY src ./src
RUN dotnet publish src/Basira.Web/Basira.Web.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV BASIRA_DATA=/data ASPNETCORE_URLS=http://+:8080
VOLUME /data
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Basira.Web.dll"]

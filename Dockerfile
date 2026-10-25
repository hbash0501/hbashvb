# منصة بصيرة على خادم Linux: docker build -t basira . && docker run -p 8080:8080 -v basira-data:/data basira
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY AIDocumentArchiveVB ./AIDocumentArchiveVB
COPY src ./src
RUN dotnet publish src/Basira.Platform/Basira.Platform.csproj -c Release -o /app -p:DebugType=none

FROM mcr.microsoft.com/dotnet/aspnet:8.0
RUN apt-get update && apt-get install -y --no-install-recommends tesseract-ocr tesseract-ocr-ara tesseract-ocr-eng poppler-utils && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV BASIRA_DATA=/data BASIRA_NO_BROWSER=1 ASPNETCORE_URLS=http://+:8080
VOLUME /data
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Basira.dll"]

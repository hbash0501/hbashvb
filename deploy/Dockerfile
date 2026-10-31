# بناء صورة البوابة (لم تُختبر على Docker في بيئة التطوير؛ تعليمات التشغيل في docs/REMOTE.md §6.3).
# من جذر المستودع:   docker build -f deploy/Dockerfile -t ptz-gateway .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/Ptz.Core src/Ptz.Core
COPY src/Ptz.Presentation src/Ptz.Presentation
COPY src/Ptz.Gateway src/Ptz.Gateway
RUN dotnet publish src/Ptz.Gateway/Ptz.Gateway.vbproj -c Release -o /out --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .
# مستخدم غير جذري (صورة .NET 8 تعرّف المستخدم app) ومجلد بيانات منفصل
RUN mkdir -p /data && chown -R app:app /data
USER app
ENV PTZ_GATEWAY_CONFIG=/config/gateway.json \
    PTZ_MASTER_KEY_FILE=/secrets/master.key \
    DOTNET_EnableDiagnostics=0
VOLUME ["/data"]
EXPOSE 8443
ENTRYPOINT ["dotnet", "PtzGateway.dll"]
CMD ["serve"]

# syntax=docker/dockerfile:1
# Una sola receta para los 11 servicios: PROJECT elige cual se publica.
#   docker build --build-arg PROJECT=MLMConquerorGlobalEdition.SignupAPI -t signupapi .
# Ver docs/deployment/despliegue-aws-eks.md (Fase 0 y Fase 7).

# Misma banda de SDK que global.json (10.0.2xx); la etiqueta 10.0 trae otra banda y no arranca.
FROM mcr.microsoft.com/dotnet/sdk:10.0.203 AS build
ARG PROJECT
WORKDIR /src
COPY . .
# Restaura respetando packages.lock.json, pero sin modo bloqueado: los proyectos WebAssembly
# guardan en el lockfile paquetes implicitos atados al parche exacto del SDK (ILLink, WebAssembly
# Pack, HotReload), y el SDK de esta imagen no es el mismo parche que el de desarrollo.
RUN dotnet publish "${PROJECT}/${PROJECT}.csproj" -c Release -o /out -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
USER root
# tzdata: el negocio usa la hora del servidor (TZ=America/New_York en el despliegue).
RUN apt-get update \
 && apt-get install -y --no-install-recommends tzdata ca-certificates \
 && rm -rf /var/lib/apt/lists/*
# CA de RDS: sin ella, Encrypt=True contra RDS falla al validar el certificado del servidor.
ADD https://truststore.pki.rds.amazonaws.com/global/global-bundle.pem \
    /usr/local/share/ca-certificates/rds-global-bundle.crt
RUN chmod 644 /usr/local/share/ca-certificates/rds-global-bundle.crt \
 && update-ca-certificates
WORKDIR /app
COPY --from=build /out .
ARG PROJECT
ENV APP_DLL=${PROJECT}.dll \
    ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP_DLL\""]

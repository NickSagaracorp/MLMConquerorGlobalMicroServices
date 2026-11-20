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
# IAM Roles Anywhere: en un servidor Docker fuera de AWS, cada servicio obtiene credenciales
# temporales de su propio rol presentando su certificado (credential_process en AWS_CONFIG_FILE).
# En EKS y en la maquina del desarrollador no se usa. Version y SHA-256 de la pagina oficial:
# https://docs.aws.amazon.com/rolesanywhere/latest/userguide/credential-helper.html
ARG ROLES_ANYWHERE_VERSION=1.8.6
ARG ROLES_ANYWHERE_SHA256=3553fdc515e5682541a9ebdd37031ec7f461169aa13ccbb6fbd5b7259644113d
ADD https://rolesanywhere.amazonaws.com/releases/${ROLES_ANYWHERE_VERSION}/X86_64/Linux/Amzn2023/aws_signing_helper /usr/local/bin/aws_signing_helper
RUN echo "${ROLES_ANYWHERE_SHA256}  /usr/local/bin/aws_signing_helper" | sha256sum -c - \
 && chmod 755 /usr/local/bin/aws_signing_helper \
 && /usr/local/bin/aws_signing_helper version
WORKDIR /app
COPY --from=build /out .
# Carpetas donde se montan los volumenes (llaves, recibos, adjuntos, certificados), ya con el
# usuario de la app: un volumen nuevo de Docker hereda el dueno de la carpeta de la imagen.
RUN mkdir -p /keys /data/payout-receipts /app/wwwroot/uploads /app/wwwroot/certificates \
 && chown -R $APP_UID:$APP_UID /keys /data /app/wwwroot/uploads /app/wwwroot/certificates
ARG PROJECT
ENV APP_DLL=${PROJECT}.dll \
    ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP_DLL\""]

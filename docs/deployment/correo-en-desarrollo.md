# Correo en desarrollo (MailHog)

En desarrollo los correos no salen a internet: van a **MailHog**, un buzón de captura. Se ve todo lo
que mandaría SES —misma plantilla, idioma, variables y remitente— porque solo cambia el transporte
(`SmtpEmailSender`); el armado lo hace `SesEmailService` en los dos casos.

## Levantarlo

```bash
docker run -d --name mailhog --restart unless-stopped -p 1025:1025 -p 8025:8025 mailhog/mailhog
# si el contenedor ya existe:
docker start mailhog
```

- Bandeja: http://localhost:8025
- SMTP: `localhost:1025`

## Configuración

En el `appsettings.Development.json` (excluido de git) de **SignupAPI, AdminAPI y BizCenter**:

```json
"Notifications": {
  "Email": {
    "Provider": "Smtp",
    "Smtp": { "Host": "localhost", "Port": 1025 },
    "Ses": { "Region": "us-east-1", "FromAddress": "no-reply@mlmconqueror.local", "FromName": "MLM Conqueror" }
  }
}
```

`Ses:FromAddress`/`FromName` siguen siendo el remitente aunque el transporte sea SMTP.

En producción el proveedor es `Ses`. `Smtp` no autentica ni cifra: es solo para un buzón de captura.

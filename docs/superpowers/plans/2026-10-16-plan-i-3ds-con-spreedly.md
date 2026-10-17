# Plan I — Cobro real con tarjeta y 3D Secure (3DS2 Global de Spreedly)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que el alta pública (`Signup.razor` y `MemberJoin.razor`) cobre de verdad con tarjeta: el número y el CVV se teclean en el iFrame de Spreedly (nunca pasan por nuestros servidores), el servidor autentica la tarjeta con 3DS2 Global (un SCA provider de Spreedly) por el importe del pedido, exige *liability shift*, cobra con cualquier gateway del router (`GatewayRouter` + `GatewayChargeOrchestrator`) pasando el `sca_authentication_token`, y SOLO DESPUÉS de cobrar activa el alta por el mismo camino atómico que Entrax (`SignupOrderClaim` + `ISignupActivationService`). Sin 3DS no se cobra, nunca.

**Architecture:** El cliente HTTP de Spreedly (`ISpreedlyClient`, Basic auth con *environment key* + *access secret*, opciones, guarda de arranque, credenciales leídas de `ApiCredential` y filtradas por `Environment`, y un simulador `FakeSpreedlyClient` para Development) vive en `Repository/Services/Spreedly`, como Entrax en el Plan H, para que Billing, SignupAPI y mañana el BizCenter lo compartan. Billing deja de tener un stub cuando `Spreedly:Enabled`: `SpreedlyCardGatewayService` compra con `sca_authentication_token`, y el orquestador pasa el token del medio de pago y el procesador elegido, y ya no salta de gateway tras un resultado dudoso. SignupAPI es dueño del cobro del alta: `SignupCardCheckout` congela el plan de ruteo, reserva el pedido (`Processing`) y autentica; `SignupCardSettlement` es el ÚNICO camino que cobra y activa (le llegan la pantalla, el callback firmado de Spreedly, el barrido `signup-card-sweep` y el *Re-check* del admin) y siempre vuelve a consultar la autenticación a Spreedly. La pantalla usa un componente único, `SharedComponents.Lite/Payments/SpreedlyCardPayment.razor`, con su JS (`spreedlyCard.js`) cargado por JSInterop solo cuando hace falta.

**Tech Stack:** .NET 10, ASP.NET Core MVC, EF Core 10 (SQL Server), MediatR, Hangfire, Blazor WebAssembly (Signups.Client), Blazor Server (AdminWeb, Syncfusion), JS (iFrame v1 y `Spreedly.ThreeDS.Lifecycle`), xUnit + FluentAssertions + Moq + bUnit, EF InMemory. Rama: `plan-i-3ds-con-spreedly`, creada desde `master` cuando incluya el trabajo de `cerrar-tarjeta-en-el-alta` (Tarea 0).

**Reglas del repo que aplican en todo el plan:**

- Hora de servidor (`IDateTimeProvider.Now`) para negocio y persistencia. `UtcNow` solo donde lo exige un protocolo (aquí: ninguno; Spreedly no firma con hora).
- Componentes unificados: la pieza de pago con tarjeta se escribe una vez en `SharedComponents.Lite` (sin dependencias pesadas) y la usan `Signup.razor` y `MemberJoin.razor`; lo específico (cómo hablar con la API, qué hacer al pagar) entra por parámetro.
- Secretos: nunca en `appsettings.json` base. Las credenciales de Spreedly van cifradas en `ApiCredential` (las escribe AdminAPI). En Development con el simulador no hace falta ninguna.
- Comentarios y nombres de pruebas en español.
- Commits: mensaje en español, solo la descripción de lo que se hizo; **sin** líneas `Co-Authored-By` ni menciones de herramientas. `git add` con rutas explícitas.
- No tocar la matriz de paridad (`MWR Life → MLMConqueror SOW BizCenter y matriz de paridad.md`).
- La migración se crea, no se aplica a mano: la aplica SignupAPI al arrancar (`MigrateAsync`).
- No se compila la solución entera mientras otro agente compile: cada tarea compila solo sus proyectos.

---

## Riesgos

| # | Riesgo | Mitigación en este plan |
|---|---|---|
| R1 | **No hay sandbox de Spreedly.** Los nombres de los campos de 3DS2 Global (`sca_authentication.token`, `eci`, `trans_status`, `authentication_value`, `flow_performed`, `message_version`) y la forma del Lifecycle salen de la documentación pública que conozco y del legado (`Join.cshtml`, `TokenPurchase3DS2.cshtml`, `Spreedly.cs`), no de una llamada real. | El lector es defensivo (acepta alias: `trans_status`/`transaction_status`, `three_ds_version`/`message_version`) y **falla cerrado**: si falta el ECI o el estado, no hay *liability shift* y no se cobra. Todo se construye contra el simulador (Tarea 5). Condición de despliegue: la prueba real de §11 con credenciales, con importe mínimo y aprobación de Nick, antes de encender `Signup:CardEnabled` fuera de Development. |
| R2 | **Cómo firma Spreedly sus callbacks.** El encargo apunta a una cabecera `X-Spreedly-Signature`. Lo que yo conozco es distinto: cada transacción del aviso lleva un bloque `signed` con `signature`, `fields` y `algorithm` (`sha1`), y la firma es HMAC-SHA1 con el *signing secret* del entorno sobre los valores de los campos listados unidos con `|`. Tampoco tengo claro si el cuerpo llega en XML o en JSON. **No está confirmado.** | El callback **no decide nada**: solo dice qué transacción mirar, y `SignupCardSettlement` vuelve a preguntar a Spreedly con nuestras credenciales. Se aceptan XML y JSON, la firma se compara en tiempo constante y sin firma válida es `401`. Si la forma real fuera otra, los callbacks se rechazan con 401 pero **ninguna alta se pierde**: la pantalla y el barrido liquidan por `GET /v1/transactions/{token}`. Verificar en la prueba real (§11). |
| R3 | **Doble cobro por un resultado dudoso.** El orquestador saltaba al siguiente gateway ante CUALQUIER fallo, también un timeout en el que el emisor pudo cargar. | El orquestador para ante un fallo que no esté en `CardChargeOutcomes.ExplicitNoChargeCodes` (Tarea 10). Lo dudoso deja la fila en `Charging` y se concilia (I11): primero por `PaymentHistory`, después por el listado de transacciones del medio de pago en Spreedly (`GET /v1/payment_methods/{token}/transactions.json`, buscando nuestro `order_id`). **Ese endpoint y su forma están por verificar** (R1); si no sirve, todo lo dudoso acaba en `ToReview` pasada la gracia, nunca en "no cobrado". |
| R4 | **PCI — prioridad ALTA, fuera de este plan.** `BizCenter/Features/Billing/AddCreditCard/AddCreditCardHandler.cs` recibe PAN y CVV en el servidor y guarda `EncryptedPan` y `EncryptedCvv`. Guardar el CVV tras la autorización está prohibido por PCI DSS (req. 3.3.1.2) y recibir el PAN mete al BizCenter en alcance. | No se reutiliza nada de ese camino. Propuesta: **Plan L** (aparte, siguiente a este) que migra el alta de tarjeta del BizCenter al mismo `SpreedlyCardPayment.razor`, borra `EncryptedCvv` (columna y datos) y `EncryptedPan`, y retira `SimulatedCardTokenizationService`. |
| R5 | **Recurrente con tarjeta.** Con `Spreedly:Enabled` el gateway exige `sca_authentication_token` (I5), así que un cobro sin cliente presente (renovación, `ChargeHandler`, `DelayedFallbackChargeJob`) falla cerrado con `SPREEDLY_SCA_REQUIRED`. Apagado sigue el stub de hoy, que devuelve un éxito simulado (y ya renueva sin cobrar). | `Spreedly:Enabled` es **por servicio** (I4): se enciende en SignupAPI; en Billing se queda apagado hasta el plan de recurrente MIT con `network_transaction_id`. Condición de despliegue escrita en §11. El arreglo del mapeo `NetworkTransactionId = GatewayToken` (Tarea 10) deja de mandar como NTID algo que no lo es. |
| R6 | **Data Protection en SignupAPI.** Para descifrar `ApiCredential`, SignupAPI pasa a llamar a `AddGatewayCredentialProtection` (Tarea 6), que fija el nombre de aplicación y guarda las llaves en `DataProtectionKeys` con el certificado compartido. También cambia las llaves de los tokens de Identity de SignupAPI (confirmación de correo y recuperación de contraseña). | Los enlaces de correo emitidos antes del despliegue (viven 15 min) dejan de valer: aceptable y anotado. Condición de despliegue: SignupAPI necesita el MISMO certificado que AdminAPI y Billing (`DataProtection:Certificate:*`); sin él no arranca (lo dice `GatewayCredentialProtectionExtensions`). |
| R7 | **Moneda.** El router puede presentar el cobro en otra moneda (`CurrencyPolicy`) y un fallback puede forzar USD. Autenticar en una moneda y cobrar en otra rompe el *liability shift*. | Se autentica con la moneda e importe del paso primario y solo se usan pasos inmediatos con **la misma moneda y el mismo importe** (I7). El plan se congela en la fila. Pregunta abierta P3. |
| R8 | **Intentos "attempted" (`trans_status = A`).** Dan *liability shift* según las reglas de Visa (ECI 06) y Mastercard (ECI 01), pero no autentican al titular. | Se aceptan por defecto (I8) porque Nick pidió "liability shift", no "autenticación completa". Si Nick prefiere solo `Y`, es una línea en `ThreeDsLiabilityPolicy`. A confirmar. |
| R9 | EF InMemory no aplica índices únicos, `RowVersion` ni `ExecuteUpdate`: las pruebas no demuestran la carrera real (pantalla + callback + barrido cobrando a la vez). | Los reclamos (`Authenticating → Charging`, `Charged → Paid`) van con `UPDATE` condicional en SQL Server y con lectura previa en InMemory, como en el Plan H. La carrera se verifica contra SQL Server en la Tarea 29 (varios `finish` y el barrido a la vez: un solo cobro). |
| R10 | El aspirante abandona el challenge (cierra la pestaña): la fila queda `Authenticating` y el pedido en `Processing`. | Botón *Cancel* (endpoint `cancel`, solo `Authenticating → Failed`, condicional) y barrido que cierra como `AUTH_ABANDONED` lo que lleve más de `Spreedly:AuthenticationTimeoutMinutes` (20). Mientras tanto, `complete` devuelve `409 CARD_IN_PROGRESS` con un texto claro. |
| R11 | Endpoints anónimos nuevos (`complete`, `finish`, `status`, `cancel`, `callback`). | Los de la pantalla exigen el id del alta **y** el id de nuestro intento (GUID aleatorio), devuelven solo estado y los datos del Lifecycle (formularios de 3DS, que no son secretos), tienen regla de rate limit propia y nunca devuelven detalles de una excepción. Ninguno cobra por lo que diga el navegador: el cobro lo decide `GET /v1/transactions/{token}` a Spreedly. |
| R12 | **CSP.** El iFrame carga `https://core.spreedly.com/iframe/iframe-v1.min.js` y el challenge abre iframes del ACS de cada banco (dominios arbitrarios). El host `Signups` hoy no emite CSP; `BizCenterWeb` sí y no permite Spreedly. | Este plan no añade CSP a Signups (I21). §11 documenta las directivas necesarias (`script-src`, `frame-src`, `connect-src` con `core.spreedly.com` y `frame-src https:` para los ACS) para quien la añada, y el Plan L las pondrá en `BizCenterWeb` cuando el componente llegue allí. |
| R13 | La *environment key* de Spreedly es pública (va al navegador) pero vive cifrada en `ApiCredential`. | `payment-options` la sirve a quien carga el alta (I18). No es un secreto: el *access secret* y el *signing secret* nunca salen del servidor ni del log. |
| R14 | El simulador no ejercita el iFrame real ni el Lifecycle real. | El JS se mantiene mínimo y sin lógica de negocio (Tarea 21); bUnit cubre el componente en modo simulado y que en modo real importe el módulo e inicialice con la clave y los ids correctos (Tarea 22). El JS real solo se prueba con credenciales (§11). |
| R15 | Spreedly cobra en **unidades menores** (centavos). Una moneda sin decimales (JPY) o con tres (KWD) multiplicaría por 100 o se quedaría corta. | `SpreedlyCurrency.ToMinorUnits` con la tabla de exponentes ISO 4217 de las monedas que no son de 2 decimales (Tarea 2). |
| R16 | Rate limit general de SignupAPI (60/min por IP) y Spreedly llamando desde pocas IP. | Reglas propias para los endpoints de la pantalla (Tarea 20). Condición de despliegue: IP de salida de Spreedly en `IpRateLimiting:IpWhitelist` si se usa el callback. |

## Decisiones

**De Nick (2026-10-16, vinculantes):** (1) modelo 3DS2 Global: `POST /v1/sca/providers/{key}/authenticate` y después `purchase` con `sca_authentication_token` en el gateway que elija el router; (2) nunca se cobra sin 3DS ni sin *liability shift*, ni hay rebaja a cobro sin 3DS; (3) simulador de Spreedly solo en Development, con variable de entorno, prohibido fuera, con dominio `.invalid` y endpoints dev; (4) primero se cobra y después se activa; (5) el PAN no pasa por nuestros servidores: iFrame de Spreedly por JSInterop; (6) `Signup:CardEnabled` ya existe (rama `cerrar-tarjeta-en-el-alta`) y este plan lo enciende al final, solo en Development con el simulador; (7) componentes unificados en `SharedComponents.Lite`; (8) hora de servidor; (9) secretos cifrados en `ApiCredential`.

**Tomadas aquí (con su porqué):**

| # | Decisión | Por qué |
|---|---|---|
| I1 | El cliente de Spreedly (`ISpreedlyClient`, `SpreedlyClient`, `FakeSpreedlyClient`, `SpreedlyOptions`, guarda, credenciales, firma, monedas, marcas, política de *liability shift* y `AddSpreedly`) va en `Repository/Services/Spreedly`. El cobro del **alta** va en SignupAPI. | Mismo criterio que H1: Billing (gateway), SignupAPI (alta) y el BizCenter (Plan L) lo necesitan, y el BizCenter no referencia Billing. |
| I2 | Credenciales en `ApiCredential`, fila `ServiceKey = "Spreedly"` del entorno `Spreedly:CredentialEnvironment` (`Production` por defecto): `ApiKeyEncrypted` = *environment key*, `SecretKeyEncrypted` = *access secret*, `MerchantIdEncrypted` = *SCA provider key*, `AdditionalSecretEncrypted` = *signing secret*. Los *gateway tokens*: una fila por `CardProcessor` **del mismo entorno** con `SpreedlyGatewayTokenEncrypted`. Siempre `IsActive` y no borradas. | El stub buscaba `"Spreedly"` sin filtrar por entorno y solo leía la *environment key*: con fila de `Sandbox` y de `Production` cogía una cualquiera, y faltaba el *access secret* para autenticarse. |
| I3 | Se descifran con `GatewayCredentialProtector` **concreto**, no con `IEncryptionService`. | En SignupAPI y en el BizCenter `IEncryptionService` es otro cifrado (el suyo, `EncryptionService`), y `AddGatewayCredentialProtection` lo registra con `TryAdd`: pedir la interfaz daría el cifrado equivocado. |
| I4 | `Spreedly:Enabled` es por servicio (cada host lee su `appsettings`). Este plan lo enciende en SignupAPI (en Development con el simulador); Billing se queda apagado. | El recurrente necesita MIT con `network_transaction_id` (fuera de alcance, R5); el alta no espera a eso. |
| I5 | Con Spreedly encendido, **sin `ScaAuthenticationToken` no hay cobro real** (`SPREEDLY_SCA_REQUIRED`, en la lista de "no se movió dinero"). Con Spreedly apagado, `SpreedlyCardGatewayService` conserva exactamente el stub de hoy. | Decisión 2 de Nick. Mantener el stub apagado evita cambiar hoy, sin avisar, lo que hace el recurrente en producción (R5). |
| I6 | El orquestador solo pasa al siguiente gateway si el fallo prueba que no se movió dinero (`CardChargeOutcomes.IsExplicitNoCharge`). Se añaden `DECLINED`, `SPREEDLY_DECLINED` y `SPREEDLY_SCA_REQUIRED` a esa lista. Con `ScaAuthenticationToken` (cliente presente) no programa pasos diferidos. | R3. `DECLINED` es lo que ya devuelven los gateways de prueba y las pruebas del orquestador al rechazar. Un cobro con 3DS no se puede reintentar una hora después: la autenticación es de este momento. |
| I7 | El plan de ruteo se resuelve **antes** de autenticar y se congela en la fila (`RouteBucketKey`, `PlannedProcessors`, `ChargeAmount`, `ChargeCurrency`). Se autentica por la moneda e importe del paso primario y solo se usan los pasos inmediatos con la misma moneda e importe, en su orden. | R7: lo autenticado y lo cobrado tienen que ser lo mismo. Volver a resolver el plan al cobrar podría elegir otro primario (los contadores de reparto se mueven). |
| I8 | *Liability shift* (`ThreeDsLiabilityPolicy`): con `trans_status = Y`, ECI `05` (Visa, Amex, JCB, Bancontact y otras) o `02` (Mastercard, Maestro); con `A`, ECI `06` u `01`. Cualquier otro caso (`N`, `R`, `U`, `I`, `C`, ECI `07`/`00`, ECI que falte) **no cobra**. Sin `trans_status`, decide el ECI. | Decisión 2 de Nick con las tablas de ECI de las marcas. R8. |
| I9 | El CAVV (`authentication_value`) **no se guarda**: solo `AuthenticationValuePresent`. Sí se guardan ECI, `trans_status`, versión y flujo. | Es un criptograma de un solo uso: no aporta nada a soporte y es dato sensible de autenticación. |
| I10 | Estados del intento: `Authenticating → Charging → Charged → Paid`, más `Failed` (no se movió dinero) y `ToReview` (se movió o pudo moverse y no se aplicó). `Authenticating → Charging` y `Charged → Paid` se reclaman con `UPDATE` condicional: solo un camino cobra y solo uno activa. `Charged` se guarda **antes** de activar. | Decisión 4 de Nick. Si el proceso cae entre cobrar y activar, la fila `Charged` la recoge el barrido y activa; nunca se cobra dos veces. |
| I11 | Conciliación de `Charging` (el cobro empezó y no consta su final): (1) un `PaymentHistory` capturado del pedido desde `ChargingStartedAt` → `Charged`; (2) si no, la compra con nuestro `order_id` en el listado de transacciones del medio de pago: `succeeded` → `Charged`; `failed`/`gateway_processing_failed` → `Failed`; (3) pasada `Spreedly:ChargeUnknownGraceMinutes` (30) sin respuesta clara → `ToReview CHARGE_OUTCOME_UNKNOWN` con `Critical`, y el pedido vuelve a `Pending` si nada más lo espera (como H22). | R3. Nunca se da por "no cobrado" lo que no se sabe. |
| I12 | Al pagar con tarjeta **no** se devuelven tokens de sesión: la pantalla dice "pago recibido, inicia sesión" (como H15). | Los endpoints de la pantalla son anónimos; entregar una sesión a quien tenga dos ids no compensa ahorrar un inicio de sesión. Cambia lo que hacía la tarjeta simulada (que sí devolvía tokens). A confirmar por Nick. |
| I13 | `ExternalOrderId = "SIGNUP-CARD-{orderId}-{n}"` (índice único); es el `order_id` que ve Spreedly en la autenticación y en la compra. | Trazable en la consola de Spreedly y es la llave de la conciliación (I11). |
| I14 | Al activar se guarda la tarjeta (`MemberCreditCard`) con `SpreedlyPaymentMethodToken`, marca, BIN, últimos 4 y caducidad **tal como los devuelve Spreedly** (`GET /v1/payment_methods/{token}`), `EncryptedPan = ""`, `EncryptedCvv = null`, `Gateway = "spreedly"`. La compra va con `retain_on_success = true`. | Nada de lo que describe la tarjeta sale del navegador. El token retenido es el que usará el recurrente MIT. |
| I15 | `CompleteSignupRequest.Card` (`SignupCardPaymentRequestDto`: token del medio de pago y `browser_info`) sustituye a `CreditCard`. `CreditCardInfoDto` y su validador se quedan sin uso en la petición, pero no se borran (los vigilan pruebas de paridad del BizCenter); se borran en el Plan L. | El alta deja de aceptar marca, BIN o tokens inventados por el cliente. |
| I16 | `POST …/card/{ref}/cancel` (anónimo, con los dos ids) solo pasa `Authenticating → Failed` (`CANCELLED`) de forma condicional, y devuelve el pedido a `Pending`. | R10. En `Authenticating` no se ha movido dinero; si otro camino ya lo reclamó para cobrar, el `UPDATE` no afecta a nada y se responde con el estado real. |
| I17 | Simulador: `Spreedly:UseFake` (solo Development; fuera no arranca) registra `FakeSpreedlyClient` y `FakeSpreedlyCredentialSource` (no hacen falta filas en `ApiCredential`). Los tokens de prueba son `fake_{escenario}_{guid}` con escenarios `frictionless`, `challenge`, `failed`, `noshift`, `declined`, `unknown`. URLs en `https://spreedly.invalid`. Endpoints dev: `POST /api/v1/dev/spreedly/{transactionToken}/{approve|reject}` y `POST /api/v1/dev/spreedly/sweep`. El componente, en modo simulado, no carga JS ni iframe: enseña un selector de escenario. | Decisión 3 de Nick. Así no se teclea ningún número de tarjeta en Development. |
| I18 | `payment-options`: `CardEnabled = Signup:CardEnabled && Spreedly listo` (encendido y con credenciales del entorno, o el simulador), más `CardEnvironmentKey` y `CardSimulated`. | La pantalla no enseña una pestaña que el servidor rechazaría. El servidor vuelve a comprobarlo al completar. |
| I19 | Guarda de arranque de SignupAPI: fuera de Development, `Signup:CardEnabled = true` sin `Spreedly:Enabled`, `UseFake`, `CallbackUrl` que no sea https absoluta o `CredentialEnvironment` desconocido → no arranca. En Development solo avisa. | Mismo patrón que `EntraxOptionsGuard`. Las credenciales están en la base y no se comprueban al arrancar: sin ellas el alta responde `503 CARD_NOT_CONFIGURED` y `payment-options` esconde la pestaña. |
| I20 | Textos de la UI por parámetro, en inglés por defecto. | Igual que H13. |
| I21 | No se añade CSP al host `Signups` en este plan. | R12: añadir una CSP al alta es un cambio con impacto propio en Blazor WASM y no lo pide el cobro. Se documenta qué necesita. |
| I22 | El reenvío del *Re-check* de AdminAPI a SignupAPI (Bearer, tope, auditoría) se extrae a `SignupApiRecheckForwarder` y lo usan Entrax y tarjeta. | Una sola versión del reenvío auditado. Sin cambiar el constructor de `AdminEntraxSignupsController` (sus pruebas siguen igual). |
| I23 | `AddCardCharging()` en Billing registra router, reparto, conversión, marcas, gateway de tarjeta y orquestador una sola vez; lo usan Billing y SignupAPI. | SignupAPI no tenía nada de esto registrado y copiar diez líneas en dos `Program.cs` es cómo divergen. |
| I24 | Log `SpreedlyApiCalls` con campos estructurados (operación, token de transacción, `order_id`, HTTP, desenlace, código, estado del proveedor, duración). **Sin cuerpos**, sin `browser_info`, sin CAVV. | Igual que H6. El PAN nunca lo tenemos; lo demás tampoco hace falta. |
| I25 | Barrido `signup-card-sweep` cada 2 min, cola `signups`, aunque `Spreedly:Enabled` sea `false`. | Igual que H16: apagar no puede dejar cobros a medias sin cerrar. |

## Fuera de alcance

- **Compra de tokens con tarjeta** (Plan F): sigue apagada (`Billing:TokenPurchase:CardEnabled = false`). Podrá reutilizar el componente y el cliente.
- **Recurrente con tarjeta (CIT/MIT con `network_transaction_id`)**: el arreglo del mapeo erróneo sí entra (Tarea 10); el flujo MIT no.
- **Upgrade de membresía** (Plan G).
- **Plan L (propuesto, prioridad alta por PCI):** migrar `AddCreditCardHandler` del BizCenter al iFrame (`SpreedlyCardPayment.razor`), borrar `EncryptedCvv` y `EncryptedPan` (columna y datos), retirar `SimulatedCardTokenizationService`, `CardPayment.PrepareAsync`, `CreditCardPaymentInfo` y `CreditCardInfoDto`, y poner la CSP de `BizCenterWeb` para Spreedly.
- **Devoluciones automáticas** de `ToReview`: se hacen a mano en la consola de Spreedly. El cliente ya trae `VoidAsync` y `CreditAsync` para el plan que las automatice.
- **CSP del host `Signups`** (I21).
- **Localización del alta** (I20).

## Preguntas abiertas para Nick

No bloquean el plan: cada una tiene un valor por defecto que se puede cambiar sin rehacer nada.

- **P1. Gateways vivos en Spreedly y mapeo `CardProcessor` → *gateway token*.** ¿Cuáles de `NmiSpreedly`, `NmiDirect`, `CheckoutEUR`, `CheckoutUS`, `CheckoutUsLlc`, `Shift4`, `StripeEms` siguen dados de alta en la consola de Spreedly y con qué *gateway token* en cada entorno? Por defecto: ninguno; el admin los carga en *API Credentials* (Tarea 27) y sin token ese paso del plan falla como "sin cobro" y se prueba el siguiente.
- **P2. SCA providers contratados.** ¿Qué *SCA provider key* (3DS2 Global) hay en `Production` y si hay uno de pruebas? Por defecto: uno por entorno en `MerchantIdEncrypted` de la fila `Spreedly`.
- **P3. Moneda del importe autenticado.** ¿Se autentica y cobra siempre en USD, o en la moneda de presentación del router (`CurrencyPolicy`)? Por defecto: la del paso primario del router, y fallback solo a pasos con la misma moneda e importe (I7).
- **P4. Acceso a la consola de Spreedly.** ¿Quién puede ver y rotar el *signing secret* y el *access secret*? Hace falta para cargar la fila `Spreedly` y para la prueba real.
- **P5. Intentos "attempted".** ¿Se aceptan (`trans_status = A`, ECI 06/01) o solo autenticación completa (`Y`)? Por defecto: se aceptan (I8).
- **P6. Sesión tras pagar con tarjeta.** ¿Basta "pago recibido, inicia sesión" (I12) o se quiere entrar directamente?

## Mapa de archivos

| Archivo | Responsabilidad |
|---|---|
| `Repository/Services/Spreedly/SpreedlyOptions.cs`, `SpreedlyOptionsGuard.cs` (nuevos) | Configuración y guarda de arranque. |
| `Repository/Services/Spreedly/SpreedlyProviderContracts.cs`, `SpreedlyCurrency.cs`, `SpreedlySignature.cs`, `SpreedlyCardBrands.cs`, `ThreeDsLiabilityPolicy.cs` (nuevos) | Forma del proveedor, unidades menores, firma de callbacks, marcas y regla de *liability shift*. |
| `Repository/Services/Spreedly/SpreedlyCredentialSource.cs`, `FakeSpreedlyCredentialSource.cs` (nuevos) | Credenciales desde `ApiCredential` filtradas por entorno, y las del simulador. |
| `Repository/Services/Spreedly/SpreedlyClient.cs`, `FakeSpreedlyClient.cs`, `SpreedlyCallLog.cs`, `SpreedlyServiceCollectionExtensions.cs` (nuevos) | Cliente HTTP, simulador, fila de log y registro. |
| `SharedKernel/Billing/SpreedlyCardContracts.cs` (nuevo), `SharedKernel/Billing/EntraxContracts.cs` | DTOs del pago con tarjeta para API y pantalla; `payment-options` con la clave de entorno. |
| `Domain/Entities/Orders/SignupCardPaymentStatus.cs`, `SignupCardPayment.cs`, `Domain/Entities/Billing/SpreedlyApiCall.cs` (nuevos) | Dominio. |
| `Repository/Configurations/SignupCardPaymentConfiguration.cs`, `Repository/Configurations/Billing/SpreedlyApiCallConfiguration.cs` (nuevos), `Repository/Context/AppDbContext.cs`, `Repository/Migrations/*_SpreedlyTarjetaEnElAlta.cs` | Esquema. |
| `Billing/Services/CardGateway/ICardGatewayService.cs`, `SpreedlyCardGatewayService.cs` | Compra real con `sca_authentication_token` cuando Spreedly está encendido. |
| `Billing/Services/Tokens/CardChargeOutcomes.cs`, `Billing/Services/Routing/IGatewayChargeOrchestrator.cs`, `GatewayChargeOrchestrator.cs`, `Billing/Jobs/DelayedFallbackChargeJob.cs`, `Billing/Services/Recurring/RecurringBillingProcessor.cs` | Tokens y procesador hasta el gateway; sin salto tras fallo dudoso; NTID bien mapeado. |
| `Billing/Services/CardGateway/CardChargingServiceCollectionExtensions.cs` (nuevo), `Billing/Program.cs`, `Billing/appsettings.json` | Registro único del cobro con tarjeta. |
| `SignupAPI/Services/Card/SignupCardErrors.cs`, `SignupCardRouting.cs`, `SignupCardCheckout.cs`, `SignupCardSettlement.cs`, `SignupCardStatusService.cs`, `SignupCardOptions.cs`, `SpreedlyCallbackProcessor.cs` (nuevos) | Lógica del alta con tarjeta. |
| `SignupAPI/DTOs/CompleteSignupRequest.cs`, `DTOs/Validators/CompleteSignupRequestValidator.cs`, `DTOs/SignupResponse.cs`, `Features/Signups/Commands/CompleteSignup/CompleteSignupHandler.cs`, `Controllers/SignupsController.cs` | Rama de tarjeta en el alta y códigos HTTP. |
| `SignupAPI/Controllers/SignupCardController.cs` (nuevo), `Controllers/EntraxSignupsController.cs` | Pantalla, callback, *Re-check*; `payment-options` con la tarjeta. |
| `SignupAPI/Jobs/SignupCardSweepJob.cs` (nuevo), `SignupAPI/Program.cs`, `SignupAPI/appsettings.json` | Barrido, DI, endpoints dev, configuración sin secretos, rate limit. |
| `SharedComponents.Lite/wwwroot/js/spreedlyCard.js`, `SharedComponents.Lite/Payments/SpreedlyCardPayment.razor` (nuevos) | Componente único del pago con tarjeta y su JS. |
| `Signups.Client/Payments/SpreedlySignupApi.cs`, `SpreedlySignupFlow.razor` (nuevos), `Signups.Client/Pages/Signup.razor`, `Pages/MemberJoin.razor`, `Signups.Client/Program.cs` | Las llamadas del alta con tarjeta (una vez) y la pestaña sin campos de PAN. |
| `AdminAPI/Services/SignupApiRecheckForwarder.cs`, `AdminAPI/Controllers/AdminSignupCardPaymentsController.cs` (nuevos), `AdminAPI/Controllers/AdminEntraxSignupsController.cs`, `AdminAPI/Controllers/AdminBillingGatewayController.cs` | Lista, *Re-check* y *gateway token* en las credenciales. |
| `AdminWeb/Components/Pages/AdminSignupCardPayments.razor` (nuevo), `AdminWeb/Components/Pages/AdminBillingCredentials.razor`, `SharedComponents/Components/Layout/AdminSidebarMenu.razor` | Pantallas de admin. |
| `docs/deployment/pruebas-de-seguridad.md` | §11 Tarjeta con 3DS en el alta. |
| `Signups.Tests/Spreedly/*`, `Signups.Tests/Card/*` (nuevos), `Billing.Tests/Services/*`, `BizCenter.Tests/SpreedlyCardPaymentTests.cs` (nuevo), `AdminAPI.Tests/Features/CardSignups/*` (nuevos) | Pruebas. |

Rutas abreviadas: todos los proyectos llevan el prefijo `MLMConquerorGlobalEdition.` (p. ej. `SignupAPI/…` = `MLMConquerorGlobalEdition.SignupAPI/…`). En los bloques de archivos de cada tarea van completas.

---

## Fase 0 — Prerrequisitos

### Task 0: Rama y trabajo previo de `cerrar-tarjeta-en-el-alta`

**Files:** ninguno (verificación).

- [ ] **Step 1: Comprobar que `master` ya tiene el interruptor de la tarjeta**

Run: `git log master --oneline -20` y `git grep -n "class SignupCardPayments" master -- MLMConquerorGlobalEdition.SignupAPI`
Expected: aparece `MLMConquerorGlobalEdition.SignupAPI/Services/SignupCardPayments.cs` en `master`.

Si no aparece, **parar**: este plan da por hechos estos nombres de esa rama y no se empieza sin ellos:

- `SignupAPI/Services/SignupCardPayments.cs` con `EnabledConfigKey = "Signup:CardEnabled"` e `IsEnabled(IConfiguration?)`.
- `CompleteSignupHandler.CardPaymentsDisabledCode` (= `TokenPurchaseErrors.CardPaymentsDisabled`, `"CARD_PAYMENTS_DISABLED"`) y el parámetro opcional `IConfiguration? configuration = null` al final del constructor.
- `SignupPaymentOptionsDto.CardEnabled` en `SharedKernel/Billing/EntraxContracts.cs`.
- `EntraxSignupStatusService.GetPaymentOptions()` rellenando `CardEnabled`.
- `Signups.Client/Payments/SignupPaymentOptionsApi.cs` con `SignupPaymentOptionsApi.GetAsync` y `SignupPaymentTabs` (`Card`, `Token`, `Crypto`, `Entrax`, `Default`, `Normalize`).
- `Signups.Tests/Helpers/TarjetaDelAlta.cs` (`Encendida()`, `Apagada()`, `Con(valor)`).
- En `Signup.razor` y `MemberJoin.razor`: `_cardEnabled`, la pestaña de tarjeta solo con `_cardEnabled` y el aviso "Card payments are not available right now…".

- [ ] **Step 2: Crear la rama**

```bash
git checkout master
git pull --ff-only
git checkout -b plan-i-3ds-con-spreedly
```

- [ ] **Step 3: Línea base de pruebas de los proyectos que se tocan**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests -nologo` y `dotnet test MLMConquerorGlobalEdition.Billing.Tests -nologo`
Expected: PASS. Si algo falla ya en `master`, anotarlo antes de empezar (no es de este plan).

---

## Fase 1 — Cliente de Spreedly y cobro real en Billing

### Task 1: Opciones de Spreedly y guarda de arranque

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Spreedly/SpreedlyOptions.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Spreedly/SpreedlyOptionsGuard.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Spreedly/SpreedlyOptionsTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Repository.Services.Spreedly;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Spreedly;

/// <summary>
/// Lo que la configuración de Spreedly tiene que impedir al arrancar fuera de Development: la tarjeta del
/// alta encendida sin Spreedly, el simulador, un callback que no es https y un entorno de credenciales que
/// no existe. En Development solo se avisa.
/// </summary>
public class SpreedlyOptionsTests
{
    private static SpreedlyOptions Encendido() => new()
    {
        Enabled = true, CredentialEnvironment = "Production", CallbackUrl = "https://signup.example.com/api/v1/signups/spreedly/callback"
    };

    [Fact]
    public void Por_defecto_esta_apagado_y_lee_las_credenciales_de_Production()
    {
        var o = SpreedlyOptions.FromConfiguration(new ConfigurationBuilder().Build());
        o.Enabled.Should().BeFalse();
        o.UseFake.Should().BeFalse();
        o.CredentialEnvironment.Should().Be("Production");
        o.BaseUrl.Should().Be(SpreedlyOptions.DefaultBaseUrl);
    }

    [Fact]
    public void Encendido_y_bien_configurado_no_tiene_problemas()
        => SpreedlyOptionsGuard.Problems(Encendido(), cardEnabled: true, isDevelopment: false).Should().BeEmpty();

    [Fact]
    public void La_tarjeta_del_alta_encendida_sin_Spreedly_no_arranca_fuera_de_Development()
    {
        var o = new SpreedlyOptions { Enabled = false };
        var act = () => SpreedlyOptionsGuard.ThrowIfInvalid(o, cardEnabled: true, isDevelopment: false);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Signup:CardEnabled*");
    }

    [Fact]
    public void El_simulador_fuera_de_Development_no_arranca_aunque_este_apagado()
    {
        var o = new SpreedlyOptions { UseFake = true };
        var act = () => SpreedlyOptionsGuard.ThrowIfInvalid(o, cardEnabled: false, isDevelopment: false);
        act.Should().Throw<InvalidOperationException>().WithMessage("*UseFake*");
    }

    [Theory]
    [InlineData("http://signup.example.com/cb")]
    [InlineData("/api/v1/signups/spreedly/callback")]
    public void Un_callback_que_no_es_https_absoluta_no_arranca(string url)
    {
        var o = Encendido();
        o.CallbackUrl = url;
        SpreedlyOptionsGuard.Problems(o, cardEnabled: false, isDevelopment: false)
            .Should().ContainSingle(p => p.Contains("CallbackUrl"));
    }

    [Fact]
    public void Sin_callback_se_arranca_porque_el_flujo_no_depende_de_el()
    {
        var o = Encendido();
        o.CallbackUrl = null;
        SpreedlyOptionsGuard.Problems(o, cardEnabled: true, isDevelopment: false).Should().BeEmpty();
    }

    [Fact]
    public void Un_entorno_de_credenciales_desconocido_no_arranca()
    {
        var o = Encendido();
        o.CredentialEnvironment = "Prod";
        SpreedlyOptionsGuard.Problems(o, cardEnabled: false, isDevelopment: false)
            .Should().ContainSingle(p => p.Contains("CredentialEnvironment"));
    }

    [Fact]
    public void En_Development_los_problemas_solo_se_avisan()
    {
        var o = new SpreedlyOptions { Enabled = false };
        var act = () => SpreedlyOptionsGuard.ThrowIfInvalid(o, cardEnabled: true, isDevelopment: true);
        act.Should().NotThrow();
        SpreedlyOptionsGuard.Problems(o, cardEnabled: true, isDevelopment: true).Should().NotBeEmpty();
    }

    [Fact]
    public void El_simulador_en_Development_es_valido_aunque_no_haya_callback()
        => SpreedlyOptionsGuard.Problems(new SpreedlyOptions { Enabled = true, UseFake = true }, cardEnabled: true, isDevelopment: true)
            .Should().BeEmpty();

    [Fact]
    public void Los_tiempos_tienen_minimos()
    {
        var o = new SpreedlyOptions
        {
            TimeoutSeconds = 0, AuthenticationTimeoutMinutes = 0, ChargeUnknownGraceMinutes = 0, PollThrottleSeconds = 0
        };
        o.Timeout.Should().Be(TimeSpan.FromSeconds(5));
        o.AuthenticationTimeout.Should().Be(TimeSpan.FromMinutes(5));
        o.ChargeUnknownGrace.Should().Be(TimeSpan.FromMinutes(10));
        o.PollThrottle.Should().Be(TimeSpan.FromSeconds(3));
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SpreedlyOptionsTests -nologo`
Expected: FAIL de compilación (`SpreedlyOptions` no existe).

- [ ] **Step 3: Crear `SpreedlyOptions.cs`**

```csharp
using Microsoft.Extensions.Configuration;

namespace MLMConquerorGlobalEdition.Repository.Services.Spreedly;

/// <summary>
/// Configuración de Spreedly (sección "Spreedly"). AQUÍ NO HAY SECRETOS: la environment key, el access
/// secret, la SCA provider key y el signing secret viven cifrados en ApiCredential (fila "Spreedly" del
/// entorno <see cref="CredentialEnvironment"/>), y los gateway tokens en la fila de cada CardProcessor.
///
/// Es por servicio: cada host lee la suya. SignupAPI la enciende para el alta; Billing la deja apagada
/// hasta que el recurrente tenga MIT (con Spreedly encendido, un cobro sin 3DS falla cerrado).
/// </summary>
public sealed class SpreedlyOptions
{
    public const string Section        = "Spreedly";
    public const string DefaultBaseUrl = "https://core.spreedly.com/v1/";

    /// <summary>Entornos válidos de ApiCredential (los mismos que ofrece la pantalla de credenciales).</summary>
    public static readonly IReadOnlyList<string> CredentialEnvironments = ["Production", "Sandbox", "Test"];

    /// <summary>Cobro real. Apagado, SpreedlyCardGatewayService se queda en el stub de siempre.</summary>
    public bool Enabled { get; set; }

    /// <summary>Solo Development: registra FakeSpreedlyClient y FakeSpreedlyCredentialSource.</summary>
    public bool UseFake { get; set; }

    /// <summary>De qué fila de ApiCredential se leen las credenciales (columna Environment).</summary>
    public string CredentialEnvironment { get; set; } = "Production";

    public string BaseUrl { get; set; } = DefaultBaseUrl;

    /// <summary>
    /// URL pública https de POST api/v1/signups/spreedly/callback. Opcional: el flujo no depende del
    /// callback (la pantalla y el barrido consultan a Spreedly); si está, Spreedly avisa antes.
    /// </summary>
    public string? CallbackUrl { get; set; }

    /// <summary>Tope de cada llamada HTTP a Spreedly.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Minutos tras los que un intento que sigue autenticando (challenge abandonado) se cierra.</summary>
    public int AuthenticationTimeoutMinutes { get; set; } = 20;

    /// <summary>Minutos tras los que un cobro sin final conocido pasa a revisión.</summary>
    public int ChargeUnknownGraceMinutes { get; set; } = 30;

    /// <summary>Segundos mínimos entre dos consultas a Spreedly del mismo intento desde la pantalla.</summary>
    public int PollThrottleSeconds { get; set; } = 10;

    /// <summary>Intentos que mira el barrido en cada pasada.</summary>
    public int SweepBatchSize { get; set; } = 50;

    public TimeSpan Timeout               => TimeSpan.FromSeconds(Math.Max(5, TimeoutSeconds));
    public TimeSpan AuthenticationTimeout => TimeSpan.FromMinutes(Math.Max(5, AuthenticationTimeoutMinutes));
    public TimeSpan ChargeUnknownGrace    => TimeSpan.FromMinutes(Math.Max(10, ChargeUnknownGraceMinutes));
    public TimeSpan PollThrottle          => TimeSpan.FromSeconds(Math.Max(3, PollThrottleSeconds));

    public static SpreedlyOptions FromConfiguration(IConfiguration configuration)
        => configuration.GetSection(Section).Get<SpreedlyOptions>() ?? new SpreedlyOptions();
}
```

- [ ] **Step 4: Crear `SpreedlyOptionsGuard.cs`**

```csharp
namespace MLMConquerorGlobalEdition.Repository.Services.Spreedly;

/// <summary>
/// Se ejecuta al arrancar. Fuera de Development no arranca: la tarjeta del alta encendida sin Spreedly
/// (cobraría nada y activaría igual), el simulador, un callback que no es https o un entorno de
/// credenciales que no existe. En Development solo se avisa por consola.
///
/// Las credenciales NO se comprueban aquí: viven en la base (ApiCredential) y las escribe el admin. Sin
/// ellas el alta responde 503 CARD_NOT_CONFIGURED y payment-options esconde la pestaña de tarjeta.
/// </summary>
public static class SpreedlyOptionsGuard
{
    public static IReadOnlyList<string> Problems(SpreedlyOptions o, bool cardEnabled, bool isDevelopment)
    {
        var problems = new List<string>();

        if (o.UseFake && !isDevelopment)
            problems.Add("Spreedly:UseFake solo se permite en Development.");

        if (cardEnabled && !o.Enabled)
            problems.Add("Signup:CardEnabled está encendido y Spreedly:Enabled no: el alta no tendría con qué cobrar.");

        if (o.UseFake && isDevelopment)
            return problems;

        if (!SpreedlyOptions.CredentialEnvironments.Contains(o.CredentialEnvironment))
            problems.Add($"Spreedly:CredentialEnvironment debe ser uno de: {string.Join(", ", SpreedlyOptions.CredentialEnvironments)}.");

        if (!IsAbsoluteHttps(o.BaseUrl))
            problems.Add("Spreedly:BaseUrl debe ser una URL https absoluta.");

        if (!string.IsNullOrWhiteSpace(o.CallbackUrl) && !IsAbsoluteHttps(o.CallbackUrl))
            problems.Add("Spreedly:CallbackUrl debe ser una URL https absoluta (o estar vacía).");

        return problems;
    }

    public static void ThrowIfInvalid(SpreedlyOptions o, bool cardEnabled, bool isDevelopment)
    {
        var problems = Problems(o, cardEnabled, isDevelopment);
        if (problems.Count == 0 || isDevelopment) return;

        throw new InvalidOperationException("[Spreedly] Configuración inválida: " + string.Join(" ", problems));
    }

    private static bool IsAbsoluteHttps(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps;
}
```

- [ ] **Step 5: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SpreedlyOptionsTests -nologo`
Expected: PASS (10 pruebas).

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Spreedly/SpreedlyOptions.cs \
        MLMConquerorGlobalEdition.Repository/Services/Spreedly/SpreedlyOptionsGuard.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Spreedly/SpreedlyOptionsTests.cs
git commit -m "Opciones de Spreedly sin secretos y guarda de arranque: la tarjeta del alta no arranca sin Spreedly ni el simulador fuera de Development"
```

---

### Task 2: Forma del proveedor, unidades menores, firma, marcas y *liability shift*

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Spreedly/SpreedlyProviderContracts.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Spreedly/SpreedlyCurrency.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Spreedly/SpreedlySignature.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Spreedly/SpreedlyCardBrands.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Spreedly/ThreeDsLiabilityPolicy.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Spreedly/SpreedlyPiezasTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using System.Security.Cryptography;
using System.Text;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.Spreedly;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Spreedly;

/// <summary>Las piezas puras del cliente: importes, firma de callbacks, marcas y la regla de liability shift.</summary>
public class SpreedlyPiezasTests
{
    // ── Importes en unidades menores ────────────────────────────────────────

    [Theory]
    [InlineData(99.95, "USD", 9995)]
    [InlineData(100, "EUR", 10000)]
    [InlineData(0.01, "usd", 1)]
    [InlineData(1500, "JPY", 1500)]
    [InlineData(12.345, "KWD", 12345)]
    [InlineData(10.005, "USD", 1001)]
    public void Convierte_a_unidades_menores_segun_la_moneda(decimal importe, string moneda, long esperado)
        => SpreedlyCurrency.ToMinorUnits(importe, moneda).Should().Be(esperado);

    [Fact]
    public void Una_moneda_que_no_son_tres_letras_no_se_convierte()
    {
        var act = () => SpreedlyCurrency.ToMinorUnits(10m, "US");
        act.Should().Throw<ArgumentException>();
    }

    // ── Firma de los callbacks ──────────────────────────────────────────────

    private static string Firma(string secreto, string datos)
        => Convert.ToHexString(HMACSHA1.HashData(Encoding.UTF8.GetBytes(secreto), Encoding.UTF8.GetBytes(datos))).ToLowerInvariant();

    [Fact]
    public void La_firma_es_hmac_sha1_de_los_valores_unidos_con_barra()
    {
        var valores = new[] { "100", "USD", "SIGNUP-CARD-o1-1", "succeeded" };
        var firma = Firma("secreto", "100|USD|SIGNUP-CARD-o1-1|succeeded");

        SpreedlySignature.Compute("secreto", valores).Should().Be(firma);
        SpreedlySignature.Verify("secreto", valores, firma).Should().BeTrue();
        SpreedlySignature.Verify("secreto", valores, firma.ToUpperInvariant()).Should().BeTrue("el hexadecimal no distingue mayúsculas");
    }

    [Theory]
    [InlineData("otro-secreto", "100")]
    [InlineData("secreto", "101")]
    public void Otro_secreto_u_otro_valor_no_validan(string secreto, string importe)
    {
        var firma = Firma("secreto", "100|USD");
        SpreedlySignature.Verify(secreto, [importe, "USD"], firma).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-es-hex")]
    public void Sin_firma_o_con_basura_no_valida(string? firma)
        => SpreedlySignature.Verify("secreto", ["100"], firma).Should().BeFalse();

    [Fact]
    public void Sin_secreto_no_valida_nada()
        => SpreedlySignature.Verify("", ["100"], Firma("", "100")).Should().BeFalse();

    // ── Marcas ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("visa", CardBrand.Visa)]
    [InlineData("master", CardBrand.MasterCard)]
    [InlineData("american_express", CardBrand.Amex)]
    [InlineData("maestro", CardBrand.Maestro)]
    [InlineData("jcb", CardBrand.Jcb)]
    [InlineData("bancontact", CardBrand.Bancontact)]
    [InlineData("discover", CardBrand.Other)]
    [InlineData(null, CardBrand.Other)]
    public void Traduce_la_marca_de_Spreedly(string? marca, CardBrand esperada)
        => SpreedlyCardBrands.FromSpreedly(marca).Should().Be(esperada);

    // ── Liability shift ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(CardBrand.Visa, "05", "Y", true)]
    [InlineData(CardBrand.Visa, "06", "A", true)]
    [InlineData(CardBrand.Visa, "07", "N", false)]
    [InlineData(CardBrand.Visa, "05", "N", false)]
    [InlineData(CardBrand.Visa, "02", "Y", false)]
    [InlineData(CardBrand.MasterCard, "02", "Y", true)]
    [InlineData(CardBrand.MasterCard, "01", "A", true)]
    [InlineData(CardBrand.MasterCard, "00", "N", false)]
    [InlineData(CardBrand.MasterCard, "05", "Y", false)]
    [InlineData(CardBrand.Maestro, "02", "Y", true)]
    [InlineData(CardBrand.Amex, "05", "Y", true)]
    [InlineData(CardBrand.Jcb, "06", "A", true)]
    [InlineData(CardBrand.Other, "05", "Y", true)]
    [InlineData(CardBrand.Visa, "05", "U", false)]
    [InlineData(CardBrand.Visa, "05", "R", false)]
    [InlineData(CardBrand.Visa, "05", "I", false)]
    [InlineData(CardBrand.Visa, "05", "C", false)]
    public void Solo_hay_liability_shift_con_las_parejas_de_cada_marca(CardBrand marca, string eci, string estado, bool shift)
        => ThreeDsLiabilityPolicy.Evaluate(marca, eci, estado).LiabilityShift.Should().Be(shift);

    [Theory]
    [InlineData(CardBrand.Visa, "5", true)]
    [InlineData(CardBrand.Visa, "07", false)]
    [InlineData(CardBrand.MasterCard, "2", true)]
    public void Sin_estado_decide_el_eci(CardBrand marca, string eci, bool shift)
        => ThreeDsLiabilityPolicy.Evaluate(marca, eci, null).LiabilityShift.Should().Be(shift);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Sin_eci_nunca_hay_shift(string? eci)
    {
        var d = ThreeDsLiabilityPolicy.Evaluate(CardBrand.Visa, eci, "Y");
        d.LiabilityShift.Should().BeFalse();
        d.Reason.Should().Be(ThreeDsLiabilityPolicy.MissingEci);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SpreedlyPiezasTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `SpreedlyProviderContracts.cs`**

```csharp
using System.Text.RegularExpressions;

namespace MLMConquerorGlobalEdition.Repository.Services.Spreedly;

/// <summary>Lo que Spreedly dice de un medio de pago (GET /v1/payment_methods/{token}.json). Sin número ni CVV: no los tiene.</summary>
public sealed record SpreedlyPaymentMethod(
    string  Token,
    string  PaymentMethodType,
    string? CardType,
    string? FirstSixDigits,
    string? LastFourDigits,
    int     Month,
    int     Year,
    string  StorageState,
    bool    Test);

/// <summary>
/// La autenticación 3DS2 de una transacción (objeto sca_authentication). El CAVV no se copia: solo si vino
/// (<see cref="HasAuthenticationValue"/>). Los campos que falten quedan null y la política los trata como
/// "sin liability shift".
/// </summary>
public sealed record SpreedlyScaAuthentication(
    string? Token,
    string? State,
    string? FlowPerformed,
    string? Eci,
    string? TransStatus,
    string? MessageVersion,
    bool    HasAuthenticationValue);

/// <summary>Una transacción de Spreedly (autenticación, compra, void, crédito), ya fuera del sobre.</summary>
public sealed record SpreedlyTransaction(
    string  Token,
    string  State,
    bool    Succeeded,
    string? TransactionType,
    long?   AmountMinor,
    string? CurrencyCode,
    string? OrderId,
    string? PaymentMethodToken,
    string? RequiredAction,
    string? DeviceFingerprintForm,
    string? ChallengeForm,
    string? ChallengeUrl,
    string? CheckoutForm,
    string? CheckoutUrl,
    string? GatewayTransactionId,
    string? NetworkTransactionId,
    string? Message,
    SpreedlyScaAuthentication? Sca);

/// <summary>POST /v1/sca/providers/{ScaProviderKey}/authenticate.json. AmountMinor en unidades menores.</summary>
public sealed record SpreedlyAuthenticateRequest(
    string  ScaProviderKey,
    string  PaymentMethodToken,
    long    AmountMinor,
    string  CurrencyCode,
    string  BrowserInfo,
    string  OrderId,
    string? CallbackUrl);

/// <summary>POST /v1/gateways/{GatewayToken}/purchase.json con el token de la autenticación 3DS.</summary>
public sealed record SpreedlyPurchaseRequest(
    string GatewayToken,
    string PaymentMethodToken,
    long   AmountMinor,
    string CurrencyCode,
    string OrderId,
    string Description,
    string ScaAuthenticationToken,
    bool   RetainOnSuccess);

/// <summary>
/// Cómo terminó una llamada. Lo que importa para el dinero es distinguir <see cref="Declined"/> (la respuesta
/// prueba que NO se cobró) de <see cref="Unknown"/> (timeout, 5xx, respuesta ilegible o
/// gateway_processing_result_unknown: pudo cobrarse).
/// </summary>
public enum SpreedlyOutcome { Ok, Declined, Unknown, NotConfigured, Invalid }

/// <summary>Resultado de una llamada. El cliente nunca lanza por un fallo del proveedor o de la red.</summary>
public sealed record SpreedlyCall<T>(T? Value, SpreedlyOutcome Outcome, int HttpStatus, string? ErrorCode, long ElapsedMs)
    where T : class
{
    public bool Ok => Outcome == SpreedlyOutcome.Ok && Value is not null;

    public static SpreedlyCall<T> Success(T value, int httpStatus, long elapsedMs)
        => new(value, SpreedlyOutcome.Ok, httpStatus, null, elapsedMs);

    public static SpreedlyCall<T> Fail(SpreedlyOutcome outcome, int httpStatus, string errorCode, long elapsedMs, T? value = null)
        => new(value, outcome, httpStatus, errorCode, elapsedMs);
}

public static class SpreedlyErrorCodes
{
    /// <summary>Sin respuesta: timeout, DNS, conexión rechazada.</summary>
    public const string Transport     = "TRANSPORT";
    /// <summary>Canceló quien llamaba.</summary>
    public const string Cancelled     = "CANCELLED";
    /// <summary>Respuesta que no es la esperada o le faltan campos.</summary>
    public const string BadResponse   = "BAD_RESPONSE";
    /// <summary>Sin credenciales del entorno o BaseUrl que no es https: no se llama.</summary>
    public const string NotConfigured = "NOT_CONFIGURED";
    /// <summary>Token con caracteres que no son de un token de Spreedly: no se llama (evita rutas inyectadas).</summary>
    public const string InvalidToken  = "INVALID_TOKEN";
    /// <summary>Spreedly respondió que la transacción falló (no se cobró).</summary>
    public const string Declined      = "DECLINED";
    /// <summary>gateway_processing_result_unknown o equivalente: pudo cobrarse.</summary>
    public const string ResultUnknown = "RESULT_UNKNOWN";
    /// <summary>404: el token no existe en este entorno de Spreedly.</summary>
    public const string NotFound      = "NOT_FOUND";
}

/// <summary>Estados de transacción de Spreedly que se distinguen aquí.</summary>
public static class SpreedlyStates
{
    public const string Succeeded        = "succeeded";
    public const string Pending          = "pending";
    public const string Failed           = "failed";
    public const string ProcessingFailed = "gateway_processing_failed";
    public const string ResultUnknown    = "gateway_processing_result_unknown";
    public const string Processing       = "processing";

    /// <summary>Estados que prueban que NO se movió dinero.</summary>
    public static bool IsNoCharge(string? state)
        => state is Failed or ProcessingFailed;
}

public static class SpreedlyTokens
{
    private static readonly Regex Shape = new("^[A-Za-z0-9_-]{8,100}$", RegexOptions.CultureInvariant);

    /// <summary>¿Puede ir en una ruta? Los tokens de Spreedly son alfanuméricos; los del simulador llevan '_'.</summary>
    public static bool IsValid(string? token) => token is not null && Shape.IsMatch(token);
}

public interface ISpreedlyClient
{
    Task<SpreedlyCall<SpreedlyPaymentMethod>> GetPaymentMethodAsync(string paymentMethodToken, CancellationToken ct);
    Task<SpreedlyCall<SpreedlyTransaction>> AuthenticateAsync(SpreedlyAuthenticateRequest request, CancellationToken ct);
    Task<SpreedlyCall<SpreedlyTransaction>> CompleteAsync(string transactionToken, CancellationToken ct);
    Task<SpreedlyCall<SpreedlyTransaction>> GetTransactionAsync(string transactionToken, CancellationToken ct);
    Task<SpreedlyCall<SpreedlyTransaction>> PurchaseAsync(SpreedlyPurchaseRequest request, CancellationToken ct);
    Task<SpreedlyCall<IReadOnlyList<SpreedlyTransaction>>> ListPaymentMethodTransactionsAsync(string paymentMethodToken, CancellationToken ct);
    Task<SpreedlyCall<SpreedlyTransaction>> VoidAsync(string transactionToken, CancellationToken ct);
    Task<SpreedlyCall<SpreedlyTransaction>> CreditAsync(string transactionToken, long? amountMinor, string? currencyCode, CancellationToken ct);
}
```

- [ ] **Step 4: Crear `SpreedlyCurrency.cs`**

```csharp
namespace MLMConquerorGlobalEdition.Repository.Services.Spreedly;

/// <summary>
/// Spreedly cobra en unidades menores (centavos). La mayoría de monedas tienen dos decimales; estas no
/// (ISO 4217). Multiplicar siempre por 100 cobraría 100 veces de más en JPY.
/// </summary>
public static class SpreedlyCurrency
{
    private static readonly HashSet<string> ZeroDecimals = new(StringComparer.OrdinalIgnoreCase)
    {
        "BIF", "CLP", "DJF", "GNF", "ISK", "JPY", "KMF", "KRW", "PYG", "RWF", "UGX", "UYI", "VND", "VUV", "XAF", "XOF", "XPF"
    };

    private static readonly HashSet<string> ThreeDecimals = new(StringComparer.OrdinalIgnoreCase)
    {
        "BHD", "IQD", "JOD", "KWD", "LYD", "OMR", "TND"
    };

    public static int Exponent(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
            throw new ArgumentException("La moneda debe ser un código ISO 4217 de tres letras.", nameof(currency));

        var c = currency.Trim();
        return ZeroDecimals.Contains(c) ? 0 : ThreeDecimals.Contains(c) ? 3 : 2;
    }

    /// <summary>Importe en unidades menores, redondeado lejos de cero (10.005 USD = 1001).</summary>
    public static long ToMinorUnits(decimal amount, string currency)
    {
        var factor = Exponent(currency) switch { 0 => 1m, 3 => 1000m, _ => 100m };
        return (long)Math.Round(amount * factor, 0, MidpointRounding.AwayFromZero);
    }
}
```

- [ ] **Step 5: Crear `SpreedlySignature.cs`**

```csharp
using System.Security.Cryptography;
using System.Text;

namespace MLMConquerorGlobalEdition.Repository.Services.Spreedly;

/// <summary>
/// Firma de las transacciones que Spreedly manda al callback: HMAC-SHA1 con el signing secret del entorno
/// sobre los valores de los campos que el propio aviso lista (signed/fields), unidos con '|'. Se compara en
/// tiempo constante. Ver R2 del Plan I: la forma exacta se verifica con la prueba real; si fuera otra, los
/// callbacks se rechazan y la liquidación sigue por consulta y barrido.
/// </summary>
public static class SpreedlySignature
{
    public static string Compute(string signingSecret, IEnumerable<string> values)
    {
        var data = string.Join("|", values);
        var mac  = HMACSHA1.HashData(Encoding.UTF8.GetBytes(signingSecret), Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(mac).ToLowerInvariant();
    }

    public static bool Verify(string? signingSecret, IEnumerable<string> values, string? signature)
    {
        if (string.IsNullOrEmpty(signingSecret) || string.IsNullOrWhiteSpace(signature))
            return false;

        byte[] given;
        try
        {
            given = Convert.FromHexString(signature.Trim());
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = Convert.FromHexString(Compute(signingSecret, values));
        return CryptographicOperations.FixedTimeEquals(expected, given);
    }
}
```

- [ ] **Step 6: Crear `SpreedlyCardBrands.cs`**

```csharp
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Repository.Services.Spreedly;

/// <summary>La marca que dice Spreedly (card_type) en el enum del router. Lo que no se conoce es Other.</summary>
public static class SpreedlyCardBrands
{
    public static CardBrand FromSpreedly(string? cardType) => (cardType ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "visa"             => CardBrand.Visa,
        "master"           => CardBrand.MasterCard,
        "mastercard"       => CardBrand.MasterCard,
        "american_express" => CardBrand.Amex,
        "maestro"          => CardBrand.Maestro,
        "jcb"              => CardBrand.Jcb,
        "bancontact"       => CardBrand.Bancontact,
        _                  => CardBrand.Other
    };

    /// <summary>Texto para MemberCreditCard.CardBrand (lo que ve la persona en su billetera).</summary>
    public static string DisplayName(CardBrand brand) => brand switch
    {
        CardBrand.Visa       => "Visa",
        CardBrand.MasterCard => "Mastercard",
        CardBrand.Amex       => "American Express",
        CardBrand.Maestro    => "Maestro",
        CardBrand.Jcb        => "JCB",
        CardBrand.Bancontact => "Bancontact",
        _                    => "Card"
    };
}
```

- [ ] **Step 7: Crear `ThreeDsLiabilityPolicy.cs`**

```csharp
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Repository.Services.Spreedly;

/// <summary>Si una autenticación 3DS2 traslada la responsabilidad al emisor y por qué.</summary>
public sealed record ThreeDsDecision(bool LiabilityShift, string Reason);

/// <summary>
/// La regla del Plan I (decisión I8): sin liability shift no se cobra.
///
///   • trans_status Y (autenticado):   ECI 05 en Visa, Amex, JCB, Bancontact y otras; 02 en Mastercard y Maestro.
///   • trans_status A (intento):        ECI 06 en Visa y demás; 01 en Mastercard y Maestro.
///   • N, R, U, I, C o cualquier otro:  no.
///   • Sin trans_status:                decide el ECI con las mismas parejas.
///   • Sin ECI:                         no (falla cerrado: un campo que no llegó no da derechos).
/// </summary>
public static class ThreeDsLiabilityPolicy
{
    public const string Authenticated  = "AUTHENTICATED";
    public const string Attempted      = "ATTEMPTED";
    public const string MissingEci     = "MISSING_ECI";
    public const string NoShiftEci     = "NO_SHIFT_ECI";
    public const string StatusNotShift = "TRANS_STATUS_NO_SHIFT";

    public static ThreeDsDecision Evaluate(CardBrand brand, string? eci, string? transStatus)
    {
        var e = NormalizeEci(eci);
        if (e is null)
            return new(false, MissingEci);

        var mastercard       = brand is CardBrand.MasterCard or CardBrand.Maestro;
        var authenticatedEci = mastercard ? "02" : "05";
        var attemptedEci     = mastercard ? "01" : "06";

        var status = transStatus?.Trim().ToUpperInvariant();
        return status switch
        {
            "Y"        => e == authenticatedEci ? new(true, Authenticated) : new(false, NoShiftEci),
            "A"        => e == attemptedEci     ? new(true, Attempted)     : new(false, NoShiftEci),
            null or "" => e == authenticatedEci ? new(true, Authenticated)
                        : e == attemptedEci     ? new(true, Attempted)
                        : new(false, NoShiftEci),
            _          => new(false, StatusNotShift)
        };
    }

    /// <summary>"5" y "05" son lo mismo; cualquier cosa que no sean uno o dos dígitos es "sin ECI".</summary>
    private static string? NormalizeEci(string? eci)
    {
        var t = eci?.Trim();
        if (string.IsNullOrEmpty(t) || t.Length > 2 || !t.All(char.IsAsciiDigit))
            return null;
        return t.PadLeft(2, '0');
    }
}
```

- [ ] **Step 8: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SpreedlyPiezasTests -nologo`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Spreedly/SpreedlyProviderContracts.cs \
        MLMConquerorGlobalEdition.Repository/Services/Spreedly/SpreedlyCurrency.cs \
        MLMConquerorGlobalEdition.Repository/Services/Spreedly/SpreedlySignature.cs \
        MLMConquerorGlobalEdition.Repository/Services/Spreedly/SpreedlyCardBrands.cs \
        MLMConquerorGlobalEdition.Repository/Services/Spreedly/ThreeDsLiabilityPolicy.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Spreedly/SpreedlyPiezasTests.cs
git commit -m "Piezas de Spreedly: forma del proveedor, importes en unidades menores, firma de callbacks, marcas y regla de liability shift por ECI"
```

---

### Task 3: Credenciales desde `ApiCredential`, filtradas por entorno

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Spreedly/SpreedlyCredentialSource.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Spreedly/FakeSpreedlyCredentialSource.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Spreedly/SpreedlyCredentialSourceTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Spreedly;
using MLMConquerorGlobalEdition.SharedKernel.Server.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Spreedly;

/// <summary>
/// Las credenciales salen de ApiCredential, de la fila del entorno configurado y activa. El stub buscaba
/// "Spreedly" sin entorno (cogía una fila cualquiera) y solo leía la environment key.
/// </summary>
public class SpreedlyCredentialSourceTests
{
    private readonly GatewayCredentialProtector _protector = new(new EphemeralDataProtectionProvider());

    private ApiCredential Fila(string serviceKey, string entorno, bool activa = true, bool completa = true)
    {
        var c = new ApiCredential
        {
            ServiceKey = serviceKey, Environment = entorno, IsActive = activa,
            CreatedBy = "test", CreationDate = DateTime.Now, LastUpdateDate = DateTime.Now
        };
        if (serviceKey == SpreedlyCredentialSource.ServiceKey)
        {
            c.ApiKeyEncrypted           = _protector.Encrypt($"env-key-{entorno}");
            c.SecretKeyEncrypted        = completa ? _protector.Encrypt($"secret-{entorno}") : null;
            c.MerchantIdEncrypted       = _protector.Encrypt($"sca-{entorno}");
            c.AdditionalSecretEncrypted = _protector.Encrypt($"signing-{entorno}");
        }
        else
        {
            c.SpreedlyGatewayTokenEncrypted = _protector.Encrypt($"gw-{serviceKey}-{entorno}");
        }
        return c;
    }

    private SpreedlyCredentialSource Fuente(AppDbContext db, string entorno = "Production")
        => new(db, _protector, new SpreedlyOptions { CredentialEnvironment = entorno }, NullLogger<SpreedlyCredentialSource>.Instance);

    [Fact]
    public async Task Lee_la_fila_del_entorno_configurado_y_no_otra()
    {
        using var db = InMemoryDbHelper.Create();
        db.ApiCredentials.AddRange(Fila("Spreedly", "Sandbox"), Fila("Spreedly", "Production"));
        await db.SaveChangesAsync();

        var c = await Fuente(db).GetAsync(default);

        c.Should().NotBeNull();
        c!.EnvironmentKey.Should().Be("env-key-Production");
        c.AccessSecret.Should().Be("secret-Production");
        c.ScaProviderKey.Should().Be("sca-Production");
        c.SigningSecret.Should().Be("signing-Production");
    }

    [Fact]
    public async Task Sin_access_secret_no_hay_credenciales()
    {
        using var db = InMemoryDbHelper.Create();
        db.ApiCredentials.Add(Fila("Spreedly", "Production", completa: false));
        await db.SaveChangesAsync();

        (await Fuente(db).GetAsync(default)).Should().BeNull();
    }

    [Fact]
    public async Task Una_fila_inactiva_o_borrada_no_cuenta()
    {
        using var db = InMemoryDbHelper.Create();
        var borrada = Fila("Spreedly", "Production");
        borrada.IsDeleted = true;
        db.ApiCredentials.AddRange(Fila("Spreedly", "Sandbox", activa: false), borrada);
        await db.SaveChangesAsync();

        (await Fuente(db).GetAsync(default)).Should().BeNull();
        (await Fuente(db, "Sandbox").GetAsync(default)).Should().BeNull();
    }

    [Fact]
    public async Task El_gateway_token_es_el_del_procesador_en_el_mismo_entorno()
    {
        using var db = InMemoryDbHelper.Create();
        db.ApiCredentials.AddRange(Fila("NmiSpreedly", "Sandbox"), Fila("NmiSpreedly", "Production"), Fila("CheckoutEUR", "Sandbox"));
        await db.SaveChangesAsync();

        (await Fuente(db).GetGatewayTokenAsync(CardProcessor.NmiSpreedly, default)).Should().Be("gw-NmiSpreedly-Production");
        (await Fuente(db).GetGatewayTokenAsync(CardProcessor.CheckoutEUR, default)).Should().BeNull("solo hay fila de Sandbox");
    }

    [Fact]
    public async Task Un_valor_que_no_se_puede_descifrar_es_como_si_no_estuviera()
    {
        using var db = InMemoryDbHelper.Create();
        var fila = Fila("Spreedly", "Production");
        fila.SecretKeyEncrypted = "ENC:esto-no-es-ciphertext";
        db.ApiCredentials.Add(fila);
        await db.SaveChangesAsync();

        (await Fuente(db).GetAsync(default)).Should().BeNull();
    }

    [Fact]
    public async Task El_simulador_no_necesita_filas()
    {
        var fake = new FakeSpreedlyCredentialSource();
        (await fake.GetAsync(default))!.EnvironmentKey.Should().Be(FakeSpreedlyCredentialSource.EnvironmentKey);
        (await fake.GetGatewayTokenAsync(CardProcessor.Shift4, default)).Should().Be("fake_gateway_Shift4");
    }
}
```

> Si `ApiCredential` tiene filtro de consulta global por `IsDeleted` (mirar `ApiCredentialConfiguration`), la fila borrada ya no se ve; la condición `!c.IsDeleted` de la consulta se deja igual por claridad.

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SpreedlyCredentialSourceTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `SpreedlyCredentialSource.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Server.Services;

namespace MLMConquerorGlobalEdition.Repository.Services.Spreedly;

/// <summary>
/// Las credenciales de un entorno de Spreedly. EnvironmentKey es pública (la usa el iFrame en el
/// navegador); AccessSecret y SigningSecret nunca salen del servidor ni del log.
/// </summary>
public sealed record SpreedlyCredentials(string EnvironmentKey, string AccessSecret, string ScaProviderKey, string? SigningSecret);

public interface ISpreedlyCredentialSource
{
    /// <summary>Las credenciales del entorno configurado, o null si faltan, están inactivas o no se descifran.</summary>
    Task<SpreedlyCredentials?> GetAsync(CancellationToken ct);

    /// <summary>El gateway token de Spreedly del procesador en el mismo entorno, o null.</summary>
    Task<string?> GetGatewayTokenAsync(CardProcessor processor, CancellationToken ct);
}

/// <summary>
/// Lee ApiCredential (decisión I2 del Plan I): fila "Spreedly" del entorno Spreedly:CredentialEnvironment,
/// activa y sin borrar. ApiKey = environment key, SecretKey = access secret, MerchantId = SCA provider key,
/// AdditionalSecret = signing secret. Los gateway tokens, en la fila de cada CardProcessor del mismo entorno.
///
/// Descifra con GatewayCredentialProtector CONCRETO (I3): en SignupAPI y BizCenter IEncryptionService es
/// otro cifrado, y pedir la interfaz daría el equivocado.
/// </summary>
public sealed class SpreedlyCredentialSource : ISpreedlyCredentialSource
{
    public const string ServiceKey = "Spreedly";

    private readonly AppDbContext                       _db;
    private readonly GatewayCredentialProtector         _protector;
    private readonly SpreedlyOptions                    _options;
    private readonly ILogger<SpreedlyCredentialSource>  _logger;

    public SpreedlyCredentialSource(
        AppDbContext db, GatewayCredentialProtector protector, SpreedlyOptions options, ILogger<SpreedlyCredentialSource> logger)
    {
        _db        = db;
        _protector = protector;
        _options   = options;
        _logger    = logger;
    }

    public async Task<SpreedlyCredentials?> GetAsync(CancellationToken ct)
    {
        var row = await FindAsync(ServiceKey, ct);
        if (row is null)
            return null;

        var environmentKey = Reveal(row, nameof(ApiCredential.ApiKeyEncrypted), row.ApiKeyEncrypted);
        var accessSecret   = Reveal(row, nameof(ApiCredential.SecretKeyEncrypted), row.SecretKeyEncrypted);
        var scaProviderKey = Reveal(row, nameof(ApiCredential.MerchantIdEncrypted), row.MerchantIdEncrypted);
        var signingSecret  = Reveal(row, nameof(ApiCredential.AdditionalSecretEncrypted), row.AdditionalSecretEncrypted);

        if (environmentKey is null || accessSecret is null || scaProviderKey is null)
        {
            _logger.LogWarning("Spreedly: la credencial {ServiceKey} de {Environment} está incompleta (environment key, access secret o SCA provider key).",
                ServiceKey, _options.CredentialEnvironment);
            return null;
        }

        return new SpreedlyCredentials(environmentKey, accessSecret, scaProviderKey, signingSecret);
    }

    public async Task<string?> GetGatewayTokenAsync(CardProcessor processor, CancellationToken ct)
    {
        var row = await FindAsync(processor.ToString(), ct);
        return row is null ? null : Reveal(row, nameof(ApiCredential.SpreedlyGatewayTokenEncrypted), row.SpreedlyGatewayTokenEncrypted);
    }

    private Task<ApiCredential?> FindAsync(string serviceKey, CancellationToken ct)
        => _db.ApiCredentials.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ServiceKey == serviceKey
                                   && c.Environment == _options.CredentialEnvironment
                                   && c.IsActive
                                   && !c.IsDeleted, ct);

    /// <summary>El valor en claro, o null si falta o no se puede descifrar (se avisa sin el valor).</summary>
    private string? Reveal(ApiCredential row, string field, string? cipher)
    {
        if (string.IsNullOrWhiteSpace(cipher))
            return null;
        try
        {
            var plain = _protector.Decrypt(cipher);
            return string.IsNullOrWhiteSpace(plain) ? null : plain.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError("Spreedly: no se pudo descifrar {Field} de {ServiceKey}/{Environment} ({Error}).",
                field, row.ServiceKey, row.Environment, ex.GetType().Name);
            return null;
        }
    }
}
```

- [ ] **Step 4: Crear `FakeSpreedlyCredentialSource.cs`**

```csharp
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Repository.Services.Spreedly;

/// <summary>Credenciales del simulador (Spreedly:UseFake, solo Development): no hacen falta filas en ApiCredential.</summary>
public sealed class FakeSpreedlyCredentialSource : ISpreedlyCredentialSource
{
    public const string EnvironmentKey = "fake-spreedly-environment";
    public const string SigningSecret  = "fake-spreedly-signing-secret";

    public Task<SpreedlyCredentials?> GetAsync(CancellationToken ct)
        => Task.FromResult<SpreedlyCredentials?>(new(EnvironmentKey, "fake-access-secret", "fake_sca_provider", SigningSecret));

    public Task<string?> GetGatewayTokenAsync(CardProcessor processor, CancellationToken ct)
        => Task.FromResult<string?>($"fake_gateway_{processor}");
}
```

- [ ] **Step 5: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SpreedlyCredentialSourceTests -nologo`
Expected: PASS (6 pruebas).

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Spreedly/SpreedlyCredentialSource.cs \
        MLMConquerorGlobalEdition.Repository/Services/Spreedly/FakeSpreedlyCredentialSource.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Spreedly/SpreedlyCredentialSourceTests.cs
git commit -m "Credenciales de Spreedly desde ApiCredential del entorno configurado, con access secret, SCA provider y signing secret, descifradas con el protector de gateways"
```

---

### Task 4: Cliente HTTP de Spreedly

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Spreedly/SpreedlyClient.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Spreedly/SpreedlyClientTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.Spreedly;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Spreedly;

/// <summary>
/// El cliente de Spreedly: Basic auth con environment key + access secret, importes en unidades menores, y
/// sobre todo cómo se lee una respuesta de dinero. Un rechazo explícito (succeeded = false con estado
/// failed) prueba que no se cobró; un timeout, un 5xx, una respuesta ilegible o
/// gateway_processing_result_unknown NO prueban nada y salen como Unknown.
/// </summary>
public class SpreedlyClientTests
{
    private sealed class Manejador(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public readonly List<(HttpRequestMessage Peticion, string Cuerpo)> Llamadas = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            var cuerpo = r.Content is null ? string.Empty : await r.Content.ReadAsStringAsync(ct);
            Llamadas.Add((r, cuerpo));
            return responder(r);
        }
    }

    private sealed class Lanza(Exception ex) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) => Task.FromException<HttpResponseMessage>(ex);
    }

    private sealed class LogEnLista : ILogger<SpreedlyClient>
    {
        public readonly List<string> Lineas = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel l, EventId e, TState s, Exception? ex, Func<TState, Exception?, string> f) => Lineas.Add(f(s, ex));
    }

    private sealed class Credenciales(SpreedlyCredentials? c) : ISpreedlyCredentialSource
    {
        public Task<SpreedlyCredentials?> GetAsync(CancellationToken ct) => Task.FromResult(c);
        public Task<string?> GetGatewayTokenAsync(CardProcessor p, CancellationToken ct) => Task.FromResult<string?>("gw_token_123");
    }

    private static readonly SpreedlyCredentials Buenas = new("ENVKEY1", "SECRETO1", "SCAPROV1", "FIRMA1");

    private static HttpResponseMessage Json(HttpStatusCode codigo, string json)
        => new(codigo) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static (SpreedlyClient Cliente, LogEnLista Log) Nuevo(HttpMessageHandler h, SpreedlyCredentials? c = null)
    {
        var log = new LogEnLista();
        var cliente = new SpreedlyClient(new HttpClient(h), new Credenciales(c ?? Buenas), new SpreedlyOptions(), log);
        return (cliente, log);
    }

    private const string Compra = """
        {"transaction":{"token":"TXPURCHASE1","state":"succeeded","succeeded":true,"transaction_type":"Purchase",
         "amount":9995,"currency_code":"USD","order_id":"SIGNUP-CARD-o1-1","gateway_transaction_id":"gw-77",
         "network_transaction_id":"NTID-9","message":"Succeeded!","payment_method":{"token":"PMTOKEN1"}}}
        """;

    private static SpreedlyPurchaseRequest Pedido() => new("GWTOKEN1", "PMTOKEN1", 9995, "USD", "SIGNUP-CARD-o1-1", "Signup", "SCATOKEN1", true);

    [Fact]
    public async Task Autentica_con_basic_environment_key_y_access_secret()
    {
        var h = new Manejador(_ => Json(HttpStatusCode.OK, Compra));
        await Nuevo(h).Cliente.PurchaseAsync(Pedido(), default);

        var auth = h.Llamadas.Single().Peticion.Headers.Authorization!;
        auth.Scheme.Should().Be("Basic");
        Encoding.UTF8.GetString(Convert.FromBase64String(auth.Parameter!)).Should().Be("ENVKEY1:SECRETO1");
    }

    [Fact]
    public async Task Sin_credenciales_no_llama()
    {
        var h = new Manejador(_ => throw new InvalidOperationException("no debía llamar"));
        var log = new LogEnLista();
        var cliente = new SpreedlyClient(new HttpClient(h), new Credenciales(null), new SpreedlyOptions(), log);

        var r = await cliente.PurchaseAsync(Pedido(), default);

        r.Outcome.Should().Be(SpreedlyOutcome.NotConfigured);
        h.Llamadas.Should().BeEmpty();
    }

    [Fact]
    public async Task La_autenticacion_va_al_sca_provider_con_importe_menor_moneda_navegador_y_order_id()
    {
        var h = new Manejador(_ => Json(HttpStatusCode.OK,
            """{"transaction":{"token":"TXAUTH1","state":"pending","succeeded":false,"required_action":"device_fingerprint","device_fingerprint_form":"<form/>"}}"""));

        var r = await Nuevo(h).Cliente.AuthenticateAsync(
            new SpreedlyAuthenticateRequest("SCAPROV1", "PMTOKEN1", 9995, "USD", "BROWSERINFO", "SIGNUP-CARD-o1-1", "https://x.example/cb"), default);

        r.Ok.Should().BeTrue();
        r.Value!.State.Should().Be("pending");
        r.Value.RequiredAction.Should().Be("device_fingerprint");

        var (peticion, cuerpo) = h.Llamadas.Single();
        peticion.Method.Should().Be(HttpMethod.Post);
        peticion.RequestUri!.AbsoluteUri.Should().Be("https://core.spreedly.com/v1/sca/providers/SCAPROV1/authenticate.json");
        var t = JsonDocument.Parse(cuerpo).RootElement.GetProperty("transaction");
        t.GetProperty("payment_method_token").GetString().Should().Be("PMTOKEN1");
        t.GetProperty("amount").GetInt64().Should().Be(9995);
        t.GetProperty("currency_code").GetString().Should().Be("USD");
        t.GetProperty("browser_info").GetString().Should().Be("BROWSERINFO");
        t.GetProperty("order_id").GetString().Should().Be("SIGNUP-CARD-o1-1");
        t.GetProperty("callback_url").GetString().Should().Be("https://x.example/cb");
    }

    [Fact]
    public async Task La_compra_lleva_el_token_de_3ds_y_retiene_la_tarjeta()
    {
        var h = new Manejador(_ => Json(HttpStatusCode.OK, Compra));
        var r = await Nuevo(h).Cliente.PurchaseAsync(Pedido(), default);

        r.Ok.Should().BeTrue();
        r.Value!.Token.Should().Be("TXPURCHASE1");
        r.Value.NetworkTransactionId.Should().Be("NTID-9");

        var (peticion, cuerpo) = h.Llamadas.Single();
        peticion.RequestUri!.AbsoluteUri.Should().Be("https://core.spreedly.com/v1/gateways/GWTOKEN1/purchase.json");
        var t = JsonDocument.Parse(cuerpo).RootElement.GetProperty("transaction");
        t.GetProperty("sca_authentication_token").GetString().Should().Be("SCATOKEN1");
        t.GetProperty("retain_on_success").GetBoolean().Should().BeTrue();
        t.GetProperty("amount").GetInt64().Should().Be(9995);
    }

    [Fact]
    public async Task Un_rechazo_explicito_es_Declined_y_trae_la_transaccion()
    {
        var h = new Manejador(_ => Json(HttpStatusCode.UnprocessableEntity,
            """{"transaction":{"token":"TXP2","state":"gateway_processing_failed","succeeded":false,"message":"Declined"}}"""));

        var r = await Nuevo(h).Cliente.PurchaseAsync(Pedido(), default);

        r.Outcome.Should().Be(SpreedlyOutcome.Declined);
        r.ErrorCode.Should().Be(SpreedlyErrorCodes.Declined);
        r.Value!.Token.Should().Be("TXP2");
    }

    [Fact]
    public async Task Resultado_desconocido_del_gateway_es_Unknown()
    {
        var h = new Manejador(_ => Json(HttpStatusCode.OK,
            """{"transaction":{"token":"TXP3","state":"gateway_processing_result_unknown","succeeded":false}}"""));

        var r = await Nuevo(h).Cliente.PurchaseAsync(Pedido(), default);

        r.Outcome.Should().Be(SpreedlyOutcome.Unknown);
        r.ErrorCode.Should().Be(SpreedlyErrorCodes.ResultUnknown);
    }

    [Fact]
    public async Task Un_500_sin_json_en_una_compra_es_Unknown_no_Declined()
    {
        var h = new Manejador(_ => new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("<html>proxy</html>") });
        var r = await Nuevo(h).Cliente.PurchaseAsync(Pedido(), default);

        r.Outcome.Should().Be(SpreedlyOutcome.Unknown);
        r.HttpStatus.Should().Be(502);
    }

    [Fact]
    public async Task Un_timeout_en_una_compra_es_Unknown()
    {
        var log = new LogEnLista();
        var cliente = new SpreedlyClient(new HttpClient(new Lanza(new TaskCanceledException("timeout"))), new Credenciales(Buenas), new SpreedlyOptions(), log);

        var r = await cliente.PurchaseAsync(Pedido(), default);

        r.Outcome.Should().Be(SpreedlyOutcome.Unknown);
        r.ErrorCode.Should().Be(SpreedlyErrorCodes.Transport);
    }

    [Fact]
    public async Task Lee_la_autenticacion_3ds_sin_copiar_el_cavv()
    {
        var h = new Manejador(_ => Json(HttpStatusCode.OK, """
            {"transaction":{"token":"TXAUTH1","state":"succeeded","succeeded":true,"amount":9995,"currency_code":"USD",
             "order_id":"SIGNUP-CARD-o1-1","payment_method":{"token":"PMTOKEN1"},
             "sca_authentication":{"token":"SCATOKEN1","state":"succeeded","flow_performed":"challenge","ecommerce_indicator":"05",
               "transaction_status":"Y","three_ds_version":"2.2.0","authentication_value":"AAABBBCCC="}}}
            """));

        var r = await Nuevo(h).Cliente.GetTransactionAsync("TXAUTH1", default);

        r.Ok.Should().BeTrue();
        var sca = r.Value!.Sca!;
        sca.Token.Should().Be("SCATOKEN1");
        sca.Eci.Should().Be("05", "ecommerce_indicator es un alias de eci");
        sca.TransStatus.Should().Be("Y", "transaction_status es un alias de trans_status");
        sca.MessageVersion.Should().Be("2.2.0");
        sca.FlowPerformed.Should().Be("challenge");
        sca.HasAuthenticationValue.Should().BeTrue();
        r.Value.PaymentMethodToken.Should().Be("PMTOKEN1");
        r.Value.AmountMinor.Should().Be(9995);
        r.Value.ToString().Should().NotContain("AAABBBCCC");
    }

    [Theory]
    [InlineData("../admin")]
    [InlineData("abc")]
    [InlineData("tok en")]
    public async Task Un_token_con_forma_rara_no_llega_a_la_ruta(string token)
    {
        var h = new Manejador(_ => throw new InvalidOperationException("no debía llamar"));
        var r = await Nuevo(h).Cliente.GetTransactionAsync(token, default);

        r.Outcome.Should().Be(SpreedlyOutcome.Invalid);
        r.ErrorCode.Should().Be(SpreedlyErrorCodes.InvalidToken);
        h.Llamadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_medio_de_pago_que_no_existe_es_NotFound()
    {
        var h = new Manejador(_ => Json(HttpStatusCode.NotFound, """{"errors":[{"key":"errors.payment_method_not_found"}]}"""));
        var r = await Nuevo(h).Cliente.GetPaymentMethodAsync("PMTOKEN404", default);

        r.Outcome.Should().Be(SpreedlyOutcome.Invalid);
        r.ErrorCode.Should().Be(SpreedlyErrorCodes.NotFound);
    }

    [Fact]
    public async Task Lee_el_medio_de_pago()
    {
        var h = new Manejador(_ => Json(HttpStatusCode.OK, """
            {"payment_method":{"token":"PMTOKEN1","payment_method_type":"credit_card","card_type":"master",
             "first_six_digits":"555555","last_four_digits":"4444","month":3,"year":2029,"storage_state":"cached","test":false}}
            """));

        var r = await Nuevo(h).Cliente.GetPaymentMethodAsync("PMTOKEN1", default);

        r.Ok.Should().BeTrue();
        r.Value.Should().Be(new SpreedlyPaymentMethod("PMTOKEN1", "credit_card", "master", "555555", "4444", 3, 2029, "cached", false));
    }

    [Fact]
    public async Task Lista_las_transacciones_del_medio_de_pago_de_la_mas_reciente_a_la_mas_antigua()
    {
        var h = new Manejador(_ => Json(HttpStatusCode.OK, """
            {"transactions":[{"token":"T2","state":"succeeded","succeeded":true,"transaction_type":"Purchase","order_id":"SIGNUP-CARD-o1-1"},
                             {"token":"T1","state":"succeeded","succeeded":true,"transaction_type":"Sca::Authentication"}]}
            """));

        var r = await Nuevo(h).Cliente.ListPaymentMethodTransactionsAsync("PMTOKEN1", default);

        r.Ok.Should().BeTrue();
        r.Value!.Select(t => t.Token).Should().Equal("T2", "T1");
        h.Llamadas.Single().Peticion.RequestUri!.AbsoluteUri
            .Should().Be("https://core.spreedly.com/v1/payment_methods/PMTOKEN1/transactions.json?order=desc");
    }

    [Fact]
    public async Task El_log_no_lleva_cuerpos_ni_secretos()
    {
        var h = new Manejador(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("detalle-interno") });
        var (cliente, log) = Nuevo(h);

        await cliente.AuthenticateAsync(new SpreedlyAuthenticateRequest("SCAPROV1", "PMTOKEN1", 100, "USD", "NAVEGADOR-SECRETO", "SIGNUP-CARD-o1-1", null), default);

        log.Lineas.Should().NotBeEmpty();
        string.Join("\n", log.Lineas).Should().NotContainAny("SECRETO1", "NAVEGADOR-SECRETO", "detalle-interno", "ENVKEY1");
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SpreedlyClientTests -nologo`
Expected: FAIL de compilación (`SpreedlyClient` no existe).

- [ ] **Step 3: Crear `SpreedlyClient.cs`**

```csharp
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace MLMConquerorGlobalEdition.Repository.Services.Spreedly;

/// <summary>
/// Cliente de la API Core de Spreedly (HttpClient tipado; el tope lo pone AddSpreedly). Basic auth con la
/// environment key y el access secret del entorno configurado (ApiCredential), leídos en cada llamada: rotar
/// una credencial en el admin no exige reiniciar.
///
/// Nunca lanza por el proveedor o la red: devuelve un <see cref="SpreedlyCall{T}"/>. En las llamadas que
/// mueven dinero (compra, void, crédito) solo es Declined lo que Spreedly dice que falló; timeout, 5xx,
/// respuesta ilegible y gateway_processing_result_unknown son Unknown.
///
/// No registra cuerpos, cabeceras ni rutas (llevan tokens): solo la operación, el HTTP y el código.
/// </summary>
public sealed class SpreedlyClient : ISpreedlyClient
{
    private static readonly JsonSerializerOptions WriteJson = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions ReadJson = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling              = JsonNumberHandling.AllowReadingFromString
    };

    private readonly HttpClient                 _http;
    private readonly ISpreedlyCredentialSource  _credentials;
    private readonly SpreedlyOptions            _options;
    private readonly ILogger<SpreedlyClient>    _logger;

    public SpreedlyClient(HttpClient http, ISpreedlyCredentialSource credentials, SpreedlyOptions options, ILogger<SpreedlyClient> logger)
    {
        _http        = http;
        _credentials = credentials;
        _options     = options;
        _logger      = logger;
    }

    public Task<SpreedlyCall<SpreedlyPaymentMethod>> GetPaymentMethodAsync(string paymentMethodToken, CancellationToken ct)
        => !SpreedlyTokens.IsValid(paymentMethodToken)
            ? Task.FromResult(InvalidToken<SpreedlyPaymentMethod>())
            : SendAsync("GetPaymentMethod", HttpMethod.Get, $"payment_methods/{paymentMethodToken}.json", null,
                text => MapPaymentMethod(JsonSerializer.Deserialize<PaymentMethodEnvelope>(text, ReadJson)?.PaymentMethod),
                _ => SpreedlyOutcome.Ok, ct);

    public Task<SpreedlyCall<SpreedlyTransaction>> AuthenticateAsync(SpreedlyAuthenticateRequest r, CancellationToken ct)
    {
        if (!SpreedlyTokens.IsValid(r.ScaProviderKey) || !SpreedlyTokens.IsValid(r.PaymentMethodToken))
            return Task.FromResult(InvalidToken<SpreedlyTransaction>());

        var body = JsonSerializer.Serialize(new
        {
            transaction = new Dictionary<string, object?>
            {
                ["payment_method_token"] = r.PaymentMethodToken,
                ["amount"]               = r.AmountMinor,
                ["currency_code"]        = r.CurrencyCode,
                ["browser_info"]         = r.BrowserInfo,
                ["order_id"]             = r.OrderId,
                ["callback_url"]         = string.IsNullOrWhiteSpace(r.CallbackUrl) ? null : r.CallbackUrl
            }.Where(kv => kv.Value is not null).ToDictionary(kv => kv.Key, kv => kv.Value)
        }, WriteJson);

        return SendTransactionAsync("Authenticate", HttpMethod.Post, $"sca/providers/{r.ScaProviderKey}/authenticate.json", body, money: false, ct);
    }

    public Task<SpreedlyCall<SpreedlyTransaction>> CompleteAsync(string transactionToken, CancellationToken ct)
        => !SpreedlyTokens.IsValid(transactionToken)
            ? Task.FromResult(InvalidToken<SpreedlyTransaction>())
            : SendTransactionAsync("Complete", HttpMethod.Post, $"transactions/{transactionToken}/complete.json", "{}", money: false, ct);

    public Task<SpreedlyCall<SpreedlyTransaction>> GetTransactionAsync(string transactionToken, CancellationToken ct)
        => !SpreedlyTokens.IsValid(transactionToken)
            ? Task.FromResult(InvalidToken<SpreedlyTransaction>())
            : SendTransactionAsync("GetTransaction", HttpMethod.Get, $"transactions/{transactionToken}.json", null, money: false, ct);

    public Task<SpreedlyCall<SpreedlyTransaction>> PurchaseAsync(SpreedlyPurchaseRequest r, CancellationToken ct)
    {
        if (!SpreedlyTokens.IsValid(r.GatewayToken) || !SpreedlyTokens.IsValid(r.PaymentMethodToken)
            || !SpreedlyTokens.IsValid(r.ScaAuthenticationToken))
            return Task.FromResult(InvalidToken<SpreedlyTransaction>());

        var body = JsonSerializer.Serialize(new
        {
            transaction = new Dictionary<string, object?>
            {
                ["payment_method_token"]     = r.PaymentMethodToken,
                ["amount"]                   = r.AmountMinor,
                ["currency_code"]            = r.CurrencyCode,
                ["order_id"]                 = r.OrderId,
                ["description"]              = r.Description,
                ["sca_authentication_token"] = r.ScaAuthenticationToken,
                ["retain_on_success"]        = r.RetainOnSuccess
            }
        }, WriteJson);

        return SendTransactionAsync("Purchase", HttpMethod.Post, $"gateways/{r.GatewayToken}/purchase.json", body, money: true, ct);
    }

    public Task<SpreedlyCall<IReadOnlyList<SpreedlyTransaction>>> ListPaymentMethodTransactionsAsync(string paymentMethodToken, CancellationToken ct)
        => !SpreedlyTokens.IsValid(paymentMethodToken)
            ? Task.FromResult(InvalidToken<IReadOnlyList<SpreedlyTransaction>>())
            : SendAsync<IReadOnlyList<SpreedlyTransaction>>("ListTransactions", HttpMethod.Get,
                $"payment_methods/{paymentMethodToken}/transactions.json?order=desc", null,
                text => JsonSerializer.Deserialize<TransactionListEnvelope>(text, ReadJson)?.Transactions?
                            .Select(MapTransaction).OfType<SpreedlyTransaction>().ToList(),
                _ => SpreedlyOutcome.Ok, ct);

    public Task<SpreedlyCall<SpreedlyTransaction>> VoidAsync(string transactionToken, CancellationToken ct)
        => !SpreedlyTokens.IsValid(transactionToken)
            ? Task.FromResult(InvalidToken<SpreedlyTransaction>())
            : SendTransactionAsync("Void", HttpMethod.Post, $"transactions/{transactionToken}/void.json", "{}", money: true, ct);

    public Task<SpreedlyCall<SpreedlyTransaction>> CreditAsync(string transactionToken, long? amountMinor, string? currencyCode, CancellationToken ct)
    {
        if (!SpreedlyTokens.IsValid(transactionToken))
            return Task.FromResult(InvalidToken<SpreedlyTransaction>());

        var body = amountMinor is null
            ? "{}"
            : JsonSerializer.Serialize(new { transaction = new { amount = amountMinor, currency_code = currencyCode } }, WriteJson);

        return SendTransactionAsync("Credit", HttpMethod.Post, $"transactions/{transactionToken}/credit.json", body, money: true, ct);
    }

    // ── Envío ───────────────────────────────────────────────────────────────

    private Task<SpreedlyCall<SpreedlyTransaction>> SendTransactionAsync(
        string operation, HttpMethod method, string relative, string? body, bool money, CancellationToken ct)
        => SendAsync(operation, method, relative, body,
            text => MapTransaction(JsonSerializer.Deserialize<TransactionEnvelope>(text, ReadJson)?.Transaction),
            tx => !money
                ? SpreedlyOutcome.Ok
                : tx.Succeeded ? SpreedlyOutcome.Ok
                : SpreedlyStates.IsNoCharge(tx.State) ? SpreedlyOutcome.Declined
                : SpreedlyOutcome.Unknown,
            ct, money);

    private async Task<SpreedlyCall<T>> SendAsync<T>(
        string operation, HttpMethod method, string relative, string? body,
        Func<string, T?> map, Func<T, SpreedlyOutcome> classify, CancellationToken ct, bool money = false)
        where T : class
    {
        // Sin respuesta legible, lo que mueve dinero es Unknown (pudo cobrarse); lo demás, Invalid.
        var unreadable = money ? SpreedlyOutcome.Unknown : SpreedlyOutcome.Invalid;

        var credentials = await _credentials.GetAsync(ct);
        if (credentials is null || !Uri.TryCreate(EnsureSlash(_options.BaseUrl), UriKind.Absolute, out var baseUri)
            || baseUri.Scheme != Uri.UriSchemeHttps)
        {
            _logger.LogWarning("Spreedly {Operation}: sin credenciales del entorno {Environment} o BaseUrl no https; no se llama.",
                operation, _options.CredentialEnvironment);
            return SpreedlyCall<T>.Fail(SpreedlyOutcome.NotConfigured, 0, SpreedlyErrorCodes.NotConfigured, 0);
        }

        var sw = Stopwatch.StartNew();
        int    status;
        string text;
        try
        {
            using var request = new HttpRequestMessage(method, new Uri(baseUri, relative));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.EnvironmentKey}:{credentials.AccessSecret}")));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (body is not null)
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request, ct);
            status = (int)response.StatusCode;
            text   = await response.Content.ReadAsStringAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return SpreedlyCall<T>.Fail(money ? SpreedlyOutcome.Unknown : SpreedlyOutcome.Invalid, 0, SpreedlyErrorCodes.Cancelled, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Spreedly {Operation}: sin respuesta ({Error}).", operation, ex.GetType().Name);
            return SpreedlyCall<T>.Fail(money ? SpreedlyOutcome.Unknown : SpreedlyOutcome.Invalid, 0, SpreedlyErrorCodes.Transport, sw.ElapsedMilliseconds);
        }

        T? value = null;
        try { value = map(text); }
        catch (JsonException) { }

        if (value is null)
        {
            var (outcome, code) = status switch
            {
                404       => (SpreedlyOutcome.Invalid, SpreedlyErrorCodes.NotFound),
                401 or 403 => (SpreedlyOutcome.NotConfigured, $"HTTP_{status}"),
                _         => (unreadable, SpreedlyErrorCodes.BadResponse)
            };
            _logger.LogWarning("Spreedly {Operation}: {Status} {Code} (respuesta sin la forma esperada).", operation, status, code);
            return SpreedlyCall<T>.Fail(outcome, status, code, sw.ElapsedMilliseconds);
        }

        if (value is SpreedlyTransaction tx && tx.State == SpreedlyStates.ResultUnknown)
        {
            _logger.LogWarning("Spreedly {Operation}: {Status} con resultado desconocido del gateway.", operation, status);
            return SpreedlyCall<T>.Fail(SpreedlyOutcome.Unknown, status, SpreedlyErrorCodes.ResultUnknown, sw.ElapsedMilliseconds, value);
        }

        var classified = classify(value);
        if (classified == SpreedlyOutcome.Ok && status is >= 200 and < 300 || classified == SpreedlyOutcome.Ok && !money)
            return SpreedlyCall<T>.Success(value, status, sw.ElapsedMilliseconds);

        var errorCode = classified switch
        {
            SpreedlyOutcome.Declined => SpreedlyErrorCodes.Declined,
            SpreedlyOutcome.Unknown  => SpreedlyErrorCodes.ResultUnknown,
            _                        => $"HTTP_{status}"
        };
        _logger.LogInformation("Spreedly {Operation}: {Status} {Code}.", operation, status, errorCode);
        return SpreedlyCall<T>.Fail(classified == SpreedlyOutcome.Ok ? unreadable : classified, status, errorCode, sw.ElapsedMilliseconds, value);
    }

    private static SpreedlyCall<T> InvalidToken<T>() where T : class
        => SpreedlyCall<T>.Fail(SpreedlyOutcome.Invalid, 0, SpreedlyErrorCodes.InvalidToken, 0);

    private static string EnsureSlash(string? url) => (url ?? string.Empty).Trim().TrimEnd('/') + "/";

    // ── Mapeo ───────────────────────────────────────────────────────────────

    private static SpreedlyPaymentMethod? MapPaymentMethod(PaymentMethodWire? p)
        => p?.Token is not { Length: > 0 } token
            ? null
            : new SpreedlyPaymentMethod(token, p.PaymentMethodType ?? string.Empty, p.CardType, p.FirstSixDigits,
                p.LastFourDigits, p.Month ?? 0, p.Year ?? 0, p.StorageState ?? string.Empty, p.Test ?? false);

    private static SpreedlyTransaction? MapTransaction(TransactionWire? t)
    {
        if (t?.Token is not { Length: > 0 } token || string.IsNullOrWhiteSpace(t.State))
            return null;

        var s = t.ScaAuthentication;
        var sca = s is null ? null : new SpreedlyScaAuthentication(
            s.Token, s.State, s.FlowPerformed,
            s.Eci ?? s.EcommerceIndicator,
            s.TransStatus ?? s.TransactionStatus,
            s.MessageVersion ?? s.ThreeDsVersion,
            !string.IsNullOrWhiteSpace(s.AuthenticationValue));

        return new SpreedlyTransaction(
            token, t.State.Trim(), t.Succeeded ?? false, t.TransactionType, t.Amount, t.CurrencyCode, t.OrderId,
            t.PaymentMethodToken ?? t.PaymentMethod?.Token,
            t.RequiredAction, t.DeviceFingerprintForm, t.ChallengeForm, t.ChallengeUrl, t.CheckoutForm, t.CheckoutUrl,
            t.GatewayTransactionId, t.NetworkTransactionId, t.Message, sca);
    }

    // ── Forma del cable (API Core v1 de Spreedly) ────────────────────────────

    private sealed class PaymentMethodEnvelope
    {
        [JsonPropertyName("payment_method")] public PaymentMethodWire? PaymentMethod { get; set; }
    }

    private sealed class PaymentMethodWire
    {
        [JsonPropertyName("token")]               public string? Token             { get; set; }
        [JsonPropertyName("payment_method_type")] public string? PaymentMethodType { get; set; }
        [JsonPropertyName("card_type")]           public string? CardType          { get; set; }
        [JsonPropertyName("first_six_digits")]    public string? FirstSixDigits    { get; set; }
        [JsonPropertyName("last_four_digits")]    public string? LastFourDigits    { get; set; }
        [JsonPropertyName("month")]               public int?    Month             { get; set; }
        [JsonPropertyName("year")]                public int?    Year              { get; set; }
        [JsonPropertyName("storage_state")]       public string? StorageState      { get; set; }
        [JsonPropertyName("test")]                public bool?   Test              { get; set; }
    }

    private sealed class TransactionEnvelope
    {
        [JsonPropertyName("transaction")] public TransactionWire? Transaction { get; set; }
    }

    private sealed class TransactionListEnvelope
    {
        [JsonPropertyName("transactions")] public List<TransactionWire>? Transactions { get; set; }
    }

    private sealed class TransactionWire
    {
        [JsonPropertyName("token")]                   public string?            Token                 { get; set; }
        [JsonPropertyName("state")]                   public string?            State                 { get; set; }
        [JsonPropertyName("succeeded")]               public bool?              Succeeded             { get; set; }
        [JsonPropertyName("transaction_type")]        public string?            TransactionType       { get; set; }
        [JsonPropertyName("amount")]                  public long?              Amount                { get; set; }
        [JsonPropertyName("currency_code")]           public string?            CurrencyCode          { get; set; }
        [JsonPropertyName("order_id")]                public string?            OrderId               { get; set; }
        [JsonPropertyName("payment_method_token")]    public string?            PaymentMethodToken    { get; set; }
        [JsonPropertyName("payment_method")]          public PaymentMethodWire? PaymentMethod         { get; set; }
        [JsonPropertyName("required_action")]         public string?            RequiredAction        { get; set; }
        [JsonPropertyName("device_fingerprint_form")] public string?            DeviceFingerprintForm { get; set; }
        [JsonPropertyName("challenge_form")]          public string?            ChallengeForm         { get; set; }
        [JsonPropertyName("challenge_url")]           public string?            ChallengeUrl          { get; set; }
        [JsonPropertyName("checkout_form")]           public string?            CheckoutForm          { get; set; }
        [JsonPropertyName("checkout_url")]            public string?            CheckoutUrl           { get; set; }
        [JsonPropertyName("gateway_transaction_id")]  public string?            GatewayTransactionId  { get; set; }
        [JsonPropertyName("network_transaction_id")]  public string?            NetworkTransactionId  { get; set; }
        [JsonPropertyName("message")]                 public string?            Message               { get; set; }
        [JsonPropertyName("sca_authentication")]      public ScaWire?           ScaAuthentication     { get; set; }
    }

    private sealed class ScaWire
    {
        [JsonPropertyName("token")]                public string? Token               { get; set; }
        [JsonPropertyName("state")]                public string? State               { get; set; }
        [JsonPropertyName("flow_performed")]       public string? FlowPerformed       { get; set; }
        [JsonPropertyName("eci")]                  public string? Eci                 { get; set; }
        [JsonPropertyName("ecommerce_indicator")]  public string? EcommerceIndicator  { get; set; }
        [JsonPropertyName("trans_status")]         public string? TransStatus         { get; set; }
        [JsonPropertyName("transaction_status")]   public string? TransactionStatus   { get; set; }
        [JsonPropertyName("message_version")]      public string? MessageVersion      { get; set; }
        [JsonPropertyName("three_ds_version")]     public string? ThreeDsVersion      { get; set; }
        [JsonPropertyName("authentication_value")] public string? AuthenticationValue { get; set; }
    }
}
```

> El `ToString()` de los `record` imprime todas las propiedades: por eso el CAVV no está en `SpreedlyScaAuthentication` (solo `HasAuthenticationValue`), y la prueba `Lee_la_autenticacion_3ds_sin_copiar_el_cavv` lo vigila.

- [ ] **Step 4: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SpreedlyClientTests -nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Spreedly/SpreedlyClient.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Spreedly/SpreedlyClientTests.cs
git commit -m "Cliente HTTP de Spreedly con Basic auth del entorno: autenticacion SCA, compra con token de 3DS, consulta, listado, void y credito; un resultado dudoso nunca sale como rechazo y el log no lleva cuerpos"
```

---

### Task 5: Simulador de Spreedly para Development

**Files:**
- Create: `MLMConquerorGlobalEdition.SharedKernel/Billing/SpreedlyFakeScenarios.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Spreedly/FakeSpreedlyClient.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Spreedly/FakeSpreedlyClientTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using MLMConquerorGlobalEdition.Repository.Services.Spreedly;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Spreedly;

/// <summary>
/// El simulador (Spreedly:UseFake, solo Development). El escenario va en el token del medio de pago, así
/// el navegador elige qué pasa sin teclear un número de tarjeta.
/// </summary>
public class FakeSpreedlyClientTests
{
    private readonly FakeSpreedlyClient _fake = new();

    private async Task<SpreedlyTransaction> Autenticar(string escenario, long importe = 9995)
    {
        var pm = SpreedlyFakeScenarios.Token(escenario);
        var r = await _fake.AuthenticateAsync(new SpreedlyAuthenticateRequest("fake_sca_provider", pm, importe, "USD", "simulated", "SIGNUP-CARD-o1-1", null), default);
        r.Ok.Should().BeTrue();
        return r.Value!;
    }

    [Fact]
    public async Task Sin_friccion_autentica_con_shift()
    {
        var t = await Autenticar(SpreedlyFakeScenarios.Frictionless);
        t.State.Should().Be(SpreedlyStates.Succeeded);
        t.Sca!.Eci.Should().Be("05");
        t.Sca.TransStatus.Should().Be("Y");
        t.Sca.Token.Should().StartWith("fake_sca_");
    }

    [Fact]
    public async Task El_challenge_queda_pendiente_hasta_aprobarlo()
    {
        var t = await Autenticar(SpreedlyFakeScenarios.Challenge);
        t.State.Should().Be(SpreedlyStates.Pending);
        t.RequiredAction.Should().Be("challenge");
        t.ChallengeUrl.Should().StartWith(FakeSpreedlyClient.Host);

        _fake.Resolve(t.Token, approve: true).Should().BeTrue();

        var despues = (await _fake.GetTransactionAsync(t.Token, default)).Value!;
        despues.State.Should().Be(SpreedlyStates.Succeeded);
        despues.Sca!.FlowPerformed.Should().Be("challenge");
    }

    [Fact]
    public async Task Rechazar_el_challenge_deja_la_autenticacion_fallida()
    {
        var t = await Autenticar(SpreedlyFakeScenarios.Challenge);
        _fake.Resolve(t.Token, approve: false).Should().BeTrue();
        (await _fake.GetTransactionAsync(t.Token, default)).Value!.State.Should().Be(SpreedlyStates.Failed);
    }

    [Fact]
    public async Task Sin_shift_autentica_pero_con_eci_07()
    {
        var t = await Autenticar(SpreedlyFakeScenarios.NoShift);
        t.State.Should().Be(SpreedlyStates.Succeeded);
        t.Sca!.Eci.Should().Be("07");
    }

    [Fact]
    public async Task La_compra_exige_el_token_de_esa_autenticacion_y_el_mismo_importe()
    {
        var t = await Autenticar(SpreedlyFakeScenarios.Frictionless);
        var pm = t.PaymentMethodToken!;

        var otroImporte = await _fake.PurchaseAsync(new SpreedlyPurchaseRequest("fake_gateway_NmiSpreedly", pm, 1, "USD", "SIGNUP-CARD-o1-1", "x", t.Sca!.Token!, true), default);
        otroImporte.Outcome.Should().Be(SpreedlyOutcome.Declined);

        var buena = await _fake.PurchaseAsync(new SpreedlyPurchaseRequest("fake_gateway_NmiSpreedly", pm, 9995, "USD", "SIGNUP-CARD-o1-1", "x", t.Sca.Token!, true), default);
        buena.Ok.Should().BeTrue();
        buena.Value!.NetworkTransactionId.Should().StartWith("fake_ntid_");
    }

    [Fact]
    public async Task Declined_rechaza_la_compra_y_unknown_la_deja_en_el_aire()
    {
        var d = await Autenticar(SpreedlyFakeScenarios.Declined);
        (await _fake.PurchaseAsync(new("fake_gateway_NmiSpreedly", d.PaymentMethodToken!, 9995, "USD", "o", "x", d.Sca!.Token!, true), default))
            .Outcome.Should().Be(SpreedlyOutcome.Declined);

        var u = await Autenticar(SpreedlyFakeScenarios.Unknown);
        var compra = await _fake.PurchaseAsync(new("fake_gateway_NmiSpreedly", u.PaymentMethodToken!, 9995, "USD", "SIGNUP-CARD-o2-1", "x", u.Sca!.Token!, true), default);
        compra.Outcome.Should().Be(SpreedlyOutcome.Unknown);

        var lista = await _fake.ListPaymentMethodTransactionsAsync(u.PaymentMethodToken!, default);
        lista.Value!.Should().ContainSingle(x => x.OrderId == "SIGNUP-CARD-o2-1" && x.State == SpreedlyStates.ResultUnknown);

        _fake.Resolve(compra.Value!.Token, approve: true).Should().BeTrue("el dev endpoint también resuelve una compra en el aire");
        (await _fake.ListPaymentMethodTransactionsAsync(u.PaymentMethodToken!, default)).Value!
            .Single(x => x.OrderId == "SIGNUP-CARD-o2-1").Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Un_token_que_no_es_del_simulador_no_existe()
        => (await _fake.GetPaymentMethodAsync("tok_real_123456", default)).ErrorCode.Should().Be(SpreedlyErrorCodes.NotFound);

    [Fact]
    public void Los_escenarios_se_leen_del_token()
    {
        SpreedlyFakeScenarios.TryParse(SpreedlyFakeScenarios.Token(SpreedlyFakeScenarios.Challenge), out var e).Should().BeTrue();
        e.Should().Be(SpreedlyFakeScenarios.Challenge);
        SpreedlyFakeScenarios.TryParse("fake_inventado_123", out _).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter FakeSpreedlyClientTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `SpreedlyFakeScenarios.cs` (SharedKernel: lo usan el simulador y el componente en modo simulado)**

```csharp
namespace MLMConquerorGlobalEdition.SharedKernel.Billing;

/// <summary>
/// Escenarios del simulador de Spreedly (solo Development, Spreedly:UseFake). Van dentro del token del
/// medio de pago, "fake_{escenario}_{guid}": el componente en modo simulado lo fabrica sin iFrame y sin que
/// nadie teclee un número de tarjeta. Un token así nunca existe en un entorno real de Spreedly.
/// </summary>
public static class SpreedlyFakeScenarios
{
    public const string Frictionless = "frictionless";
    public const string Challenge    = "challenge";
    public const string Failed       = "failed";
    public const string NoShift      = "noshift";
    public const string Declined     = "declined";
    public const string Unknown      = "unknown";

    public const string TokenPrefix  = "fake_";

    public static readonly IReadOnlyList<string> All = [Frictionless, Challenge, Failed, NoShift, Declined, Unknown];

    public static string Token(string scenario) => $"{TokenPrefix}{scenario}_{Guid.NewGuid():N}";

    public static bool TryParse(string? token, out string scenario)
    {
        scenario = string.Empty;
        if (token is null || !token.StartsWith(TokenPrefix, StringComparison.Ordinal))
            return false;

        var rest = token[TokenPrefix.Length..];
        var cut  = rest.IndexOf('_');
        var name = cut < 0 ? rest : rest[..cut];
        if (!All.Contains(name))
            return false;

        scenario = name;
        return true;
    }
}
```

- [ ] **Step 4: Crear `FakeSpreedlyClient.cs`**

```csharp
using System.Collections.Concurrent;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Repository.Services.Spreedly;

/// <summary>
/// Spreedly de mentira para Development (Spreedly:UseFake = true; SpreedlyOptionsGuard impide arrancar con él
/// fuera de Development). Singleton en memoria: lo que hace cada tarjeta lo dice el escenario de su token
/// (<see cref="SpreedlyFakeScenarios"/>). Los challenges y las compras "en el aire" se resuelven con
/// POST /api/v1/dev/spreedly/{transactionToken}/{approve|reject} de SignupAPI (→ <see cref="Resolve"/>).
/// Todas las URLs son de spreedly.invalid: nada sale hacia un dominio real.
/// </summary>
public sealed class FakeSpreedlyClient : ISpreedlyClient
{
    public const string Host = "https://spreedly.invalid";

    private sealed class Tx
    {
        public required string Token     { get; init; }
        public required string Type      { get; init; }
        public required string PmToken   { get; init; }
        public required string Scenario  { get; init; }
        public required long   Amount    { get; init; }
        public required string Currency  { get; init; }
        public required string OrderId   { get; init; }
        public DateTime        CreatedAt { get; } = DateTime.Now;
        public string  State          { get; set; } = SpreedlyStates.Pending;
        public bool    Succeeded      { get; set; }
        public string? RequiredAction { get; set; }
        public string? Eci            { get; set; }
        public string? TransStatus    { get; set; }
        public string? Flow           { get; set; }
        public string? ScaToken       { get; set; }
        public string? NetworkTxId    { get; set; }
    }

    private readonly ConcurrentDictionary<string, Tx> _txs = new(StringComparer.Ordinal);

    public Task<SpreedlyCall<SpreedlyPaymentMethod>> GetPaymentMethodAsync(string token, CancellationToken ct)
    {
        if (!SpreedlyFakeScenarios.TryParse(token, out _))
            return Task.FromResult(SpreedlyCall<SpreedlyPaymentMethod>.Fail(SpreedlyOutcome.Invalid, 404, SpreedlyErrorCodes.NotFound, 1));

        var pm = new SpreedlyPaymentMethod(token, "credit_card", "visa", "411111", "1111", 12, DateTime.Now.Year + 3, "cached", true);
        return Task.FromResult(SpreedlyCall<SpreedlyPaymentMethod>.Success(pm, 200, 1));
    }

    public Task<SpreedlyCall<SpreedlyTransaction>> AuthenticateAsync(SpreedlyAuthenticateRequest r, CancellationToken ct)
    {
        if (!SpreedlyFakeScenarios.TryParse(r.PaymentMethodToken, out var scenario))
            return Task.FromResult(SpreedlyCall<SpreedlyTransaction>.Fail(SpreedlyOutcome.Invalid, 404, SpreedlyErrorCodes.NotFound, 1));

        var tx = new Tx
        {
            Token = $"fake_auth_{Guid.NewGuid():N}", Type = "Sca::Authentication", PmToken = r.PaymentMethodToken,
            Scenario = scenario, Amount = r.AmountMinor, Currency = r.CurrencyCode, OrderId = r.OrderId
        };

        switch (scenario)
        {
            case SpreedlyFakeScenarios.Challenge:
                tx.State = SpreedlyStates.Pending;
                tx.RequiredAction = "challenge";
                break;
            case SpreedlyFakeScenarios.Failed:
                Finish(tx, ok: false, eci: "07", status: "N", flow: "frictionless");
                break;
            case SpreedlyFakeScenarios.NoShift:
                Finish(tx, ok: true, eci: "07", status: "U", flow: "frictionless");
                break;
            default:
                Finish(tx, ok: true, eci: "05", status: "Y", flow: "frictionless");
                break;
        }

        _txs[tx.Token] = tx;
        return Task.FromResult(SpreedlyCall<SpreedlyTransaction>.Success(Map(tx), 200, 1));
    }

    public Task<SpreedlyCall<SpreedlyTransaction>> CompleteAsync(string transactionToken, CancellationToken ct)
        => GetTransactionAsync(transactionToken, ct);

    public Task<SpreedlyCall<SpreedlyTransaction>> GetTransactionAsync(string transactionToken, CancellationToken ct)
        => Task.FromResult(_txs.TryGetValue(transactionToken, out var tx)
            ? SpreedlyCall<SpreedlyTransaction>.Success(Map(tx), 200, 1)
            : SpreedlyCall<SpreedlyTransaction>.Fail(SpreedlyOutcome.Invalid, 404, SpreedlyErrorCodes.NotFound, 1));

    public Task<SpreedlyCall<SpreedlyTransaction>> PurchaseAsync(SpreedlyPurchaseRequest r, CancellationToken ct)
    {
        var auth = _txs.Values.FirstOrDefault(t => t.Type == "Sca::Authentication" && t.ScaToken == r.ScaAuthenticationToken);
        var purchase = new Tx
        {
            Token = $"fake_purchase_{Guid.NewGuid():N}", Type = "Purchase", PmToken = r.PaymentMethodToken,
            Scenario = auth?.Scenario ?? SpreedlyFakeScenarios.Declined, Amount = r.AmountMinor, Currency = r.CurrencyCode, OrderId = r.OrderId
        };
        _txs[purchase.Token] = purchase;

        // Como un gateway de verdad con 3DS: el token tiene que ser de una autenticación lograda de ESTA tarjeta,
        // por ESTE importe y moneda.
        var valid = auth is { State: SpreedlyStates.Succeeded }
                    && auth.PmToken == r.PaymentMethodToken
                    && auth.Amount == r.AmountMinor
                    && string.Equals(auth.Currency, r.CurrencyCode, StringComparison.OrdinalIgnoreCase);

        if (!valid || purchase.Scenario == SpreedlyFakeScenarios.Declined)
        {
            purchase.State = SpreedlyStates.ProcessingFailed;
            return Task.FromResult(SpreedlyCall<SpreedlyTransaction>.Fail(SpreedlyOutcome.Declined, 422, SpreedlyErrorCodes.Declined, 1, Map(purchase)));
        }

        if (purchase.Scenario == SpreedlyFakeScenarios.Unknown)
        {
            purchase.State = SpreedlyStates.ResultUnknown;
            return Task.FromResult(SpreedlyCall<SpreedlyTransaction>.Fail(SpreedlyOutcome.Unknown, 200, SpreedlyErrorCodes.ResultUnknown, 1, Map(purchase)));
        }

        purchase.State       = SpreedlyStates.Succeeded;
        purchase.Succeeded   = true;
        purchase.NetworkTxId = $"fake_ntid_{Guid.NewGuid():N}";
        return Task.FromResult(SpreedlyCall<SpreedlyTransaction>.Success(Map(purchase), 200, 1));
    }

    public Task<SpreedlyCall<IReadOnlyList<SpreedlyTransaction>>> ListPaymentMethodTransactionsAsync(string pm, CancellationToken ct)
    {
        IReadOnlyList<SpreedlyTransaction> list = _txs.Values.Where(t => t.PmToken == pm)
            .OrderByDescending(t => t.CreatedAt).Select(Map).ToList();
        return Task.FromResult(SpreedlyCall<IReadOnlyList<SpreedlyTransaction>>.Success(list, 200, 1));
    }

    public Task<SpreedlyCall<SpreedlyTransaction>> VoidAsync(string transactionToken, CancellationToken ct)
        => Reverse(transactionToken, "Void");

    public Task<SpreedlyCall<SpreedlyTransaction>> CreditAsync(string transactionToken, long? amountMinor, string? currencyCode, CancellationToken ct)
        => Reverse(transactionToken, "Credit");

    /// <summary>
    /// Resuelve una autenticación con challenge (aprobada o rechazada por "el banco") o una compra con
    /// resultado desconocido (cobrada o no). Devuelve false si el token no existe o ya no está en el aire.
    /// </summary>
    public bool Resolve(string transactionToken, bool approve)
    {
        if (!_txs.TryGetValue(transactionToken, out var tx))
            return false;

        if (tx.Type == "Sca::Authentication" && tx.State == SpreedlyStates.Pending)
        {
            if (approve) Finish(tx, ok: true, eci: "05", status: "Y", flow: "challenge");
            else         Finish(tx, ok: false, eci: "07", status: "N", flow: "challenge");
            tx.RequiredAction = null;
            return true;
        }

        if (tx.Type == "Purchase" && tx.State == SpreedlyStates.ResultUnknown)
        {
            tx.State       = approve ? SpreedlyStates.Succeeded : SpreedlyStates.ProcessingFailed;
            tx.Succeeded   = approve;
            tx.NetworkTxId = approve ? $"fake_ntid_{Guid.NewGuid():N}" : null;
            return true;
        }

        return false;
    }

    private Task<SpreedlyCall<SpreedlyTransaction>> Reverse(string token, string type)
    {
        if (!_txs.TryGetValue(token, out var original) || !original.Succeeded)
            return Task.FromResult(SpreedlyCall<SpreedlyTransaction>.Fail(SpreedlyOutcome.Declined, 422, SpreedlyErrorCodes.Declined, 1));

        var tx = new Tx
        {
            Token = $"fake_{type.ToLowerInvariant()}_{Guid.NewGuid():N}", Type = type, PmToken = original.PmToken,
            Scenario = original.Scenario, Amount = original.Amount, Currency = original.Currency, OrderId = original.OrderId,
            State = SpreedlyStates.Succeeded, Succeeded = true
        };
        _txs[tx.Token] = tx;
        return Task.FromResult(SpreedlyCall<SpreedlyTransaction>.Success(Map(tx), 200, 1));
    }

    private static void Finish(Tx tx, bool ok, string eci, string status, string flow)
    {
        tx.State       = ok ? SpreedlyStates.Succeeded : SpreedlyStates.Failed;
        tx.Succeeded   = ok;
        tx.Eci         = eci;
        tx.TransStatus = status;
        tx.Flow        = flow;
        tx.ScaToken    = ok ? $"fake_sca_{Guid.NewGuid():N}" : null;
    }

    private static SpreedlyTransaction Map(Tx t) => new(
        t.Token, t.State, t.Succeeded, t.Type, t.Amount, t.Currency, t.OrderId, t.PmToken,
        t.RequiredAction,
        DeviceFingerprintForm: null,
        ChallengeForm: null,
        ChallengeUrl: t.RequiredAction == "challenge" ? $"{Host}/challenge/{t.Token}" : null,
        CheckoutForm: null, CheckoutUrl: null,
        GatewayTransactionId: t.Type == "Purchase" ? $"fake_gw_{t.Token}" : null,
        NetworkTransactionId: t.NetworkTxId,
        Message: t.State,
        Sca: t.Type == "Sca::Authentication" && t.Eci is not null
            ? new SpreedlyScaAuthentication(t.ScaToken, t.State, t.Flow, t.Eci, t.TransStatus, "2.2.0", t.Succeeded)
            : null);
}
```

- [ ] **Step 5: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter FakeSpreedlyClientTests -nologo`
Expected: PASS (8 pruebas).

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedKernel/Billing/SpreedlyFakeScenarios.cs \
        MLMConquerorGlobalEdition.Repository/Services/Spreedly/FakeSpreedlyClient.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Spreedly/FakeSpreedlyClientTests.cs
git commit -m "Simulador de Spreedly para Development: escenarios sin friccion, challenge, fallo, sin liability shift, rechazo y resultado desconocido, con URLs en spreedly.invalid"
```

---

### Task 6: Registro de Spreedly en el contenedor

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Services/Spreedly/SpreedlyServiceCollectionExtensions.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Spreedly/SpreedlyRegistroTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Spreedly;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Spreedly;

/// <summary>Qué registra AddSpreedly: el simulador solo en Development, y si no el cliente real con su fuente de credenciales.</summary>
public class SpreedlyRegistroTests
{
    private static ServiceCollection Base()
    {
        var s = new ServiceCollection();
        s.AddLogging();
        s.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        // Lo que en el host pone AddGatewayCredentialProtection (key ring + certificado).
        s.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        return s;
    }

    [Fact]
    public void Con_el_simulador_registra_el_fake_y_sus_credenciales()
    {
        var s = Base();
        s.AddSpreedly(new SpreedlyOptions { Enabled = true, UseFake = true }, isDevelopment: true, cardEnabled: true);
        using var sp = s.BuildServiceProvider();

        sp.GetRequiredService<ISpreedlyClient>().Should().BeOfType<FakeSpreedlyClient>();
        sp.GetRequiredService<ISpreedlyClient>().Should().BeSameAs(sp.GetRequiredService<FakeSpreedlyClient>());
        sp.GetRequiredService<ISpreedlyCredentialSource>().Should().BeOfType<FakeSpreedlyCredentialSource>();
    }

    [Fact]
    public void Sin_simulador_registra_el_cliente_real()
    {
        var s = Base();
        s.AddSpreedly(new SpreedlyOptions { Enabled = true }, isDevelopment: false, cardEnabled: true);
        using var sp = s.BuildServiceProvider();
        using var scope = sp.CreateScope();

        scope.ServiceProvider.GetRequiredService<ISpreedlyClient>().Should().BeOfType<SpreedlyClient>();
        scope.ServiceProvider.GetRequiredService<ISpreedlyCredentialSource>().Should().BeOfType<SpreedlyCredentialSource>();
        sp.GetService<FakeSpreedlyClient>().Should().BeNull();
    }

    [Fact]
    public void El_simulador_fuera_de_Development_no_llega_a_registrarse()
    {
        var s = Base();
        var act = () => s.AddSpreedly(new SpreedlyOptions { UseFake = true }, isDevelopment: false);
        act.Should().Throw<InvalidOperationException>();
        s.Should().NotContain(d => d.ServiceType == typeof(FakeSpreedlyClient));
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SpreedlyRegistroTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `SpreedlyServiceCollectionExtensions.cs`**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MLMConquerorGlobalEdition.SharedKernel.Server.Services;

namespace MLMConquerorGlobalEdition.Repository.Services.Spreedly;

public static class SpreedlyServiceCollectionExtensions
{
    /// <summary>
    /// Registra las opciones (singleton) y el cliente: el simulador con sus credenciales si
    /// <see cref="SpreedlyOptions.UseFake"/>, o el real (HttpClient tipado con <see cref="SpreedlyOptions.Timeout"/>)
    /// con <see cref="SpreedlyCredentialSource"/>. Pasa la guarda de arranque antes de registrar nada.
    ///
    /// El cliente real descifra ApiCredential con GatewayCredentialProtector, que necesita el key ring compartido:
    /// el host llama ANTES a AddGatewayCredentialProtection (Billing ya lo hace dentro de AddPayoutServices;
    /// SignupAPI lo llama en su Program.cs). No se llama aquí para no configurar Data Protection dos veces.
    /// </summary>
    public static IServiceCollection AddSpreedly(
        this IServiceCollection services, SpreedlyOptions options, bool isDevelopment, bool cardEnabled = false)
    {
        SpreedlyOptionsGuard.ThrowIfInvalid(options, cardEnabled, isDevelopment);

        services.AddSingleton(options);

        if (options.UseFake)
        {
            services.AddSingleton<FakeSpreedlyClient>();
            services.AddSingleton<ISpreedlyClient>(sp => sp.GetRequiredService<FakeSpreedlyClient>());
            services.AddSingleton<ISpreedlyCredentialSource, FakeSpreedlyCredentialSource>();
            return services;
        }

        // Concreto a propósito (decisión I3): IEncryptionService puede ser otro cifrado en este host.
        services.TryAddScoped<GatewayCredentialProtector>();
        services.AddScoped<ISpreedlyCredentialSource, SpreedlyCredentialSource>();
        services.AddHttpClient<ISpreedlyClient, SpreedlyClient>(c => c.Timeout = options.Timeout);
        return services;
    }
}
```

- [ ] **Step 4: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter "SpreedlyRegistroTests|SpreedlyOptionsTests|SpreedlyPiezasTests|SpreedlyCredentialSourceTests|SpreedlyClientTests|FakeSpreedlyClientTests" -nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository/Services/Spreedly/SpreedlyServiceCollectionExtensions.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Spreedly/SpreedlyRegistroTests.cs
git commit -m "AddSpreedly: el simulador solo en Development y, si no, el cliente real con credenciales de ApiCredential descifradas por el protector de gateways"
```

---

### Task 7: Contratos compartidos del pago con tarjeta (SharedKernel)

**Files:**
- Create: `MLMConquerorGlobalEdition.SharedKernel/Billing/SpreedlyCardContracts.cs`
- Modify: `MLMConquerorGlobalEdition.SharedKernel/Billing/EntraxContracts.cs` (clase `SignupPaymentOptionsDto`)

- [ ] **Step 1: Crear `SpreedlyCardContracts.cs`**

```csharp
namespace MLMConquerorGlobalEdition.SharedKernel.Billing;

/// <summary>
/// Estados del intento de pago con tarjeta del alta tal como los ve la pantalla. Son los nombres de
/// <c>Domain.Entities.Orders.SignupCardPaymentStatus</c>; una prueba vigila que no se separen.
/// </summary>
public static class SignupCardStatuses
{
    public const string Authenticating = "Authenticating";
    public const string Charging       = "Charging";
    public const string Charged        = "Charged";
    public const string Paid           = "Paid";
    public const string Failed         = "Failed";
    public const string ToReview       = "ToReview";

    public static readonly IReadOnlyList<string> All = [Authenticating, Charging, Charged, Paid, Failed, ToReview];
}

/// <summary>
/// Lo que la pantalla manda al completar el alta con tarjeta: el token que devolvió el iFrame de Spreedly y
/// los datos del navegador que pide 3DS2 (Spreedly.ThreeDS.serialize). Ni número, ni CVV, ni marca, ni
/// importe: lo que describe la tarjeta lo pregunta el servidor a Spreedly y el importe es el del pedido.
/// </summary>
public sealed class SignupCardPaymentRequestDto
{
    public string PaymentMethodToken { get; set; } = string.Empty;
    public string BrowserInfo        { get; set; } = string.Empty;
}

/// <summary>
/// Lo que necesita <c>Spreedly.ThreeDS.Lifecycle</c> en el navegador para la huella del dispositivo o el
/// challenge. Son los formularios y la URL de 3DS que Spreedly devolvió: no son secretos ni dan acceso a nada.
/// </summary>
public sealed class SpreedlyThreeDsStepDto
{
    public string  TransactionToken      { get; set; } = string.Empty;
    public string  State                 { get; set; } = string.Empty;
    public string? RequiredAction        { get; set; }
    public string? DeviceFingerprintForm { get; set; }
    public string? ChallengeForm         { get; set; }
    public string? ChallengeUrl          { get; set; }
    public string? CheckoutForm          { get; set; }
    public string? CheckoutUrl           { get; set; }
}

/// <summary>
/// Dónde está el pago con tarjeta del alta. Lo devuelven completar el alta, terminar la autenticación, el
/// estado y cancelar. Solo lleva <see cref="ThreeDs"/> mientras el navegador tiene algo que hacer.
/// </summary>
public sealed class SignupCardStepDto
{
    /// <summary>Id de nuestro intento (GUID aleatorio). Junto con el id del alta es lo que piden los endpoints.</summary>
    public string PaymentRef { get; set; } = string.Empty;

    /// <summary>Uno de <see cref="SignupCardStatuses"/>.</summary>
    public string Status { get; set; } = SignupCardStatuses.Authenticating;

    /// <summary>Texto para la persona cuando el pago no siguió (Failed, ToReview). Nunca detalles internos.</summary>
    public string? Message { get; set; }

    public SpreedlyThreeDsStepDto? ThreeDs { get; set; }
}

/// <summary>
/// Lo que la pantalla recibe de cada llamada del pago con tarjeta: el paso, o un error para enseñar, o que
/// el alta ya estaba pagada (409 SIGNUP_ALREADY_SETTLED).
/// </summary>
public sealed record SignupCardStepResult(SignupCardStepDto? Step, string? Error, bool AlreadySettled = false)
{
    public static SignupCardStepResult Ok(SignupCardStepDto step) => new(step, null);
    public static SignupCardStepResult Fail(string error) => new(null, error);
    public static SignupCardStepResult Settled() => new(null, null, AlreadySettled: true);
}
```

- [ ] **Step 2: Añadir la clave de entorno a `SignupPaymentOptionsDto`**

En `MLMConquerorGlobalEdition.SharedKernel/Billing/EntraxContracts.cs`, dentro de `SignupPaymentOptionsDto`, debajo de `CardEnabled`:

```csharp
    /// <summary>
    /// Environment key de Spreedly para el iFrame (pública: es lo que identifica al comercio ante
    /// core.spreedly.com desde el navegador). Null si la tarjeta no se ofrece.
    /// </summary>
    public string? CardEnvironmentKey { get; set; }

    /// <summary>
    /// Development con el simulador de Spreedly: la pantalla no carga el iFrame y ofrece escenarios de
    /// prueba en vez de pedir un número de tarjeta.
    /// </summary>
    public bool CardSimulated { get; set; }
```

- [ ] **Step 3: Compilar**

Run: `dotnet build MLMConquerorGlobalEdition.SharedKernel -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedKernel/Billing/SpreedlyCardContracts.cs \
        MLMConquerorGlobalEdition.SharedKernel/Billing/EntraxContracts.cs
git commit -m "Contratos del pago con tarjeta del alta: token del iFrame y datos del navegador, paso de 3DS para el Lifecycle, estado y clave de entorno en payment-options"
```

---

### Task 8: Dominio, configuración y migración

**Files:**
- Create: `MLMConquerorGlobalEdition.Domain/Entities/Orders/SignupCardPaymentStatus.cs`
- Create: `MLMConquerorGlobalEdition.Domain/Entities/Orders/SignupCardPayment.cs`
- Create: `MLMConquerorGlobalEdition.Domain/Entities/Billing/SpreedlyApiCall.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Configurations/SignupCardPaymentConfiguration.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Configurations/Billing/SpreedlyApiCallConfiguration.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Spreedly/SpreedlyCallLog.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Context/AppDbContext.cs:99` (junto a `EntraxApiCalls`)
- Create: `MLMConquerorGlobalEdition.Repository/Migrations/*_SpreedlyTarjetaEnElAlta.cs` (vía `dotnet ef`)
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Card/SignupCardPaymentModelTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.Spreedly;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Card;

/// <summary>
/// Lo que el modelo garantiza por sí mismo: un order_id de Spreedly es de un solo intento, una transacción
/// de Spreedly es de un solo intento, un pedido lo consume como mucho un intento, y la fila lleva token de
/// concurrencia. EF InMemory no aplica los índices: aquí se comprueba que están declarados.
/// </summary>
public class SignupCardPaymentModelTests
{
    [Fact]
    public void Los_estados_del_dominio_y_los_de_la_pantalla_se_llaman_igual()
        => Enum.GetNames<SignupCardPaymentStatus>().Should().BeEquivalentTo(SignupCardStatuses.All);

    [Fact]
    public void Los_ids_externos_son_unicos()
    {
        using var db = InMemoryDbHelper.Create();
        var tipo = db.Model.FindEntityType(typeof(SignupCardPayment))!;

        foreach (var columna in new[]
                 {
                     nameof(SignupCardPayment.ExternalOrderId),
                     nameof(SignupCardPayment.ScaTransactionToken),
                     nameof(SignupCardPayment.PurchaseTransactionToken)
                 })
            tipo.GetIndexes().Should().Contain(i => i.IsUnique && i.Properties.Count == 1 && i.Properties[0].Name == columna, columna);
    }

    [Fact]
    public void Un_pedido_lo_consume_como_mucho_un_intento()
    {
        using var db = InMemoryDbHelper.Create();
        var indice = db.Model.FindEntityType(typeof(SignupCardPayment))!.GetIndexes()
            .Single(i => i.GetDatabaseName() == "UX_SignupCardPayments_OrderId_Consumed");
        indice.IsUnique.Should().BeTrue();
        indice.GetFilter().Should().Be("[Consumed] = 1");
    }

    [Fact]
    public void La_fila_lleva_token_de_concurrencia()
    {
        using var db = InMemoryDbHelper.Create();
        db.Model.FindEntityType(typeof(SignupCardPayment))!
            .FindProperty(nameof(SignupCardPayment.RowVersion))!.IsConcurrencyToken.Should().BeTrue();
    }

    [Fact]
    public void El_order_id_de_Spreedly_lleva_el_pedido_y_el_intento()
        => SignupCardPayment.BuildExternalOrderId("8f0c", 2).Should().Be("SIGNUP-CARD-8f0c-2");

    [Fact]
    public void Los_procesadores_planificados_se_guardan_y_se_leen_en_orden()
    {
        var p = new SignupCardPayment();
        p.SetPlannedProcessors([CardProcessor.CheckoutEUR, CardProcessor.NmiSpreedly]);
        p.PlannedProcessors.Should().Be("3,1");
        p.GetPlannedProcessors().Should().Equal(CardProcessor.CheckoutEUR, CardProcessor.NmiSpreedly);
    }

    [Fact]
    public void El_log_de_llamadas_no_tiene_columnas_para_cuerpos()
        => typeof(SpreedlyApiCall).GetProperties().Select(p => p.Name)
            .Should().NotContain(n => n.Contains("Body") || n.Contains("Request") || n.Contains("Response") || n.Contains("BrowserInfo"));

    [Fact]
    public void La_fila_del_log_sale_de_la_llamada()
    {
        var call = SpreedlyCall<SpreedlyTransaction>.Fail(SpreedlyOutcome.Declined, 422, SpreedlyErrorCodes.Declined, 37,
            new SpreedlyTransaction("TXP2", "gateway_processing_failed", false, "Purchase", 100, "USD", "SIGNUP-CARD-o-1",
                "PM1", null, null, null, null, null, null, null, null, "Declined", null));

        var fila = SpreedlyCallLog.From(SpreedlyCallLog.Purchase, call, null, "SIGNUP-CARD-o-1", new DateTime(2026, 10, 16, 9, 0, 0), "SignupCard:Browser");

        fila.Operation.Should().Be("Purchase");
        fila.TransactionToken.Should().Be("TXP2");
        fila.Outcome.Should().Be("Declined");
        fila.ProviderState.Should().Be("gateway_processing_failed");
        fila.HttpStatusCode.Should().Be(422);
        fila.DurationMs.Should().Be(37);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SignupCardPaymentModelTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `SignupCardPaymentStatus.cs`**

```csharp
namespace MLMConquerorGlobalEdition.Domain.Entities.Orders;

/// <summary>
/// Estado de un intento de pagar un alta con tarjeta (Spreedly, 3DS2 Global). Los nombres son los de
/// <c>SharedKernel.Billing.SignupCardStatuses</c>: la pantalla los recibe tal cual.
/// </summary>
public enum SignupCardPaymentStatus
{
    /// <summary>Autenticación 3DS pedida; esperando la huella del dispositivo, el challenge o su resultado. No se ha cobrado.</summary>
    Authenticating = 1,

    /// <summary>
    /// Autenticado con liability shift y reclamado para cobrar (UPDATE condicional: solo un camino llega aquí).
    /// La compra está en vuelo o terminó sin final conocido: se concilia, nunca se da por no cobrada.
    /// </summary>
    Charging = 2,

    /// <summary>Cobrado (PaymentHistory capturado) y pendiente de activar el alta.</summary>
    Charged = 3,

    /// <summary>Cobrado y el alta activada con este cobro. Final.</summary>
    Paid = 4,

    /// <summary>No se movió dinero: 3DS fallido o sin liability shift, rechazo explícito, abandono o cancelación. El pedido vuelve a Pending.</summary>
    Failed = 5,

    /// <summary>Hubo (o pudo haber) cobro y no se aplicó: alta ya activada por otro medio, resultado desconocido, cobro simulado.</summary>
    ToReview = 6
}
```

- [ ] **Step 4: Crear `SignupCardPayment.cs`**

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Orders;

/// <summary>
/// Un intento de pagar un alta con tarjeta. Un pedido puede tener varios (una tarjeta rechazada y otra),
/// pero solo uno lo consume: ScaTransactionToken y PurchaseTransactionToken son únicos y Consumed solo se
/// pone una vez, con el índice filtrado UX_SignupCardPayments_OrderId_Consumed debajo.
///
/// Lo que manda es el SERVIDOR: el importe es Orders.TotalAmount congelado en AmountUsd, y lo que se
/// autentica y se cobra (ChargeAmount en ChargeCurrency, por los procesadores de PlannedProcessors) lo
/// decidió el router al empezar. Nada de esto lo manda el navegador.
///
/// Del 3DS se guarda lo que sirve para soporte y disputas (ECI, trans_status, versión, flujo) y NO el
/// CAVV: solo si vino.
/// </summary>
public class SignupCardPayment : AuditChangesStringKey
{
    public string OrderId  { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;

    /// <summary>"SIGNUP-CARD-{OrderId}-{intento}". Único. Es el order_id que ve Spreedly.</summary>
    public string ExternalOrderId { get; set; } = string.Empty;

    public SignupCardPaymentStatus Status { get; set; } = SignupCardPaymentStatus.Authenticating;

    /// <summary>Total del pedido en USD al empezar, redondeado a 2 decimales.</summary>
    public decimal AmountUsd { get; set; }

    /// <summary>Lo que se autentica y se cobra: importe y moneda del paso primario del router.</summary>
    public decimal ChargeAmount   { get; set; }
    public string  ChargeCurrency { get; set; } = "USD";

    /// <summary>Bucket del router, para los contadores y GatewayChargeAttempts.</summary>
    public string RouteBucketKey { get; set; } = string.Empty;

    /// <summary>CardProcessor (int) separados por coma, en el orden del router: primario y fallbacks válidos.</summary>
    public string PlannedProcessors { get; set; } = string.Empty;

    /// <summary>Token del medio de pago en Spreedly (lo creó el iFrame). No es el número de la tarjeta.</summary>
    public string PaymentMethodToken { get; set; } = string.Empty;

    /// <summary>Marca, BIN, últimos 4 y caducidad tal como los dice Spreedly (GET payment_methods).</summary>
    public string CardBrand   { get; set; } = string.Empty;
    public string First6      { get; set; } = string.Empty;
    public string Last4       { get; set; } = string.Empty;
    public int    ExpiryMonth { get; set; }
    public int    ExpiryYear  { get; set; }

    /// <summary>Token de la transacción de autenticación SCA en Spreedly. Único.</summary>
    public string? ScaTransactionToken { get; set; }

    /// <summary>Token de la autenticación que se pasa a la compra (sca_authentication_token).</summary>
    public string? ScaAuthenticationToken { get; set; }

    public string? ThreeDsVersion  { get; set; }
    public string? FlowPerformed   { get; set; }
    public string? Eci             { get; set; }
    public string? TransStatus     { get; set; }
    public bool?   LiabilityShift  { get; set; }

    /// <summary>Spreedly devolvió un CAVV. El valor no se guarda.</summary>
    public bool AuthenticationValuePresent { get; set; }

    /// <summary>El procesador que cobró (CardProcessor.ToString()).</summary>
    public string? ProcessorUsed { get; set; }

    /// <summary>Token de la compra en Spreedly (GatewayTransactionId del PaymentHistory). Único.</summary>
    public string? PurchaseTransactionToken { get; set; }

    /// <summary>Id de la red de la tarjeta para cobros posteriores sin cliente (MIT).</summary>
    public string? NetworkTransactionId { get; set; }

    public string? PaymentHistoryId { get; set; }

    /// <summary>
    /// Cuándo empezó el intento (hora de servidor). Mide la caducidad de la autenticación. No se usa
    /// CreationDate porque el AuditInterceptor la reescribe al insertar.
    /// </summary>
    public DateTime StartedAt { get; set; }

    public DateTime? ChargingStartedAt { get; set; }
    public DateTime? ChargedAt         { get; set; }
    public DateTime? ActivatedAt       { get; set; }

    /// <summary>Este intento ya activó un alta. Nunca vuelve a false.</summary>
    public bool Consumed { get; set; }

    /// <summary>Última consulta a Spreedly. Estrangula la de la pantalla.</summary>
    public DateTime? LastCheckedAt { get; set; }

    public string? FailureCode { get; set; }
    public string? Notes       { get; set; }

    public static string BuildExternalOrderId(string orderId, int attempt) => $"SIGNUP-CARD-{orderId}-{attempt}";

    public void SetPlannedProcessors(IEnumerable<CardProcessor> processors)
        => PlannedProcessors = string.Join(",", processors.Select(p => (int)p));

    public IReadOnlyList<CardProcessor> GetPlannedProcessors()
        => PlannedProcessors.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var n) && Enum.IsDefined(typeof(CardProcessor), n) ? (CardProcessor?)n : null)
            .OfType<CardProcessor>()
            .ToList();
}
```

- [ ] **Step 5: Crear `SpreedlyApiCall.cs`**

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Billing;

/// <summary>
/// Una llamada a Spreedly o un callback de Spreedly. SOLO campos estructurados: ni cuerpos, ni cabeceras,
/// ni browser_info, ni el CAVV. El número de la tarjeta no lo tenemos nunca (iFrame).
/// </summary>
public class SpreedlyApiCall : AuditChangesLongKey
{
    /// <summary>GetPaymentMethod, Authenticate, Complete, GetTransaction, Purchase, ListTransactions, Callback…</summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>Token de la transacción de Spreedly (autenticación o compra), si lo hay.</summary>
    public string? TransactionToken { get; set; }

    /// <summary>Nuestro order_id (SIGNUP-CARD-{pedido}-{n}).</summary>
    public string? OrderReference { get; set; }

    /// <summary>0 si no hubo respuesta HTTP o es un callback entrante.</summary>
    public int HttpStatusCode { get; set; }

    /// <summary>Ok, Declined, Unknown, NotConfigured o Invalid.</summary>
    public string Outcome { get; set; } = string.Empty;

    public string? ErrorCode     { get; set; }

    /// <summary>Estado de la transacción en Spreedly (succeeded, pending, failed…).</summary>
    public string? ProviderState { get; set; }

    public int DurationMs { get; set; }
}
```

- [ ] **Step 6: Crear `SignupCardPaymentConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class SignupCardPaymentConfiguration : IEntityTypeConfiguration<SignupCardPayment>
{
    public void Configure(EntityTypeBuilder<SignupCardPayment> builder)
    {
        builder.HasKey(x => x.Id);

        // Pantalla, callback, barrido y admin pueden llegar a la vez: el segundo UPDATE de la fila choca.
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.Property(x => x.OrderId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ExternalOrderId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.AmountUsd).HasPrecision(18, 4);
        builder.Property(x => x.ChargeAmount).HasPrecision(18, 4);
        builder.Property(x => x.ChargeCurrency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.RouteBucketKey).IsRequired().HasMaxLength(200);
        builder.Property(x => x.PlannedProcessors).IsRequired().HasMaxLength(100);
        builder.Property(x => x.PaymentMethodToken).IsRequired().HasMaxLength(100);
        builder.Property(x => x.CardBrand).IsRequired().HasMaxLength(30);
        builder.Property(x => x.First6).IsRequired().HasMaxLength(6);
        builder.Property(x => x.Last4).IsRequired().HasMaxLength(4);
        builder.Property(x => x.ScaTransactionToken).HasMaxLength(100);
        builder.Property(x => x.ScaAuthenticationToken).HasMaxLength(100);
        builder.Property(x => x.ThreeDsVersion).HasMaxLength(10);
        builder.Property(x => x.FlowPerformed).HasMaxLength(20);
        builder.Property(x => x.Eci).HasMaxLength(2);
        builder.Property(x => x.TransStatus).HasMaxLength(1);
        builder.Property(x => x.ProcessorUsed).HasMaxLength(30);
        builder.Property(x => x.PurchaseTransactionToken).HasMaxLength(100);
        builder.Property(x => x.NetworkTransactionId).HasMaxLength(100);
        builder.Property(x => x.PaymentHistoryId).HasMaxLength(450);
        builder.Property(x => x.FailureCode).HasMaxLength(60);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        builder.HasIndex(x => x.ExternalOrderId).IsUnique();
        builder.HasIndex(x => x.ScaTransactionToken).IsUnique().HasFilter("[ScaTransactionToken] IS NOT NULL");
        builder.HasIndex(x => x.PurchaseTransactionToken).IsUnique().HasFilter("[PurchaseTransactionToken] IS NOT NULL");
        builder.HasIndex(x => x.OrderId);

        // Un pedido lo activa como mucho UN intento con tarjeta: la red debajo de SignupOrderClaim.
        builder.HasIndex(x => x.OrderId, "UX_SignupCardPayments_OrderId_Consumed")
               .IsUnique()
               .HasFilter("[Consumed] = 1");
        builder.HasIndex(x => new { x.Status, x.CreationDate });

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
```

- [ ] **Step 7: Crear `SpreedlyApiCallConfiguration.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Billing;

public class SpreedlyApiCallConfiguration : IEntityTypeConfiguration<SpreedlyApiCall>
{
    public void Configure(EntityTypeBuilder<SpreedlyApiCall> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Operation).IsRequired().HasMaxLength(30);
        builder.Property(x => x.TransactionToken).HasMaxLength(100);
        builder.Property(x => x.OrderReference).HasMaxLength(100);
        builder.Property(x => x.Outcome).IsRequired().HasMaxLength(20);
        builder.Property(x => x.ErrorCode).HasMaxLength(60);
        builder.Property(x => x.ProviderState).HasMaxLength(40);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);

        builder.HasIndex(x => x.CreationDate);
        builder.HasIndex(x => x.TransactionToken);
        builder.HasIndex(x => x.OrderReference);
    }
}
```

- [ ] **Step 8: Crear `SpreedlyCallLog.cs`**

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.Billing;

namespace MLMConquerorGlobalEdition.Repository.Services.Spreedly;

/// <summary>La fila de SpreedlyApiCalls de una llamada. Quien llama la añade al contexto y la guarda con lo suyo.</summary>
public static class SpreedlyCallLog
{
    public const string GetPaymentMethod = "GetPaymentMethod";
    public const string Authenticate     = "Authenticate";
    public const string Complete         = "Complete";
    public const string GetTransaction   = "GetTransaction";
    public const string Purchase         = "Purchase";
    public const string ListTransactions = "ListTransactions";
    public const string Callback         = "Callback";

    public static SpreedlyApiCall From<T>(
        string operation, SpreedlyCall<T> call, string? transactionToken, string? orderReference, DateTime now, string actor)
        where T : class
    {
        var tx = call.Value as SpreedlyTransaction;
        return new SpreedlyApiCall
        {
            Operation        = operation,
            TransactionToken = Cut(transactionToken ?? tx?.Token, 100),
            OrderReference   = Cut(orderReference ?? tx?.OrderId, 100),
            HttpStatusCode   = call.HttpStatus,
            Outcome          = call.Outcome.ToString(),
            ErrorCode        = Cut(call.ErrorCode, 60),
            ProviderState    = Cut(tx?.State, 40),
            DurationMs       = (int)Math.Min(int.MaxValue, Math.Max(0, call.ElapsedMs)),
            CreationDate     = now,
            CreatedBy        = Cut(actor, 100) ?? "spreedly"
        };
    }

    /// <summary>Un callback entrante: sin HTTP de salida ni duración.</summary>
    public static SpreedlyApiCall Incoming(string? transactionToken, string? state, string outcome, DateTime now)
        => new()
        {
            Operation        = Callback,
            TransactionToken = Cut(transactionToken, 100),
            Outcome          = outcome,
            ProviderState    = Cut(state, 40),
            CreationDate     = now,
            CreatedBy        = "spreedly-callback"
        };

    private static string? Cut(string? value, int max) => value is null ? null : value.Length <= max ? value : value[..max];
}
```

- [ ] **Step 9: Añadir los `DbSet` en `AppDbContext.cs`**

Debajo de `public DbSet<EntraxApiCall> EntraxApiCalls => Set<EntraxApiCall>();` (línea 99):

```csharp
    public DbSet<SignupCardPayment> SignupCardPayments => Set<SignupCardPayment>();
    public DbSet<SpreedlyApiCall> SpreedlyApiCalls => Set<SpreedlyApiCall>();
```

(`SignupCardPayment` está en `Domain.Entities.Orders` y `SpreedlyApiCall` en `Domain.Entities.Billing`; los dos `using` ya están en `AppDbContext.cs` por `EntraxPayment` y `ApiCredential`.)

- [ ] **Step 10: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SignupCardPaymentModelTests -nologo`
Expected: PASS (8 pruebas).

- [ ] **Step 11: Generar la migración**

```bash
dotnet ef migrations add SpreedlyTarjetaEnElAlta \
  --project MLMConquerorGlobalEdition.Repository \
  --startup-project MLMConquerorGlobalEdition.SignupAPI
```

Revisar `Up`: solo `CreateTable("SignupCardPayments")`, `CreateTable("SpreedlyApiCalls")` y sus índices (`ExternalOrderId` único, `ScaTransactionToken` y `PurchaseTransactionToken` únicos filtrados `IS NOT NULL`, `OrderId`, `UX_SignupCardPayments_OrderId_Consumed` filtrado `[Consumed] = 1`, `(Status, CreationDate)`, y en el log `CreationDate`, `TransactionToken`, `OrderReference`). Si aparecen bloques `migrationBuilder.UpdateData(...)` de semillas que no cambian nada, **borrarlos de `Up` y de `Down`** (no del `.Designer.cs` ni del snapshot). Cualquier otra operación (columnas o tablas ajenas) es deriva del modelo: parar y avisar.

Run: `grep -c "UpdateData" MLMConquerorGlobalEdition.Repository/Migrations/*_SpreedlyTarjetaEnElAlta.cs`
Expected: `0`

Run: `grep -c "SignupCardPayments\|SpreedlyApiCalls" MLMConquerorGlobalEdition.Repository/Migrations/*_SpreedlyTarjetaEnElAlta.cs`
Expected: > 0

- [ ] **Step 12: Compilar**

Run: `dotnet build MLMConquerorGlobalEdition.Repository -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 13: Commit**

```bash
git add MLMConquerorGlobalEdition.Domain/Entities/Orders/SignupCardPaymentStatus.cs \
        MLMConquerorGlobalEdition.Domain/Entities/Orders/SignupCardPayment.cs \
        MLMConquerorGlobalEdition.Domain/Entities/Billing/SpreedlyApiCall.cs \
        MLMConquerorGlobalEdition.Repository/Configurations/SignupCardPaymentConfiguration.cs \
        MLMConquerorGlobalEdition.Repository/Configurations/Billing/SpreedlyApiCallConfiguration.cs \
        MLMConquerorGlobalEdition.Repository/Services/Spreedly/SpreedlyCallLog.cs \
        MLMConquerorGlobalEdition.Repository/Context/AppDbContext.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/*_SpreedlyTarjetaEnElAlta.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/*_SpreedlyTarjetaEnElAlta.Designer.cs \
        MLMConquerorGlobalEdition.Repository/Migrations/AppDbContextModelSnapshot.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Card/SignupCardPaymentModelTests.cs
git commit -m "Intentos de pago con tarjeta del alta y log de llamadas a Spreedly: ids externos unicos, un solo intento consume el pedido, ECI y estado 3DS sin CAVV"
```

---

### Task 9: `SpreedlyCardGatewayService` compra de verdad con el token de 3DS

**Files:**
- Modify: `MLMConquerorGlobalEdition.Billing/Services/CardGateway/ICardGatewayService.cs`
- Modify: `MLMConquerorGlobalEdition.Billing/Services/CardGateway/SpreedlyCardGatewayService.cs`
- Modify: `MLMConquerorGlobalEdition.Billing/Services/Tokens/CardChargeOutcomes.cs`
- Test: `MLMConquerorGlobalEdition.Billing.Tests/Services/SpreedlyCardGatewayServiceRealTests.cs`

Las pruebas que ya existen (`SpreedlyCardGatewayServiceTests`) construyen el servicio con `(db, logger)`: siguen cubriendo el stub, que es lo que se hace con Spreedly apagado (I5). No se tocan.

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Billing.Services.CardGateway;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Spreedly;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services;

/// <summary>
/// Con Spreedly encendido el gateway compra de verdad: el gateway token del procesador elegido por el router,
/// el token del medio de pago, el importe en unidades menores y el token de la autenticación 3DS. Sin ese
/// token no hay cobro. Un rechazo es "no cobrado"; un resultado desconocido, no.
/// </summary>
public class SpreedlyCardGatewayServiceRealTests
{
    private readonly Mock<ISpreedlyClient> _client = new();
    private readonly Mock<ISpreedlyCredentialSource> _creds = new();

    public SpreedlyCardGatewayServiceRealTests()
    {
        _creds.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(new SpreedlyCredentials("ENVKEY1", "SECRETO1", "SCAPROV1", "FIRMA1"));
        _creds.Setup(c => c.GetGatewayTokenAsync(CardProcessor.CheckoutEUR, It.IsAny<CancellationToken>()))
              .ReturnsAsync("GW_CHECKOUT_EUR");
    }

    private SpreedlyCardGatewayService Servicio(AppDbContext db, bool encendido = true)
        => new(db, NullLogger<SpreedlyCardGatewayService>.Instance, _client.Object, _creds.Object,
               new SpreedlyOptions { Enabled = encendido });

    private static GatewayChargeRequest Pedido(string? sca = "SCATOKEN1") => new()
    {
        MemberId = "AMB-1", Amount = 99.95m, Currency = "EUR", Description = "Signup",
        SpreedlyPaymentMethodToken = "PMTOKEN1", ScaAuthenticationToken = sca,
        OrderReference = "SIGNUP-CARD-o1-1", DownstreamProcessor = CardProcessor.CheckoutEUR
    };

    private static SpreedlyTransaction Tx(string state, bool ok) => new(
        "TXPURCHASE1", state, ok, "Purchase", 9995, "EUR", "SIGNUP-CARD-o1-1", "PMTOKEN1",
        null, null, null, null, null, null, "gw-77", ok ? "NTID-9" : null, state, null);

    [Fact]
    public async Task Sin_token_de_3ds_no_cobra_y_consta_como_no_cobrado()
    {
        using var db = TestDbContextFactory.Create();
        var r = await Servicio(db).ChargeAsync(Pedido(sca: null));

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be("SPREEDLY_SCA_REQUIRED");
        CardChargeOutcomes.IsExplicitNoCharge(r.ErrorCode).Should().BeTrue();
        _client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Compra_con_el_gateway_del_procesador_el_importe_menor_y_el_token_de_3ds()
    {
        using var db = TestDbContextFactory.Create();
        SpreedlyPurchaseRequest? enviado = null;
        _client.Setup(c => c.PurchaseAsync(It.IsAny<SpreedlyPurchaseRequest>(), It.IsAny<CancellationToken>()))
               .Callback<SpreedlyPurchaseRequest, CancellationToken>((p, _) => enviado = p)
               .ReturnsAsync(SpreedlyCall<SpreedlyTransaction>.Success(Tx("succeeded", true), 200, 5));

        var r = await Servicio(db).ChargeAsync(Pedido());

        r.IsSuccess.Should().BeTrue();
        r.Value!.GatewayTransactionId.Should().Be("TXPURCHASE1");
        r.Value.NetworkTransactionId.Should().Be("NTID-9");
        enviado.Should().Be(new SpreedlyPurchaseRequest("GW_CHECKOUT_EUR", "PMTOKEN1", 9995, "EUR", "SIGNUP-CARD-o1-1", "Signup", "SCATOKEN1", true));
        db.ChangeTracker.Entries<SpreedlyApiCall>().Should().ContainSingle("la llamada queda anotada para guardarse con el intento");
    }

    [Fact]
    public async Task Un_rechazo_es_no_cobrado()
    {
        using var db = TestDbContextFactory.Create();
        _client.Setup(c => c.PurchaseAsync(It.IsAny<SpreedlyPurchaseRequest>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(SpreedlyCall<SpreedlyTransaction>.Fail(SpreedlyOutcome.Declined, 422, SpreedlyErrorCodes.Declined, 5, Tx("gateway_processing_failed", false)));

        var r = await Servicio(db).ChargeAsync(Pedido());

        r.ErrorCode.Should().Be("SPREEDLY_DECLINED");
        CardChargeOutcomes.IsExplicitNoCharge(r.ErrorCode).Should().BeTrue();
    }

    [Fact]
    public async Task Un_resultado_desconocido_no_es_no_cobrado()
    {
        using var db = TestDbContextFactory.Create();
        _client.Setup(c => c.PurchaseAsync(It.IsAny<SpreedlyPurchaseRequest>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(SpreedlyCall<SpreedlyTransaction>.Fail(SpreedlyOutcome.Unknown, 0, SpreedlyErrorCodes.Transport, 30000));

        var r = await Servicio(db).ChargeAsync(Pedido());

        r.ErrorCode.Should().Be("SPREEDLY_RESULT_UNKNOWN");
        CardChargeOutcomes.IsExplicitNoCharge(r.ErrorCode).Should().BeFalse();
    }

    [Fact]
    public async Task Sin_gateway_token_del_procesador_no_llama()
    {
        using var db = TestDbContextFactory.Create();
        var p = Pedido();
        var r = await Servicio(db).ChargeAsync(new GatewayChargeRequest
        {
            MemberId = p.MemberId, Amount = p.Amount, Currency = p.Currency, Description = p.Description,
            SpreedlyPaymentMethodToken = p.SpreedlyPaymentMethodToken, ScaAuthenticationToken = p.ScaAuthenticationToken,
            OrderReference = p.OrderReference, DownstreamProcessor = CardProcessor.Shift4
        });

        r.ErrorCode.Should().Be("SPREEDLY_DOWNSTREAM_TOKEN_NOT_SET");
        _client.Verify(c => c.PurchaseAsync(It.IsAny<SpreedlyPurchaseRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Apagado_sigue_siendo_el_stub_de_siempre()
    {
        using var db = TestDbContextFactory.Create();
        db.ApiCredentials.Add(new ApiCredential
        {
            ServiceKey = "Spreedly", Environment = "Production", IsActive = true, ApiKeyEncrypted = "ENC:x",
            CreatedBy = "t", CreationDate = DateTime.Now, LastUpdateDate = DateTime.Now
        });
        db.ApiCredentials.Add(new ApiCredential
        {
            ServiceKey = "CheckoutEUR", Environment = "Production", IsActive = true, SpreedlyGatewayTokenEncrypted = "ENC:y",
            CreatedBy = "t", CreationDate = DateTime.Now, LastUpdateDate = DateTime.Now
        });
        await db.SaveChangesAsync();

        var r = await Servicio(db, encendido: false).ChargeAsync(Pedido());

        r.Value!.GatewayTransactionId.Should().StartWith("simulated-spreedly-txn-");
        _client.VerifyNoOtherCalls();
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter SpreedlyCardGatewayServiceRealTests -nologo`
Expected: FAIL de compilación (`ScaAuthenticationToken`, `OrderReference`, `NetworkTransactionId` y el constructor no existen).

- [ ] **Step 3: Ampliar `GatewayChargeRequest` y `GatewayChargeResult` en `ICardGatewayService.cs`**

Dentro de `GatewayChargeRequest`, debajo de `DownstreamProcessor`:

```csharp
    /// <summary>
    /// Token de la autenticación 3DS2 (Spreedly sca_authentication_token). Con Spreedly encendido es
    /// OBLIGATORIO: sin él no se cobra (SPREEDLY_SCA_REQUIRED). Solo lo tiene un cobro con el cliente delante.
    /// </summary>
    public string? ScaAuthenticationToken { get; init; }

    /// <summary>Nuestro order_id para el proveedor (p. ej. SIGNUP-CARD-{pedido}-{n}); es la llave de la conciliación.</summary>
    public string? OrderReference { get; init; }
```

Dentro de `GatewayChargeResult`, debajo de `RawResponse`:

```csharp
    /// <summary>Id de la red de la tarjeta, para cobros posteriores sin cliente (MIT). Null si el gateway no lo da.</summary>
    public string? NetworkTransactionId { get; init; }
```

- [ ] **Step 4: Reescribir `SpreedlyCardGatewayService.cs`**

El stub actual (pasos 1 a 5 de `ChargeWithProcessorAsync` y `RefundWithProcessorAsync`) se conserva tal cual, renombrado a `SimulatedChargeAsync` y `SimulatedRefundAsync`. El archivo queda así:

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Spreedly;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.CardGateway;

/// <summary>
/// Proxy universal de Spreedly para TODOS los CardProcessor (BILLING-RULES §3): una bóveda (el
/// payment_method_token del miembro), muchos procesadores (el gateway token de cada uno). El router elige
/// el procesador; este servicio compra con su gateway token.
///
/// DOS MODOS (Plan I, decisión I5):
///   • Spreedly:Enabled = true → compra real: POST /v1/gateways/{gateway_token}/purchase.json con el
///     sca_authentication_token de 3DS2 Global. SIN ese token NO se cobra (SPREEDLY_SCA_REQUIRED): un cobro
///     sin cliente presente (recurrente MIT) espera a su propio plan. Credenciales y gateway tokens de
///     ApiCredential, del entorno configurado.
///   • Apagado → el stub de siempre (éxito "simulated-…"), sin cambios: así el recurrente de hoy no cambia
///     de comportamiento por este plan. CardChargeOutcomes.IsSimulated impide emitir o activar por él.
///
/// Los códigos de fallo que prueban que NO se cobró están en CardChargeOutcomes.ExplicitNoChargeCodes; un
/// resultado desconocido es SPREEDLY_RESULT_UNKNOWN y el orquestador no prueba otro gateway tras él.
/// </summary>
public class SpreedlyCardGatewayService : ICardGatewayService
{
    public CardProcessor Processor => CardProcessor.NmiSpreedly; // nominal; el resolver lo ignora

    private const string SpreedlyServiceKey = "Spreedly";
    private const string Actor              = "billing-engine";

    private readonly AppDbContext                         _db;
    private readonly ILogger<SpreedlyCardGatewayService>  _logger;
    private readonly ISpreedlyClient?                     _client;
    private readonly ISpreedlyCredentialSource?           _credentials;
    private readonly SpreedlyOptions?                     _options;
    private readonly IDateTimeProvider?                   _clock;

    public SpreedlyCardGatewayService(
        AppDbContext db,
        ILogger<SpreedlyCardGatewayService> logger,
        // Opcionales: sin ellos (pruebas del stub, o un host sin AddSpreedly) el servicio es el stub.
        ISpreedlyClient? client = null,
        ISpreedlyCredentialSource? credentials = null,
        SpreedlyOptions? options = null,
        IDateTimeProvider? clock = null)
    {
        _db          = db;
        _logger      = logger;
        _client      = client;
        _credentials = credentials;
        _options     = options;
        _clock       = clock;
    }

    private bool RealCharging => _options is { Enabled: true } && _client is not null && _credentials is not null;

    public async Task<Result<GatewayChargeResult>> ChargeAsync(GatewayChargeRequest req, CancellationToken ct = default)
        => await ChargeWithProcessorAsync(req, req.DownstreamProcessor, ct);

    public Task<Result<GatewayChargeResult>> ChargeWithProcessorAsync(
        GatewayChargeRequest req, CardProcessor processor, CancellationToken ct = default)
        => RealCharging ? RealChargeAsync(req, processor) : SimulatedChargeAsync(req, processor, ct);

    public async Task<Result<bool>> RefundAsync(string gatewayTransactionId, decimal amount, CancellationToken ct = default)
        => await RefundWithProcessorAsync(gatewayTransactionId, amount, CardProcessor.NmiSpreedly, ct);

    public Task<Result<bool>> RefundWithProcessorAsync(
        string gatewayTransactionId, decimal amount, CardProcessor processor, CancellationToken ct = default)
        => RealCharging
            // Las devoluciones de verdad quedan fuera del Plan I: se hacen en la consola de Spreedly.
            ? Task.FromResult(Result<bool>.Failure("SPREEDLY_REFUND_NOT_SUPPORTED",
                "Card refunds are done in the Spreedly console for now."))
            : SimulatedRefundAsync(gatewayTransactionId, amount, processor, ct);

    // ── Cobro real ───────────────────────────────────────────────────────────

    private async Task<Result<GatewayChargeResult>> RealChargeAsync(GatewayChargeRequest req, CardProcessor processor)
    {
        // Lo que mueve dinero no se cancela a medias: la compra y su anotación van con CancellationToken.None.
        var none = CancellationToken.None;

        if (string.IsNullOrWhiteSpace(req.ScaAuthenticationToken))
        {
            _logger.LogWarning("[Spreedly] {MemberId}: cobro sin autenticación 3DS; no se cobra (SPREEDLY_SCA_REQUIRED).", req.MemberId);
            return Result<GatewayChargeResult>.Failure("SPREEDLY_SCA_REQUIRED",
                "A card charge needs a 3-D Secure authentication.");
        }

        if (string.IsNullOrWhiteSpace(req.SpreedlyPaymentMethodToken))
            return Result<GatewayChargeResult>.Failure("SPREEDLY_MEMBER_TOKEN_MISSING",
                $"Member {req.MemberId} has no Spreedly payment_method_token.");

        if (await _credentials!.GetAsync(none) is null)
            return Result<GatewayChargeResult>.Failure("SPREEDLY_CREDENTIAL_MISSING",
                "The Spreedly credential of this environment is missing, inactive or incomplete.");

        var gatewayToken = await _credentials.GetGatewayTokenAsync(processor, none);
        if (gatewayToken is null)
            return Result<GatewayChargeResult>.Failure("SPREEDLY_DOWNSTREAM_TOKEN_NOT_SET",
                $"ApiCredential '{processor}' has no Spreedly gateway token in this environment.");

        long amountMinor;
        try
        {
            amountMinor = SpreedlyCurrency.ToMinorUnits(req.Amount, req.Currency);
        }
        catch (ArgumentException)
        {
            return Result<GatewayChargeResult>.Failure("SPREEDLY_INVALID_CURRENCY", $"Unsupported currency '{req.Currency}'.");
        }

        var orderReference = req.OrderReference ?? $"MEMBER-{req.MemberId}";
        var call = await _client!.PurchaseAsync(new SpreedlyPurchaseRequest(
            gatewayToken, req.SpreedlyPaymentMethodToken, amountMinor, req.Currency.Trim().ToUpperInvariant(),
            orderReference, req.Description, req.ScaAuthenticationToken, RetainOnSuccess: true), none);

        // Se guarda con el GatewayChargeAttempt que escribe el orquestador justo después.
        _db.SpreedlyApiCalls.Add(SpreedlyCallLog.From(SpreedlyCallLog.Purchase, call, null, orderReference,
            _clock?.Now ?? DateTime.Now, Actor));

        switch (call.Outcome)
        {
            case SpreedlyOutcome.Ok:
                _logger.LogInformation("[Spreedly] Cobro {OrderReference} hecho con {Processor}.", orderReference, processor);
                return Result<GatewayChargeResult>.Success(new GatewayChargeResult
                {
                    GatewayTransactionId = call.Value!.Token,
                    Status               = call.Value.State,
                    NetworkTransactionId = call.Value.NetworkTransactionId
                });

            case SpreedlyOutcome.Declined:
                return Result<GatewayChargeResult>.Failure("SPREEDLY_DECLINED", call.Value?.Message ?? "The card was declined.");

            case SpreedlyOutcome.NotConfigured:
                return Result<GatewayChargeResult>.Failure("SPREEDLY_CREDENTIAL_MISSING", "Spreedly rejected the credentials of this environment.");

            case SpreedlyOutcome.Invalid when call.ErrorCode == SpreedlyErrorCodes.InvalidToken:
                return Result<GatewayChargeResult>.Failure("SPREEDLY_INVALID_TOKEN", "A token has an invalid format; nothing was sent.");

            default:
                _logger.LogError("[Spreedly] Cobro {OrderReference} con {Processor}: resultado DESCONOCIDO ({Code}). Puede haberse cobrado.",
                    orderReference, processor, call.ErrorCode);
                return Result<GatewayChargeResult>.Failure("SPREEDLY_RESULT_UNKNOWN",
                    "The card charge result is unknown; it will be reconciled.");
        }
    }

    // ── Stub (Spreedly apagado): sin cambios respecto a antes del Plan I ───────

    private async Task<Result<GatewayChargeResult>> SimulatedChargeAsync(
        GatewayChargeRequest req, CardProcessor processor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.SpreedlyPaymentMethodToken))
        {
            _logger.LogWarning(
                "[Spreedly] Member {MemberId}: SpreedlyPaymentMethodToken is missing. Cannot charge.",
                req.MemberId);
            return Result<GatewayChargeResult>.Failure(
                "SPREEDLY_MEMBER_TOKEN_MISSING",
                $"Member {req.MemberId} has no Spreedly payment_method_token. Vault the card through Spreedly before charging.");
        }

        var spreedlyCred = await _db.ApiCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ServiceKey == SpreedlyServiceKey && !c.IsDeleted, ct);

        if (spreedlyCred is null || !spreedlyCred.IsActive)
            return Result<GatewayChargeResult>.Failure(
                "SPREEDLY_CREDENTIAL_MISSING",
                "ApiCredential 'Spreedly' is missing or inactive. Configure it via the admin Credentials page.");

        if (string.IsNullOrWhiteSpace(spreedlyCred.ApiKeyEncrypted))
            return Result<GatewayChargeResult>.Failure(
                "SPREEDLY_CREDENTIAL_INCOMPLETE",
                "ApiCredential 'Spreedly'.ApiKeyEncrypted is not set.");

        var processorKey = processor.ToString();
        var processorCred = await _db.ApiCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ServiceKey == processorKey && !c.IsDeleted, ct);

        if (processorCred is null)
            return Result<GatewayChargeResult>.Failure(
                "SPREEDLY_DOWNSTREAM_TOKEN_MISSING",
                $"ApiCredential '{processorKey}' not found. " +
                $"Provision the processor in Spreedly and store the resulting gateway token via the admin Credentials page.");

        if (string.IsNullOrWhiteSpace(processorCred.SpreedlyGatewayTokenEncrypted))
            return Result<GatewayChargeResult>.Failure(
                "SPREEDLY_DOWNSTREAM_TOKEN_NOT_SET",
                $"ApiCredential '{processorKey}'.SpreedlyGatewayTokenEncrypted is not set. " +
                $"After provisioning the gateway in Spreedly, copy the gateway_token into the admin Credentials page.");

        _logger.LogInformation(
            "[Spreedly][STUB] Charge request — Processor: {Processor}, MemberId: {MemberId}, Amount: {Amount} {Currency}, " +
            "IsRecurring: {IsRecurring}.",
            processor, req.MemberId, req.Amount, req.Currency, req.IsRecurring);

        var simulatedTxId = $"simulated-spreedly-txn-{Guid.NewGuid():N}";

        _logger.LogInformation("[Spreedly][STUB] Simulated success — TxId: {TxId}, Processor: {Processor}.", simulatedTxId, processor);

        return Result<GatewayChargeResult>.Success(new GatewayChargeResult
        {
            GatewayTransactionId = simulatedTxId,
            Status               = "simulated_success",
            RawResponse          = $"{{\"stub\":true,\"processor\":\"{processor}\",\"transaction_token\":\"{simulatedTxId}\"}}"
        });
    }

    private async Task<Result<bool>> SimulatedRefundAsync(
        string gatewayTransactionId, decimal amount, CardProcessor processor, CancellationToken ct)
    {
        var spreedlyCred = await _db.ApiCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ServiceKey == SpreedlyServiceKey && !c.IsDeleted, ct);

        if (spreedlyCred is null || !spreedlyCred.IsActive || string.IsNullOrWhiteSpace(spreedlyCred.ApiKeyEncrypted))
            return Result<bool>.Failure(
                "SPREEDLY_CREDENTIAL_MISSING",
                "ApiCredential 'Spreedly' is missing, inactive, or has no ApiKeyEncrypted.");

        var processorKey  = processor.ToString();
        var processorCred = await _db.ApiCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ServiceKey == processorKey && !c.IsDeleted, ct);

        if (processorCred is null || string.IsNullOrWhiteSpace(processorCred.SpreedlyGatewayTokenEncrypted))
            return Result<bool>.Failure(
                "SPREEDLY_DOWNSTREAM_TOKEN_NOT_SET",
                $"ApiCredential '{processorKey}'.SpreedlyGatewayTokenEncrypted is not set.");

        _logger.LogInformation(
            "[Spreedly][STUB] Refund — GatewayTxId: {TxId}, Amount: {Amount}, Processor: {Processor}.",
            gatewayTransactionId, amount, processor);

        return Result<bool>.Success(true);
    }
}
```

> Los dos métodos `Simulated…` son el stub de antes del Plan I (líneas 62-138 y 153-179 del archivo actual) con un solo cambio: el log del stub ya no imprime el `payment_method_token` ni un trozo del gateway token, que el anterior escribía en el log. Las pruebas de `SpreedlyCardGatewayServiceTests` siguen cubriéndolos sin cambios.

- [ ] **Step 5: Ampliar la lista de "no se cobró" en `CardChargeOutcomes.cs`**

Sustituir el conjunto `ExplicitNoChargeCodes` y su comentario por:

```csharp
    /// <summary>
    /// Códigos que prueban que NO se movió dinero.
    ///   • GATEWAY_NOT_REGISTERED — GatewayChargeOrchestrator: no hay servicio para ese procesador.
    ///   • SPREEDLY_* de comprobación previa — falta el token del miembro, la credencial, el gateway token,
    ///     la autenticación 3DS (SCA_REQUIRED), o un token o moneda no válidos: se corta antes de llamar.
    ///   • SPREEDLY_DECLINED — Spreedly respondió que la compra falló (failed / gateway_processing_failed).
    ///   • CREDENTIAL_* — StubGatewayBase: misma comprobación previa en los gateways de prueba.
    /// NO están (a propósito): SPREEDLY_RESULT_UNKNOWN, un timeout, un 5xx, ni el "DECLINED" genérico de los
    /// stubs, que no viene de ningún proveedor (Plan F, I2).
    /// </summary>
    public static readonly IReadOnlySet<string> ExplicitNoChargeCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        "GATEWAY_NOT_REGISTERED",
        "SPREEDLY_MEMBER_TOKEN_MISSING",
        "SPREEDLY_CREDENTIAL_MISSING",
        "SPREEDLY_CREDENTIAL_INCOMPLETE",
        "SPREEDLY_DOWNSTREAM_TOKEN_MISSING",
        "SPREEDLY_DOWNSTREAM_TOKEN_NOT_SET",
        "SPREEDLY_SCA_REQUIRED",
        "SPREEDLY_INVALID_TOKEN",
        "SPREEDLY_INVALID_CURRENCY",
        "SPREEDLY_DECLINED",
        "CREDENTIAL_NOT_FOUND",
        "CREDENTIAL_INACTIVE",
        "CREDENTIAL_INCOMPLETE"
    };
```

- [ ] **Step 6: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "SpreedlyCardGatewayService|TokenPurchaseServiceTests" -nologo`
Expected: PASS (las nuevas, las del stub y las de la compra de tokens, que siguen tratando `DECLINED` como dudoso).

- [ ] **Step 7: Commit**

```bash
git add MLMConquerorGlobalEdition.Billing/Services/CardGateway/ICardGatewayService.cs \
        MLMConquerorGlobalEdition.Billing/Services/CardGateway/SpreedlyCardGatewayService.cs \
        MLMConquerorGlobalEdition.Billing/Services/Tokens/CardChargeOutcomes.cs \
        MLMConquerorGlobalEdition.Billing.Tests/Services/SpreedlyCardGatewayServiceRealTests.cs
git commit -m "SpreedlyCardGatewayService compra de verdad con Spreedly encendido: gateway token del procesador, importe en unidades menores y token de 3DS obligatorio; apagado sigue el stub"
```

---

### Task 10: El orquestador pasa los tokens y el procesador, y no salta de gateway tras un fallo dudoso

**Files:**
- Modify: `MLMConquerorGlobalEdition.Billing/Services/Routing/IGatewayChargeOrchestrator.cs`
- Modify: `MLMConquerorGlobalEdition.Billing/Services/Routing/GatewayChargeOrchestrator.cs:52-166`
- Modify: `MLMConquerorGlobalEdition.Billing/Jobs/DelayedFallbackChargeJob.cs:104-121`
- Modify: `MLMConquerorGlobalEdition.Billing/Services/Recurring/RecurringBillingProcessor.cs:283-291`
- Modify: `MLMConquerorGlobalEdition.Billing/Features/RenewMembership/RenewMembershipHandler.cs:159-167`
- Modify: `MLMConquerorGlobalEdition.Billing.Tests/Services/GatewayChargeOrchestratorTests.cs`

- [ ] **Step 1: Escribir las pruebas que fallan (al final de `GatewayChargeOrchestratorTests`)**

```csharp
    // ── Plan I: tokens hasta el gateway y nada de segundo cobro tras un fallo dudoso ─────────────

    [Fact]
    public async Task ExecuteAsync_PasaAlGatewayElTokenDelMedioDePago_El3dsYElProcesadorDelPaso()
    {
        using var db = TestDbContextFactory.Create();
        GatewayChargeRequest? recibido = null;
        var gw = new Mock<ICardGatewayService>();
        gw.Setup(g => g.ChargeAsync(It.IsAny<GatewayChargeRequest>(), It.IsAny<CancellationToken>()))
          .Callback<GatewayChargeRequest, CancellationToken>((r, _) => recibido = r)
          .ReturnsAsync(Result<GatewayChargeResult>.Success(new GatewayChargeResult
          {
              GatewayTransactionId = "TXP1", Status = "succeeded", NetworkTransactionId = "NTID-1"
          }));
        var resolver = new Mock<ICardGatewayResolver>();
        resolver.Setup(r => r.Resolve(CardProcessor.CheckoutEUR)).Returns(gw.Object);

        var result = await CreateOrchestrator(db, resolver.Object).ExecuteAsync(
            Plan(Step(CardProcessor.CheckoutEUR)), DefaultCtx,
            new OrchestratorChargeRequest
            {
                MemberId = "member-1", OrderId = "order-1", Description = "Signup",
                SpreedlyPaymentMethodToken = "PMTOKEN1", ScaAuthenticationToken = "SCATOKEN1", OrderReference = "SIGNUP-CARD-order-1-1"
            });

        result.Value!.NetworkTransactionId.Should().Be("NTID-1");
        recibido!.SpreedlyPaymentMethodToken.Should().Be("PMTOKEN1");
        recibido.ScaAuthenticationToken.Should().Be("SCATOKEN1");
        recibido.OrderReference.Should().Be("SIGNUP-CARD-order-1-1");
        recibido.DownstreamProcessor.Should().Be(CardProcessor.CheckoutEUR);
    }

    [Theory]
    [InlineData("SPREEDLY_RESULT_UNKNOWN")]
    [InlineData("GATEWAY_TIMEOUT")]
    [InlineData("DECLINED")]
    public async Task ExecuteAsync_TrasUnFalloDudoso_NoPruebaElSiguienteGateway(string codigo)
    {
        using var db = TestDbContextFactory.Create();
        var gw1 = new Mock<ICardGatewayService>();
        gw1.Setup(g => g.ChargeAsync(It.IsAny<GatewayChargeRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Result<GatewayChargeResult>.Failure(codigo, "no se sabe"));
        var gw2 = new Mock<ICardGatewayService>();
        var resolver = new Mock<ICardGatewayResolver>();
        resolver.Setup(r => r.Resolve(CardProcessor.NmiSpreedly)).Returns(gw1.Object);
        resolver.Setup(r => r.Resolve(CardProcessor.NmiDirect)).Returns(gw2.Object);

        var result = await CreateOrchestrator(db, resolver.Object)
            .ExecuteAsync(Plan(Step(CardProcessor.NmiSpreedly, 0), Step(CardProcessor.NmiDirect, 1)), DefaultCtx, DefaultChargeReq);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(codigo);
        gw2.Verify(g => g.ChargeAsync(It.IsAny<GatewayChargeRequest>(), It.IsAny<CancellationToken>()), Times.Never,
            "un timeout en el primero y un cobro en el segundo serían dos cobros");
        db.GatewayChargeAttempts.Single().Outcome.Should().Be("Unknown");
    }

    [Fact]
    public async Task ExecuteAsync_ConClientePresente_NoProgramaPasosDiferidos()
    {
        using var db = TestDbContextFactory.Create();
        var resolver = GatewayResolverReturning(CardProcessor.NmiSpreedly,
            Result<GatewayChargeResult>.Failure("SPREEDLY_DECLINED", "Declined"));
        var hangfire = new Mock<IBackgroundJobClient>();

        var result = await CreateOrchestrator(db, resolver.Object, hangfire.Object).ExecuteAsync(
            Plan(Step(CardProcessor.NmiSpreedly, 0), Step(CardProcessor.CheckoutUS, 1, delay: 60)), DefaultCtx,
            new OrchestratorChargeRequest { MemberId = "member-1", ScaAuthenticationToken = "SCATOKEN1", SpreedlyPaymentMethodToken = "PMTOKEN1" });

        result.IsSuccess.Should().BeFalse();
        hangfire.Verify(h => h.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<IState>()), Times.Never);
    }
```

Y en las tres pruebas existentes que usan `"DECLINED"` para provocar el fallback (`ExecuteAsync_WhenPrimaryFailsFallbackSucceeds_ReturnsFallbackSuccess`, `ExecuteAsync_WhenAllStepsFail_ReturnsFailure` y `ExecuteAsync_WhenFallbackHasDelay_EnqueuesHangfireJobAndReturnsScheduled`), cambiar el código por `"SPREEDLY_DECLINED"`: un rechazo explícito sigue pasando al siguiente paso; el `DECLINED` genérico ya no (prueba de arriba).

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter GatewayChargeOrchestratorTests -nologo`
Expected: FAIL de compilación (`SpreedlyPaymentMethodToken`, `ScaAuthenticationToken`, `OrderReference` en `OrchestratorChargeRequest`; `NetworkTransactionId` en el resultado).

- [ ] **Step 3: Ampliar `OrchestratorChargeRequest` y `OrchestratorChargeResult`**

En `IGatewayChargeOrchestrator.cs`, dentro de `OrchestratorChargeRequest`, debajo de `IsRecurring`:

```csharp
    /// <summary>payment_method_token de Spreedly de la tarjeta (MemberCreditCard.SpreedlyPaymentMethodToken o el del iFrame).</summary>
    public string? SpreedlyPaymentMethodToken { get; init; }

    /// <summary>Token de la autenticación 3DS2. Con él, el cobro es con el cliente delante: no se programan pasos diferidos.</summary>
    public string? ScaAuthenticationToken { get; init; }

    /// <summary>Nuestro order_id para el proveedor (llave de la conciliación).</summary>
    public string? OrderReference { get; init; }
```

Dentro de `OrchestratorChargeResult`, debajo de `ScheduledJobId`:

```csharp
    public string? NetworkTransactionId { get; init; }
```

- [ ] **Step 4: Cambiar el bucle de `GatewayChargeOrchestrator.ExecuteAsync`**

Añadir `using MLMConquerorGlobalEdition.Billing.Services.Tokens;` arriba. Sustituir el cuerpo de `ExecuteAsync` (líneas 48-171) por:

```csharp
        var now = _dateTime.Now;
        string? lastError     = null;
        string? lastErrorCode = null;

        // Con el cliente delante (3DS) un paso diferido no tiene sentido: la autenticación es de ahora.
        var customerPresent = !string.IsNullOrWhiteSpace(chargeReq.ScaAuthenticationToken);

        for (int i = 0; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];

            if (step.DelayMinutes > 0 && i > 0)
            {
                if (customerPresent)
                {
                    lastErrorCode ??= "NO_IMMEDIATE_FALLBACK";
                    lastError     ??= "No immediate gateway left for a customer-present charge.";
                    break;
                }

                var jobId = _hangfire.Schedule<DelayedFallbackChargeJob>(
                    job => job.ExecuteAsync(
                        plan.RouteBucketKey,
                        chargeReq.MemberId,
                        (int)step.CardProcessor,
                        step.Amount,
                        step.PresentmentCurrency,
                        step.FallbackStepIndex,
                        chargeReq.OrderId,
                        chargeReq.TokenizedCardRef,
                        chargeReq.NetworkTransactionId,
                        chargeReq.Description,
                        CancellationToken.None),
                    TimeSpan.FromMinutes(step.DelayMinutes));

                await LogAttemptAsync(plan.RouteBucketKey, step, ctx, chargeReq.MemberId, "Scheduled", null, null, now, ct);

                return Result<OrchestratorChargeResult>.Success(new OrchestratorChargeResult
                {
                    Status               = "Scheduled",
                    ProcessorUsed        = step.CardProcessor.ToString(),
                    AmountCharged        = step.Amount,
                    CurrencyCharged      = step.PresentmentCurrency,
                    ScheduledJobId       = jobId,
                    PaymentHistoryId     = string.Empty,
                    GatewayTransactionId = string.Empty
                });
            }

            ICardGatewayService gateway;
            try { gateway = _gatewayResolver.Resolve(step.CardProcessor); }
            catch (InvalidOperationException ex)
            {
                lastError     = ex.Message;
                lastErrorCode = "GATEWAY_NOT_REGISTERED";
                _logger.LogError(ex, "Orchestrator: gateway {P} not registered.", step.CardProcessor);
                continue;
            }

            var req = new GatewayChargeRequest
            {
                MemberId                   = chargeReq.MemberId,
                Amount                     = step.Amount,
                Currency                   = step.PresentmentCurrency,
                Description                = chargeReq.Description,
                TokenizedCardRef           = chargeReq.TokenizedCardRef,
                NetworkTransactionId       = chargeReq.NetworkTransactionId,
                IsRecurring                = chargeReq.IsRecurring,
                SpreedlyPaymentMethodToken = chargeReq.SpreedlyPaymentMethodToken,
                ScaAuthenticationToken     = chargeReq.ScaAuthenticationToken,
                OrderReference             = chargeReq.OrderReference,
                // El procesador del PASO: con el proxy universal es lo que elige el gateway token.
                DownstreamProcessor        = step.CardProcessor
            };

            var chargeResult = await gateway.ChargeAsync(req, ct);

            if (!chargeResult.IsSuccess)
            {
                lastError     = chargeResult.Error;
                lastErrorCode = chargeResult.ErrorCode;

                // Solo se prueba el siguiente gateway si este fallo PRUEBA que no se cobró. Un timeout, un 5xx o
                // un resultado desconocido pudieron cargar: probar otro sería un cobro doble. Quien llamó concilia.
                var noCharge = CardChargeOutcomes.IsExplicitNoCharge(chargeResult.ErrorCode);
                await LogAttemptAsync(plan.RouteBucketKey, step, ctx, chargeReq.MemberId,
                    noCharge ? "Failed" : "Unknown", null, chargeResult.Error, now, ct);

                if (!noCharge)
                {
                    _logger.LogError(
                        "Orchestrator: step {Step}/{Total} via {Proc} ended without a known outcome ({Code}). Not trying another gateway.",
                        i + 1, plan.Steps.Count, step.CardProcessor, chargeResult.ErrorCode);
                    return Result<OrchestratorChargeResult>.Failure(
                        chargeResult.ErrorCode ?? "CHARGE_OUTCOME_UNKNOWN", chargeResult.Error ?? "Unknown charge outcome.");
                }

                _logger.LogWarning("Orchestrator: step {Step}/{Total} failed via {Proc}. Error: {Err}",
                    i + 1, plan.Steps.Count, step.CardProcessor, chargeResult.Error);
                continue;
            }

            var txId = chargeResult.Value!.GatewayTransactionId;

            var payment = new PaymentHistory
            {
                OrderId              = chargeReq.OrderId ?? string.Empty,
                MemberId             = chargeReq.MemberId,
                Amount               = step.Amount,
                GatewayName          = step.CardProcessor.ToString(),
                GatewayTransactionId = txId,
                TransactionStatus    = PaymentHistoryTransactionStatus.Captured,
                ProcessedAt          = now,
                CreationDate         = now,
                CreatedBy            = "billing-engine",
                LastUpdateDate       = now,
                LastUpdateBy         = "billing-engine"
            };
            _db.PaymentHistories.Add(payment);

            await LogAttemptAsync(plan.RouteBucketKey, step, ctx, chargeReq.MemberId, "Success", txId, null, now, ct, payment.Id);

            await _db.SaveChangesAsync(ct);

            return Result<OrchestratorChargeResult>.Success(new OrchestratorChargeResult
            {
                PaymentHistoryId     = payment.Id,
                GatewayTransactionId = txId,
                ProcessorUsed        = step.CardProcessor.ToString(),
                AmountCharged        = step.Amount,
                CurrencyCharged      = step.PresentmentCurrency,
                Status               = "Success",
                NetworkTransactionId = chargeResult.Value.NetworkTransactionId
            });
        }

        return Result<OrchestratorChargeResult>.Failure(
            lastErrorCode ?? "ALL_GATEWAYS_FAILED",
            lastError     ?? "All gateway attempts were exhausted.");
```

Actualizar también el `<summary>` de la clase: "On failure: advance to next step **only if the failure proves no money moved** (`CardChargeOutcomes`); an unknown outcome stops the plan."

- [ ] **Step 5: `DelayedFallbackChargeJob` manda el token de la tarjeta y el procesador (sin cambiar la firma del job)**

En `DelayedFallbackChargeJob.ExecuteAsync`, sustituir la construcción de `req` (líneas 112-121) por:

```csharp
        // La firma del job NO cambia (Hangfire guarda los argumentos de los ya programados): el token de la
        // tarjeta se lee aquí, de la tarjeta por defecto del miembro.
        var spreedlyToken = await _db.CreditCards.AsNoTracking()
            .Where(c => c.MemberId == memberId && c.IsDefault && !c.IsDeleted)
            .OrderBy(c => c.Priority)
            .Select(c => c.SpreedlyPaymentMethodToken)
            .FirstOrDefaultAsync(ct);

        var req = new GatewayChargeRequest
        {
            MemberId                   = memberId,
            Amount                     = amount,
            Currency                   = currency,
            Description                = description,
            TokenizedCardRef           = tokenizedCardRef,
            NetworkTransactionId       = networkTransactionId,
            IsRecurring                = true,
            SpreedlyPaymentMethodToken = spreedlyToken,
            DownstreamProcessor        = processor
        };
```

Añadir `using Microsoft.EntityFrameworkCore;` arriba. (Con Spreedly encendido en Billing este job falla cerrado con `SPREEDLY_SCA_REQUIRED`, R5; Billing lo deja apagado.)

- [ ] **Step 6: El recurrente y la renovación dejan de mandar el token de la tarjeta como NTID**

En `RecurringBillingProcessor.cs`, sustituir el bloque `var chargeReq = new OrchestratorChargeRequest { … }` (líneas 283-291) por:

```csharp
        var chargeReq = new OrchestratorChargeRequest
        {
            MemberId                   = state.MemberId,
            TokenizedCardRef           = creditCard?.CardToken,
            // GatewayToken es el token del medio de pago, NO un network_transaction_id: mandarlo como NTID era
            // inventarse un MIT. Hasta el plan de recurrente con NTID real, no se manda ninguno.
            NetworkTransactionId       = null,
            SpreedlyPaymentMethodToken = creditCard?.SpreedlyPaymentMethodToken,
            Description                = $"Recurring billing — {plan.Name}",
            OrderId                    = order.Id,
            IsRecurring                = true
        };
```

En `RenewMembershipHandler.cs` (líneas 159-167), el mismo cambio:

```csharp
        var chargeReq = new OrchestratorChargeRequest
        {
            MemberId                   = req.MemberId,
            TokenizedCardRef           = creditCard?.CardToken,
            NetworkTransactionId       = null, // ver RecurringBillingProcessor: GatewayToken no es un NTID
            SpreedlyPaymentMethodToken = creditCard?.SpreedlyPaymentMethodToken,
            Description                = $"Membership renewal: {subscription.MembershipLevel!.Name}",
            OrderId                    = order.Id,
            IsRecurring                = true
        };
```

- [ ] **Step 7: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests -nologo`
Expected: PASS (todas: orquestador, recurrente, renovación, compra de tokens).

- [ ] **Step 8: Commit**

```bash
git add MLMConquerorGlobalEdition.Billing/Services/Routing/IGatewayChargeOrchestrator.cs \
        MLMConquerorGlobalEdition.Billing/Services/Routing/GatewayChargeOrchestrator.cs \
        MLMConquerorGlobalEdition.Billing/Jobs/DelayedFallbackChargeJob.cs \
        MLMConquerorGlobalEdition.Billing/Services/Recurring/RecurringBillingProcessor.cs \
        MLMConquerorGlobalEdition.Billing/Features/RenewMembership/RenewMembershipHandler.cs \
        MLMConquerorGlobalEdition.Billing.Tests/Services/GatewayChargeOrchestratorTests.cs
git commit -m "El orquestador pasa al gateway el token de la tarjeta, el de 3DS y el procesador del paso, no prueba otro gateway tras un fallo dudoso ni programa pasos diferidos con cliente presente; el recurrente deja de mandar el token de la tarjeta como NTID"
```

---

### Task 11: Registro único del cobro con tarjeta (`AddCardCharging`)

**Files:**
- Create: `MLMConquerorGlobalEdition.Billing/Services/CardGateway/CardChargingServiceCollectionExtensions.cs`
- Modify: `MLMConquerorGlobalEdition.Billing/Program.cs:77-92` y después de `AddPayoutServices()` (línea 119)
- Modify: `MLMConquerorGlobalEdition.Billing/appsettings.json`
- Test: `MLMConquerorGlobalEdition.Billing.Tests/DI/CardChargingDiTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.CardGateway;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Spreedly;

namespace MLMConquerorGlobalEdition.Billing.Tests.DI;

/// <summary>AddCardCharging deja el cobro con tarjeta completo, y con Spreedly encendido el gateway es el real.</summary>
public class CardChargingDiTests
{
    private static ServiceProvider Contenedor(SpreedlyOptions spreedly)
    {
        var s = new ServiceCollection();
        s.AddLogging();
        s.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        s.AddDistributedMemoryCache();
        s.AddSingleton(Mock.Of<IBackgroundJobClient>());
        s.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        s.AddSpreedly(spreedly, isDevelopment: true);
        s.AddCardCharging();
        return s.BuildServiceProvider();
    }

    [Fact]
    public void Resuelve_router_orquestador_y_gateway()
    {
        using var sp = Contenedor(new SpreedlyOptions { Enabled = true, UseFake = true });
        using var scope = sp.CreateScope();

        scope.ServiceProvider.GetRequiredService<IGatewayRouter>().Should().BeOfType<GatewayRouter>();
        scope.ServiceProvider.GetRequiredService<IGatewayChargeOrchestrator>().Should().BeOfType<GatewayChargeOrchestrator>();
        scope.ServiceProvider.GetRequiredService<ICardGatewayResolver>().Resolve(CardProcessor.Shift4)
            .Should().BeOfType<SpreedlyCardGatewayService>();
    }

    [Fact]
    public async Task Con_Spreedly_encendido_el_gateway_es_el_real_y_exige_3ds()
    {
        using var sp = Contenedor(new SpreedlyOptions { Enabled = true, UseFake = true });
        using var scope = sp.CreateScope();
        var gateway = scope.ServiceProvider.GetRequiredService<ICardGatewayResolver>().Resolve(CardProcessor.NmiSpreedly);

        var r = await gateway.ChargeAsync(new GatewayChargeRequest
        {
            MemberId = "AMB-1", Amount = 10m, Currency = "USD", SpreedlyPaymentMethodToken = "fake_frictionless_x",
            DownstreamProcessor = CardProcessor.NmiSpreedly
        });

        r.ErrorCode.Should().Be("SPREEDLY_SCA_REQUIRED");
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter CardChargingDiTests -nologo`
Expected: FAIL de compilación (`AddCardCharging` no existe).

- [ ] **Step 3: Crear `CardChargingServiceCollectionExtensions.cs`**

```csharp
using Microsoft.Extensions.DependencyInjection.Extensions;
using MLMConquerorGlobalEdition.Billing.Services.Routing;

namespace MLMConquerorGlobalEdition.Billing.Services.CardGateway;

public static class CardChargingServiceCollectionExtensions
{
    /// <summary>
    /// Todo lo que hace falta para cobrar una tarjeta por el router, registrado UNA vez (decisión I23):
    /// gateway (proxy universal de Spreedly), resolver, reparto, router, conversión de moneda, marcas y
    /// orquestador. Lo usan Billing y SignupAPI. El host registra antes AddSpreedly (opciones, cliente y
    /// credenciales): sin él, SpreedlyCardGatewayService es el stub.
    ///
    /// Necesita del host: AppDbContext, IDistributedCache (conversión), IBackgroundJobClient (pasos diferidos)
    /// y el IDateTimeProvider de Billing.
    /// </summary>
    public static IServiceCollection AddCardCharging(this IServiceCollection services)
    {
        services.TryAddScoped<SpreedlyCardGatewayService>();
        services.TryAddScoped<ICardGatewayService>(sp => sp.GetRequiredService<SpreedlyCardGatewayService>());
        services.TryAddScoped<ICardGatewayResolver, CardGatewayResolver>();

        services.TryAddScoped<IGatewaySplitSelector, GatewaySplitSelector>();
        services.TryAddScoped<IGatewayRouter, GatewayRouter>();
        services.TryAddScoped<IGatewayChargeOrchestrator, GatewayChargeOrchestrator>();
        services.TryAddSingleton<ICardBrandDetector, CardBrandDetector>();

        services.AddHttpClient("CurrencyConverter");
        services.TryAddScoped<ICurrencyConversionService, CurrencyConversionService>();
        return services;
    }
}
```

- [ ] **Step 4: Usarlo en `Billing/Program.cs`**

Sustituir el bloque de las líneas 77-92 (desde `// ── Card gateway — Spreedly universal proxy` hasta `builder.Services.AddScoped<ICurrencyConversionService, CurrencyConversionService>();`) por:

```csharp
// ── Cobro con tarjeta: proxy universal de Spreedly + router + orquestador (Plan I, AddCardCharging) ──
// Spreedly va APAGADO en Billing (appsettings "Spreedly:Enabled": false): el recurrente no tiene MIT
// todavía y con Spreedly encendido un cobro sin 3DS falla cerrado (SPREEDLY_SCA_REQUIRED). Apagado,
// SpreedlyCardGatewayService sigue siendo el stub de siempre.
builder.Services.AddCardCharging();
```

Y justo después de `builder.Services.AddPayoutServices();` (que configura el cifrado de credenciales que necesita el cliente real):

```csharp
// Spreedly (Plan I): opciones, cliente y credenciales de ApiCredential. Va después de AddPayoutServices,
// que registra el key ring compartido con el que se descifran.
builder.Services.AddSpreedly(
    MLMConquerorGlobalEdition.Repository.Services.Spreedly.SpreedlyOptions.FromConfiguration(builder.Configuration),
    builder.Environment.IsDevelopment());
```

Añadir arriba `using MLMConquerorGlobalEdition.Repository.Services.Spreedly;` y quitar el nombre completo si se prefiere.

- [ ] **Step 5: Sección `Spreedly` en `Billing/appsettings.json`**

Debajo del bloque `"Billing": { … }`:

```jsonc
  // Spreedly (Plan I). AQUÍ NO HAY SECRETOS: environment key, access secret, SCA provider key, signing
  // secret y gateway tokens viven cifrados en ApiCredential (Admin → Billing → API Credentials), en las filas
  // del entorno CredentialEnvironment. En Billing va APAGADO: el recurrente necesita MIT con
  // network_transaction_id (plan aparte) y, encendido, un cobro sin 3DS falla cerrado.
  "Spreedly": {
    "Enabled": false,
    "UseFake": false,
    "CredentialEnvironment": "Production"
  },
```

- [ ] **Step 6: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests -nologo`
Expected: PASS.

Run: `dotnet build MLMConquerorGlobalEdition.Billing -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 7: Commit**

```bash
git add MLMConquerorGlobalEdition.Billing/Services/CardGateway/CardChargingServiceCollectionExtensions.cs \
        MLMConquerorGlobalEdition.Billing/Program.cs \
        MLMConquerorGlobalEdition.Billing/appsettings.json \
        MLMConquerorGlobalEdition.Billing.Tests/DI/CardChargingDiTests.cs
git commit -m "AddCardCharging registra una vez el cobro con tarjeta (gateway, router, reparto, conversion y orquestador) y Billing registra Spreedly apagado"
```

---

## Fase 3 — El alta cobra con tarjeta (SignupAPI)

> La Fase 2 (el componente) va después de la Fase 3 en este documento porque el componente consume los endpoints que se crean aquí. Se pueden ejecutar en ese orden sin problema.

### Task 12: Piezas del alta con tarjeta: errores, pasos, ruteo y disponibilidad

**Files:**
- Create: `MLMConquerorGlobalEdition.SignupAPI/Services/Card/SignupCardErrors.cs`
- Create: `MLMConquerorGlobalEdition.SignupAPI/Services/Card/SignupCardRouting.cs`
- Create: `MLMConquerorGlobalEdition.SignupAPI/Services/Card/SignupCardOptions.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Card/SignupCardPiezasTests.cs`


- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.Spreedly;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SignupAPI.Services.Card;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Card;

public class SignupCardPiezasTests
{
    // El escenario completo (EscenarioTarjeta) llega con la Tarea 13; aquí bastan estas dos piezas.
    private static GatewayRoutingPlan Plan(params (CardProcessor Procesador, string Moneda, decimal Importe, int Espera)[] pasos)
        => new()
        {
            RouteBucketKey = "bucket-pruebas",
            Steps = pasos.Select((s, i) => new GatewayAttemptPlan
            {
                CardProcessor = s.Procesador, PresentmentCurrency = s.Moneda, Amount = s.Importe, FallbackStepIndex = i, DelayMinutes = s.Espera
            }).ToList()
        };

    private static IConfiguration Configuracion(bool tarjeta) => TarjetaDelAlta.Con(tarjeta ? "true" : "false");

    // ── Ruteo: lo autenticado y lo cobrado son lo mismo ──────────────────────────

    [Fact]
    public void Solo_usa_pasos_inmediatos_con_la_moneda_y_el_importe_del_primario()
    {
        var plan = Plan(
            (CardProcessor.CheckoutEUR, "EUR", 74.10m, 0),
            (CardProcessor.NmiSpreedly, "USD", 80m, 0),
            (CardProcessor.Shift4, "EUR", 74.10m, 0),
            (CardProcessor.CheckoutUS, "EUR", 74.10m, 60),
            (CardProcessor.CheckoutEUR, "EUR", 74.10m, 0));

        SignupCardRouting.UsableSteps(plan).Select(s => s.CardProcessor)
            .Should().Equal(CardProcessor.CheckoutEUR, CardProcessor.Shift4);
    }

    [Fact]
    public void Sin_pasos_no_hay_nada_que_usar()
        => SignupCardRouting.UsableSteps(Plan()).Should().BeEmpty();

    [Fact]
    public void El_plan_congelado_se_reconstruye_con_la_moneda_y_el_importe_autenticados()
    {
        var p = new SignupCardPayment { RouteBucketKey = "b", ChargeAmount = 74.10m, ChargeCurrency = "EUR" };
        p.SetPlannedProcessors([CardProcessor.CheckoutEUR, CardProcessor.Shift4]);

        var plan = SignupCardRouting.Rebuild(p);

        plan.RouteBucketKey.Should().Be("b");
        plan.Steps.Should().HaveCount(2);
        plan.Steps.Should().OnlyContain(s => s.PresentmentCurrency == "EUR" && s.Amount == 74.10m && s.DelayMinutes == 0);
        plan.Steps.Select(s => s.CardProcessor).Should().Equal(CardProcessor.CheckoutEUR, CardProcessor.Shift4);
    }

    // ── Lo que ve la pantalla ───────────────────────────────────────────────────

    [Theory]
    [InlineData("NO_LIABILITY_SHIFT", SignupCardMessages.AuthenticationFailed)]
    [InlineData("AUTH_FAILED", SignupCardMessages.AuthenticationFailed)]
    [InlineData("DECLINED:SPREEDLY_DECLINED", SignupCardMessages.Declined)]
    [InlineData("CHARGE_DECLINED", SignupCardMessages.Declined)]
    [InlineData("CANCELLED", SignupCardMessages.Cancelled)]
    [InlineData("AUTH_ABANDONED", SignupCardMessages.Abandoned)]
    [InlineData("ORDER_ALREADY_SETTLED", SignupCardMessages.AlreadyPaid)]
    public void Un_fallo_se_explica_sin_detalles_internos(string codigo, string texto)
        => SignupCardSteps.From(new SignupCardPayment { Status = SignupCardPaymentStatus.Failed, FailureCode = codigo })
            .Message.Should().Be(texto);

    [Fact]
    public void El_paso_de_3ds_solo_va_mientras_se_autentica()
    {
        var tx = new SpreedlyTransaction("TX1", "pending", false, "Sca::Authentication", 8000, "USD", "o", "pm", "challenge",
            null, "<form/>", "https://acs.example/c", null, null, null, null, null, null);

        var autenticando = SignupCardSteps.From(new SignupCardPayment { Id = "r1", Status = SignupCardPaymentStatus.Authenticating }, tx);
        autenticando.ThreeDs!.TransactionToken.Should().Be("TX1");
        autenticando.ThreeDs.RequiredAction.Should().Be("challenge");
        autenticando.ThreeDs.ChallengeForm.Should().Be("<form/>");
        autenticando.Status.Should().Be(SignupCardStatuses.Authenticating);

        SignupCardSteps.From(new SignupCardPayment { Status = SignupCardPaymentStatus.Paid }, tx).ThreeDs.Should().BeNull();
    }

    // ── Disponibilidad (payment-options) ────────────────────────────────────────

    private static ISpreedlyCredentialSource Credenciales(bool hay)
    {
        var m = new Mock<ISpreedlyCredentialSource>();
        m.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
         .ReturnsAsync(hay ? new SpreedlyCredentials("ENVKEY1", "S", "SCA", null) : null);
        return m.Object;
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task Sin_interruptor_sin_Spreedly_o_sin_credenciales_no_se_ofrece(bool interruptor, bool spreedly, bool credenciales)
    {
        var o = new SignupCardOptions(new SpreedlyOptions { Enabled = spreedly }, Credenciales(credenciales), Configuracion(interruptor));
        var r = await o.GetAsync(default);
        r.Ready.Should().BeFalse();
        r.EnvironmentKey.Should().BeNull();
    }

    [Fact]
    public async Task Encendido_y_con_credenciales_da_la_clave_publica_del_entorno()
    {
        var o = new SignupCardOptions(new SpreedlyOptions { Enabled = true }, Credenciales(true), Configuracion(true));
        var r = await o.GetAsync(default);
        r.Should().Be(new SignupCardAvailability(true, "ENVKEY1", Simulated: false));
    }

    [Fact]
    public async Task Con_el_simulador_avisa_a_la_pantalla()
    {
        var o = new SignupCardOptions(new SpreedlyOptions { Enabled = true, UseFake = true }, new FakeSpreedlyCredentialSource(), Configuracion(true));
        (await o.GetAsync(default)).Simulated.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SignupCardPiezasTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `SignupCardErrors.cs`**

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Services.Spreedly;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.SignupAPI.Services.Card;

/// <summary>Códigos de error del alta con tarjeta que ve la pantalla (y su código HTTP en SignupsController).</summary>
public static class SignupCardErrors
{
    /// <summary>Sin credenciales de Spreedly en el entorno. 503: elegir otro método.</summary>
    public const string NotConfigured   = "CARD_NOT_CONFIGURED";
    /// <summary>El token del iFrame no existe en Spreedly, no es una tarjeta o está caducada. 400.</summary>
    public const string InvalidCard     = "CARD_INVALID";
    /// <summary>El router no da ningún gateway inmediato para esta tarjeta y país. 503.</summary>
    public const string NoGateway       = "CARD_NO_GATEWAY";
    /// <summary>Otro intento con tarjeta de este alta está en curso. 409.</summary>
    public const string InProgress      = "CARD_IN_PROGRESS";
    /// <summary>Spreedly no respondió al empezar. 502: reintentar u otro método.</summary>
    public const string Unavailable     = "CARD_PROVIDER_UNAVAILABLE";
    public const string NothingToPay    = "CARD_NOTHING_TO_PAY";
    public const string PaymentNotFound = "CARD_PAYMENT_NOT_FOUND";
}

/// <summary>Textos para la persona (inglés, como el resto del alta). Nunca detalles internos.</summary>
public static class SignupCardMessages
{
    public const string AuthenticationFailed = "Your bank could not verify this card. Please try another card or payment method.";
    public const string Declined             = "The card was declined. Please try another card or payment method.";
    public const string Cancelled            = "The card payment was cancelled.";
    public const string Abandoned            = "The card verification expired. Please try again.";
    public const string AlreadyPaid          = "This signup is already paid. Please sign in.";
    public const string Review               = "We could not confirm your payment yet. Our team will review it; please do not pay again.";
    public const string Unavailable          = "We couldn't reach the card processor. Please try again or choose another payment method.";
    public const string NotConfigured        = "Card payments are temporarily unavailable. Please choose another payment method.";
    public const string InvalidCard          = "This card can't be used. Please check it or use another card.";
    public const string NoGateway            = "Card payments are not available for this card right now. Please choose another payment method.";
    public const string InProgress           = "A card payment for this signup is already being verified. Please wait a few minutes and try again.";
}

/// <summary>Lo que la pantalla recibe de un intento: estado, texto y, mientras se autentica, el paso de 3DS.</summary>
public static class SignupCardSteps
{
    public static SignupCardStepDto From(SignupCardPayment p, SpreedlyTransaction? threeDs = null) => new()
    {
        PaymentRef = p.Id,
        Status     = p.Status.ToString(),
        Message    = MessageFor(p),
        ThreeDs    = p.Status == SignupCardPaymentStatus.Authenticating && threeDs is not null ? ThreeDsFrom(threeDs) : null
    };

    public static SpreedlyThreeDsStepDto ThreeDsFrom(SpreedlyTransaction t) => new()
    {
        TransactionToken      = t.Token,
        State                 = t.State,
        RequiredAction        = t.RequiredAction,
        DeviceFingerprintForm = t.DeviceFingerprintForm,
        ChallengeForm         = t.ChallengeForm,
        ChallengeUrl          = t.ChallengeUrl,
        CheckoutForm          = t.CheckoutForm,
        CheckoutUrl           = t.CheckoutUrl
    };

    public static string? MessageFor(SignupCardPayment p) => p.Status switch
    {
        SignupCardPaymentStatus.ToReview => SignupCardMessages.Review,
        SignupCardPaymentStatus.Failed   => p.FailureCode switch
        {
            "CANCELLED"             => SignupCardMessages.Cancelled,
            "AUTH_ABANDONED"        => SignupCardMessages.Abandoned,
            "ORDER_ALREADY_SETTLED" => SignupCardMessages.AlreadyPaid,
            "AUTH_UNAVAILABLE"      => SignupCardMessages.Unavailable,
            { } c when c.StartsWith("DECLINED", StringComparison.Ordinal) || c == "CHARGE_DECLINED" => SignupCardMessages.Declined,
            _                       => SignupCardMessages.AuthenticationFailed
        },
        _ => null
    };
}
```

- [ ] **Step 4: Crear `SignupCardRouting.cs`**

```csharp
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;

namespace MLMConquerorGlobalEdition.SignupAPI.Services.Card;

/// <summary>
/// El plan de ruteo del alta con tarjeta (decisión I7). Se resuelve una vez, antes de autenticar, y se congela
/// en la fila: lo autenticado (importe y moneda del primario) es exactamente lo que se cobra, en cualquiera
/// de los procesadores que sigan.
/// </summary>
public static class SignupCardRouting
{
    /// <summary>
    /// El primario y los fallbacks INMEDIATOS con su misma moneda e importe, en orden y sin repetir procesador.
    /// Un paso diferido no sirve (la autenticación 3DS es de ahora) y uno en otra moneda rompería el liability shift.
    /// </summary>
    public static IReadOnlyList<GatewayAttemptPlan> UsableSteps(GatewayRoutingPlan plan)
    {
        var primary = plan.Steps.FirstOrDefault();
        if (primary is null || primary.DelayMinutes > 0)
            return [];

        return plan.Steps
            .Where(s => s.DelayMinutes == 0
                     && string.Equals(s.PresentmentCurrency, primary.PresentmentCurrency, StringComparison.OrdinalIgnoreCase)
                     && s.Amount == primary.Amount)
            .DistinctBy(s => s.CardProcessor)
            .ToList();
    }

    /// <summary>El plan congelado de un intento, para el orquestador.</summary>
    public static GatewayRoutingPlan Rebuild(SignupCardPayment p) => new()
    {
        RouteBucketKey = p.RouteBucketKey,
        Steps = p.GetPlannedProcessors().Select((processor, i) => new GatewayAttemptPlan
        {
            CardProcessor       = processor,
            PresentmentCurrency = p.ChargeCurrency,
            Amount              = p.ChargeAmount,
            FallbackStepIndex   = i,
            DelayMinutes        = 0
        }).ToList()
    };
}
```

- [ ] **Step 5: Crear `SignupCardOptions.cs`**

```csharp
using MLMConquerorGlobalEdition.Repository.Services.Spreedly;

namespace MLMConquerorGlobalEdition.SignupAPI.Services.Card;

/// <summary>Si el alta ofrece tarjeta y con qué clave pública inicializa el iFrame.</summary>
public sealed record SignupCardAvailability(bool Ready, string? EnvironmentKey, bool Simulated)
{
    public static readonly SignupCardAvailability Off = new(false, null, false);
}

public interface ISignupCardOptions
{
    Task<SignupCardAvailability> GetAsync(CancellationToken ct);
}

/// <summary>
/// La tarjeta se ofrece solo si las tres cosas se cumplen (decisión I18): Signup:CardEnabled, Spreedly:Enabled
/// y credenciales del entorno (o el simulador). La environment key es pública: es lo que el iFrame manda a
/// core.spreedly.com desde el navegador.
/// </summary>
public sealed class SignupCardOptions : ISignupCardOptions
{
    private readonly SpreedlyOptions            _options;
    private readonly ISpreedlyCredentialSource  _credentials;
    private readonly IConfiguration             _configuration;

    public SignupCardOptions(SpreedlyOptions options, ISpreedlyCredentialSource credentials, IConfiguration configuration)
    {
        _options       = options;
        _credentials   = credentials;
        _configuration = configuration;
    }

    public async Task<SignupCardAvailability> GetAsync(CancellationToken ct)
    {
        if (!SignupCardPayments.IsEnabled(_configuration) || !_options.Enabled)
            return SignupCardAvailability.Off;

        var credentials = await _credentials.GetAsync(ct);
        return credentials is null
            ? SignupCardAvailability.Off
            : new SignupCardAvailability(true, credentials.EnvironmentKey, _options.UseFake);
    }
}
```

- [ ] **Step 6: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SignupCardPiezasTests -nologo`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add MLMConquerorGlobalEdition.SignupAPI/Services/Card/SignupCardErrors.cs \
        MLMConquerorGlobalEdition.SignupAPI/Services/Card/SignupCardRouting.cs \
        MLMConquerorGlobalEdition.SignupAPI/Services/Card/SignupCardOptions.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Card/SignupCardPiezasTests.cs
git commit -m "Piezas del alta con tarjeta: codigos y textos para la pantalla, plan de ruteo congelado con la moneda e importe autenticados, y disponibilidad segun interruptor, Spreedly y credenciales"
```

---

### Task 13: Liquidar un intento (`SignupCardSettlement`), el único camino que cobra y activa

**Files:**
- Create: `MLMConquerorGlobalEdition.Signups.Tests/Card/EscenarioTarjeta.cs`
- Create: `MLMConquerorGlobalEdition.SignupAPI/Services/Card/SignupCardSettlement.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Card/SignupCardSettlementTests.cs`

- [ ] **Step 1: Crear el escenario de pruebas `EscenarioTarjeta.cs`** (lo usan todas las pruebas de tarjeta del alta)

```csharp
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Spreedly;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Services.Card;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Card;

/// <summary>
/// El aspirante del escenario de Entrax (fase 1 y productos hechos, todo pendiente), pagando con tarjeta
/// contra el simulador de Spreedly. El orquestador es un doble: el cobro de verdad lo prueban Billing.Tests.
/// </summary>
internal static class EscenarioTarjeta
{
    public static DateTime Ahora => EscenarioEntrax.Ahora;
    public const string OrderId  = EscenarioEntrax.OrderId;
    public const string MemberId = EscenarioEntrax.MemberId;
    public const string Email    = EscenarioEntrax.Email;
    public const decimal Total   = EscenarioEntrax.Total;

    public static SpreedlyOptions Opciones(Action<SpreedlyOptions>? ajustar = null)
    {
        var o = new SpreedlyOptions { Enabled = true, UseFake = true };
        ajustar?.Invoke(o);
        return o;
    }

    public static Task SembrarAltaAsync(AppDbContext db, OrderStatus estado = OrderStatus.Pending)
        => EscenarioEntrax.SembrarAltaAsync(db, estado, Total, pais: "US");

    public static SignupCardPaymentRequestDto Pedido(string escenario)
        => new() { PaymentMethodToken = SpreedlyFakeScenarios.Token(escenario), BrowserInfo = "simulated-browser-info" };

    /// <summary>Una autenticación hecha en el simulador para la tarjeta del escenario; devuelve (token del medio de pago, transacción).</summary>
    public static async Task<(string Pm, SpreedlyTransaction Auth)> AutenticarAsync(
        FakeSpreedlyClient fake, string escenario, string externalOrderId, long importeMenor = 8000, string moneda = "USD")
    {
        var pm = SpreedlyFakeScenarios.Token(escenario);
        var r  = await fake.AuthenticateAsync(new SpreedlyAuthenticateRequest("fake_sca_provider", pm, importeMenor, moneda, "simulated", externalOrderId, null), default);
        return (pm, r.Value!);
    }

    public static async Task<SignupCardPayment> SembrarIntentoAsync(
        AppDbContext db,
        SignupCardPaymentStatus estado,
        string paymentMethodToken,
        string? scaTransactionToken,
        int intento = 1,
        DateTime? empezado = null,
        DateTime? cobrando = null,
        string moneda = "USD",
        decimal importe = Total,
        params CardProcessor[] procesadores)
    {
        var p = new SignupCardPayment
        {
            OrderId = OrderId, MemberId = MemberId, ExternalOrderId = SignupCardPayment.BuildExternalOrderId(OrderId, intento),
            Status = estado, AmountUsd = Total, ChargeAmount = importe, ChargeCurrency = moneda, RouteBucketKey = "bucket-pruebas",
            PaymentMethodToken = paymentMethodToken, CardBrand = CardBrand.Visa.ToString(), First6 = "411111", Last4 = "1111",
            ExpiryMonth = 12, ExpiryYear = Ahora.Year + 3, ScaTransactionToken = scaTransactionToken,
            StartedAt = empezado ?? Ahora, ChargingStartedAt = cobrando,
            CreatedBy = Email, CreationDate = Ahora, LastUpdateDate = Ahora
        };
        p.SetPlannedProcessors(procesadores.Length == 0 ? [CardProcessor.NmiSpreedly] : procesadores);
        db.SignupCardPayments.Add(p);
        await db.SaveChangesAsync();
        return p;
    }

    public static GatewayRoutingPlan Plan(params (CardProcessor Procesador, string Moneda, decimal Importe, int Espera)[] pasos)
        => new()
        {
            RouteBucketKey = "bucket-pruebas",
            Steps = pasos.Select((s, i) => new GatewayAttemptPlan
            {
                CardProcessor = s.Procesador, PresentmentCurrency = s.Moneda, Amount = s.Importe, FallbackStepIndex = i, DelayMinutes = s.Espera
            }).ToList()
        };

    public static Mock<IGatewayRouter> Router(GatewayRoutingPlan? plan = null)
    {
        var m = new Mock<IGatewayRouter>();
        m.Setup(r => r.ResolveAsync(It.IsAny<GatewayRoutingContext>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<GatewayRoutingPlan>.Success(plan ?? Plan((CardProcessor.NmiSpreedly, "USD", Total, 0))));
        return m;
    }

    public static Result<OrchestratorChargeResult> Cobrado(string tx = "fake_purchase_1", CardProcessor procesador = CardProcessor.NmiSpreedly, string historial = "PH-1")
        => Result<OrchestratorChargeResult>.Success(new OrchestratorChargeResult
        {
            Status = "Success", GatewayTransactionId = tx, ProcessorUsed = procesador.ToString(), PaymentHistoryId = historial,
            AmountCharged = Total, CurrencyCharged = "USD", NetworkTransactionId = "fake_ntid_1"
        });

    public static Mock<IGatewayChargeOrchestrator> Orquestador(Result<OrchestratorChargeResult>? resultado = null)
    {
        var m = new Mock<IGatewayChargeOrchestrator>();
        m.Setup(o => o.ExecuteAsync(It.IsAny<GatewayRoutingPlan>(), It.IsAny<GatewayRoutingContext>(),
                                    It.IsAny<OrchestratorChargeRequest>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(resultado ?? Cobrado());
        return m;
    }

    public static IEncryptionService Cifrado()
    {
        var m = new Mock<IEncryptionService>();
        m.Setup(e => e.Encrypt(It.IsAny<string>())).Returns<string>(s => "ENC:" + s);
        return m.Object;
    }

    public static SignupCardSettlement Liquidacion(
        AppDbContext db, ISpreedlyClient cliente, IGatewayChargeOrchestrator orquestador,
        ApplicationUser usuario, RegistroDePrueba<SignupCardSettlement>? log = null,
        SpreedlyOptions? opciones = null, DateTime? ahora = null)
        => new(db, cliente, orquestador, EscenarioEntrax.Activacion(db), EscenarioEntrax.Usuarios(usuario).Object,
               Cifrado(), opciones ?? Opciones(), EscenarioEntrax.Reloj(ahora).Object,
               log ?? new RegistroDePrueba<SignupCardSettlement>());

    public static IConfiguration Configuracion(bool tarjeta)
        => TarjetaDelAlta.Con(tarjeta ? "true" : "false");
}
```

- [ ] **Step 2: Escribir la prueba que falla**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Spreedly;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SignupAPI.Services.Card;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Card;

/// <summary>
/// El único camino que cobra y activa un alta con tarjeta. Siempre vuelve a consultar la autenticación a
/// Spreedly (no se fía del navegador), exige liability shift, cobra UNA vez por el router y solo después
/// activa. Lo que no se sabe si se cobró nunca se da por no cobrado.
/// </summary>
public class SignupCardSettlementTests
{
    private sealed record Montaje(
        AppDbContext Db, FakeSpreedlyClient Fake, Mock<IGatewayChargeOrchestrator> Orq, ApplicationUser Usuario,
        RegistroDePrueba<SignupCardSettlement> Log, SignupCardSettlement Liquidacion, string Ref, string Pm, string ScaTx);

    private static async Task<Montaje> MontarAsync(
        string escenario = SpreedlyFakeScenarios.Frictionless,
        SignupCardPaymentStatus estado = SignupCardPaymentStatus.Authenticating,
        OrderStatus pedido = OrderStatus.Processing,
        Result<OrchestratorChargeResult>? cobro = null,
        DateTime? empezado = null,
        DateTime? cobrando = null,
        DateTime? ahora = null)
    {
        var db = InMemoryDbHelper.Create();
        await EscenarioTarjeta.SembrarAltaAsync(db, pedido);
        var fake = new FakeSpreedlyClient();
        var externo = SignupCardPayment.BuildExternalOrderId(EscenarioTarjeta.OrderId, 1);
        var (pm, auth) = await EscenarioTarjeta.AutenticarAsync(fake, escenario, externo);
        var fila = await EscenarioTarjeta.SembrarIntentoAsync(db, estado, pm, auth.Token, empezado: empezado, cobrando: cobrando);

        var orq = EscenarioTarjeta.Orquestador(cobro);
        var usuario = EscenarioEntrax.UsuarioInactivo();
        var log = new RegistroDePrueba<SignupCardSettlement>();
        var liq = EscenarioTarjeta.Liquidacion(db, fake, orq.Object, usuario, log, ahora: ahora);
        return new Montaje(db, fake, orq, usuario, log, liq, fila.Id, pm, auth.Token);
    }

    private static Task<SignupCardPayment> Fila(Montaje m) => m.Db.SignupCardPayments.SingleAsync(p => p.Id == m.Ref);

    // ── Autenticado con shift: se cobra y se activa ─────────────────────────────

    [Fact]
    public async Task Autenticado_con_shift_cobra_una_vez_y_despues_activa()
    {
        var m = await MontarAsync();
        OrchestratorChargeRequest? pedido = null;
        m.Orq.Setup(o => o.ExecuteAsync(It.IsAny<GatewayRoutingPlan>(), It.IsAny<GatewayRoutingContext>(), It.IsAny<OrchestratorChargeRequest>(), It.IsAny<CancellationToken>()))
             .Callback<GatewayRoutingPlan, GatewayRoutingContext, OrchestratorChargeRequest, CancellationToken>((_, _, r, _) => pedido = r)
             .ReturnsAsync(EscenarioTarjeta.Cobrado());

        var r = await m.Liquidacion.SettleAsync(m.Ref, SignupCardSettlementSource.Browser, default);

        r.Should().Be(SignupCardSettlementOutcome.Paid);
        var fila = await Fila(m);
        fila.Status.Should().Be(SignupCardPaymentStatus.Paid);
        fila.Consumed.Should().BeTrue();
        fila.Eci.Should().Be("05");
        fila.TransStatus.Should().Be("Y");
        fila.LiabilityShift.Should().BeTrue();
        fila.AuthenticationValuePresent.Should().BeTrue();
        fila.PurchaseTransactionToken.Should().Be("fake_purchase_1");
        fila.NetworkTransactionId.Should().Be("fake_ntid_1");
        fila.PaymentHistoryId.Should().Be("PH-1");

        pedido!.ScaAuthenticationToken.Should().StartWith("fake_sca_");
        pedido.SpreedlyPaymentMethodToken.Should().Be(m.Pm);
        pedido.OrderReference.Should().Be(fila.ExternalOrderId);
        pedido.IsRecurring.Should().BeFalse();

        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Completed);
        (await m.Db.MemberProfiles.SingleAsync()).Status.Should().Be(MemberAccountStatus.Active);
        m.Usuario.IsActive.Should().BeTrue();

        var tarjeta = await m.Db.CreditCards.SingleAsync();
        tarjeta.SpreedlyPaymentMethodToken.Should().Be(m.Pm);
        tarjeta.Last4.Should().Be("1111");
        tarjeta.EncryptedPan.Should().BeEmpty("el PAN no lo tenemos nunca");
        tarjeta.EncryptedCvv.Should().BeNull();
        tarjeta.Gateway.Should().Be("spreedly");
    }

    [Fact]
    public async Task Un_segundo_aviso_no_vuelve_a_cobrar()
    {
        var m = await MontarAsync();
        await m.Liquidacion.SettleAsync(m.Ref, SignupCardSettlementSource.Browser, default);

        var segunda = await m.Liquidacion.SettleAsync(m.Ref, SignupCardSettlementSource.Webhook, default);

        segunda.Should().Be(SignupCardSettlementOutcome.AlreadyResolved);
        m.Orq.Verify(o => o.ExecuteAsync(It.IsAny<GatewayRoutingPlan>(), It.IsAny<GatewayRoutingContext>(), It.IsAny<OrchestratorChargeRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Con_challenge_sin_resolver_sigue_autenticando_y_no_cobra()
    {
        var m = await MontarAsync(SpreedlyFakeScenarios.Challenge);

        (await m.Liquidacion.SettleAsync(m.Ref, SignupCardSettlementSource.Poll, default)).Should().Be(SignupCardSettlementOutcome.StillAuthenticating);
        m.Orq.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Con_challenge_aprobado_cobra_y_anota_el_flujo()
    {
        var m = await MontarAsync(SpreedlyFakeScenarios.Challenge);
        m.Fake.Resolve(m.ScaTx, approve: true);

        (await m.Liquidacion.SettleAsync(m.Ref, SignupCardSettlementSource.Browser, default)).Should().Be(SignupCardSettlementOutcome.Paid);
        (await Fila(m)).FlowPerformed.Should().Be("challenge");
    }

    // ── Nunca se cobra sin 3DS ───────────────────────────────────────────────────

    [Theory]
    [InlineData(SpreedlyFakeScenarios.NoShift, "NO_LIABILITY_SHIFT")]
    [InlineData(SpreedlyFakeScenarios.Failed, "AUTH_FAILED")]
    public async Task Sin_shift_o_con_3ds_fallido_no_cobra_y_el_pedido_vuelve_a_Pending(string escenario, string codigo)
    {
        var m = await MontarAsync(escenario);

        (await m.Liquidacion.SettleAsync(m.Ref, SignupCardSettlementSource.Browser, default)).Should().Be(SignupCardSettlementOutcome.Failed);

        var fila = await Fila(m);
        fila.Status.Should().Be(SignupCardPaymentStatus.Failed);
        fila.FailureCode.Should().Be(codigo);
        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Pending);
        m.Orq.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Si_Spreedly_dice_otra_tarjeta_u_otro_pedido_no_cobra()
    {
        var m = await MontarAsync();
        var fila = await Fila(m);
        fila.PaymentMethodToken = "fake_frictionless_otra";
        await m.Db.SaveChangesAsync();

        (await m.Liquidacion.SettleAsync(m.Ref, SignupCardSettlementSource.Webhook, default)).Should().Be(SignupCardSettlementOutcome.Failed);
        (await Fila(m)).FailureCode.Should().Be("PROVIDER_MISMATCH");
        m.Log.Entradas.Should().Contain(e => e.Nivel == LogLevel.Critical);
        m.Orq.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Si_el_alta_ya_se_activo_por_otro_medio_no_se_cobra()
    {
        var m = await MontarAsync(pedido: OrderStatus.Completed);

        (await m.Liquidacion.SettleAsync(m.Ref, SignupCardSettlementSource.Browser, default)).Should().Be(SignupCardSettlementOutcome.Failed);
        (await Fila(m)).FailureCode.Should().Be("ORDER_ALREADY_SETTLED");
        m.Orq.VerifyNoOtherCalls();
    }

    // ── El cobro ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Un_rechazo_explicito_no_cobro_y_el_pedido_vuelve_a_Pending()
    {
        var m = await MontarAsync(cobro: Result<OrchestratorChargeResult>.Failure("SPREEDLY_DECLINED", "Declined"));

        (await m.Liquidacion.SettleAsync(m.Ref, SignupCardSettlementSource.Browser, default)).Should().Be(SignupCardSettlementOutcome.Failed);
        (await Fila(m)).FailureCode.Should().Be("DECLINED:SPREEDLY_DECLINED");
        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Pending);
        (await m.Db.MemberProfiles.SingleAsync()).Status.Should().Be(MemberAccountStatus.Pending);
    }

    [Fact]
    public async Task Un_resultado_dudoso_queda_cobrando_sin_activar_ni_soltar_el_pedido()
    {
        var m = await MontarAsync(cobro: Result<OrchestratorChargeResult>.Failure("SPREEDLY_RESULT_UNKNOWN", "?"));

        (await m.Liquidacion.SettleAsync(m.Ref, SignupCardSettlementSource.Browser, default)).Should().Be(SignupCardSettlementOutcome.Charging);
        (await Fila(m)).Status.Should().Be(SignupCardPaymentStatus.Charging);
        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Processing);
        (await m.Db.MemberProfiles.SingleAsync()).Status.Should().Be(MemberAccountStatus.Pending);
    }

    [Fact]
    public async Task Un_cobro_simulado_nunca_activa()
    {
        var m = await MontarAsync(cobro: EscenarioTarjeta.Cobrado(tx: "simulated-spreedly-txn-1"));

        (await m.Liquidacion.SettleAsync(m.Ref, SignupCardSettlementSource.Browser, default)).Should().Be(SignupCardSettlementOutcome.ToReview);
        (await Fila(m)).FailureCode.Should().Be("CARD_SIMULATED_CHARGE");
        (await m.Db.Orders.SingleAsync()).Status.Should().NotBe(OrderStatus.Completed);
    }

    // ── Conciliación de lo que quedó cobrando ───────────────────────────────────

    [Fact]
    public async Task Cobrando_con_PaymentHistory_capturado_se_da_por_cobrado_y_activa()
    {
        var m = await MontarAsync(estado: SignupCardPaymentStatus.Charging, cobrando: EscenarioTarjeta.Ahora);
        m.Db.PaymentHistories.Add(new PaymentHistory
        {
            OrderId = EscenarioTarjeta.OrderId, MemberId = EscenarioTarjeta.MemberId, Amount = 80m, GatewayName = "NmiSpreedly",
            GatewayTransactionId = "fake_purchase_X", TransactionStatus = PaymentHistoryTransactionStatus.Captured,
            CreatedBy = "billing-engine", CreationDate = EscenarioTarjeta.Ahora, LastUpdateDate = EscenarioTarjeta.Ahora
        });
        await m.Db.SaveChangesAsync();

        (await m.Liquidacion.SettleAsync(m.Ref, SignupCardSettlementSource.Sweep, default)).Should().Be(SignupCardSettlementOutcome.Paid);
        (await Fila(m)).PurchaseTransactionToken.Should().Be("fake_purchase_X");
        m.Orq.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Cobrando_sin_rastro_local_pregunta_a_Spreedly_por_nuestro_order_id()
    {
        var m = await MontarAsync(SpreedlyFakeScenarios.Unknown, estado: SignupCardPaymentStatus.Charging, cobrando: EscenarioTarjeta.Ahora);
        var auth = (await m.Fake.GetTransactionAsync(m.ScaTx, default)).Value!;
        var compra = await m.Fake.PurchaseAsync(new SpreedlyPurchaseRequest("fake_gateway_NmiSpreedly", m.Pm, 8000, "USD",
            SignupCardPayment.BuildExternalOrderId(EscenarioTarjeta.OrderId, 1), "x", auth.Sca!.Token!, true), default);
        m.Fake.Resolve(compra.Value!.Token, approve: true);

        (await m.Liquidacion.SettleAsync(m.Ref, SignupCardSettlementSource.Sweep, default)).Should().Be(SignupCardSettlementOutcome.Paid);
        var historial = await m.Db.PaymentHistories.SingleAsync();
        historial.GatewayTransactionId.Should().Be(compra.Value.Token);
        historial.TransactionStatus.Should().Be(PaymentHistoryTransactionStatus.Captured);
    }

    [Fact]
    public async Task Cobrando_y_rechazado_en_Spreedly_no_se_cobro()
    {
        var m = await MontarAsync(SpreedlyFakeScenarios.Unknown, estado: SignupCardPaymentStatus.Charging, cobrando: EscenarioTarjeta.Ahora);
        var auth = (await m.Fake.GetTransactionAsync(m.ScaTx, default)).Value!;
        var compra = await m.Fake.PurchaseAsync(new SpreedlyPurchaseRequest("fake_gateway_NmiSpreedly", m.Pm, 8000, "USD",
            SignupCardPayment.BuildExternalOrderId(EscenarioTarjeta.OrderId, 1), "x", auth.Sca!.Token!, true), default);
        m.Fake.Resolve(compra.Value!.Token, approve: false);

        (await m.Liquidacion.SettleAsync(m.Ref, SignupCardSettlementSource.Sweep, default)).Should().Be(SignupCardSettlementOutcome.Failed);
        (await Fila(m)).FailureCode.Should().Be("CHARGE_DECLINED");
        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task Cobrando_sin_noticias_pasada_la_gracia_va_a_revision_y_suelta_el_pedido()
    {
        var m = await MontarAsync(estado: SignupCardPaymentStatus.Charging, cobrando: EscenarioTarjeta.Ahora.AddMinutes(-45));

        (await m.Liquidacion.SettleAsync(m.Ref, SignupCardSettlementSource.Sweep, default)).Should().Be(SignupCardSettlementOutcome.ToReview);
        (await Fila(m)).FailureCode.Should().Be("CHARGE_OUTCOME_UNKNOWN");
        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Pending);
        m.Log.Entradas.Should().Contain(e => e.Nivel == LogLevel.Critical);
    }

    [Fact]
    public async Task Cobrando_sin_noticias_dentro_de_la_gracia_espera()
    {
        var m = await MontarAsync(estado: SignupCardPaymentStatus.Charging, cobrando: EscenarioTarjeta.Ahora.AddMinutes(-5));
        (await m.Liquidacion.SettleAsync(m.Ref, SignupCardSettlementSource.Sweep, default)).Should().Be(SignupCardSettlementOutcome.Charging);
        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Processing);
    }

    // ── Cobrado pero el alta ya estaba activada ─────────────────────────────────

    [Fact]
    public async Task Cobrado_con_el_alta_ya_activada_queda_a_revision_con_Critical()
    {
        var m = await MontarAsync(estado: SignupCardPaymentStatus.Charged, pedido: OrderStatus.Completed);

        (await m.Liquidacion.SettleAsync(m.Ref, SignupCardSettlementSource.Sweep, default)).Should().Be(SignupCardSettlementOutcome.ToReview);
        (await Fila(m)).FailureCode.Should().Be("ORDER_ALREADY_SETTLED");
        m.Log.Entradas.Should().Contain(e => e.Nivel == LogLevel.Critical);
    }

    // ── Abandono y cancelación ───────────────────────────────────────────────────

    [Fact]
    public async Task Un_challenge_abandonado_lo_cierra_el_barrido_y_no_la_pantalla()
    {
        var m = await MontarAsync(SpreedlyFakeScenarios.Challenge, empezado: EscenarioTarjeta.Ahora.AddMinutes(-25));

        (await m.Liquidacion.SettleAsync(m.Ref, SignupCardSettlementSource.Poll, default)).Should().Be(SignupCardSettlementOutcome.StillAuthenticating);
        (await m.Liquidacion.SettleAsync(m.Ref, SignupCardSettlementSource.Sweep, default)).Should().Be(SignupCardSettlementOutcome.Failed);
        (await Fila(m)).FailureCode.Should().Be("AUTH_ABANDONED");
        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task Cancelar_solo_cierra_lo_que_aun_se_autentica()
    {
        var m = await MontarAsync(SpreedlyFakeScenarios.Challenge);

        (await m.Liquidacion.CancelAsync(m.Ref, default)).Should().BeTrue();
        (await Fila(m)).FailureCode.Should().Be("CANCELLED");
        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Pending);

        var cobrando = await MontarAsync(estado: SignupCardPaymentStatus.Charging, cobrando: EscenarioTarjeta.Ahora);
        (await cobrando.Liquidacion.CancelAsync(cobrando.Ref, default)).Should().BeFalse("en Charging pudo haberse cobrado");
    }

    [Fact]
    public async Task Un_intento_que_no_existe()
        => (await (await MontarAsync()).Liquidacion.SettleAsync("no-existe", SignupCardSettlementSource.Admin, default))
            .Should().Be(SignupCardSettlementOutcome.NotFound);
}
```

- [ ] **Step 3: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SignupCardSettlementTests -nologo`
Expected: FAIL de compilación (`SignupCardSettlement` no existe).

- [ ] **Step 4: Crear `SignupCardSettlement.cs`**

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Spreedly;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SignupAPI.Services.Card;

public enum SignupCardSettlementSource { Browser, Poll, Webhook, Sweep, Admin }

public enum SignupCardSettlementOutcome
{
    NotFound,
    Paid,
    AlreadyResolved,
    StillAuthenticating,
    Charging,
    Failed,
    ToReview,
    ProviderUnavailable
}

public interface ISignupCardSettlement
{
    /// <summary>Lleva el intento a su estado real según Spreedly; si está autenticado con shift, cobra y activa.</summary>
    Task<SignupCardSettlementOutcome> SettleAsync(string paymentRef, SignupCardSettlementSource source, CancellationToken ct);

    /// <summary>Cierra un intento que SOLO se estaba autenticando (no se cobró nada). False si ya no estaba ahí.</summary>
    Task<bool> CancelAsync(string paymentRef, CancellationToken ct);
}

/// <summary>
/// EL ÚNICO CAMINO QUE COBRA Y ACTIVA UN ALTA PAGADA CON TARJETA. Le llegan la pantalla (al terminar el
/// Lifecycle o consultando el estado), el callback de Spreedly, el barrido y el Re-check del admin.
///
///   Authenticating ─(GET a Spreedly: succeeded + liability shift + mismo importe, tarjeta y pedido)─▶
///   Charging (UPDATE condicional: solo uno cobra) ─(orquestador: purchase con sca_authentication_token)─▶
///   Charged (guardado ANTES de activar) ─(SignupOrderClaim + ISignupActivationService)─▶ Paid
///
/// Lo que el legado hacía mal y aquí no: no se fía del navegador (siempre re-consulta); no hay cobro sin
/// 3DS ni rebaja a "sin 3DS"; "pending" nunca es pagado; un timeout no es "no cobrado" (queda Charging y se
/// concilia); y se cobra antes de activar, nunca al revés.
///
/// Una vez empezado no se cancela con la petición: lo que mueve dinero usa CancellationToken.None.
/// </summary>
public sealed class SignupCardSettlement : ISignupCardSettlement
{
    /// <summary>Lo que se espera a que un intento sin token de autenticación lo reciba antes de cerrarlo.</summary>
    public static readonly TimeSpan NoTokenGrace = TimeSpan.FromMinutes(5);

    private const int    MaxAttempts        = 3;
    private const string Actor              = "SignupCard";
    private const string OrderConsumedIndex = "UX_SignupCardPayments_OrderId_Consumed";

    private readonly AppDbContext                       _db;
    private readonly ISpreedlyClient                    _client;
    private readonly IGatewayChargeOrchestrator         _orchestrator;
    private readonly ISignupActivationService           _activation;
    private readonly UserManager<ApplicationUser>       _users;
    private readonly IEncryptionService                 _encryption;
    private readonly SpreedlyOptions                    _options;
    private readonly IDateTimeProvider                  _clock;
    private readonly ILogger<SignupCardSettlement>      _logger;
    private readonly ISignupOrderClaim                  _orderClaim;

    public SignupCardSettlement(
        AppDbContext db, ISpreedlyClient client, IGatewayChargeOrchestrator orchestrator,
        ISignupActivationService activation, UserManager<ApplicationUser> users, IEncryptionService encryption,
        SpreedlyOptions options, IDateTimeProvider clock, ILogger<SignupCardSettlement> logger,
        ISignupOrderClaim? orderClaim = null)
    {
        _db           = db;
        _client       = client;
        _orchestrator = orchestrator;
        _activation   = activation;
        _users        = users;
        _encryption   = encryption;
        _options      = options;
        _clock        = clock;
        _logger       = logger;
        _orderClaim   = orderClaim ?? new SignupOrderClaim(db);
    }

    public async Task<SignupCardSettlementOutcome> SettleAsync(string paymentRef, SignupCardSettlementSource source, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await SettleOnceAsync(paymentRef, source);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts)
            {
                // Otro camino guardó la misma fila (o el miembro) entre la lectura y el guardado: se relee.
                _db.ChangeTracker.Clear();
                _logger.LogWarning("Tarjeta del alta: carrera al liquidar {PaymentRef} ({Source}); se vuelve a leer.", paymentRef, source);
            }
        }
    }

    private async Task<SignupCardSettlementOutcome> SettleOnceAsync(string id, SignupCardSettlementSource source)
    {
        var now = _clock.Now;
        var p = await _db.SignupCardPayments.FirstOrDefaultAsync(x => x.Id == id, CancellationToken.None);
        if (p is null)
            return SignupCardSettlementOutcome.NotFound;

        switch (p.Status)
        {
            case SignupCardPaymentStatus.Paid:
                await EnsureUserActiveAsync(p.Id);
                return SignupCardSettlementOutcome.AlreadyResolved;
            case SignupCardPaymentStatus.Failed or SignupCardPaymentStatus.ToReview:
                return SignupCardSettlementOutcome.AlreadyResolved;
            case SignupCardPaymentStatus.Charged:
                return await ActivateAsync(p, now, source);
            case SignupCardPaymentStatus.Charging:
                return await ReconcileChargingAsync(p, now, source);
            default:
                return await AuthenticatingAsync(p, now, source);
        }
    }

    // ── 1. Autenticación: siempre lo que diga Spreedly ──────────────────────────

    private async Task<SignupCardSettlementOutcome> AuthenticatingAsync(SignupCardPayment p, DateTime now, SignupCardSettlementSource source)
    {
        var none    = CancellationToken.None;
        var expired = now - p.StartedAt > _options.AuthenticationTimeout;

        if (p.ScaTransactionToken is null)
            return now - p.StartedAt < NoTokenGrace
                ? SignupCardSettlementOutcome.StillAuthenticating
                : await FailAsync(p, "NO_AUTH_TOKEN", now);

        var call = await _client.GetTransactionAsync(p.ScaTransactionToken, none);
        _db.SpreedlyApiCalls.Add(SpreedlyCallLog.From(SpreedlyCallLog.GetTransaction, call, p.ScaTransactionToken, p.ExternalOrderId, now, $"{Actor}:{source}"));
        p.LastCheckedAt = now;
        await _db.SaveChangesAsync(none);

        if (!call.Ok)
            return source == SignupCardSettlementSource.Sweep && expired
                ? await FailAsync(p, "AUTH_ABANDONED", now)
                : SignupCardSettlementOutcome.ProviderUnavailable;

        var tx = call.Value!;

        // Lo que dice Spreedly tiene que ser de ESTE intento: la transacción, la tarjeta y nuestro order_id.
        if (!string.Equals(tx.Token, p.ScaTransactionToken, StringComparison.Ordinal)
            || (tx.PaymentMethodToken is not null && tx.PaymentMethodToken != p.PaymentMethodToken)
            || (tx.OrderId is not null && tx.OrderId != p.ExternalOrderId))
        {
            _logger.LogCritical("Tarjeta del alta {OrderId}: Spreedly devolvió una autenticación que no es de este intento ({PaymentRef}). No se cobra.",
                p.OrderId, p.Id);
            return await FailAsync(p, "PROVIDER_MISMATCH", now);
        }

        if (tx.State == SpreedlyStates.Pending)
            return source == SignupCardSettlementSource.Sweep && expired
                ? await FailAsync(p, "AUTH_ABANDONED", now)
                : SignupCardSettlementOutcome.StillAuthenticating;

        Record3ds(p, tx.Sca);

        if (tx.State != SpreedlyStates.Succeeded)
            return await FailAsync(p, Cut("AUTH_" + tx.State.ToUpperInvariant(), 60)!, now);

        var expectedMinor = SpreedlyCurrency.ToMinorUnits(p.ChargeAmount, p.ChargeCurrency);
        if ((tx.AmountMinor is not null && tx.AmountMinor != expectedMinor)
            || (tx.CurrencyCode is not null && !string.Equals(tx.CurrencyCode, p.ChargeCurrency, StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogCritical("Tarjeta del alta {OrderId}: la autenticación es por {Amount} {Currency} y el intento por {Expected} {ExpectedCurrency}. No se cobra.",
                p.OrderId, tx.AmountMinor, tx.CurrencyCode, expectedMinor, p.ChargeCurrency);
            return await FailAsync(p, "AUTH_AMOUNT_MISMATCH", now);
        }

        var brand    = Enum.TryParse<CardBrand>(p.CardBrand, out var b) ? b : CardBrand.Other;
        var decision = ThreeDsLiabilityPolicy.Evaluate(brand, p.Eci, p.TransStatus);
        p.LiabilityShift = decision.LiabilityShift;
        if (!decision.LiabilityShift)
        {
            p.Notes = decision.Reason;
            return await FailAsync(p, "NO_LIABILITY_SHIFT", now);
        }

        return await ChargeAsync(p, tx.Sca?.Token ?? tx.Token, brand, now, source);
    }

    private static void Record3ds(SignupCardPayment p, SpreedlyScaAuthentication? sca)
    {
        if (sca is null) return;
        p.Eci                        = Cut(sca.Eci?.Trim().PadLeft(2, '0'), 2);
        p.TransStatus                = Cut(sca.TransStatus?.Trim().ToUpperInvariant(), 1);
        p.ThreeDsVersion             = Cut(sca.MessageVersion, 10);
        p.FlowPerformed              = Cut(sca.FlowPerformed, 20);
        p.AuthenticationValuePresent = sca.HasAuthenticationValue;
    }

    // ── 2. Cobro: una vez, por el router ────────────────────────────────────────

    private async Task<SignupCardSettlementOutcome> ChargeAsync(
        SignupCardPayment p, string scaToken, CardBrand brand, DateTime now, SignupCardSettlementSource source)
    {
        var none  = CancellationToken.None;
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == p.OrderId, none);
        if (order is null)
            return await FailAsync(p, "ORDER_NOT_FOUND", now, release: false);

        // Otro medio (token, cripto, un QR de Entrax tardío) ya activó el alta: NO se cobra. Nada que devolver.
        if (order.Status is not (OrderStatus.Pending or OrderStatus.Processing))
            return await FailAsync(p, SignupOrderClaim.AlreadySettledCode, now, release: false);

        if (!await TryClaimForChargeAsync(p, scaToken, now))
        {
            _db.ChangeTracker.Clear();
            return SignupCardSettlementOutcome.AlreadyResolved;
        }

        var routing = new GatewayRoutingContext
        {
            OperationType        = BillingOperationType.Payment,
            CardBrand            = brand,
            CardholderCountryIso = "US",
            AmountUsd            = p.AmountUsd,
            MemberId             = p.MemberId
        };
        var request = new OrchestratorChargeRequest
        {
            MemberId                   = p.MemberId,
            OrderId                    = p.OrderId,
            Description                = $"Signup {order.OrderNo ?? order.Id}",
            SpreedlyPaymentMethodToken = p.PaymentMethodToken,
            ScaAuthenticationToken     = scaToken,
            OrderReference             = p.ExternalOrderId,
            IsRecurring                = false
        };

        Result<OrchestratorChargeResult> result;
        try
        {
            result = await _orchestrator.ExecuteAsync(SignupCardRouting.Rebuild(p), routing, request, none);
        }
        catch (Exception ex)
        {
            // Caída a mitad del cobro: no se sabe si el emisor cargó. Queda Charging; el barrido concilia.
            _db.ChangeTracker.Clear();
            _logger.LogCritical(ex, "Tarjeta del alta {OrderId}: el cobro {ExternalOrderId} terminó sin resultado; se conciliará.",
                p.OrderId, p.ExternalOrderId);
            return SignupCardSettlementOutcome.Charging;
        }

        if (result.IsSuccess && result.Value is { Status: "Success" } charged)
        {
            if (CardChargeOutcomes.IsSimulated(charged.GatewayTransactionId))
                return await ReviewAsync(p, "CARD_SIMULATED_CHARGE", now);

            p.Status                   = SignupCardPaymentStatus.Charged;
            p.ProcessorUsed            = charged.ProcessorUsed;
            p.PurchaseTransactionToken = charged.GatewayTransactionId;
            p.NetworkTransactionId     = charged.NetworkTransactionId;
            p.PaymentHistoryId         = charged.PaymentHistoryId;
            p.ChargedAt                = now;
            p.LastUpdateDate           = now;
            p.LastUpdateBy             = Actor;
            await _db.SaveChangesAsync(none);

            // Primero se cobra (guardado arriba) y después se activa.
            return await ActivateAsync(p, now, source);
        }

        if (result.IsSuccess)
            // Solo hay pasos inmediatos: "Scheduled" no debería llegar. Si llega, hay un cobro programado.
            return await ReviewAsync(p, Cut("CARD_CHARGE_" + (result.Value?.Status ?? "EMPTY").ToUpperInvariant(), 60)!, now);

        if (CardChargeOutcomes.IsExplicitNoCharge(result.ErrorCode))
            return await FailAsync(p, Cut("DECLINED:" + result.ErrorCode, 60)!, now);

        _logger.LogError("Tarjeta del alta {OrderId}: el cobro {ExternalOrderId} terminó dudoso ({Code}); se conciliará.",
            p.OrderId, p.ExternalOrderId, result.ErrorCode);
        return SignupCardSettlementOutcome.Charging;
    }

    /// <summary>Authenticating → Charging con UPDATE condicional: el segundo que llega no cobra.</summary>
    private async Task<bool> TryClaimForChargeAsync(SignupCardPayment p, string scaToken, DateTime now)
    {
        var none = CancellationToken.None;

        if (!_db.Database.IsRelational())
        {
            var current = await _db.SignupCardPayments.AsNoTracking().Where(x => x.Id == p.Id).Select(x => x.Status).FirstAsync(none);
            if (current != SignupCardPaymentStatus.Authenticating) return false;

            p.Status                 = SignupCardPaymentStatus.Charging;
            p.ScaAuthenticationToken = scaToken;
            p.ChargingStartedAt      = now;
            p.LastUpdateDate         = now;
            p.LastUpdateBy           = Actor;
            await _db.SaveChangesAsync(none);
            return true;
        }

        var affected = await _db.SignupCardPayments
            .Where(x => x.Id == p.Id && x.Status == SignupCardPaymentStatus.Authenticating)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, SignupCardPaymentStatus.Charging)
                .SetProperty(x => x.ScaAuthenticationToken, scaToken)
                .SetProperty(x => x.ChargingStartedAt, now)
                .SetProperty(x => x.Eci, p.Eci)
                .SetProperty(x => x.TransStatus, p.TransStatus)
                .SetProperty(x => x.ThreeDsVersion, p.ThreeDsVersion)
                .SetProperty(x => x.FlowPerformed, p.FlowPerformed)
                .SetProperty(x => x.LiabilityShift, p.LiabilityShift)
                .SetProperty(x => x.AuthenticationValuePresent, p.AuthenticationValuePresent)
                .SetProperty(x => x.LastUpdateDate, now)
                .SetProperty(x => x.LastUpdateBy, Actor), none);

        if (affected != 1) return false;
        await _db.Entry(p).ReloadAsync(none);
        return true;
    }

    // ── 3. Conciliación de lo que quedó cobrando ────────────────────────────────

    private async Task<SignupCardSettlementOutcome> ReconcileChargingAsync(SignupCardPayment p, DateTime now, SignupCardSettlementSource source)
    {
        var none = CancellationToken.None;

        // (a) El orquestador guarda el PaymentHistory al cobrar: si está y no es de otro intento, se cobró.
        var processors = p.GetPlannedProcessors().Select(x => x.ToString()).ToList();
        var history = await _db.PaymentHistories.AsNoTracking()
            .Where(h => h.OrderId == p.OrderId
                     && h.TransactionStatus == PaymentHistoryTransactionStatus.Captured
                     && processors.Contains(h.GatewayName)
                     && !_db.SignupCardPayments.Any(o => o.Id != p.Id && o.PaymentHistoryId == h.Id))
            .OrderBy(h => h.CreationDate)
            .FirstOrDefaultAsync(none);

        if (history is not null)
        {
            if (CardChargeOutcomes.IsSimulated(history.GatewayTransactionId))
                return await ReviewAsync(p, "CARD_SIMULATED_CHARGE", now);
            return await MarkChargedAsync(p, history.Id, history.GatewayTransactionId, history.GatewayName, null, now, source);
        }

        // (b) Spreedly: la compra de esta tarjeta con nuestro order_id.
        var list = await _client.ListPaymentMethodTransactionsAsync(p.PaymentMethodToken, none);
        _db.SpreedlyApiCalls.Add(SpreedlyCallLog.From(SpreedlyCallLog.ListTransactions, list, null, p.ExternalOrderId, now, $"{Actor}:{source}"));
        p.LastCheckedAt = now;
        await _db.SaveChangesAsync(none);

        if (list.Ok)
        {
            var purchases = list.Value!
                .Where(t => t.OrderId == p.ExternalOrderId && string.Equals(t.TransactionType, "Purchase", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var succeeded = purchases.FirstOrDefault(t => t.Succeeded);
            if (succeeded is not null)
            {
                // Cobrado sin PaymentHistory (el proceso cayó entre la compra y el guardado): se escribe aquí.
                var payment = new PaymentHistory
                {
                    OrderId              = p.OrderId,
                    MemberId             = p.MemberId,
                    Amount               = p.ChargeAmount,
                    GatewayName          = p.ProcessorUsed ?? "Spreedly",
                    GatewayTransactionId = succeeded.Token,
                    TransactionStatus    = PaymentHistoryTransactionStatus.Captured,
                    ProcessedAt          = now,
                    CreationDate         = now,
                    CreatedBy            = Actor,
                    LastUpdateDate       = now,
                    LastUpdateBy         = Actor
                };
                _db.PaymentHistories.Add(payment);
                return await MarkChargedAsync(p, payment.Id, succeeded.Token, payment.GatewayName, succeeded.NetworkTransactionId, now, source);
            }

            if (purchases.Count > 0 && purchases.All(t => SpreedlyStates.IsNoCharge(t.State)))
                return await FailAsync(p, "CHARGE_DECLINED", now);
        }

        // (c) Sin noticias claras: se espera; pasada la gracia, a revisión (nunca "no cobrado").
        if (source is SignupCardSettlementSource.Sweep or SignupCardSettlementSource.Admin
            && now - (p.ChargingStartedAt ?? p.StartedAt) > _options.ChargeUnknownGrace)
            return await ReviewAsync(p, "CHARGE_OUTCOME_UNKNOWN", now);

        return SignupCardSettlementOutcome.Charging;
    }

    private async Task<SignupCardSettlementOutcome> MarkChargedAsync(
        SignupCardPayment p, string paymentHistoryId, string? transactionToken, string processor, string? networkTransactionId,
        DateTime now, SignupCardSettlementSource source)
    {
        p.Status                   = SignupCardPaymentStatus.Charged;
        p.PaymentHistoryId         = paymentHistoryId;
        p.PurchaseTransactionToken = transactionToken;
        p.ProcessorUsed            = processor;
        p.NetworkTransactionId     = networkTransactionId ?? p.NetworkTransactionId;
        p.ChargedAt                = now;
        p.LastUpdateDate           = now;
        p.LastUpdateBy             = Actor;
        await _db.SaveChangesAsync(CancellationToken.None);
        return await ActivateAsync(p, now, source);
    }

    // ── 4. Activación: solo con el dinero cobrado ───────────────────────────────

    private async Task<SignupCardSettlementOutcome> ActivateAsync(SignupCardPayment p, DateTime now, SignupCardSettlementSource source)
    {
        var none  = CancellationToken.None;
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == p.OrderId, none);
        if (order is null)
            return await ReviewAsync(p, "ORDER_NOT_FOUND", now);

        // Cobrado y el alta ya estaba activada por otro medio: doble cobro, a devolver a mano.
        if (order.Status is not (OrderStatus.Pending or OrderStatus.Processing))
            return await ReviewAsync(p, SignupOrderClaim.AlreadySettledCode, now);

        string? failure = null;
        Result<MemberProfile>? activation = null;
        await using (var tx = await _db.Database.BeginTransactionAsync(none))
        {
            try
            {
                if (!await TryClaimForActivationAsync(p, now))
                {
                    await tx.RollbackAsync(none);
                    _db.ChangeTracker.Clear();
                    await EnsureUserActiveAsync(p.Id);
                    return SignupCardSettlementOutcome.AlreadyResolved;
                }

                activation = await _activation.ActivatePendingSignupAsync(order, now, none);
                if (activation.IsSuccess)
                {
                    await AddCardOnFileAsync(p, activation.Value!, now);
                    await _db.SaveChangesAsync(none);
                    await tx.CommitAsync(none);
                }
                else
                {
                    await tx.RollbackAsync(none);
                    failure = activation.ErrorCode ?? "ACTIVATION_FAILED";
                }
            }
            catch (Exception ex) when (ex is not DbUpdateConcurrencyException && IsOrderAlreadyConsumed(ex))
            {
                await tx.RollbackAsync(none);
                failure = SignupOrderClaim.AlreadySettledCode;
            }
            catch
            {
                _db.ChangeTracker.Clear();
                throw;
            }
        }

        if (failure is not null)
        {
            _db.ChangeTracker.Clear();
            var again = await _db.SignupCardPayments.FirstAsync(x => x.Id == p.Id, none);
            if (again.Status is SignupCardPaymentStatus.Paid or SignupCardPaymentStatus.ToReview)
            {
                if (again.Status == SignupCardPaymentStatus.Paid)
                    await EnsureUserActiveAsync(again.Id);
                return SignupCardSettlementOutcome.AlreadyResolved;
            }
            return await ReviewAsync(again, failure, now);
        }

        await SignupUserActivation.ActivateAsync(_users, activation!.Value!.Email);

        _logger.LogInformation("Tarjeta del alta {OrderId}: cobrada y activada ({ExternalOrderId}, {Source}).", p.OrderId, p.ExternalOrderId, source);
        return SignupCardSettlementOutcome.Paid;
    }

    /// <summary>Charged → Paid (Consumed) con UPDATE condicional dentro de la transacción de la activación.</summary>
    private async Task<bool> TryClaimForActivationAsync(SignupCardPayment p, DateTime now)
    {
        var none = CancellationToken.None;

        if (!_db.Database.IsRelational())
        {
            var current = await _db.SignupCardPayments.AsNoTracking().Where(x => x.Id == p.Id).Select(x => x.Status).FirstAsync(none);
            if (current != SignupCardPaymentStatus.Charged) return false;

            p.Status         = SignupCardPaymentStatus.Paid;
            p.Consumed       = true;
            p.ActivatedAt    = now;
            p.LastUpdateDate = now;
            p.LastUpdateBy   = Actor;
            return true;
        }

        int affected;
        try
        {
            affected = await _db.SignupCardPayments
                .Where(x => x.Id == p.Id && x.Status == SignupCardPaymentStatus.Charged)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, SignupCardPaymentStatus.Paid)
                    .SetProperty(x => x.Consumed, true)
                    .SetProperty(x => x.ActivatedAt, now)
                    .SetProperty(x => x.LastUpdateDate, now)
                    .SetProperty(x => x.LastUpdateBy, Actor), none);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            // Solo puede chocar con UX_SignupCardPayments_OrderId_Consumed: otro intento ya consumió el pedido.
            throw new OrderAlreadyConsumedException(ex);
        }

        if (affected != 1) return false;
        await _db.Entry(p).ReloadAsync(none);
        return true;
    }

    /// <summary>La tarjeta queda guardada para el miembro con lo que dijo Spreedly. Sin PAN y sin CVV (I14).</summary>
    private async Task AddCardOnFileAsync(SignupCardPayment p, MemberProfile member, DateTime now)
    {
        var none = CancellationToken.None;
        if (await _db.CreditCards.AnyAsync(c => c.MemberId == member.MemberId && c.SpreedlyPaymentMethodToken == p.PaymentMethodToken, none))
            return;

        var hasDefault = await _db.CreditCards.AnyAsync(c => c.MemberId == member.MemberId && c.IsDefault && !c.IsDeleted, none);
        var brand = Enum.TryParse<CardBrand>(p.CardBrand, out var b) ? b : CardBrand.Other;

        _db.CreditCards.Add(new MemberCreditCard
        {
            MemberId                   = member.MemberId,
            MaskedCardNumber           = string.IsNullOrEmpty(p.First6) ? $"******{p.Last4}" : $"{p.First6}******{p.Last4}",
            Last4                      = p.Last4,
            First6                     = p.First6,
            EncryptedPan               = string.Empty,
            CardBrand                  = SpreedlyCardBrands.DisplayName(brand),
            EncryptedExpiry            = _encryption.Encrypt($"{p.ExpiryMonth:00}/{p.ExpiryYear:0000}"),
            EncryptedCvv               = null,
            Gateway                    = "spreedly",
            GatewayToken               = p.PaymentMethodToken,
            CardToken                  = p.PaymentMethodToken,
            SpreedlyPaymentMethodToken = p.PaymentMethodToken,
            IsDefault                  = !hasDefault,
            IsExpired                  = false,
            CreatedBy                  = member.Email,
            CreationDate               = now,
            LastUpdateDate             = now
        });
    }

    // ── Cancelar (solo lo que se autentica) ─────────────────────────────────────

    public async Task<bool> CancelAsync(string paymentRef, CancellationToken ct)
    {
        var none = CancellationToken.None;
        var now  = _clock.Now;
        var p = await _db.SignupCardPayments.FirstOrDefaultAsync(x => x.Id == paymentRef, ct);
        if (p is null || p.Status != SignupCardPaymentStatus.Authenticating)
            return false;

        if (_db.Database.IsRelational())
        {
            var affected = await _db.SignupCardPayments
                .Where(x => x.Id == p.Id && x.Status == SignupCardPaymentStatus.Authenticating)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, SignupCardPaymentStatus.Failed)
                    .SetProperty(x => x.FailureCode, "CANCELLED")
                    .SetProperty(x => x.LastUpdateDate, now)
                    .SetProperty(x => x.LastUpdateBy, Actor), none);
            if (affected != 1) return false;
            await _db.Entry(p).ReloadAsync(none);
        }
        else
        {
            p.Status = SignupCardPaymentStatus.Failed;
            p.FailureCode = "CANCELLED";
            p.LastUpdateDate = now;
            p.LastUpdateBy = Actor;
        }

        await using var tx = await _db.Database.BeginTransactionAsync(none);
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == p.OrderId, none);
        if (order is not null)
            await ReleaseOrderIfIdleAsync(order, p.Id, now);
        await _db.SaveChangesAsync(none);
        await tx.CommitAsync(none);
        return true;
    }

    // ── Cierres ─────────────────────────────────────────────────────────────────

    private async Task<SignupCardSettlementOutcome> FailAsync(SignupCardPayment p, string code, DateTime now, bool release = true)
    {
        var none = CancellationToken.None;
        p.Status         = SignupCardPaymentStatus.Failed;
        p.FailureCode    = Cut(code, 60);
        p.LastUpdateDate = now;
        p.LastUpdateBy   = Actor;

        // El intento cerrado y la vuelta (condicional) del pedido a Pending, juntos.
        await using var tx = await _db.Database.BeginTransactionAsync(none);
        if (release)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == p.OrderId, none);
            if (order is not null)
                await ReleaseOrderIfIdleAsync(order, p.Id, now);
        }
        await _db.SaveChangesAsync(none);
        await tx.CommitAsync(none);

        _logger.LogInformation("Tarjeta del alta {OrderId}: intento {ExternalOrderId} cerrado sin cobro ({Code}).", p.OrderId, p.ExternalOrderId, code);
        return SignupCardSettlementOutcome.Failed;
    }

    private async Task<SignupCardSettlementOutcome> ReviewAsync(SignupCardPayment p, string reason, DateTime now)
    {
        var none = CancellationToken.None;
        p.Status         = SignupCardPaymentStatus.ToReview;
        p.FailureCode    = Cut(reason, 60);
        p.LastUpdateDate = now;
        p.LastUpdateBy   = Actor;

        // Como H22 de Entrax: el intento a revisar nunca activa, así que si nada más espera el pedido, vuelve a
        // Pending (condicional) y el aspirante puede pagar con otro método. Si el pedido ya está Completed, se queda.
        await using (var tx = await _db.Database.BeginTransactionAsync(none))
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == p.OrderId, none);
            if (order is not null)
                await ReleaseOrderIfIdleAsync(order, p.Id, now);
            await _db.SaveChangesAsync(none);
            await tx.CommitAsync(none);
        }

        _logger.LogCritical(
            "Tarjeta del alta {OrderId}: el intento {ExternalOrderId} queda A REVISAR ({Reason}). No se activó nada; " +
            "si hubo cobro (compra {PurchaseToken}) hay que aplicarlo o devolverlo en la consola de Spreedly.",
            p.OrderId, p.ExternalOrderId, reason, p.PurchaseTransactionToken);
        return SignupCardSettlementOutcome.ToReview;
    }

    /// <summary>El pedido vuelve a Pending solo si nada más lo espera: otro intento con tarjeta, un QR de Entrax o una cripto.</summary>
    private async Task ReleaseOrderIfIdleAsync(Orders order, string exceptPaymentId, DateTime now)
    {
        var none = CancellationToken.None;
        if (order.Status != OrderStatus.Processing) return;

        var otherCard = await _db.SignupCardPayments.AnyAsync(x => x.OrderId == order.Id && x.Id != exceptPaymentId
            && (x.Status == SignupCardPaymentStatus.Authenticating
                || x.Status == SignupCardPaymentStatus.Charging
                || x.Status == SignupCardPaymentStatus.Charged), none);
        var entrax = await _db.EntraxPayments.AnyAsync(x => x.OrderId == order.Id && x.Status == EntraxPaymentStatus.AwaitingPayment, none);
        var crypto = await _db.CryptoPaymentConfirmations.AnyAsync(c => c.OrderId == order.Id
            && c.Status == CryptoPaymentConfirmationStatus.AwaitingPayment, none);
        if (otherCard || entrax || crypto) return;

        await _orderClaim.TryReleaseAsync(order, now, Actor, none);
    }

    /// <summary>
    /// Repara la cuenta de un alta ya pagada si el proceso cayó entre confirmar y activar el usuario. Solo con
    /// el intento Paid, el pedido Completed y el miembro Active: nunca reactiva a quien desactivó un administrador.
    /// </summary>
    private async Task EnsureUserActiveAsync(string paymentRef)
    {
        var none = CancellationToken.None;
        var paid = await _db.SignupCardPayments.AsNoTracking()
            .Where(x => x.Id == paymentRef && x.Status == SignupCardPaymentStatus.Paid)
            .Select(x => new { x.MemberId, x.OrderId })
            .FirstOrDefaultAsync(none);
        if (paid is null) return;

        if (!await _db.Orders.AsNoTracking().AnyAsync(o => o.Id == paid.OrderId && o.Status == OrderStatus.Completed, none))
            return;

        var email = await _db.MemberProfiles.AsNoTracking()
            .Where(m => m.MemberId == paid.MemberId && m.Status == MemberAccountStatus.Active)
            .Select(m => m.Email)
            .FirstOrDefaultAsync(none);

        if (email is not null)
            await SignupUserActivation.ActivateAsync(_users, email);
    }

    private static string? Cut(string? value, int max) => value is null ? null : value.Length <= max ? value : value[..max];

    private static bool IsOrderAlreadyConsumed(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            if (e is OrderAlreadyConsumedException)
                return true;
            if (e.Message.Contains(OrderConsumedIndex, StringComparison.OrdinalIgnoreCase)
                && (e is not SqlException sql || sql.Number is 2601 or 2627))
                return true;
        }
        return false;
    }

    private sealed class OrderAlreadyConsumedException(Exception inner)
        : Exception("Another card attempt already consumed this signup order.", inner);
}
```

> `CardholderCountryIso = "US"` en el contexto del cobro es solo para el registro de `GatewayChargeAttempts`: el plan ya está congelado y el orquestador no vuelve a rutear. Si se quiere el país real en los intentos, añadir `CountryIso` a `SignupCardPayment` (no hace falta para cobrar).

- [ ] **Step 5: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SignupCardSettlementTests -nologo`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.SignupAPI/Services/Card/SignupCardSettlement.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Card/EscenarioTarjeta.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Card/SignupCardSettlementTests.cs
git commit -m "Liquidar un pago con tarjeta del alta: re-consulta la autenticacion a Spreedly, exige liability shift, cobra una vez por el router, guarda el cobro antes de activar y concilia lo dudoso sin darlo nunca por no cobrado"
```

---

### Task 14: Empezar el pago con tarjeta (`SignupCardCheckout`)

**Files:**
- Create: `MLMConquerorGlobalEdition.SignupAPI/Services/Card/SignupCardCheckout.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Card/SignupCardCheckoutTests.cs`

`StartAsync` termina en la liquidación de la Tarea 13 cuando la autenticación es sin fricción (o falla): no decide nada por su cuenta.

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Spreedly;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SignupAPI.Services.Card;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Card;

/// <summary>
/// Empezar el pago con tarjeta: Spreedly describe la tarjeta (no el navegador), el router fija el plan, el
/// pedido queda en Processing y se autentica por el importe del pedido. Sin fricción, se cobra en el acto;
/// con challenge, la pantalla recibe el paso de 3DS. Nada de esto cobra sin liability shift.
/// </summary>
public class SignupCardCheckoutTests
{
    private sealed record Montaje(AppDbContext Db, FakeSpreedlyClient Fake, Mock<IGatewayChargeOrchestrator> Orq,
        Mock<IGatewayRouter> Router, SignupCardCheckout Checkout, Orders Pedido, MemberProfile Miembro);

    private static async Task<Montaje> MontarAsync(
        GatewayRoutingPlan? plan = null, ISpreedlyCredentialSource? credenciales = null, ISpreedlyClient? cliente = null)
    {
        var db = InMemoryDbHelper.Create();
        await EscenarioTarjeta.SembrarAltaAsync(db);
        var fake = new FakeSpreedlyClient();
        var orq  = EscenarioTarjeta.Orquestador();
        var router = EscenarioTarjeta.Router(plan);
        var usuario = EscenarioEntrax.UsuarioInactivo();
        var liquidacion = EscenarioTarjeta.Liquidacion(db, cliente ?? fake, orq.Object, usuario);

        var checkout = new SignupCardCheckout(db, cliente ?? fake, credenciales ?? new FakeSpreedlyCredentialSource(),
            EscenarioTarjeta.Opciones(), router.Object, liquidacion, new RegistroDePrueba<SignupCardCheckout>());

        var pedido  = await db.Orders.SingleAsync();
        var miembro = await db.MemberProfiles.SingleAsync();
        return new Montaje(db, fake, orq, router, checkout, pedido, miembro);
    }

    [Fact]
    public async Task Sin_friccion_autentica_por_el_importe_del_pedido_cobra_y_activa()
    {
        var m = await MontarAsync();

        var r = await m.Checkout.StartAsync(m.Pedido, m.Miembro, EscenarioTarjeta.Pedido(SpreedlyFakeScenarios.Frictionless), EscenarioTarjeta.Ahora, default);

        r.IsSuccess.Should().BeTrue();
        r.Value!.Status.Should().Be(SignupCardStatuses.Paid);

        var fila = await m.Db.SignupCardPayments.SingleAsync();
        fila.ExternalOrderId.Should().Be("SIGNUP-CARD-ORD-RU-1-1");
        fila.ChargeAmount.Should().Be(80m);
        fila.ChargeCurrency.Should().Be("USD");
        fila.Last4.Should().Be("1111", "lo dice Spreedly, no el navegador");
        fila.Eci.Should().Be("05");
        fila.LiabilityShift.Should().BeTrue();

        var auth = (await m.Fake.GetTransactionAsync(fila.ScaTransactionToken!, default)).Value!;
        auth.AmountMinor.Should().Be(8000);
        auth.OrderId.Should().Be(fila.ExternalOrderId);

        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Completed);
    }

    [Fact]
    public async Task Con_challenge_la_pantalla_recibe_el_paso_de_3ds_y_no_se_cobra()
    {
        var m = await MontarAsync();

        var r = await m.Checkout.StartAsync(m.Pedido, m.Miembro, EscenarioTarjeta.Pedido(SpreedlyFakeScenarios.Challenge), EscenarioTarjeta.Ahora, default);

        r.Value!.Status.Should().Be(SignupCardStatuses.Authenticating);
        r.Value.ThreeDs!.RequiredAction.Should().Be("challenge");
        r.Value.ThreeDs.ChallengeUrl.Should().StartWith(FakeSpreedlyClient.Host);
        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Processing);
        m.Orq.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Autenticacion_fallida_no_cobra_y_devuelve_el_pedido_a_Pending()
    {
        var m = await MontarAsync();

        var r = await m.Checkout.StartAsync(m.Pedido, m.Miembro, EscenarioTarjeta.Pedido(SpreedlyFakeScenarios.Failed), EscenarioTarjeta.Ahora, default);

        r.Value!.Status.Should().Be(SignupCardStatuses.Failed);
        r.Value.Message.Should().Be(SignupCardMessages.AuthenticationFailed);
        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Pending);
        m.Orq.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Sin_credenciales_no_toca_el_pedido()
    {
        var sin = new Mock<ISpreedlyCredentialSource>();
        var m = await MontarAsync(credenciales: sin.Object);

        var r = await m.Checkout.StartAsync(m.Pedido, m.Miembro, EscenarioTarjeta.Pedido(SpreedlyFakeScenarios.Frictionless), EscenarioTarjeta.Ahora, default);

        r.ErrorCode.Should().Be(SignupCardErrors.NotConfigured);
        (await m.Db.SignupCardPayments.CountAsync()).Should().Be(0);
        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task Un_token_que_Spreedly_no_conoce_no_es_una_tarjeta()
    {
        var m = await MontarAsync();
        var r = await m.Checkout.StartAsync(m.Pedido, m.Miembro,
            new SignupCardPaymentRequestDto { PaymentMethodToken = "tok_inventado_123", BrowserInfo = "x" }, EscenarioTarjeta.Ahora, default);

        r.ErrorCode.Should().Be(SignupCardErrors.InvalidCard);
        (await m.Db.SignupCardPayments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Una_tarjeta_caducada_no_se_intenta()
    {
        var cliente = new Mock<ISpreedlyClient>();
        cliente.Setup(c => c.GetPaymentMethodAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(SpreedlyCall<SpreedlyPaymentMethod>.Success(
                   new SpreedlyPaymentMethod("PMVIEJA1", "credit_card", "visa", "411111", "1111", 9, 2026, "cached", false), 200, 3));
        var m = await MontarAsync(cliente: cliente.Object);

        var r = await m.Checkout.StartAsync(m.Pedido, m.Miembro,
            new SignupCardPaymentRequestDto { PaymentMethodToken = "PMVIEJA1", BrowserInfo = "x" }, EscenarioTarjeta.Ahora, default);

        r.ErrorCode.Should().Be(SignupCardErrors.InvalidCard, "caducó en septiembre de 2026 y el escenario es octubre");
        cliente.Verify(c => c.AuthenticateAsync(It.IsAny<SpreedlyAuthenticateRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sin_gateway_inmediato_no_se_empieza()
    {
        var m = await MontarAsync(plan: EscenarioTarjeta.Plan());

        var r = await m.Checkout.StartAsync(m.Pedido, m.Miembro, EscenarioTarjeta.Pedido(SpreedlyFakeScenarios.Frictionless), EscenarioTarjeta.Ahora, default);

        r.ErrorCode.Should().Be(SignupCardErrors.NoGateway);
        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task Con_moneda_del_router_autentica_y_congela_el_plan_en_esa_moneda()
    {
        var m = await MontarAsync(plan: EscenarioTarjeta.Plan(
            (CardProcessor.CheckoutEUR, "EUR", 74.10m, 0),
            (CardProcessor.NmiSpreedly, "USD", 80m, 0),
            (CardProcessor.Shift4, "EUR", 74.10m, 0)));

        await m.Checkout.StartAsync(m.Pedido, m.Miembro, EscenarioTarjeta.Pedido(SpreedlyFakeScenarios.Challenge), EscenarioTarjeta.Ahora, default);

        var fila = await m.Db.SignupCardPayments.SingleAsync();
        fila.ChargeCurrency.Should().Be("EUR");
        fila.ChargeAmount.Should().Be(74.10m);
        fila.GetPlannedProcessors().Should().Equal(CardProcessor.CheckoutEUR, CardProcessor.Shift4);
        (await m.Fake.GetTransactionAsync(fila.ScaTransactionToken!, default)).Value!.AmountMinor.Should().Be(7410);
    }

    [Fact]
    public async Task El_router_recibe_la_marca_de_Spreedly_y_el_pais_del_aspirante()
    {
        var m = await MontarAsync();
        GatewayRoutingContext? ctx = null;
        m.Router.Setup(r => r.ResolveAsync(It.IsAny<GatewayRoutingContext>(), It.IsAny<CancellationToken>()))
                .Callback<GatewayRoutingContext, CancellationToken>((c, _) => ctx = c)
                .ReturnsAsync(Result<GatewayRoutingPlan>.Success(EscenarioTarjeta.Plan((CardProcessor.NmiSpreedly, "USD", 80m, 0))));

        await m.Checkout.StartAsync(m.Pedido, m.Miembro, EscenarioTarjeta.Pedido(SpreedlyFakeScenarios.Challenge), EscenarioTarjeta.Ahora, default);

        ctx!.CardBrand.Should().Be(CardBrand.Visa);
        ctx.CardholderCountryIso.Should().Be("US");
        ctx.AmountUsd.Should().Be(80m);
        ctx.OperationType.Should().Be(BillingOperationType.Payment);
    }

    [Fact]
    public async Task Un_segundo_intento_lleva_el_siguiente_numero()
    {
        var m = await MontarAsync();
        await m.Checkout.StartAsync(m.Pedido, m.Miembro, EscenarioTarjeta.Pedido(SpreedlyFakeScenarios.Failed), EscenarioTarjeta.Ahora, default);
        var pedido = await m.Db.Orders.SingleAsync();

        await m.Checkout.StartAsync(pedido, m.Miembro, EscenarioTarjeta.Pedido(SpreedlyFakeScenarios.Challenge), EscenarioTarjeta.Ahora, default);

        (await m.Db.SignupCardPayments.Select(p => p.ExternalOrderId).ToListAsync())
            .Should().BeEquivalentTo("SIGNUP-CARD-ORD-RU-1-1", "SIGNUP-CARD-ORD-RU-1-2");
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SignupCardCheckoutTests -nologo`
Expected: FAIL de compilación (`SignupCardCheckout` no existe).

- [ ] **Step 3: Crear `SignupCardCheckout.cs`**

```csharp
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Spreedly;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.SignupAPI.Services.Card;

public interface ISignupCardCheckout
{
    /// <summary>
    /// Empieza el pago con tarjeta del alta: pregunta a Spreedly qué tarjeta es, fija el plan del router,
    /// reserva el pedido (Processing), guarda el intento y autentica con 3DS2 Global. GUARDA el contexto
    /// (también lo que el handler dejó pendiente, como la fila de estadísticas).
    /// </summary>
    Task<Result<SignupCardStepDto>> StartAsync(
        Orders order, MemberProfile member, SignupCardPaymentRequestDto request, DateTime now, CancellationToken ct);
}

/// <summary>
/// Lo que el legado hacía mal y aquí no:
///  - el PAN no pasa por aquí (iFrame); marca, BIN, últimos 4 y caducidad los da Spreedly, no el cliente;
///  - el importe es Orders.TotalAmount y el gateway lo elige el router: el cliente no manda ni importe, ni
///    gateway, ni token de gateway;
///  - no hay "NONTHREEDS": se autentica siempre, y la decisión de cobrar la toma SignupCardSettlement
///    volviendo a consultar a Spreedly;
///  - nada del cobro depende del correo del aspirante.
/// </summary>
public sealed class SignupCardCheckout : ISignupCardCheckout
{
    private const string ExternalOrderIdIndex = "IX_SignupCardPayments_ExternalOrderId";

    private readonly AppDbContext                   _db;
    private readonly ISpreedlyClient                _client;
    private readonly ISpreedlyCredentialSource      _credentials;
    private readonly SpreedlyOptions                _options;
    private readonly IGatewayRouter                 _router;
    private readonly ISignupCardSettlement          _settlement;
    private readonly ILogger<SignupCardCheckout>    _logger;
    private readonly ISignupOrderClaim              _orderClaim;

    public SignupCardCheckout(
        AppDbContext db, ISpreedlyClient client, ISpreedlyCredentialSource credentials, SpreedlyOptions options,
        IGatewayRouter router, ISignupCardSettlement settlement, ILogger<SignupCardCheckout> logger,
        ISignupOrderClaim? orderClaim = null)
    {
        _db          = db;
        _client      = client;
        _credentials = credentials;
        _options     = options;
        _router      = router;
        _settlement  = settlement;
        _logger      = logger;
        _orderClaim  = orderClaim ?? new SignupOrderClaim(db);
    }

    public async Task<Result<SignupCardStepDto>> StartAsync(
        Orders order, MemberProfile member, SignupCardPaymentRequestDto request, DateTime now, CancellationToken ct)
    {
        var amountUsd = Math.Round(order.TotalAmount, 2, MidpointRounding.AwayFromZero);
        if (amountUsd < 0.01m)
            return Result<SignupCardStepDto>.Failure(SignupCardErrors.NothingToPay, "This signup has nothing to pay.");

        var credentials = await _credentials.GetAsync(ct);
        if (credentials is null)
        {
            _logger.LogError("Tarjeta del alta: no hay credenciales de Spreedly en el entorno {Environment}.", _options.CredentialEnvironment);
            return Result<SignupCardStepDto>.Failure(SignupCardErrors.NotConfigured, SignupCardMessages.NotConfigured);
        }

        // 1) Qué tarjeta es lo dice Spreedly. El token lo creó el iFrame en NUESTRO entorno; si no existe aquí, no vale.
        var pmCall = await _client.GetPaymentMethodAsync(request.PaymentMethodToken, ct);
        _db.SpreedlyApiCalls.Add(SpreedlyCallLog.From(SpreedlyCallLog.GetPaymentMethod, pmCall, null, null, now, member.Email));
        if (!pmCall.Ok)
        {
            await _db.SaveChangesAsync(ct);
            return pmCall.Outcome == SpreedlyOutcome.Invalid
                ? Result<SignupCardStepDto>.Failure(SignupCardErrors.InvalidCard, SignupCardMessages.InvalidCard)
                : Result<SignupCardStepDto>.Failure(SignupCardErrors.Unavailable, SignupCardMessages.Unavailable);
        }

        var pm = pmCall.Value!;
        if (!string.Equals(pm.PaymentMethodType, "credit_card", StringComparison.OrdinalIgnoreCase) || IsExpired(pm, now))
        {
            await _db.SaveChangesAsync(ct);
            return Result<SignupCardStepDto>.Failure(SignupCardErrors.InvalidCard, SignupCardMessages.InvalidCard);
        }

        var brand = SpreedlyCardBrands.FromSpreedly(pm.CardType);

        // 2) El plan del router, una vez y congelado (I7).
        var routing = new GatewayRoutingContext
        {
            OperationType        = BillingOperationType.Payment,
            CardBrand            = brand,
            CardholderCountryIso = member.Country is { Length: 2 } c ? c.ToUpperInvariant() : "US",
            AmountUsd            = amountUsd,
            MemberId             = member.MemberId
        };
        var planResult = await _router.ResolveAsync(routing, ct);
        var steps = planResult.IsSuccess ? SignupCardRouting.UsableSteps(planResult.Value!) : [];
        if (steps.Count == 0)
        {
            _logger.LogWarning("Tarjeta del alta {OrderId}: el router no da gateway inmediato ({Code}).", order.Id, planResult.ErrorCode);
            await _db.SaveChangesAsync(ct);
            return Result<SignupCardStepDto>.Failure(SignupCardErrors.NoGateway, SignupCardMessages.NoGateway);
        }

        // 3) El intento y el pedido reservado, guardados ANTES de autenticar.
        var attempt = await _db.SignupCardPayments.IgnoreQueryFilters().CountAsync(p => p.OrderId == order.Id, ct) + 1;
        var payment = new SignupCardPayment
        {
            OrderId            = order.Id,
            MemberId           = member.MemberId,
            ExternalOrderId    = SignupCardPayment.BuildExternalOrderId(order.Id, attempt),
            Status             = SignupCardPaymentStatus.Authenticating,
            AmountUsd          = amountUsd,
            ChargeAmount       = steps[0].Amount,
            ChargeCurrency     = steps[0].PresentmentCurrency.Trim().ToUpperInvariant(),
            RouteBucketKey     = planResult.Value!.RouteBucketKey,
            PaymentMethodToken = pm.Token,
            CardBrand          = brand.ToString(),
            First6             = Cut(pm.FirstSixDigits, 6),
            Last4              = Cut(pm.LastFourDigits, 4),
            ExpiryMonth        = pm.Month,
            ExpiryYear         = pm.Year,
            StartedAt          = now,
            CreatedBy          = member.Email,
            CreationDate       = now,
            LastUpdateDate     = now,
            LastUpdateBy       = member.Email
        };
        payment.SetPlannedProcessors(steps.Select(s => s.CardProcessor));
        _db.SignupCardPayments.Add(payment);

        // Como cripto y Entrax: el pedido EN PROCESO impide que CompleteSignup se repita mientras se autentica.
        order.Status          = OrderStatus.Processing;
        order.LastUpdateDate  = now;
        order.LastUpdateBy    = member.Email;
        member.LastUpdateDate = now;
        member.LastUpdateBy   = member.Email;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex is DbUpdateConcurrencyException || IsExternalOrderIdTaken(ex))
        {
            _db.ChangeTracker.Clear();
            if (await _db.Orders.AsNoTracking().AnyAsync(o => o.Id == order.Id && o.Status == OrderStatus.Completed, ct))
                return Result<SignupCardStepDto>.Failure(SignupOrderClaim.SignupAlreadySettledCode,
                    "This signup was already completed with another payment.");

            return Result<SignupCardStepDto>.Failure(SignupCardErrors.InProgress, SignupCardMessages.InProgress);
        }

        // 4) Autenticar. A partir de aquí nada se cancela con la petición del navegador.
        var none = CancellationToken.None;
        var auth = await _client.AuthenticateAsync(new SpreedlyAuthenticateRequest(
            credentials.ScaProviderKey, pm.Token,
            SpreedlyCurrency.ToMinorUnits(payment.ChargeAmount, payment.ChargeCurrency), payment.ChargeCurrency,
            request.BrowserInfo, payment.ExternalOrderId, _options.CallbackUrl), none);
        _db.SpreedlyApiCalls.Add(SpreedlyCallLog.From(SpreedlyCallLog.Authenticate, auth, null, payment.ExternalOrderId, now, member.Email));

        if (!auth.Ok)
        {
            // No se movió dinero (autenticar no cobra). El intento se cierra y el pedido vuelve a Pending.
            payment.Status         = SignupCardPaymentStatus.Failed;
            payment.FailureCode    = "AUTH_UNAVAILABLE";
            payment.LastUpdateDate = now;
            await using (var tx = await _db.Database.BeginTransactionAsync(none))
            {
                await _orderClaim.TryReleaseAsync(order, now, member.Email, none);
                await _db.SaveChangesAsync(none);
                await tx.CommitAsync(none);
            }
            _logger.LogWarning("Tarjeta del alta {OrderId}: Spreedly no autenticó ({Code}).", order.Id, auth.ErrorCode);
            return Result<SignupCardStepDto>.Failure(SignupCardErrors.Unavailable, SignupCardMessages.Unavailable);
        }

        var transaction = auth.Value!;
        payment.ScaTransactionToken = transaction.Token;
        payment.LastCheckedAt       = now;
        payment.LastUpdateDate      = now;
        await _db.SaveChangesAsync(none);

        // Con huella del dispositivo o challenge, el navegador sigue con el Lifecycle de Spreedly.
        if (transaction.State == SpreedlyStates.Pending)
            return Result<SignupCardStepDto>.Success(SignupCardSteps.From(payment, transaction));

        // Sin fricción (o fallida): decide la liquidación, que vuelve a consultar la autenticación a Spreedly.
        await _settlement.SettleAsync(payment.Id, SignupCardSettlementSource.Browser, none);

        _db.ChangeTracker.Clear();
        var settled = await _db.SignupCardPayments.AsNoTracking().FirstAsync(p => p.Id == payment.Id, none);
        return Result<SignupCardStepDto>.Success(SignupCardSteps.From(settled));
    }

    /// <summary>Caduca al terminar su mes (MM/AAAA de la tarjeta frente a la hora de servidor).</summary>
    private static bool IsExpired(SpreedlyPaymentMethod pm, DateTime now)
        => pm.Year <= 0 || pm.Month is < 1 or > 12 || new DateTime(pm.Year, pm.Month, 1).AddMonths(1) <= now;

    private static string Cut(string? value, int max)
        => string.IsNullOrEmpty(value) ? string.Empty : value.Length <= max ? value : value[..max];

    private static bool IsExternalOrderIdTaken(DbUpdateException ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
            if (e.Message.Contains(ExternalOrderIdIndex, StringComparison.OrdinalIgnoreCase)
                && (e is not SqlException sql || sql.Number is 2601 or 2627))
                return true;
        return false;
    }
}
```

- [ ] **Step 4: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter "SignupCardCheckoutTests|SignupCardSettlementTests" -nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.SignupAPI/Services/Card/SignupCardCheckout.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Card/SignupCardCheckoutTests.cs
git commit -m "Empezar el pago con tarjeta del alta: Spreedly describe la tarjeta, el router fija el plan, el pedido queda en Processing y se autentica con 3DS2 por el importe del pedido"
```

---

### Task 15: `CompleteSignup` con tarjeta delega en el cobro con 3DS

**Files:**
- Modify: `MLMConquerorGlobalEdition.SignupAPI/DTOs/CompleteSignupRequest.cs`
- Modify: `MLMConquerorGlobalEdition.SignupAPI/DTOs/Validators/CompleteSignupRequestValidator.cs`
- Modify: `MLMConquerorGlobalEdition.SignupAPI/DTOs/SignupResponse.cs`
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Features/Signups/Commands/CompleteSignup/CompleteSignupHandler.cs`
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Controllers/SignupsController.cs` (acción `CompleteSignup`)
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Card/CompleteSignupConTarjetaTests.cs`
- Modify (pruebas que usaban la tarjeta como "camino inmediato"): `Signups.Tests/Entrax/AltaInmediataReclamaElPedidoTests.cs`, `Signups.Tests/Entrax/AltaYaCompletadaYQrActivoTests.cs`, `Signups.Tests/Features/Signups/AltaPorCriptoTests.cs`, `Signups.Tests/Features/Signups/CompleteSignupHandlerTests.cs`, `Signups.Tests/Features/Signups/TarjetaApagadaEnElAltaTests.cs`, `Signups.Tests/Validators/CompleteSignupRequestValidatorTests.cs`

Hasta ahora, con `Signup:CardEnabled` encendido, la rama de tarjeta guardaba la tarjeta que describía el cliente y activaba **sin cobrar**. Esta tarea la sustituye: con tarjeta, `CompleteSignup` solo empieza el pago (`ISignupCardCheckout`) y quien cobra y activa es `SignupCardSettlement`.

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediatR;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Crypto;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Controllers;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.CompleteSignup;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Services.Card;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Card;

/// <summary>
/// Con tarjeta, CompleteSignup ya no activa: empieza el pago con 3DS y devuelve el paso para la pantalla.
/// Sin interruptor o sin el cobro registrado, la tarjeta se rechaza sin tocar nada.
/// </summary>
public class CompleteSignupConTarjetaTests
{
    private static CompleteSignupHandler Handler(AppDbContext db, ISignupCardCheckout? tarjeta, bool interruptor = true)
        => new(db, EscenarioEntrax.Reloj().Object, Mock.Of<IS3FileService>(),
               EscenarioEntrax.Usuarios(EscenarioEntrax.UsuarioInactivo()).Object, Mock.Of<IJwtService>(),
               Mock.Of<IEncryptionService>(), Mock.Of<ITokenRedemptionService>(),
               new SignupActivationService(db, Mock.Of<ISponsorBonusService>(), Mock.Of<IFastStartBonusService>(),
                   Mock.Of<IRecurringBillingEnrollmentService>()),
               Mock.Of<ICryptoDepositAddressReader>(), new PuertaDeFondosDePrueba(db),
               configuration: interruptor ? TarjetaDelAlta.Encendida() : TarjetaDelAlta.Apagada(),
               cardCheckout: tarjeta);

    private static CompleteSignupRequest ConTarjeta() => new()
    {
        PaymentMethod = PaymentMethodType.CreditCard,
        Card = new SignupCardPaymentRequestDto { PaymentMethodToken = "fake_frictionless_1234", BrowserInfo = "navegador" }
    };

    [Fact]
    public async Task Con_tarjeta_empieza_el_cobro_y_no_activa_ni_da_sesion()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db);
        var paso = new SignupCardStepDto { PaymentRef = "ref-1", Status = SignupCardStatuses.Authenticating };
        var tarjeta = new Mock<ISignupCardCheckout>();
        SignupCardPaymentRequestDto? recibido = null;
        tarjeta.Setup(t => t.StartAsync(It.IsAny<Orders>(), It.IsAny<MemberProfile>(), It.IsAny<SignupCardPaymentRequestDto>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
               .Callback<Orders, MemberProfile, SignupCardPaymentRequestDto, DateTime, CancellationToken>((_, _, r, _, _) => recibido = r)
               .ReturnsAsync(Result<SignupCardStepDto>.Success(paso));

        var r = await Handler(db, tarjeta.Object).Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, ConTarjeta()), default);

        r.IsSuccess.Should().BeTrue();
        r.Value!.Card.Should().BeSameAs(paso);
        r.Value.AccessToken.Should().BeNull("la sesión se abre iniciando sesión cuando el cobro esté hecho");
        recibido!.PaymentMethodToken.Should().Be("fake_frictionless_1234");
        (await db.MemberProfiles.SingleAsync()).Status.Should().Be(MemberAccountStatus.Pending);
        (await db.CreditCards.CountAsync()).Should().Be(0, "la tarjeta la guarda la liquidación con lo que dice Spreedly");
    }

    [Fact]
    public async Task Con_el_interruptor_pero_sin_el_cobro_registrado_se_rechaza()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db);

        var r = await Handler(db, tarjeta: null).Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, ConTarjeta()), default);

        r.ErrorCode.Should().Be(CompleteSignupHandler.CardPaymentsDisabledCode);
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task Sin_el_bloque_de_tarjeta_no_se_empieza()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db);
        var tarjeta = new Mock<ISignupCardCheckout>();

        var r = await Handler(db, tarjeta.Object).Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId,
            new CompleteSignupRequest { PaymentMethod = PaymentMethodType.CreditCard }), default);

        r.ErrorCode.Should().Be(SignupCardErrors.InvalidCard);
        tarjeta.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(PaymentMethodType.CreditCard)]
    [InlineData(PaymentMethodType.Token)]
    [InlineData(PaymentMethodType.Entrax)]
    public async Task Con_un_pago_con_tarjeta_en_curso_cualquier_metodo_es_CARD_IN_PROGRESS(PaymentMethodType metodo)
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        await EscenarioTarjeta.SembrarIntentoAsync(db, SignupCardPaymentStatus.Authenticating, "fake_challenge_1234", "fake_auth_1");

        var r = await Handler(db, Mock.Of<ISignupCardCheckout>()).Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId,
            new CompleteSignupRequest { PaymentMethod = metodo, Card = ConTarjeta().Card, TokenCode = "TOKEN-1" }), default);

        r.ErrorCode.Should().Be(SignupCardErrors.InProgress);
    }

    [Theory]
    [InlineData(SignupCardErrors.InProgress, StatusCodes.Status409Conflict)]
    [InlineData(SignupCardErrors.NotConfigured, StatusCodes.Status503ServiceUnavailable)]
    [InlineData(SignupCardErrors.NoGateway, StatusCodes.Status503ServiceUnavailable)]
    [InlineData(SignupCardErrors.Unavailable, StatusCodes.Status502BadGateway)]
    [InlineData(SignupCardErrors.InvalidCard, StatusCodes.Status400BadRequest)]
    public async Task El_controlador_traduce_los_codigos_de_tarjeta(string codigo, int http)
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(x => x.Send(It.IsAny<CompleteSignupCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<SignupResponse>.Failure(codigo, "x"));
        var controller = new SignupsController(mediator.Object, Mock.Of<IFraudFingerprintService>());

        var r = (await controller.CompleteSignup(EscenarioEntrax.OrderId, ConTarjeta(), default)).Should().BeAssignableTo<ObjectResult>().Subject;

        r.StatusCode.Should().Be(http);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter CompleteSignupConTarjetaTests -nologo`
Expected: FAIL de compilación (`Card`, `cardCheckout`).

- [ ] **Step 3: `CompleteSignupRequest.Card` sustituye a `CreditCard`**

En `CompleteSignupRequest.cs`, añadir `using MLMConquerorGlobalEdition.SharedKernel.Billing;` y sustituir

```csharp
    /// <summary>Populated when PaymentMethod = CreditCard.</summary>
    public CreditCardInfoDto? CreditCard { get; set; }
```

por

```csharp
    /// <summary>
    /// Con PaymentMethod = CreditCard: el token que creó el iFrame de Spreedly y los datos del navegador para
    /// 3DS2. NADA que describa la tarjeta (marca, BIN, últimos 4, caducidad) ni importe: eso lo dice Spreedly y
    /// el pedido. El antiguo CreditCard (que traía todo eso del cliente) ya no existe (Plan I, I15).
    /// </summary>
    public SignupCardPaymentRequestDto? Card { get; set; }
```

`CreditCardInfoDto` y `CreditCardInfoDtoValidator` se quedan en el proyecto sin uso por la petición (los vigilan las pruebas de paridad de `TokenizacionDeTarjetaTests`); se borran en el Plan L.

- [ ] **Step 4: El validador exige el token y los datos del navegador**

En `CompleteSignupRequestValidator.cs`, sustituir el bloque `When(x => x.PaymentMethod == PaymentMethodType.CreditCard, …)` por:

```csharp
        // Tarjeta: solo el token del iFrame y los datos del navegador (Plan I). El token tiene la forma de un
        // token de Spreedly (o del simulador); browser_info es texto imprimible de Spreedly.ThreeDS.serialize.
        When(x => x.PaymentMethod == PaymentMethodType.CreditCard, () =>
        {
            RuleFor(x => x.Card)
                .NotNull().WithMessage("Card is required when PaymentMethod is CreditCard.");
            RuleFor(x => x.Card!.PaymentMethodToken)
                .NotEmpty()
                .Matches("^[A-Za-z0-9_-]{8,100}$").WithMessage("Card payment method token is not valid.")
                .When(x => x.Card is not null);
            RuleFor(x => x.Card!.BrowserInfo)
                .NotEmpty()
                .MaximumLength(8192)
                .Matches(@"^[\x20-\x7E]+$").WithMessage("Browser info contains invalid characters.")
                .When(x => x.Card is not null);
        });
```

- [ ] **Step 5: `SignupResponse.Card`**

En `SignupResponse.cs`, debajo de `Entrax`:

```csharp
    /// <summary>
    /// Solo con PaymentMethod = CreditCard: dónde está el pago (y, si toca, el paso de 3DS para el navegador).
    /// Sin tokens de sesión: el alta se activa cuando el cobro está hecho y la persona inicia sesión (I12).
    /// </summary>
    public SignupCardStepDto? Card { get; set; }
```

- [ ] **Step 6: La rama de tarjeta del handler**

En `CompleteSignupHandler.cs`:

1. `using MLMConquerorGlobalEdition.SignupAPI.Services.Card;` arriba.
2. Campo `private readonly ISignupCardCheckout? _cardCheckout;` y un último parámetro opcional en el constructor, después de `IConfiguration? configuration = null`:

```csharp
        // Opcional por lo mismo que Entrax: sin el cobro con 3DS registrado, la tarjeta se rechaza
        // (CARD_PAYMENTS_DISABLED) aunque Signup:CardEnabled esté encendido. Falla cerrado.
        ISignupCardCheckout? cardCheckout = null)
```

con `_cardCheckout = cardCheckout;` en el cuerpo.

3. La comprobación del interruptor (la que añadió `cerrar-tarjeta-en-el-alta`, al principio de `Handle`) pasa a ser:

```csharp
        // Antes de leer nada: la tarjeta solo existe con el interruptor Y con el cobro con 3DS registrado.
        // Apagada, no se activa, no se guarda nada, no se sube la captura y el pedido no se reclama.
        if (req.PaymentMethod == PaymentMethodType.CreditCard
            && (!SignupCardPayments.IsEnabled(_configuration) || _cardCheckout is null))
            return Result<SignupResponse>.Failure(CardPaymentsDisabledCode,
                "Card payments are not available right now. Please choose another payment method.");
```

4. **Borrar** el bloque que guardaba la tarjeta descrita por el cliente:

```csharp
            if (req.PaymentMethod == PaymentMethodType.CreditCard && req.CreditCard is not null)
            {
                var cc = req.CreditCard;
                await _db.CreditCards.AddAsync(new MemberCreditCard
                {
                    …
                }, ct);
            }
```

y el método privado `BuildMaskedCardNumber`, que deja de usarse.

5. Justo después del bloque `if (req.PaymentMethod == PaymentMethodType.Entrax) { … }`, añadir:

```csharp
            if (req.PaymentMethod == PaymentMethodType.CreditCard)
            {
                // Plan I: la tarjeta NO activa aquí. Se autentica con 3DS2 y la cobra y activa SignupCardSettlement
                // (primero se cobra, después se activa). StartAsync guarda: también la fila de estadísticas de arriba.
                if (req.Card is null)
                    return Result<SignupResponse>.Failure(SignupCardErrors.InvalidCard, SignupCardMessages.InvalidCard);

                var started = await _cardCheckout!.StartAsync(order, member, req.Card, now, ct);
                if (!started.IsSuccess)
                    return Result<SignupResponse>.Failure(started.ErrorCode!, started.Error!);

                return Result<SignupResponse>.Success(new SignupResponse
                {
                    SignupId   = order.Id,
                    MemberId   = member.MemberId,
                    Email      = member.Email,
                    MemberType = member.MemberType.ToString(),
                    EnrollDate = member.EnrollDate,
                    Card       = started.Value
                });
            }
```

6. En `ExplainMissingPendingOrderAsync`, dentro de `if (status is not null) { … }` y **antes** de la consulta de `EntraxPayments`:

```csharp
            // Un pago con tarjeta autenticándose, cobrándose o cobrado y sin activar: no se empieza otro pago.
            if (await _db.SignupCardPayments.AsNoTracking().AnyAsync(p => p.OrderId == signupId
                    && (p.Status == SignupCardPaymentStatus.Authenticating
                        || p.Status == SignupCardPaymentStatus.Charging
                        || p.Status == SignupCardPaymentStatus.Charged), ct))
                return Result<SignupResponse>.Failure(SignupCardErrors.InProgress, SignupCardMessages.InProgress);
```

7. Actualizar el `<summary>` de la clase: el párrafo "CON TARJETA NO SE COBRA NADA AQUÍ…" pasa a:

```csharp
/// CON TARJETA (Plan I) esta fase solo EMPIEZA el pago: ISignupCardCheckout pregunta a Spreedly qué tarjeta
/// es, fija el plan del router, deja el pedido en Processing y autentica con 3DS2 Global. Quien cobra y
/// activa es SignupCardSettlement, y solo con liability shift. Sin Signup:CardEnabled o sin el cobro
/// registrado, la tarjeta se rechaza con CARD_PAYMENTS_DISABLED antes de leer el pedido.
```

- [ ] **Step 7: Los códigos HTTP de la tarjeta en `SignupsController.CompleteSignup`**

Añadir `using MLMConquerorGlobalEdition.SignupAPI.Services.Card;` y sustituir el `switch` por:

```csharp
            return result.ErrorCode switch
            {
                CompleteSignupHandler.CardPaymentsDisabledCode => BadRequest(body),
                CompleteSignupHandler.TokenBusyCode or EntraxSignupErrors.InProgress
                    or EntraxSignupErrors.QrAlreadyActive or EntraxSignupErrors.PaymentConfirming
                    or SignupCardErrors.InProgress
                    or CompleteSignupHandler.SignupAlreadySettledCode => Conflict(body),
                EntraxSignupErrors.Unavailable or SignupCardErrors.Unavailable
                    => StatusCode(StatusCodes.Status502BadGateway, body),
                SignupCardErrors.NotConfigured or SignupCardErrors.NoGateway
                    => StatusCode(StatusCodes.Status503ServiceUnavailable, body),
                _ => BadRequest(body)
            };
```

- [ ] **Step 8: Ajustar las pruebas que usaban la tarjeta como camino inmediato**

La tarjeta ya no activa en `CompleteSignup`; el único camino inmediato que queda es el token. Cambios exactos:

a) `Entrax/AltaInmediataReclamaElPedidoTests.cs` — el camino inmediato pasa a ser el token. Sustituir `ConTarjeta()` por:

```csharp
    // La tarjeta ya no activa en CompleteSignup (Plan I: se cobra con 3DS fuera). El camino inmediato que
    // queda es el token: su canje se simula aceptado.
    private static CompleteSignupCommand ConToken()
        => new(EscenarioEntrax.OrderId, new CompleteSignupRequest { PaymentMethod = PaymentMethodType.Token, TokenCode = "TOKEN-INMEDIATO" });

    private static ITokenRedemptionService CanjeQueAcepta()
    {
        var m = new Mock<ITokenRedemptionService>();
        m.Setup(t => t.RedeemForSignupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<bool>.Success(true));
        return m.Object;
    }
```

en `Handler(...)` usar `canje ?? CanjeQueAcepta()` en vez de `canje ?? Mock.Of<ITokenRedemptionService>()`, quitar `configuration: TarjetaDelAlta.Encendida()` (ya no hace falta) y cambiar todas las llamadas `ConTarjeta()` por `ConToken()`. Las aserciones no cambian (el token reclama desde Pending igual que la tarjeta antes).

b) `Entrax/AltaYaCompletadaYQrActivoTests.cs` — en `Handler(...)` añadir el argumento `cardCheckout: Mock.Of<MLMConquerorGlobalEdition.SignupAPI.Services.Card.ISignupCardCheckout>()`. Con él, los casos `PaymentMethodType.CreditCard` siguen llegando a la explicación del pedido (`SIGNUP_ALREADY_SETTLED`, `ENTRAX_QR_ALREADY_ACTIVE`, …) en lugar de cortarse en `CARD_PAYMENTS_DISABLED`.

c) `Features/Signups/AltaPorCriptoTests.cs` — en `Completar_PorLasViasQueYaCobraron_SigueActivandoYComisionando`, borrar `[InlineData(PaymentMethodType.CreditCard)]` y el argumento `tarjetaEncendida: metodo == PaymentMethodType.CreditCard` (la tarjeta ya no "ya cobró" al llegar aquí; su activación la prueba `SignupCardSettlementTests`).

d) `Features/Signups/CompleteSignupHandlerTests.cs` — `Handle_WhenCreditCardPayment_StoresCreditCard` describía justo lo que este plan prohíbe (guardar la tarjeta que dice el cliente). Sustituirla por:

```csharp
    [Fact]
    public async Task Handle_WhenCreditCardPayment_DelegatesToTheCardCheckoutAndStoresNothing()
    {
        await using var db = InMemoryDbHelper.Create();
        const string memberId = "AMB-015";
        const string email    = "amb015@example.com";

        await db.MemberProfiles.AddAsync(BuildMember(memberId, email));
        await db.Orders.AddAsync(BuildPendingOrder("ORD-015", memberId));
        await db.OrderDetails.AddAsync(BuildOrderDetail("ORD-015", "P-001", 80));
        await db.MembershipSubscriptions.AddAsync(BuildPendingSubscription("SUB-015", memberId));
        await db.SaveChangesAsync();

        var userMgr = UserManagerHelper.Create();
        userMgr.Setup(u => u.FindByEmailAsync(email)).ReturnsAsync(BuildInactiveUser(memberId, email));

        var checkout = new Mock<MLMConquerorGlobalEdition.SignupAPI.Services.Card.ISignupCardCheckout>();
        checkout.Setup(c => c.StartAsync(It.IsAny<Orders>(), It.IsAny<MemberProfile>(),
                    It.IsAny<MLMConquerorGlobalEdition.SharedKernel.Billing.SignupCardPaymentRequestDto>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<MLMConquerorGlobalEdition.SharedKernel.Billing.SignupCardStepDto>.Success(
                    new MLMConquerorGlobalEdition.SharedKernel.Billing.SignupCardStepDto { PaymentRef = "r", Status = "Authenticating" }));

        var handler = new CompleteSignupHandler(
            db, BuildDateTimeMock().Object, BuildS3Mock().Object,
            userMgr.Object, BuildJwtMock().Object, BuildEncryptionMock().Object,
            BuildTokenRedemptionMock().Object, BuildActivation(db),
            Mock.Of<MLMConquerorGlobalEdition.Repository.Services.Crypto.ICryptoDepositAddressReader>(),
            new PuertaDeFondosDePrueba(db),
            configuration: TarjetaDelAlta.Encendida(),
            cardCheckout: checkout.Object);

        var result = await handler.Handle(new CompleteSignupCommand("ORD-015", new CompleteSignupRequest
        {
            PaymentMethod = PaymentMethodType.CreditCard,
            Card = new MLMConquerorGlobalEdition.SharedKernel.Billing.SignupCardPaymentRequestDto
            {
                PaymentMethodToken = "fake_frictionless_1234", BrowserInfo = "navegador"
            }
        }), CancellationToken.None);

        result.Value!.Card!.Status.Should().Be("Authenticating");
        (await db.CreditCards.CountAsync()).Should().Be(0);
        checkout.VerifyAll();
    }
```

(Los `using` de `Orders` y `MemberProfile` ya están en ese archivo; si no, añadir `MLMConquerorGlobalEdition.Domain.Entities.Orders` y `MLMConquerorGlobalEdition.Domain.Entities.Member`.)

e) `Features/Signups/TarjetaApagadaEnElAltaTests.cs` — en `ConTarjeta()` sustituir el bloque `CreditCard = new CreditCardInfoDto { … }` por `Card = new SignupCardPaymentRequestDto { PaymentMethodToken = "fake_frictionless_1234", BrowserInfo = "navegador" }` (con `using MLMConquerorGlobalEdition.SharedKernel.Billing;`), y sustituir `Con_la_tarjeta_encendida_se_mantiene_el_comportamiento_actual` por:

```csharp
    [Fact]
    public async Task Con_la_tarjeta_encendida_pero_sin_cobro_con_3DS_registrado_sigue_cerrada()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db);
        var m = Montar(db, TarjetaDelAlta.Encendida());

        var r = await m.Handler.Handle(new CompleteSignupCommand(EscenarioEntrax.OrderId, ConTarjeta()), default);

        r.ErrorCode.Should().Be(CompleteSignupHandler.CardPaymentsDisabledCode,
            "desde el Plan I la tarjeta solo existe con el cobro con 3DS; la activación sin cobro desapareció");
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Pending);
        (await db.CreditCards.CountAsync()).Should().Be(0);
    }
```

f) `Validators/CompleteSignupRequestValidatorTests.cs` — sustituir `ValidCard()` y las dos pruebas de tarjeta por:

```csharp
    private static MLMConquerorGlobalEdition.SharedKernel.Billing.SignupCardPaymentRequestDto ValidCard() => new()
    {
        PaymentMethodToken = "fake_frictionless_1234",
        BrowserInfo        = "eyJ3aWR0aCI6MTI4MH0="
    };

    [Fact]
    public void Validate_CreditCardPath_WhenComplete_PassesValidation()
        => _validator.TestValidate(new CompleteSignupRequest
        {
            PaymentMethod = PaymentMethodType.CreditCard, Card = ValidCard(), CheckoutScreenshotContentType = "image/png"
        }).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_CreditCardPath_WhenCardNull_FailsValidation()
        => _validator.TestValidate(new CompleteSignupRequest
        {
            PaymentMethod = PaymentMethodType.CreditCard, Card = null, CheckoutScreenshotContentType = "image/png"
        }).ShouldHaveValidationErrorFor(x => x.Card);

    [Theory]
    [InlineData("../x")]
    [InlineData("corto")]
    [InlineData("tok en con espacios")]
    public void Validate_CreditCardPath_WhenTokenMalformed_FailsValidation(string token)
        => _validator.TestValidate(new CompleteSignupRequest
        {
            PaymentMethod = PaymentMethodType.CreditCard,
            Card = new() { PaymentMethodToken = token, BrowserInfo = "x" },
            CheckoutScreenshotContentType = "image/png"
        }).ShouldHaveAnyValidationError();
```

- [ ] **Step 9: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests -nologo`
Expected: PASS (todas: las nuevas y las ajustadas).

- [ ] **Step 10: Commit**

```bash
git add MLMConquerorGlobalEdition.SignupAPI/DTOs/CompleteSignupRequest.cs \
        MLMConquerorGlobalEdition.SignupAPI/DTOs/Validators/CompleteSignupRequestValidator.cs \
        MLMConquerorGlobalEdition.SignupAPI/DTOs/SignupResponse.cs \
        MLMConquerorGlobalEdition.SignupAPI/Features/Signups/Commands/CompleteSignup/CompleteSignupHandler.cs \
        MLMConquerorGlobalEdition.SignupAPI/Controllers/SignupsController.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Card/CompleteSignupConTarjetaTests.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Entrax/AltaInmediataReclamaElPedidoTests.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Entrax/AltaYaCompletadaYQrActivoTests.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Features/Signups/AltaPorCriptoTests.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Features/Signups/CompleteSignupHandlerTests.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Features/Signups/TarjetaApagadaEnElAltaTests.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Validators/CompleteSignupRequestValidatorTests.cs
git commit -m "CompleteSignup con tarjeta ya no activa sin cobrar: solo empieza el pago con 3DS (token del iFrame y datos del navegador) y la tarjeta en curso bloquea otros metodos con CARD_IN_PROGRESS"
```

---

### Task 16: Lo que pide la pantalla: estado, completar 3DS, terminar y cancelar

**Files:**
- Create: `MLMConquerorGlobalEdition.SignupAPI/Services/Card/SignupCardStatusService.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Card/SignupCardStatusServiceTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Services.Spreedly;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SignupAPI.Services.Card;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Card;

/// <summary>
/// Los endpoints anónimos de la pantalla: exigen el id del alta Y el de nuestro intento, no deciden nada por
/// su cuenta (todo pasa por la liquidación) y no machacan a Spreedly (la consulta del estado se estrangula).
/// </summary>
public class SignupCardStatusServiceTests
{
    private static async Task<(MLMConquerorGlobalEdition.Repository.Context.AppDbContext Db, FakeSpreedlyClient Fake, Mock<ISignupCardSettlement> Liq, SignupCardStatusService Svc, SignupCardPayment Fila)>
        MontarAsync(string escenario = SpreedlyFakeScenarios.Challenge, DateTime? consultado = null)
    {
        var db = InMemoryDbHelper.Create();
        await EscenarioTarjeta.SembrarAltaAsync(db, OrderStatus.Processing);
        var fake = new FakeSpreedlyClient();
        var (pm, auth) = await EscenarioTarjeta.AutenticarAsync(fake, escenario, SignupCardPayment.BuildExternalOrderId(EscenarioTarjeta.OrderId, 1));
        var fila = await EscenarioTarjeta.SembrarIntentoAsync(db, SignupCardPaymentStatus.Authenticating, pm, auth.Token);
        if (consultado is not null) { fila.LastCheckedAt = consultado; await db.SaveChangesAsync(); }

        var liq = new Mock<ISignupCardSettlement>();
        var svc = new SignupCardStatusService(db, fake, liq.Object, EscenarioTarjeta.Opciones(),
            EscenarioEntrax.Reloj().Object, new RegistroDePrueba<SignupCardStatusService>());
        return (db, fake, liq, svc, fila);
    }

    [Fact]
    public async Task Sin_los_dos_ids_correctos_no_hay_nada()
    {
        var m = await MontarAsync();
        (await m.Svc.GetAsync("OTRO-ALTA", m.Fila.Id, default)).Should().BeNull();
        (await m.Svc.FinishAsync(EscenarioTarjeta.OrderId, "otro-ref", default)).Should().BeNull();
        m.Liq.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task El_estado_consulta_a_Spreedly_por_la_liquidacion_si_hace_rato_que_no()
    {
        var m = await MontarAsync(consultado: EscenarioTarjeta.Ahora.AddMinutes(-1));

        var dto = await m.Svc.GetAsync(EscenarioTarjeta.OrderId, m.Fila.Id, default);

        dto!.Status.Should().Be(SignupCardStatuses.Authenticating);
        m.Liq.Verify(l => l.SettleAsync(m.Fila.Id, SignupCardSettlementSource.Poll, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task El_estado_no_consulta_si_se_acaba_de_consultar()
    {
        var m = await MontarAsync(consultado: EscenarioTarjeta.Ahora.AddSeconds(-2));
        await m.Svc.GetAsync(EscenarioTarjeta.OrderId, m.Fila.Id, default);
        m.Liq.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Terminar_liquida_ya_lo_que_dijo_el_navegador_no_cuenta()
    {
        var m = await MontarAsync();
        await m.Svc.FinishAsync(EscenarioTarjeta.OrderId, m.Fila.Id, default);
        m.Liq.Verify(l => l.SettleAsync(m.Fila.Id, SignupCardSettlementSource.Browser, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Completar_3ds_devuelve_el_paso_siguiente_mientras_siga_pendiente()
    {
        var m = await MontarAsync();

        var dto = await m.Svc.CompleteThreeDsAsync(EscenarioTarjeta.OrderId, m.Fila.Id, default);

        dto!.ThreeDs!.TransactionToken.Should().Be(m.Fila.ScaTransactionToken);
        dto.ThreeDs.RequiredAction.Should().Be("challenge");
        (await m.Db.SpreedlyApiCalls.SingleAsync()).Operation.Should().Be(SpreedlyCallLog.Complete);
        m.Liq.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Completar_3ds_ya_resuelto_pasa_a_la_liquidacion()
    {
        var m = await MontarAsync();
        m.Fake.Resolve(m.Fila.ScaTransactionToken!, approve: true);

        await m.Svc.CompleteThreeDsAsync(EscenarioTarjeta.OrderId, m.Fila.Id, default);

        m.Liq.Verify(l => l.SettleAsync(m.Fila.Id, SignupCardSettlementSource.Browser, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancelar_pasa_por_la_liquidacion()
    {
        var m = await MontarAsync();
        await m.Svc.CancelAsync(EscenarioTarjeta.OrderId, m.Fila.Id, default);
        m.Liq.Verify(l => l.CancelAsync(m.Fila.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Un_fallo_de_la_liquidacion_no_llega_al_navegador()
    {
        var m = await MontarAsync(consultado: EscenarioTarjeta.Ahora.AddMinutes(-1));
        m.Liq.Setup(l => l.SettleAsync(It.IsAny<string>(), It.IsAny<SignupCardSettlementSource>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("detalle interno"));

        var dto = await m.Svc.GetAsync(EscenarioTarjeta.OrderId, m.Fila.Id, default);

        dto!.Status.Should().Be(SignupCardStatuses.Authenticating);
        dto.Message.Should().BeNull();
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SignupCardStatusServiceTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `SignupCardStatusService.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Spreedly;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SignupAPI.Services.Card;

public interface ISignupCardStatusService
{
    /// <summary>Estado para la pantalla. Si sigue abierto y hace rato que no se consultó, liquida (Poll).</summary>
    Task<SignupCardStepDto?> GetAsync(string signupId, string paymentRef, CancellationToken ct);

    /// <summary>El Lifecycle terminó en el navegador (succeeded, error o timeout): liquida ya (Browser).</summary>
    Task<SignupCardStepDto?> FinishAsync(string signupId, string paymentRef, CancellationToken ct);

    /// <summary>
    /// El Lifecycle pidió "trigger-completion": se completa la autenticación en Spreedly (con NUESTRAS
    /// credenciales y el token de la fila, no uno del navegador) y se devuelve el paso siguiente.
    /// </summary>
    Task<SignupCardStepDto?> CompleteThreeDsAsync(string signupId, string paymentRef, CancellationToken ct);

    /// <summary>La persona canceló mientras se autenticaba.</summary>
    Task<SignupCardStepDto?> CancelAsync(string signupId, string paymentRef, CancellationToken ct);
}

/// <summary>
/// Anónimo (la cuenta aún no puede iniciar sesión), así que: exige el id del alta Y el de nuestro intento,
/// devuelve solo estado, texto y el paso de 3DS, y no decide nada: todo lo que cobra o cierra pasa por
/// SignupCardSettlement. Los fallos de la liquidación se registran y no se enseñan al navegador.
/// </summary>
public sealed class SignupCardStatusService : ISignupCardStatusService
{
    private readonly AppDbContext                       _db;
    private readonly ISpreedlyClient                    _client;
    private readonly ISignupCardSettlement              _settlement;
    private readonly SpreedlyOptions                    _options;
    private readonly IDateTimeProvider                  _clock;
    private readonly ILogger<SignupCardStatusService>   _logger;

    public SignupCardStatusService(
        AppDbContext db, ISpreedlyClient client, ISignupCardSettlement settlement, SpreedlyOptions options,
        IDateTimeProvider clock, ILogger<SignupCardStatusService> logger)
    {
        _db         = db;
        _client     = client;
        _settlement = settlement;
        _options    = options;
        _clock      = clock;
        _logger     = logger;
    }

    public async Task<SignupCardStepDto?> GetAsync(string signupId, string paymentRef, CancellationToken ct)
    {
        var p = await ReadAsync(signupId, paymentRef, ct);
        if (p is null) return null;

        var open = p.Status is SignupCardPaymentStatus.Authenticating or SignupCardPaymentStatus.Charging or SignupCardPaymentStatus.Charged;
        if (open && (p.LastCheckedAt is null || _clock.Now - p.LastCheckedAt.Value >= _options.PollThrottle))
            await SettleQuietlyAsync(p.Id, SignupCardSettlementSource.Poll);

        return SignupCardSteps.From(await ReadAsync(signupId, paymentRef, CancellationToken.None) ?? p);
    }

    public async Task<SignupCardStepDto?> FinishAsync(string signupId, string paymentRef, CancellationToken ct)
    {
        var p = await ReadAsync(signupId, paymentRef, ct);
        if (p is null) return null;

        await SettleQuietlyAsync(p.Id, SignupCardSettlementSource.Browser);
        return SignupCardSteps.From(await ReadAsync(signupId, paymentRef, CancellationToken.None) ?? p);
    }

    public async Task<SignupCardStepDto?> CompleteThreeDsAsync(string signupId, string paymentRef, CancellationToken ct)
    {
        var p = await ReadAsync(signupId, paymentRef, ct);
        if (p is null) return null;
        if (p.Status != SignupCardPaymentStatus.Authenticating || p.ScaTransactionToken is null)
            return SignupCardSteps.From(p);

        var now  = _clock.Now;
        var call = await _client.CompleteAsync(p.ScaTransactionToken, CancellationToken.None);
        _db.SpreedlyApiCalls.Add(SpreedlyCallLog.From(SpreedlyCallLog.Complete, call, p.ScaTransactionToken, p.ExternalOrderId, now, "SignupCard:Browser"));
        await _db.SaveChangesAsync(CancellationToken.None);

        // Sigue pendiente (p. ej. ahora toca el challenge): el navegador continúa con el Lifecycle.
        if (call.Ok && call.Value!.State == SpreedlyStates.Pending)
            return SignupCardSteps.From(p, call.Value);

        // Resuelta (o Spreedly no contestó): decide la liquidación, que vuelve a consultar.
        await SettleQuietlyAsync(p.Id, SignupCardSettlementSource.Browser);
        return SignupCardSteps.From(await ReadAsync(signupId, paymentRef, CancellationToken.None) ?? p);
    }

    public async Task<SignupCardStepDto?> CancelAsync(string signupId, string paymentRef, CancellationToken ct)
    {
        var p = await ReadAsync(signupId, paymentRef, ct);
        if (p is null) return null;

        try
        {
            await _settlement.CancelAsync(p.Id, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Tarjeta del alta: no se pudo cancelar el intento {PaymentRef}.", p.Id);
        }

        return SignupCardSteps.From(await ReadAsync(signupId, paymentRef, CancellationToken.None) ?? p);
    }

    private async Task SettleQuietlyAsync(string paymentRef, SignupCardSettlementSource source)
    {
        try
        {
            await _settlement.SettleAsync(paymentRef, source, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Tarjeta del alta: la liquidación de {PaymentRef} ({Source}) falló; se reintentará.", paymentRef, source);
        }
        finally
        {
            _db.ChangeTracker.Clear();
        }
    }

    private Task<SignupCardPayment?> ReadAsync(string signupId, string paymentRef, CancellationToken ct)
        => _db.SignupCardPayments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == paymentRef && p.OrderId == signupId, ct);
}
```

- [ ] **Step 4: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SignupCardStatusServiceTests -nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.SignupAPI/Services/Card/SignupCardStatusService.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Card/SignupCardStatusServiceTests.cs
git commit -m "Lo que pide la pantalla del pago con tarjeta: estado estrangulado, completar 3DS con el token de la fila, terminar y cancelar, todo por la liquidacion y con los dos ids"
```

---

### Task 17: Callback de Spreedly (`SpreedlyCallbackProcessor`)

**Files:**
- Create: `MLMConquerorGlobalEdition.SignupAPI/Services/Card/SpreedlyCallbackProcessor.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Card/SpreedlyCallbackProcessorTests.cs`

Ver R2: la forma exacta de la firma y del cuerpo se confirma en la prueba real. El callback **no decide nada**: solo dice qué transacción mirar.

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using System.Text;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Services.Spreedly;
using MLMConquerorGlobalEdition.SignupAPI.Services.Card;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Card;

/// <summary>
/// El aviso de Spreedly: cada transacción firmada con HMAC-SHA1 (signing secret del entorno) sobre los campos
/// que ella misma lista. Sin firma válida, 401. Con firma, solo dice qué intento liquidar; la liquidación
/// vuelve a preguntar a Spreedly.
/// </summary>
public class SpreedlyCallbackProcessorTests
{
    private const string Secreto = FakeSpreedlyCredentialSource.SigningSecret;

    private static string Firma(string secreto, params string[] valores) => SpreedlySignature.Compute(secreto, valores);

    private static byte[] Xml(string token, string estado, string? firma = null, string campos = "amount state token")
    {
        var f = firma ?? Firma(Secreto, "8000", estado, token);
        return Encoding.UTF8.GetBytes($"""
            <transactions><transaction>
              <amount type="integer">8000</amount><state>{estado}</state><token>{token}</token>
              <signed><signature>{f}</signature><fields>{campos}</fields><algorithm>sha1</algorithm></signed>
            </transaction></transactions>
            """);
    }

    private static async Task<(SpreedlyCallbackProcessor Proc, Mock<ISignupCardSettlement> Liq, SignupCardPayment Fila)> MontarAsync(
        ISpreedlyCredentialSource? credenciales = null)
    {
        var db = InMemoryDbHelper.Create();
        await EscenarioTarjeta.SembrarAltaAsync(db, OrderStatus.Processing);
        var fila = await EscenarioTarjeta.SembrarIntentoAsync(db, SignupCardPaymentStatus.Authenticating, "fake_challenge_1234", "TXAUTH12345");
        var liq = new Mock<ISignupCardSettlement>();
        liq.Setup(l => l.SettleAsync(It.IsAny<string>(), It.IsAny<SignupCardSettlementSource>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(SignupCardSettlementOutcome.Paid);
        var proc = new SpreedlyCallbackProcessor(db, credenciales ?? new FakeSpreedlyCredentialSource(), liq.Object,
            EscenarioEntrax.Reloj().Object, new RegistroDePrueba<SpreedlyCallbackProcessor>());
        return (proc, liq, fila);
    }

    [Fact]
    public async Task Firmado_en_xml_liquida_el_intento_de_esa_transaccion()
    {
        var m = await MontarAsync();
        (await m.Proc.ProcessAsync(Xml("TXAUTH12345", "succeeded"), "application/xml", default)).Should().Be(SpreedlyCallbackOutcome.Accepted);
        m.Liq.Verify(l => l.SettleAsync(m.Fila.Id, SignupCardSettlementSource.Webhook, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Firmado_en_json_tambien()
    {
        var m = await MontarAsync();
        var firma = Firma(Secreto, "8000", "succeeded", "TXAUTH12345");
        var json = $$"""
            {"transactions":[{"amount":8000,"state":"succeeded","token":"TXAUTH12345",
              "signed":{"signature":"{{firma}}","fields":"amount state token","algorithm":"sha1"}}]}
            """;

        (await m.Proc.ProcessAsync(Encoding.UTF8.GetBytes(json), "application/json", default)).Should().Be(SpreedlyCallbackOutcome.Accepted);
        m.Liq.Verify(l => l.SettleAsync(m.Fila.Id, SignupCardSettlementSource.Webhook, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("otro-secreto")]
    [InlineData(null)]
    public async Task Sin_firma_valida_es_401_y_no_liquida(string? secreto)
    {
        var m = await MontarAsync();
        var cuerpo = secreto is null
            ? Encoding.UTF8.GetBytes("<transactions><transaction><token>TXAUTH12345</token><state>succeeded</state></transaction></transactions>")
            : Xml("TXAUTH12345", "succeeded", Firma(secreto, "8000", "succeeded", "TXAUTH12345"));

        (await m.Proc.ProcessAsync(cuerpo, "application/xml", default)).Should().Be(SpreedlyCallbackOutcome.Unauthorized);
        m.Liq.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Cambiar_un_valor_firmado_invalida_la_firma()
    {
        var m = await MontarAsync();
        var firmaDeOtroEstado = Firma(Secreto, "8000", "failed", "TXAUTH12345");
        (await m.Proc.ProcessAsync(Xml("TXAUTH12345", "succeeded", firmaDeOtroEstado), "application/xml", default))
            .Should().Be(SpreedlyCallbackOutcome.Unauthorized);
    }

    [Fact]
    public async Task Sin_signing_secret_configurado_se_rechaza_todo()
    {
        var sin = new Mock<ISpreedlyCredentialSource>();
        sin.Setup(c => c.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new SpreedlyCredentials("E", "S", "SCA", SigningSecret: null));
        var m = await MontarAsync(sin.Object);

        (await m.Proc.ProcessAsync(Xml("TXAUTH12345", "succeeded"), "application/xml", default)).Should().Be(SpreedlyCallbackOutcome.Unauthorized);
    }

    [Fact]
    public async Task Una_transaccion_que_no_es_nuestra_se_acepta_sin_hacer_nada()
    {
        var m = await MontarAsync();
        (await m.Proc.ProcessAsync(Xml("TXDEOTRO999", "succeeded"), "application/xml", default)).Should().Be(SpreedlyCallbackOutcome.Accepted);
        m.Liq.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("no es nada")]
    [InlineData("<!DOCTYPE x [<!ENTITY e SYSTEM \"file:///c:/windows/win.ini\">]><transactions>&e;</transactions>")]
    public async Task Un_cuerpo_ilegible_o_con_DTD_es_invalido(string cuerpo)
    {
        var m = await MontarAsync();
        (await m.Proc.ProcessAsync(Encoding.UTF8.GetBytes(cuerpo), "application/xml", default)).Should().Be(SpreedlyCallbackOutcome.Invalid);
        m.Liq.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Si_Spreedly_no_contesta_al_liquidar_se_pide_reintento()
    {
        var m = await MontarAsync();
        m.Liq.Setup(l => l.SettleAsync(It.IsAny<string>(), It.IsAny<SignupCardSettlementSource>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(SignupCardSettlementOutcome.ProviderUnavailable);

        (await m.Proc.ProcessAsync(Xml("TXAUTH12345", "succeeded"), "application/xml", default)).Should().Be(SpreedlyCallbackOutcome.RetryLater);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SpreedlyCallbackProcessorTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `SpreedlyCallbackProcessor.cs`**

```csharp
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Spreedly;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SignupAPI.Services.Card;

public enum SpreedlyCallbackOutcome
{
    /// <summary>200: procesado, ya resuelto o no es nuestro.</summary>
    Accepted,
    /// <summary>401: sin firma, firma mala, algoritmo desconocido o sin signing secret.</summary>
    Unauthorized,
    /// <summary>400: cuerpo ilegible o con DTD.</summary>
    Invalid,
    /// <summary>503: no se pudo resolver ahora (Spreedly no contestó al re-consultar).</summary>
    RetryLater
}

public interface ISpreedlyCallbackProcessor
{
    Task<SpreedlyCallbackOutcome> ProcessAsync(byte[] rawBody, string? contentType, CancellationToken ct);
}

/// <summary>
/// El callback de Spreedly. Cada transacción del aviso trae un bloque signed (signature, fields, algorithm):
/// la firma es HMAC-SHA1 con el signing secret del entorno sobre los valores de los campos listados, unidos
/// con '|', comparada en tiempo constante (R2 del Plan I: confirmar con la prueba real). TODAS las
/// transacciones tienen que validar; si una no, 401 y no se hace nada.
///
/// El cuerpo no decide nada: solo dice qué intento mirar, y SignupCardSettlement vuelve a preguntar a
/// Spreedly con nuestras credenciales. Se aceptan XML (sin DTD ni entidades) y JSON.
/// </summary>
public sealed class SpreedlyCallbackProcessor : ISpreedlyCallbackProcessor
{
    private sealed record SignedTransaction(string Token, string? State, string? Signature, string? Algorithm, IReadOnlyList<string> Values);

    private readonly AppDbContext                           _db;
    private readonly ISpreedlyCredentialSource              _credentials;
    private readonly ISignupCardSettlement                  _settlement;
    private readonly IDateTimeProvider                      _clock;
    private readonly ILogger<SpreedlyCallbackProcessor>     _logger;

    public SpreedlyCallbackProcessor(
        AppDbContext db, ISpreedlyCredentialSource credentials, ISignupCardSettlement settlement,
        IDateTimeProvider clock, ILogger<SpreedlyCallbackProcessor> logger)
    {
        _db          = db;
        _credentials = credentials;
        _settlement  = settlement;
        _clock       = clock;
        _logger      = logger;
    }

    public async Task<SpreedlyCallbackOutcome> ProcessAsync(byte[] rawBody, string? contentType, CancellationToken ct)
    {
        IReadOnlyList<SignedTransaction> transactions;
        try
        {
            var text = Encoding.UTF8.GetString(rawBody).Trim();
            transactions = text.StartsWith('<') ? ParseXml(text) : ParseJson(text);
        }
        catch (Exception ex) when (ex is XmlException or JsonException or InvalidOperationException)
        {
            _logger.LogWarning("Spreedly callback rechazado: cuerpo ilegible ({Error}).", ex.GetType().Name);
            return SpreedlyCallbackOutcome.Invalid;
        }

        if (transactions.Count == 0)
            return SpreedlyCallbackOutcome.Invalid;

        var secret = (await _credentials.GetAsync(ct))?.SigningSecret;
        if (string.IsNullOrEmpty(secret))
        {
            _logger.LogError("Spreedly callback rechazado: no hay signing secret en la credencial del entorno.");
            return SpreedlyCallbackOutcome.Unauthorized;
        }

        foreach (var t in transactions)
        {
            if (!string.Equals(t.Algorithm, "sha1", StringComparison.OrdinalIgnoreCase)
                || !SpreedlySignature.Verify(secret, t.Values, t.Signature))
            {
                _logger.LogWarning("Spreedly callback rechazado: la firma de una transacción no es válida.");
                return SpreedlyCallbackOutcome.Unauthorized;
            }
        }

        var now = _clock.Now;
        var retry = false;
        foreach (var t in transactions)
        {
            var paymentRef = await _db.SignupCardPayments.AsNoTracking()
                .Where(p => p.ScaTransactionToken == t.Token || p.PurchaseTransactionToken == t.Token)
                .Select(p => p.Id)
                .FirstOrDefaultAsync(ct);

            _db.SpreedlyApiCalls.Add(SpreedlyCallLog.Incoming(t.Token, t.State, paymentRef is null ? "NotOurs" : "Accepted", now));
            await _db.SaveChangesAsync(ct);

            if (paymentRef is null)
                continue;

            var outcome = await _settlement.SettleAsync(paymentRef, SignupCardSettlementSource.Webhook, CancellationToken.None);
            retry |= outcome == SignupCardSettlementOutcome.ProviderUnavailable;
        }

        return retry ? SpreedlyCallbackOutcome.RetryLater : SpreedlyCallbackOutcome.Accepted;
    }

    private static IReadOnlyList<SignedTransaction> ParseXml(string text)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(new StringReader(text), settings);
        var doc = XDocument.Load(reader);

        return doc.Descendants("transaction").Select(t =>
        {
            var signed = t.Element("signed");
            var fields = (signed?.Element("fields")?.Value ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return new SignedTransaction(
                t.Element("token")?.Value ?? throw new InvalidOperationException("Transacción sin token."),
                t.Element("state")?.Value,
                signed?.Element("signature")?.Value,
                signed?.Element("algorithm")?.Value,
                fields.Select(f => t.Element(f)?.Value ?? string.Empty).ToList());
        }).ToList();
    }

    private static IReadOnlyList<SignedTransaction> ParseJson(string text)
    {
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        var items = root.TryGetProperty("transactions", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().ToList()
            : root.TryGetProperty("transaction", out var one) ? [one] : [];

        return items.Select(t =>
        {
            var signed = t.TryGetProperty("signed", out var s) ? s : default;
            string? Str(JsonElement e, string name) =>
                e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) ? Scalar(v) : null;

            var fields = (Str(signed, "fields") ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return new SignedTransaction(
                Str(t, "token") ?? throw new InvalidOperationException("Transacción sin token."),
                Str(t, "state"),
                Str(signed, "signature"),
                Str(signed, "algorithm"),
                fields.Select(f => Str(t, f) ?? string.Empty).ToList());
        }).ToList();
    }

    /// <summary>El valor tal como viaja: texto sin comillas, números y booleanos en crudo, null como vacío.</summary>
    private static string Scalar(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.String => v.GetString() ?? string.Empty,
        JsonValueKind.Number => v.GetRawText(),
        JsonValueKind.True   => "true",
        JsonValueKind.False  => "false",
        _                    => string.Empty
    };
}
```

- [ ] **Step 4: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SpreedlyCallbackProcessorTests -nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.SignupAPI/Services/Card/SpreedlyCallbackProcessor.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Card/SpreedlyCallbackProcessorTests.cs
git commit -m "Callback de Spreedly: cada transaccion firmada con HMAC-SHA1 del signing secret y comparada en tiempo constante, XML sin DTD o JSON, y solo dice que intento re-consultar"
```

---

### Task 18: Endpoints de SignupAPI (`SignupCardController`) y `payment-options` con la tarjeta

**Files:**
- Create: `MLMConquerorGlobalEdition.SignupAPI/Controllers/SignupCardController.cs`
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Controllers/EntraxSignupsController.cs` (acción `GetPaymentOptions` y constructor)
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Card/SignupCardControllerTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SignupAPI.Controllers;
using MLMConquerorGlobalEdition.SignupAPI.Services.Card;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Card;

public class SignupCardControllerTests
{
    private static SignupCardController Controlador(Mock<ISignupCardStatusService> estado, Mock<ISpreedlyCallbackProcessor>? callbacks = null,
        Mock<ISignupCardSettlement>? liq = null, byte[]? cuerpo = null)
        => new(estado.Object, (callbacks ?? new()).Object, (liq ?? new()).Object, NullLogger<SignupCardController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { Request = { Body = new MemoryStream(cuerpo ?? []), ContentType = "application/xml" } }
            }
        };

    [Fact]
    public async Task Sin_el_intento_es_404_sin_detalles()
    {
        var r = await Controlador(new()).GetStatus("ALTA", "ref", default);
        r.Should().BeOfType<NotFoundObjectResult>().Which.Value.Should().BeOfType<ApiResponse<SignupCardStepDto>>()
         .Which.ErrorCode.Should().Be(SignupCardErrors.PaymentNotFound);
    }

    [Fact]
    public async Task Terminar_devuelve_el_paso()
    {
        var estado = new Mock<ISignupCardStatusService>();
        estado.Setup(s => s.FinishAsync("ALTA", "ref", It.IsAny<CancellationToken>()))
              .ReturnsAsync(new SignupCardStepDto { PaymentRef = "ref", Status = SignupCardStatuses.Paid });

        var r = await Controlador(estado).Finish("ALTA", "ref", default);

        r.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<ApiResponse<SignupCardStepDto>>()
         .Which.Data!.Status.Should().Be(SignupCardStatuses.Paid);
    }

    [Theory]
    [InlineData(SpreedlyCallbackOutcome.Accepted, 200)]
    [InlineData(SpreedlyCallbackOutcome.Unauthorized, 401)]
    [InlineData(SpreedlyCallbackOutcome.Invalid, 400)]
    [InlineData(SpreedlyCallbackOutcome.RetryLater, 503)]
    public async Task El_callback_contesta_segun_lo_procesado(SpreedlyCallbackOutcome resultado, int http)
    {
        var callbacks = new Mock<ISpreedlyCallbackProcessor>();
        callbacks.Setup(c => c.ProcessAsync(It.IsAny<byte[]>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(resultado);

        var r = await Controlador(new(), callbacks, cuerpo: "<transactions/>"u8.ToArray()).SpreedlyCallback(default);

        ((IStatusCodeActionResult)r).StatusCode.Should().Be(http);
    }

    [Fact]
    public async Task Un_callback_de_mas_de_64_KB_es_413_sin_procesar()
    {
        var callbacks = new Mock<ISpreedlyCallbackProcessor>();
        var r = await Controlador(new(), callbacks, cuerpo: new byte[SignupCardController.MaxCallbackBytes + 1]).SpreedlyCallback(default);

        ((IStatusCodeActionResult)r).StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
        callbacks.VerifyNoOtherCalls();
    }

    [Fact]
    public void Re_check_exige_los_roles_de_cobro()
        => typeof(SignupCardController).GetMethod(nameof(SignupCardController.Recheck))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be(AppRoles.CryptoPaymentApprovers);

    [Fact]
    public async Task Re_check_devuelve_el_desenlace()
    {
        var liq = new Mock<ISignupCardSettlement>();
        liq.Setup(l => l.SettleAsync("ref", SignupCardSettlementSource.Admin, It.IsAny<CancellationToken>()))
           .ReturnsAsync(SignupCardSettlementOutcome.Paid);

        var r = await Controlador(new(), liq: liq).Recheck("ref", default);

        r.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<ApiResponse<SignupCardController.RecheckResponse>>()
         .Which.Data!.Outcome.Should().Be("Paid");
    }

    [Fact]
    public async Task Payment_options_lleva_la_clave_publica_cuando_la_tarjeta_esta_lista()
    {
        var estado = new Mock<IEntraxSignupStatusService>();
        estado.Setup(s => s.GetPaymentOptions()).Returns(new SignupPaymentOptionsDto { CardEnabled = true });
        var tarjeta = new Mock<ISignupCardOptions>();
        tarjeta.Setup(t => t.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new SignupCardAvailability(true, "ENVKEY1", false));
        var c = new EntraxSignupsController(Mock.Of<IEntraxCallbackProcessor>(), estado.Object, Mock.Of<IEntraxSignupSettlement>(),
            NullLogger<EntraxSignupsController>.Instance, tarjeta.Object);

        var r = await c.GetPaymentOptions(default);

        var dto = r.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<ApiResponse<SignupPaymentOptionsDto>>().Which.Data!;
        dto.CardEnabled.Should().BeTrue();
        dto.CardEnvironmentKey.Should().Be("ENVKEY1");
    }

    [Fact]
    public async Task Payment_options_esconde_la_tarjeta_si_Spreedly_no_esta_listo()
    {
        var estado = new Mock<IEntraxSignupStatusService>();
        estado.Setup(s => s.GetPaymentOptions()).Returns(new SignupPaymentOptionsDto { CardEnabled = true });
        var tarjeta = new Mock<ISignupCardOptions>();
        tarjeta.Setup(t => t.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(SignupCardAvailability.Off);
        var c = new EntraxSignupsController(Mock.Of<IEntraxCallbackProcessor>(), estado.Object, Mock.Of<IEntraxSignupSettlement>(),
            NullLogger<EntraxSignupsController>.Instance, tarjeta.Object);

        var dto = ((ApiResponse<SignupPaymentOptionsDto>)((OkObjectResult)await c.GetPaymentOptions(default)).Value!).Data!;

        dto.CardEnabled.Should().BeFalse();
        dto.CardEnvironmentKey.Should().BeNull();
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SignupCardControllerTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `SignupCardController.cs`**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SignupAPI.Services.Card;

namespace MLMConquerorGlobalEdition.SignupAPI.Controllers;

/// <summary>
/// El pago con tarjeta del alta (Plan I): lo que pide la pantalla durante 3DS, el callback de Spreedly y el
/// Re-check del admin. Todo lo que cobra o activa pasa por SignupCardSettlement. Empezar el pago no está
/// aquí: es POST api/v1/signups/{signupId}/complete con PaymentMethod = CreditCard.
///
/// Ninguna respuesta lleva detalles de una excepción (el legado devolvía errores internos en endpoints
/// anónimos): el detalle va al log.
/// </summary>
[ApiController]
[Route("api/v1/signups")]
public class SignupCardController : ControllerBase
{
    /// <summary>Un aviso de Spreedly son unas pocas transacciones; más de 64 KB no es un aviso.</summary>
    public const int MaxCallbackBytes = 64 * 1024;

    private readonly ISignupCardStatusService           _status;
    private readonly ISpreedlyCallbackProcessor         _callbacks;
    private readonly ISignupCardSettlement              _settlement;
    private readonly ILogger<SignupCardController>      _logger;

    public SignupCardController(
        ISignupCardStatusService status, ISpreedlyCallbackProcessor callbacks, ISignupCardSettlement settlement,
        ILogger<SignupCardController> logger)
    {
        _status     = status;
        _callbacks  = callbacks;
        _settlement = settlement;
        _logger     = logger;
    }

    /// <summary>GET api/v1/signups/{signupId}/card/{paymentRef}/status — la pantalla, si perdió la respuesta de finish.</summary>
    [HttpGet("{signupId}/card/{paymentRef}/status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStatus(string signupId, string paymentRef, CancellationToken ct)
        => Step(await _status.GetAsync(signupId, paymentRef, ct));

    /// <summary>POST …/complete — el Lifecycle pidió "trigger-completion".</summary>
    [HttpPost("{signupId}/card/{paymentRef}/complete")]
    [AllowAnonymous]
    public async Task<IActionResult> CompleteThreeDs(string signupId, string paymentRef, CancellationToken ct)
        => Step(await _status.CompleteThreeDsAsync(signupId, paymentRef, ct));

    /// <summary>POST …/finish — el Lifecycle terminó en el navegador; se liquida YA, re-consultando a Spreedly.</summary>
    [HttpPost("{signupId}/card/{paymentRef}/finish")]
    [AllowAnonymous]
    public async Task<IActionResult> Finish(string signupId, string paymentRef, CancellationToken ct)
        => Step(await _status.FinishAsync(signupId, paymentRef, ct));

    /// <summary>POST …/cancel — la persona canceló durante la autenticación (no se había cobrado nada).</summary>
    [HttpPost("{signupId}/card/{paymentRef}/cancel")]
    [AllowAnonymous]
    public async Task<IActionResult> Cancel(string signupId, string paymentRef, CancellationToken ct)
        => Step(await _status.CancelAsync(signupId, paymentRef, ct));

    /// <summary>
    /// POST api/v1/signups/spreedly/callback — el aviso de Spreedly. Cuerpo crudo (la firma va sobre sus
    /// valores) con tope de 64 KB; 2xx solo si quedó procesado.
    /// </summary>
    [HttpPost("spreedly/callback")]
    [AllowAnonymous]
    [RequestSizeLimit(MaxCallbackBytes)]
    public async Task<IActionResult> SpreedlyCallback(CancellationToken ct)
    {
        if (Request.ContentLength > MaxCallbackBytes)
            return TooLarge();

        byte[]? body;
        try
        {
            body = await EntraxSignupsController.ReadBodyAsync(Request.Body, MaxCallbackBytes, ct);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            body = null;
        }

        if (body is null)
            return TooLarge();

        SpreedlyCallbackOutcome outcome;
        try
        {
            outcome = await _callbacks.ProcessAsync(body, Request.ContentType, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Spreedly callback: error inesperado.");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        return outcome switch
        {
            SpreedlyCallbackOutcome.Accepted     => Ok(new { received = true }),
            SpreedlyCallbackOutcome.Unauthorized => Unauthorized(),
            SpreedlyCallbackOutcome.Invalid      => BadRequest(new { error = "INVALID_CALLBACK" }),
            _                                    => StatusCode(StatusCodes.Status503ServiceUnavailable)
        };
    }

    /// <summary>
    /// POST api/v1/signups/card/{paymentRef}/recheck — vuelve a preguntar a Spreedly y liquida (concilia un
    /// cobro dudoso, cierra una autenticación abandonada). Lo llama AdminAPI con el Bearer del administrador.
    /// </summary>
    [HttpPost("card/{paymentRef}/recheck")]
    [Authorize(Roles = AppRoles.CryptoPaymentApprovers)]
    public async Task<IActionResult> Recheck(string paymentRef, CancellationToken ct)
    {
        var outcome = await _settlement.SettleAsync(paymentRef, SignupCardSettlementSource.Admin, ct);
        return outcome == SignupCardSettlementOutcome.NotFound
            ? NotFound(ApiResponse<RecheckResponse>.Fail(SignupCardErrors.PaymentNotFound, "Payment not found."))
            : Ok(ApiResponse<RecheckResponse>.Ok(new RecheckResponse(outcome.ToString())));
    }

    public sealed record RecheckResponse(string Outcome);

    private IActionResult Step(SignupCardStepDto? dto)
        => dto is null
            ? NotFound(ApiResponse<SignupCardStepDto>.Fail(SignupCardErrors.PaymentNotFound, "Payment not found."))
            : Ok(ApiResponse<SignupCardStepDto>.Ok(dto));

    private IActionResult TooLarge()
    {
        _logger.LogWarning("Spreedly callback rechazado: el cuerpo pasa de {Max} bytes.", MaxCallbackBytes);
        return StatusCode(StatusCodes.Status413PayloadTooLarge);
    }
}
```

- [ ] **Step 4: `payment-options` añade la tarjeta en `EntraxSignupsController`**

Añadir `using MLMConquerorGlobalEdition.SignupAPI.Services.Card;`, un campo `private readonly ISignupCardOptions? _cardOptions;`, un último parámetro opcional en el constructor:

```csharp
        ILogger<EntraxSignupsController> logger,
        // Opcional para no tocar las construcciones existentes; sin él la tarjeta se anuncia apagada.
        ISignupCardOptions? cardOptions = null)
```

(con `_cardOptions = cardOptions;`), y sustituir la acción `GetPaymentOptions` por:

```csharp
    /// <summary>
    /// GET api/v1/signups/payment-options — países donde se ofrece pagar en rublos y si se ofrece tarjeta.
    /// La tarjeta solo con Signup:CardEnabled, Spreedly encendido y credenciales del entorno (I18); entonces va
    /// también la environment key, que es pública (la usa el iFrame en el navegador).
    /// </summary>
    [HttpGet("payment-options")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPaymentOptions(CancellationToken ct = default)
    {
        var options = _status.GetPaymentOptions();

        var card = options.CardEnabled && _cardOptions is not null
            ? await _cardOptions.GetAsync(ct)
            : SignupCardAvailability.Off;

        options.CardEnabled        = card.Ready;
        options.CardEnvironmentKey = card.Ready ? card.EnvironmentKey : null;
        options.CardSimulated      = card.Ready && card.Simulated;

        return Ok(ApiResponse<SignupPaymentOptionsDto>.Ok(options));
    }
```

Las pruebas existentes que llaman `GetPaymentOptions()` sin argumentos (`EntraxSignupsControllerTests`) pasan a `await …GetPaymentOptions(default)`; sin `cardOptions`, `CardEnabled` sale `false` aunque el interruptor esté encendido (falla cerrado). Si alguna comprobaba `CardEnabled = true` solo por el interruptor (de `cerrar-tarjeta-en-el-alta`), pasarle un `ISignupCardOptions` que devuelva `new SignupCardAvailability(true, "ENVKEY1", false)`.

- [ ] **Step 5: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter "SignupCardControllerTests|EntraxSignupsControllerTests|OpcionesDePagoDelAltaTests" -nologo`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.SignupAPI/Controllers/SignupCardController.cs \
        MLMConquerorGlobalEdition.SignupAPI/Controllers/EntraxSignupsController.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Card/SignupCardControllerTests.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Entrax/EntraxSignupsControllerTests.cs
git commit -m "Endpoints del pago con tarjeta del alta (estado, completar 3DS, terminar, cancelar, callback de 64 KB y Re-check con roles de cobro) y payment-options con la clave publica solo si Spreedly esta listo"
```

---

### Task 19: Barrido de Hangfire (`signup-card-sweep`)

**Files:**
- Create: `MLMConquerorGlobalEdition.SignupAPI/Jobs/SignupCardSweepJob.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Card/SignupCardSweepJobTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.SignupAPI.Jobs;
using MLMConquerorGlobalEdition.SignupAPI.Services.Card;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Card;

/// <summary>
/// El barrido cierra lo que nadie cerró: autenticaciones abandonadas, cobros dudosos (conciliación) y cobrados
/// sin activar. Uno por scope, los menos consultados primero, y corre aunque Spreedly esté apagado.
/// </summary>
public class SignupCardSweepJobTests
{
    private static SignupCardSweepJob Job(MLMConquerorGlobalEdition.Repository.Context.AppDbContext db, Mock<ISignupCardSettlement> liq,
        bool spreedly = true, RegistroDePrueba<SignupCardSweepJob>? log = null)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => liq.Object);
        var sp = services.BuildServiceProvider();
        return new SignupCardSweepJob(db, sp.GetRequiredService<IServiceScopeFactory>(),
            EscenarioTarjeta.Opciones(o => o.Enabled = spreedly), log ?? new RegistroDePrueba<SignupCardSweepJob>());
    }

    [Fact]
    public async Task Liquida_los_abiertos_y_no_toca_los_cerrados()
    {
        using var db = InMemoryDbHelper.Create();
        await EscenarioTarjeta.SembrarAltaAsync(db, OrderStatus.Processing);
        var abiertos = new[]
        {
            await EscenarioTarjeta.SembrarIntentoAsync(db, SignupCardPaymentStatus.Authenticating, "fake_challenge_0001", "TXA00000001", intento: 1),
            await EscenarioTarjeta.SembrarIntentoAsync(db, SignupCardPaymentStatus.Charging, "fake_unknown_00002", "TXA00000002", intento: 2),
            await EscenarioTarjeta.SembrarIntentoAsync(db, SignupCardPaymentStatus.Charged, "fake_frictionless_3", "TXA00000003", intento: 3)
        };
        await EscenarioTarjeta.SembrarIntentoAsync(db, SignupCardPaymentStatus.Paid, "fake_frictionless_4", "TXA00000004", intento: 4);
        await EscenarioTarjeta.SembrarIntentoAsync(db, SignupCardPaymentStatus.Failed, "fake_failed_000005", "TXA00000005", intento: 5);
        var liq = new Mock<ISignupCardSettlement>();

        await Job(db, liq).ExecuteAsync(default);

        foreach (var a in abiertos)
            liq.Verify(l => l.SettleAsync(a.Id, SignupCardSettlementSource.Sweep, It.IsAny<CancellationToken>()), Times.Once);
        liq.Verify(l => l.SettleAsync(It.IsAny<string>(), It.IsAny<SignupCardSettlementSource>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Un_fallo_en_uno_no_para_a_los_demas()
    {
        using var db = InMemoryDbHelper.Create();
        await EscenarioTarjeta.SembrarAltaAsync(db, OrderStatus.Processing);
        var a = await EscenarioTarjeta.SembrarIntentoAsync(db, SignupCardPaymentStatus.Charging, "fake_unknown_00001", "TXA00000001", intento: 1);
        var b = await EscenarioTarjeta.SembrarIntentoAsync(db, SignupCardPaymentStatus.Charged, "fake_frictionless_2", "TXA00000002", intento: 2);
        var liq = new Mock<ISignupCardSettlement>();
        liq.Setup(l => l.SettleAsync(a.Id, It.IsAny<SignupCardSettlementSource>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("x"));

        await Job(db, liq).ExecuteAsync(default);

        liq.Verify(l => l.SettleAsync(b.Id, SignupCardSettlementSource.Sweep, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Con_Spreedly_apagado_y_cobros_abiertos_avisa_con_Critical_y_sigue()
    {
        using var db = InMemoryDbHelper.Create();
        await EscenarioTarjeta.SembrarAltaAsync(db, OrderStatus.Processing);
        await EscenarioTarjeta.SembrarIntentoAsync(db, SignupCardPaymentStatus.Charging, "fake_unknown_00001", "TXA00000001");
        var liq = new Mock<ISignupCardSettlement>();
        var log = new RegistroDePrueba<SignupCardSweepJob>();

        await Job(db, liq, spreedly: false, log: log).ExecuteAsync(default);

        log.Entradas.Should().Contain(e => e.Nivel == LogLevel.Critical);
        liq.Verify(l => l.SettleAsync(It.IsAny<string>(), SignupCardSettlementSource.Sweep, It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SignupCardSweepJobTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `SignupCardSweepJob.cs`**

```csharp
using Hangfire;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Spreedly;
using MLMConquerorGlobalEdition.SignupAPI.Services.Card;

namespace MLMConquerorGlobalEdition.SignupAPI.Jobs;

/// <summary>
/// HangFire, cada 2 minutos, cola "signups": lleva a su final los pagos con tarjeta del alta que nadie más
/// cerró — autenticaciones abandonadas (las cierra pasada Spreedly:AuthenticationTimeoutMinutes), cobros
/// sin final conocido (concilia; pasada Spreedly:ChargeUnknownGraceMinutes, a revisión) y cobrados sin
/// activar (activa). Como mucho Spreedly:SweepBatchSize por pasada, los menos consultados primero.
///
/// Corre aunque Spreedly esté apagado (decisión I25): apagar no puede dejar cobros a medias sin cerrar.
/// Cada intento en su propio scope (su propio DbContext).
/// </summary>
[Queue("signups")]
public class SignupCardSweepJob
{
    public const string RecurringJobId = "signup-card-sweep";
    public const string Cron           = "*/2 * * * *";

    private readonly AppDbContext                   _db;
    private readonly IServiceScopeFactory           _scopes;
    private readonly SpreedlyOptions                _options;
    private readonly ILogger<SignupCardSweepJob>    _logger;

    public SignupCardSweepJob(AppDbContext db, IServiceScopeFactory scopes, SpreedlyOptions options, ILogger<SignupCardSweepJob> logger)
    {
        _db      = db;
        _scopes  = scopes;
        _options = options;
        _logger  = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var ids = await _db.SignupCardPayments.AsNoTracking()
            .Where(p => p.Status == SignupCardPaymentStatus.Authenticating
                     || p.Status == SignupCardPaymentStatus.Charging
                     || p.Status == SignupCardPaymentStatus.Charged)
            .OrderBy(p => p.LastCheckedAt ?? DateTime.MinValue)
            .ThenBy(p => p.StartedAt)
            .Select(p => p.Id)
            .Take(Math.Max(1, _options.SweepBatchSize))
            .ToListAsync(ct);

        if (!_options.Enabled && ids.Count > 0)
            _logger.LogCritical(
                "Spreedly está apagado y hay {Open} pagos con tarjeta del alta abiertos. El barrido los sigue cerrando: " +
                "no quites la credencial de Spreedly (apagar es dejar de ofrecer la tarjeta, no quitar credenciales).",
                ids.Count);

        foreach (var id in ids)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var settlement = scope.ServiceProvider.GetRequiredService<ISignupCardSettlement>();
                await settlement.SettleAsync(id, SignupCardSettlementSource.Sweep, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Tarjeta del alta: el barrido no pudo liquidar {PaymentRef}; se reintentará.", id);
            }
        }
    }
}
```

- [ ] **Step 4: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SignupCardSweepJobTests -nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.SignupAPI/Jobs/SignupCardSweepJob.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Card/SignupCardSweepJobTests.cs
git commit -m "Barrido signup-card-sweep: cierra autenticaciones abandonadas, concilia cobros dudosos y activa cobrados, uno por scope y aunque Spreedly este apagado"
```

---

### Task 20: Cableado en SignupAPI (`Program.cs` y `appsettings.json`)

**Files:**
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Program.cs`
- Modify: `MLMConquerorGlobalEdition.SignupAPI/appsettings.json`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Card/SpreedlyConfiguracionTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Repository.Services.Spreedly;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Card;

/// <summary>
/// El appsettings.json base va al repositorio: Spreedly apagado, sin simulador, sin ningún secreto (viven en
/// ApiCredential), con el certificado del key ring de credenciales y con los límites de los endpoints de la pantalla.
/// </summary>
public class SpreedlyConfiguracionTests
{
    private static IConfiguration Base()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MLMConquerorGlobalEdition.slnx"))) dir = dir.Parent;
        return new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(dir!.FullName, "MLMConquerorGlobalEdition.SignupAPI", "appsettings.json"))
            .Build();
    }

    [Fact]
    public void Spreedly_viene_apagado_sin_simulador_y_con_las_credenciales_de_Production()
    {
        var o = SpreedlyOptions.FromConfiguration(Base());
        o.Enabled.Should().BeFalse();
        o.UseFake.Should().BeFalse();
        o.CredentialEnvironment.Should().Be("Production");
    }

    [Fact]
    public void No_hay_ningun_secreto_de_Spreedly_en_el_appsettings_base()
    {
        var claves = Base().GetSection("Spreedly").AsEnumerable().Select(kv => kv.Key.ToLowerInvariant()).ToList();
        claves.Should().NotContain(k => k.Contains("secret") || k.Contains("environmentkey") || k.Contains("accesskey") || k.Contains("token"));
    }

    [Fact]
    public void La_tarjeta_del_alta_sigue_apagada()
        => Base()["Signup:CardEnabled"].Should().BeOneOf("False", "false");

    [Fact]
    public void El_key_ring_de_credenciales_tiene_certificado_configurado()
        => Base()["DataProtection:Certificate:Path"].Should().NotBeNullOrWhiteSpace();

    [Theory]
    [InlineData("POST:/api/v1/signups/*/card/*/complete", "10")]
    [InlineData("POST:/api/v1/signups/*/card/*/finish", "10")]
    [InlineData("POST:/api/v1/signups/*/card/*/cancel", "10")]
    [InlineData("GET:/api/v1/signups/*/card/*/status", "30")]
    public void Los_endpoints_de_la_pantalla_tienen_su_limite(string endpoint, string limite)
        => Base().GetSection("IpRateLimiting:GeneralRules").GetChildren()
            .Should().Contain(r => r["Endpoint"] == endpoint && r["Limit"] == limite);
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SpreedlyConfiguracionTests -nologo`
Expected: FAIL (falta la sección `Spreedly`, el certificado y las reglas).

- [ ] **Step 3: `appsettings.json` de SignupAPI**

Añadir al principio del objeto raíz (antes de `"ConnectionStrings"`):

```json
  "DataProtection": {
    "//": "Key ring de las credenciales de gateway (ApiCredential): el MISMO certificado que AdminAPI y Billing. SignupAPI lo necesita desde el Plan I para leer la credencial de Spreedly. En produccion, Thumbprint (almacen de Windows) o Path + Password por variables de entorno; en Development, si el PFX no existe se genera uno. Ver el articulo de KB gateway-credential-encryption.",
    "Certificate": {
      "Thumbprint": "",
      "Path": "%LOCALAPPDATA%/MLMConqueror/DataProtection/gateway-credentials-dev.pfx",
      "Password": ""
    }
  },
```

Añadir debajo de la sección `"Entrax"`:

```json
  "Spreedly": {
    "//": "Tarjeta con 3DS2 Global en el alta (Plan I). AQUI NO HAY SECRETOS: environment key, access secret, SCA provider key, signing secret y gateway tokens viven cifrados en ApiCredential (Admin -> Billing -> API Credentials, filas del entorno CredentialEnvironment). Fuera de Development: Signup:CardEnabled=true sin Enabled=true no arranca, UseFake no arranca, y CallbackUrl (opcional) tiene que ser https absoluta. Apagar la tarjeta es Signup:CardEnabled=false; no quitar la credencial (el barrido sigue cerrando cobros en vuelo). UseFake solo en Development.",
    "Enabled": false,
    "UseFake": false,
    "CredentialEnvironment": "Production",
    "BaseUrl": "https://core.spreedly.com/v1/",
    "CallbackUrl": "",
    "TimeoutSeconds": 30,
    "AuthenticationTimeoutMinutes": 20,
    "ChargeUnknownGraceMinutes": 30,
    "PollThrottleSeconds": 10,
    "SweepBatchSize": 50
  }
```

Y en `IpRateLimiting:GeneralRules`, antes de la regla `"*"`:

```json
      {
        "//": "Pago con tarjeta del alta (Plan I): unas pocas llamadas por pago; la pantalla solo consulta el estado si perdio la respuesta de finish.",
        "Endpoint": "POST:/api/v1/signups/*/card/*/complete",
        "Period": "1m",
        "Limit": 10
      },
      {
        "Endpoint": "POST:/api/v1/signups/*/card/*/finish",
        "Period": "1m",
        "Limit": 10
      },
      {
        "Endpoint": "POST:/api/v1/signups/*/card/*/cancel",
        "Period": "1m",
        "Limit": 10
      },
      {
        "Endpoint": "GET:/api/v1/signups/*/card/*/status",
        "Period": "1m",
        "Limit": 30
      },
```

- [ ] **Step 4: `Program.cs` de SignupAPI**

Añadir los `using`:

```csharp
using MLMConquerorGlobalEdition.Billing.Services.CardGateway;
using MLMConquerorGlobalEdition.Repository.Services.Payout;
using MLMConquerorGlobalEdition.Repository.Services.Spreedly;
using MLMConquerorGlobalEdition.SignupAPI.Services.Card;
```

Justo después del bloque de Entrax (tras `builder.Services.AddScoped<IEntraxSignupStatusService, EntraxSignupStatusService>();`):

```csharp
// Tarjeta con 3DS2 Global de Spreedly (Plan I). La guarda tumba el arranque fuera de Development si la
// tarjeta del alta está encendida sin Spreedly, con el simulador o con un callback que no es https; en
// Development solo avisa. Las credenciales están en ApiCredential: para descifrarlas SignupAPI usa el
// key ring compartido con AdminAPI y Billing (mismo certificado, R6). El simulador no las necesita.
var spreedlyOptions   = SpreedlyOptions.FromConfiguration(builder.Configuration);
var signupCardEnabled = SignupCardPayments.IsEnabled(builder.Configuration);
foreach (var problem in SpreedlyOptionsGuard.Problems(spreedlyOptions, signupCardEnabled, builder.Environment.IsDevelopment()))
    Console.WriteLine($"[Spreedly] {problem}");
if (!spreedlyOptions.UseFake)
    builder.Services.AddGatewayCredentialProtection();
builder.Services.AddSpreedly(spreedlyOptions, builder.Environment.IsDevelopment(), signupCardEnabled);
// Router, reparto, conversión, gateway de tarjeta y orquestador: los mismos de Billing (AddCardCharging).
builder.Services.AddCardCharging();
builder.Services.AddScoped<ISignupCardOptions, SignupCardOptions>();
builder.Services.AddScoped<ISignupCardSettlement, SignupCardSettlement>();
builder.Services.AddScoped<ISignupCardCheckout, SignupCardCheckout>();
builder.Services.AddScoped<ISignupCardStatusService, SignupCardStatusService>();
builder.Services.AddScoped<ISpreedlyCallbackProcessor, SpreedlyCallbackProcessor>();
```

En el bloque de HangFire, junto a `builder.Services.AddScoped<EntraxSignupSweepJob>();`:

```csharp
builder.Services.AddScoped<SignupCardSweepJob>();
```

Debajo del `RecurringJob.AddOrUpdate<EntraxSignupSweepJob>(…)`:

```csharp
// Tarjeta del alta (Plan I): cierra autenticaciones abandonadas, concilia cobros dudosos y activa los
// cobrados que no se activaron. Corre aunque Spreedly esté apagado.
RecurringJob.AddOrUpdate<SignupCardSweepJob>(
    SignupCardSweepJob.RecurringJobId,
    job => job.ExecuteAsync(CancellationToken.None),
    SignupCardSweepJob.Cron,
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc, QueueName = "signups" });
```

Y debajo del bloque `if (app.Environment.IsDevelopment() && entraxOptions.UseFake) { … }`:

```csharp
// Solo Development con Spreedly:UseFake: aprobar o rechazar el challenge de una autenticación (o resolver una
// compra con resultado desconocido) del simulador, y forzar un barrido. Fuera de Development UseFake no arranca.
if (app.Environment.IsDevelopment() && spreedlyOptions.UseFake)
{
    app.MapPost("/api/v1/dev/spreedly/{transactionToken}/{decision}",
        (string transactionToken, string decision, FakeSpreedlyClient fake) =>
        {
            var ok = decision is "approve" or "reject" && fake.Resolve(transactionToken, decision == "approve");
            return ok ? Results.Ok(new { transactionToken, decision }) : Results.NotFound();
        }).AllowAnonymous();

    app.MapPost("/api/v1/dev/spreedly/sweep",
        async (SignupCardSweepJob job, CancellationToken ct) =>
        {
            await job.ExecuteAsync(ct);
            return Results.Ok(new { swept = true });
        }).AllowAnonymous();
}
```

- [ ] **Step 5: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter "SpreedlyConfiguracionTests|EntraxConfiguracionTests|TarjetaApagadaEnElAltaTests" -nologo`
Expected: PASS.

Run: `dotnet build MLMConquerorGlobalEdition.SignupAPI -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.SignupAPI/Program.cs \
        MLMConquerorGlobalEdition.SignupAPI/appsettings.json \
        MLMConquerorGlobalEdition.Signups.Tests/Card/SpreedlyConfiguracionTests.cs
git commit -m "SignupAPI registra Spreedly, el cobro con tarjeta por el router, el alta con 3DS, el barrido y los endpoints del simulador; appsettings sin secretos, con el certificado del key ring y los limites de la pantalla"
```

---

## Fase 2 — Componente único del pago con tarjeta

### Task 21: El JS del iFrame y del Lifecycle (`spreedlyCard.js`)

**Files:**
- Create: `MLMConquerorGlobalEdition.SharedComponents.Lite/wwwroot/js/spreedlyCard.js`

El JS no tiene lógica de negocio: carga el script de Spreedly cuando hace falta, inicializa el iFrame, tokeniza, serializa los datos del navegador y lleva el Lifecycle de 3DS, avisando a .NET por `DotNetObjectReference`. **El número y el CVV viven solo en los iframes de `core.spreedly.com`: este módulo nunca los ve.** No hay prueba automática del JS (R14); lo cubren la prueba bUnit de la Tarea 22 (que se llama con los argumentos correctos) y la prueba real de §11.

- [ ] **Step 1: Crear `spreedlyCard.js`**

```js
// Pago con tarjeta (Plan I): iFrame de Spreedly para número y CVV, datos del navegador para 3DS2 y el
// Lifecycle de 3DS (huella del dispositivo y challenge). El número y el CVV viven SOLO en los iframes de
// core.spreedly.com: este módulo no los ve, no los guarda y no los manda a ningún sitio.
//
// Lo carga SpreedlyCardPayment.razor por JSInterop (import) solo cuando la tarjeta se va a usar, y el script
// de Spreedly se inyecta aquí la primera vez. Spreedly es un objeto global: hay un pago con tarjeta por página.

const SCRIPT_URL = "https://core.spreedly.com/iframe/iframe-v1.min.js";
const ACCEPT_HEADER = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";

let loading = null;
let dotnet = null;
let pendingTokenize = null;
let lastStatusEvent = null;

function loadScript() {
    if (window.Spreedly) return Promise.resolve();
    if (loading) return loading;
    loading = new Promise((resolve, reject) => {
        const s = document.createElement("script");
        s.src = SCRIPT_URL;
        s.async = true;
        s.onload = () => resolve();
        s.onerror = () => { loading = null; reject(new Error("spreedly-script")); };
        document.head.appendChild(s);
    });
    return loading;
}

function onThreeDsStatus(event) {
    lastStatusEvent = event;
    if (dotnet) dotnet.invokeMethodAsync("OnThreeDsStatus", event && event.action ? String(event.action) : "");
}

export async function init(dotnetRef, environmentKey, numberEl, cvvEl) {
    dotnet = dotnetRef;
    await loadScript();
    if (typeof Spreedly.removeHandlers === "function") Spreedly.removeHandlers();

    Spreedly.on("ready", () => {
        Spreedly.setFieldType("number", "text");
        Spreedly.setNumberFormat("prettyFormat");
        Spreedly.setFieldType("cvv", "text");
        Spreedly.setStyle("number", "width:100%;font-size:1rem;");
        Spreedly.setStyle("cvv", "width:100%;font-size:1rem;");
        if (dotnet) dotnet.invokeMethodAsync("OnSpreedlyReady");
    });

    Spreedly.on("paymentMethod", (token, pm) => {
        const p = pendingTokenize;
        pendingTokenize = null;
        if (p) p.resolve({ token: token, last4: pm ? pm.last_four_digits : null, brand: pm ? pm.card_type : null, errors: [] });
    });

    Spreedly.on("errors", (errors) => {
        const list = (errors || []).map(e => ({ attribute: e.attribute || "", key: e.key || "", message: e.message || "" }));
        const p = pendingTokenize;
        pendingTokenize = null;
        if (p) p.resolve({ token: null, last4: null, brand: null, errors: list });
    });

    Spreedly.on("3ds:status", onThreeDsStatus);
    Spreedly.init(environmentKey, { numberEl: numberEl, cvvEl: cvvEl });
}

export function tokenize(fullName, month, year) {
    return new Promise((resolve) => {
        pendingTokenize = { resolve: resolve };
        Spreedly.tokenizeCreditCard({ full_name: fullName, month: String(month), year: String(year) });
    });
}

export function browserInfo() {
    // "05" = ventana completa para el challenge (como el legado).
    return Spreedly.ThreeDS.serialize("05", ACCEPT_HEADER);
}

function toSpreedly(step) {
    return {
        state: step.state,
        required_action: step.requiredAction,
        device_fingerprint_form: step.deviceFingerprintForm,
        challenge_form: step.challengeForm,
        challenge_url: step.challengeUrl,
        checkout_form: step.checkoutForm,
        checkout_url: step.checkoutUrl,
        token: step.transactionToken
    };
}

export function startLifecycle(environmentKey, step, hiddenEl, challengeEl) {
    const lifecycle = new Spreedly.ThreeDS.Lifecycle({
        environmentKey: environmentKey,
        hiddenIframeLocation: hiddenEl,
        challengeIframeLocation: challengeEl,
        transactionToken: step.transactionToken,
        challengeIframeClasses: "spreedly-challenge-frame"
    });
    lifecycle.start(toSpreedly(step));
}

export function finalize(step) {
    // Tras "trigger-completion" el servidor completó la autenticación; si sigue pendiente, el Lifecycle continúa.
    if (lastStatusEvent && typeof lastStatusEvent.finalize === "function") lastStatusEvent.finalize(toSpreedly(step));
}

export function reload() {
    try { if (window.Spreedly && typeof Spreedly.reload === "function") Spreedly.reload(); } catch (e) { /* nada que hacer */ }
}

export function dispose() {
    try { if (window.Spreedly && typeof Spreedly.removeHandlers === "function") Spreedly.removeHandlers(); } catch (e) { /* nada */ }
    dotnet = null;
    pendingTokenize = null;
    lastStatusEvent = null;
}
```

- [ ] **Step 2: Comprobar que el archivo se publica como recurso estático de la biblioteca**

Run: `dotnet build MLMConquerorGlobalEdition.SharedComponents.Lite -v q -nologo`
Expected: `0 Error(s)` y el archivo en `MLMConquerorGlobalEdition.SharedComponents.Lite/obj/**/staticwebassets*` (el SDK Razor publica `wwwroot` como `_content/MLMConquerorGlobalEdition.SharedComponents.Lite/js/spreedlyCard.js`).

- [ ] **Step 3: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedComponents.Lite/wwwroot/js/spreedlyCard.js
git commit -m "JS del pago con tarjeta: carga el iFrame de Spreedly solo cuando hace falta, tokeniza, serializa el navegador para 3DS2 y lleva el Lifecycle; el numero y el CVV no salen de los iframes de Spreedly"
```

---

### Task 22: Componente único `SpreedlyCardPayment.razor`

**Files:**
- Create: `MLMConquerorGlobalEdition.SharedComponents.Lite/Payments/SpreedlyCardPayment.razor`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/SpreedlyCardPaymentTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using MLMConquerorGlobalEdition.SharedComponents.Lite.Payments;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// La pieza de pago con tarjeta: en modo simulado no carga ni JS ni iframe y deja elegir el escenario; en modo
/// real importa el módulo e inicializa el iFrame con la clave pública. En los dos, lo que decide es el servidor:
/// el componente solo enseña el paso que le devuelven.
/// </summary>
public class SpreedlyCardPaymentTests : BunitContext
{
    private const string Modulo = "./_content/MLMConquerorGlobalEdition.SharedComponents.Lite/js/spreedlyCard.js";

    private static SignupCardStepResult Paso(string estado, SpreedlyThreeDsStepDto? tresDs = null, string? texto = null)
        => SignupCardStepResult.Ok(new SignupCardStepDto { PaymentRef = "ref-1", Status = estado, ThreeDs = tresDs, Message = texto });

    private sealed class Avisos
    {
        public readonly List<bool> Pagado = [];
        public readonly List<string> Fallos = [];
        public readonly List<SignupCardPaymentRequestDto> Inicios = [];
        public readonly List<(string Token, bool Aprobar)> Simulados = [];
        public int Terminados;
    }

    private (IRenderedComponent<SpreedlyCardPayment> Cut, Avisos A) Pinta(
        Func<SignupCardPaymentRequestDto, SignupCardStepResult> inicio,
        Func<SignupCardStepResult>? terminar = null,
        Func<SignupCardStepResult>? estado = null,
        bool simulado = true, string? clave = null)
    {
        var a = new Avisos();
        var cut = Render<SpreedlyCardPayment>(p => p
            .Add(x => x.Simulated, simulado)
            .Add(x => x.EnvironmentKey, clave)
            .Add(x => x.PollInterval, TimeSpan.Zero)
            .Add(x => x.Start, (Func<SignupCardPaymentRequestDto, Task<SignupCardStepResult>>)(r => { a.Inicios.Add(r); return Task.FromResult(inicio(r)); }))
            .Add(x => x.Finish, (Func<string, Task<SignupCardStepResult>>)(_ => { a.Terminados++; return Task.FromResult((terminar ?? (() => Paso(SignupCardStatuses.Paid)))()); }))
            .Add(x => x.GetStatus, (Func<string, Task<SignupCardStepResult>>)(_ => Task.FromResult((estado ?? (() => Paso(SignupCardStatuses.Paid)))())))
            .Add(x => x.CompleteThreeDs, (Func<string, Task<SignupCardStepResult>>)(_ => Task.FromResult(Paso(SignupCardStatuses.Paid))))
            .Add(x => x.Cancel, (Func<string, Task>)(_ => Task.CompletedTask))
            .Add(x => x.SimulateChallenge, (Func<string, bool, Task>)((t, ok) => { a.Simulados.Add((t, ok)); return Task.CompletedTask; }))
            .Add(x => x.OnPaid, (bool ya) => a.Pagado.Add(ya))
            .Add(x => x.OnFailed, (string t) => a.Fallos.Add(t)));
        return (cut, a);
    }

    [Fact]
    public async Task En_modo_simulado_no_carga_JS_y_paga_con_un_token_del_escenario()
    {
        // JSInterop estricto (por defecto en bUnit): cualquier llamada a JS haría fallar la prueba.
        var (cut, a) = Pinta(_ => Paso(SignupCardStatuses.Paid));
        cut.Find("select.spreedly-scenario").Change(SpreedlyFakeScenarios.Frictionless);

        await cut.InvokeAsync(() => cut.Instance.PayAsync());

        a.Inicios.Single().PaymentMethodToken.Should().StartWith("fake_frictionless_");
        a.Pagado.Should().Equal(false);
        cut.Find(".spreedly-card").GetAttribute("data-phase").Should().Be("Paid");
    }

    [Fact]
    public async Task Con_challenge_simulado_aprobar_termina_y_paga()
    {
        var reto = new SpreedlyThreeDsStepDto { TransactionToken = "fake_auth_1", State = "pending", RequiredAction = "challenge" };
        var (cut, a) = Pinta(_ => Paso(SignupCardStatuses.Authenticating, reto));
        cut.Find("select.spreedly-scenario").Change(SpreedlyFakeScenarios.Challenge);

        await cut.InvokeAsync(() => cut.Instance.PayAsync());
        cut.Find(".spreedly-card").GetAttribute("data-phase").Should().Be("Challenge");

        await cut.Find("button.spreedly-approve").ClickAsync(new MouseEventArgs());

        a.Simulados.Should().Equal(("fake_auth_1", true));
        a.Terminados.Should().Be(1);
        a.Pagado.Should().Equal(false);
    }

    [Fact]
    public async Task Un_fallo_lo_explica_y_avisa_a_la_pagina()
    {
        var (cut, a) = Pinta(_ => Paso(SignupCardStatuses.Failed, texto: "The card was declined."));

        await cut.InvokeAsync(() => cut.Instance.PayAsync());

        a.Fallos.Should().Equal("The card was declined.");
        cut.Find(".spreedly-message").TextContent.Should().Contain("declined");
        cut.Instance.Busy.Should().BeFalse("con otra tarjeta se puede volver a intentar");
    }

    [Fact]
    public async Task Si_el_alta_ya_estaba_pagada_avisa_como_pagada()
    {
        var (cut, a) = Pinta(_ => SignupCardStepResult.Settled());
        await cut.InvokeAsync(() => cut.Instance.PayAsync());
        a.Pagado.Should().Equal(true);
    }

    [Fact]
    public async Task Cobrando_pregunta_el_estado_hasta_el_final()
    {
        var veces = 0;
        var (cut, a) = Pinta(_ => Paso(SignupCardStatuses.Charging),
            estado: () => ++veces < 3 ? Paso(SignupCardStatuses.Charging) : Paso(SignupCardStatuses.Paid));

        await cut.InvokeAsync(() => cut.Instance.PayAsync());

        veces.Should().Be(3);
        a.Pagado.Should().Equal(false);
    }

    [Fact]
    public async Task Mientras_paga_no_empieza_otro_pago()
    {
        var reto = new SpreedlyThreeDsStepDto { TransactionToken = "fake_auth_1", State = "pending", RequiredAction = "challenge" };
        var (cut, a) = Pinta(_ => Paso(SignupCardStatuses.Authenticating, reto));

        await cut.InvokeAsync(() => cut.Instance.PayAsync());
        await cut.InvokeAsync(() => cut.Instance.PayAsync());

        a.Inicios.Should().HaveCount(1);
    }

    [Fact]
    public void En_modo_real_importa_el_modulo_e_inicializa_el_iframe_con_la_clave_publica()
    {
        var modulo = JSInterop.SetupModule(Modulo);
        var init = modulo.SetupVoid("init", _ => true);
        init.SetVoidResult();

        var (cut, _) = Pinta(_ => Paso(SignupCardStatuses.Paid), simulado: false, clave: "ENVKEY1");

        var llamada = init.Invocations.Single();
        llamada.Arguments[1].Should().Be("ENVKEY1");
        cut.Find($"#{llamada.Arguments[2]}").ClassList.Should().Contain("spreedly-field");
        cut.Find($"#{llamada.Arguments[3]}").ClassList.Should().Contain("spreedly-field");
        cut.FindAll("input[type=password], input[inputmode=numeric]").Should().BeEmpty("el número y el CVV no son inputs nuestros");
    }

    [Fact]
    public async Task En_modo_real_si_Spreedly_rechaza_la_tarjeta_no_se_llama_al_servidor()
    {
        var modulo = JSInterop.SetupModule(Modulo);
        modulo.SetupVoid("init", _ => true).SetVoidResult();
        modulo.SetupVoid("reload", _ => true).SetVoidResult();
        modulo.Setup<SpreedlyCardPayment.TokenizeResult>("tokenize", _ => true)
              .SetResult(new SpreedlyCardPayment.TokenizeResult(null, null, null,
                  [new SpreedlyCardPayment.TokenizeError("number", "errors.invalid", "Number is invalid")]));

        var (cut, a) = Pinta(_ => Paso(SignupCardStatuses.Paid), simulado: false, clave: "ENVKEY1");
        cut.Find("input.spreedly-name").Change("Ivan Petrov");
        cut.Find("select.spreedly-month").Change("12");
        cut.Find("select.spreedly-year").Change((DateTime.Now.Year + 2).ToString());

        await cut.InvokeAsync(() => cut.Instance.PayAsync());

        a.Inicios.Should().BeEmpty();
        a.Fallos.Should().Equal("Number is invalid");
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter SpreedlyCardPaymentTests -nologo`
Expected: FAIL de compilación (`SpreedlyCardPayment` no existe).

- [ ] **Step 3: Crear `SpreedlyCardPayment.razor`**

```razor
@using MLMConquerorGlobalEdition.SharedKernel.Billing
@using Microsoft.JSInterop
@implements IAsyncDisposable
@inject IJSRuntime JS
@*
    Pago con tarjeta con 3D Secure (Spreedly, 3DS2 Global). UNA pieza para cualquier portal: hoy Signup.razor y
    MemberJoin.razor (por SpreedlySignupFlow); mañana la compra de tokens o el alta de tarjeta del BizCenter.

    EL NÚMERO Y EL CVV NO SON INPUTS NUESTROS: van en los iframes de core.spreedly.com (spreedlyCard.js). Aquí
    solo hay el nombre, el mes y el año, que no son datos de la tarjeta protegidos.

    Lo específico entra por parámetro: cómo empezar el pago (Start), completar 3DS (CompleteThreeDs), terminar
    (Finish), consultar (GetStatus) y cancelar (Cancel) contra la API de cada portal; qué hacer al pagar
    (OnPaid) o al fallar (OnFailed); y los textos (inglés por defecto, como EntraxQrPayment).

    NO DECIDE NADA: el pago lo cobra y lo da por hecho el servidor (SignupCardSettlement); esto enseña el paso
    que devuelve. "succeeded" del Lifecycle no es "pagado": es "pregúntale al servidor" (Finish).

    En modo simulado (Development con el simulador de Spreedly) no carga JS ni iframe: ofrece los escenarios
    del simulador y aprueba o rechaza el challenge con SimulateChallenge.
*@

<div class="spreedly-card" data-phase="@_phase" aria-live="polite">
    @if (Simulated)
    {
        <div class="alert alert-warning small mb-3 spreedly-simulated">@SimulatedText</div>
        <select class="form-select mb-3 spreedly-scenario" @bind="_scenario" disabled="@Busy">
            @foreach (var s in SpreedlyFakeScenarios.All)
            {
                <option value="@s">@s</option>
            }
        </select>
    }
    else
    {
        <div class="row g-3 @(Busy ? "opacity-75" : "")">
            <div class="col-12">
                <label class="form-label text-secondary small">@NameLabel</label>
                <input type="text" class="form-control spreedly-name" autocomplete="cc-name" @bind="_fullName" disabled="@Busy" />
            </div>
            <div class="col-lg-8">
                <label class="form-label text-secondary small">@NumberLabel</label>
                <div id="@_numberId" class="form-control spreedly-field" style="height:2.5rem"></div>
            </div>
            <div class="col-lg-4">
                <label class="form-label text-secondary small">@CvvLabel</label>
                <div id="@_cvvId" class="form-control spreedly-field" style="height:2.5rem"></div>
            </div>
            <div class="col-6">
                <select class="form-select spreedly-month" @bind="_month" disabled="@Busy">
                    <option value="0">@MonthLabel</option>
                    @for (var m = 1; m <= 12; m++)
                    {
                        var mo = m;
                        <option value="@mo">@mo.ToString("D2")</option>
                    }
                </select>
            </div>
            <div class="col-6">
                <select class="form-select spreedly-year" @bind="_year" disabled="@Busy">
                    <option value="0">@YearLabel</option>
                    @for (var y = DateTime.Now.Year; y <= DateTime.Now.Year + 12; y++)
                    {
                        var yr = y;
                        <option value="@yr">@yr</option>
                    }
                </select>
            </div>
        </div>
    }

    @* Donde el Lifecycle pone el iframe oculto de la huella del dispositivo y el del challenge del banco. *@
    <div id="@_fingerprintId" class="d-none"></div>
    <div class="spreedly-challenge mt-3 @(_phase == Phase.Challenge ? "" : "d-none")">
        <p class="small text-muted mb-2">@ChallengeText</p>
        <div id="@_challengeId"></div>
        @if (Simulated && _phase == Phase.Challenge)
        {
            <div class="d-flex gap-2 mt-2">
                <button type="button" class="btn btn-success btn-sm spreedly-approve" @onclick="() => SimulatedDecisionAsync(true)">Approve (simulated bank)</button>
                <button type="button" class="btn btn-outline-danger btn-sm spreedly-reject" @onclick="() => SimulatedDecisionAsync(false)">Reject (simulated bank)</button>
            </div>
        }
        <button type="button" class="btn btn-link btn-sm mt-2 spreedly-cancel" @onclick="CancelAsync">@CancelText</button>
    </div>

    @if (_phase is Phase.Tokenizing or Phase.Authenticating or Phase.Charging)
    {
        <p class="small text-muted mt-3 mb-0 spreedly-working">
            <span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span>@(_phase == Phase.Charging ? ChargingText : VerifyingText)
        </p>
    }
    else if (_phase == Phase.Paid)
    {
        <p class="mt-3 mb-0 fw-semibold text-success spreedly-paid">@PaidText</p>
    }

    @if (!string.IsNullOrEmpty(_message))
    {
        <p class="mt-3 mb-0 small text-danger spreedly-message">@_message</p>
    }
</div>

@code {
    public const string ModulePath = "./_content/MLMConquerorGlobalEdition.SharedComponents.Lite/js/spreedlyCard.js";

    public enum Phase { Idle, Tokenizing, Authenticating, Challenge, Charging, Paid, Failed }

    public sealed record TokenizeError(string Attribute, string Key, string Message);
    public sealed record TokenizeResult(string? Token, string? Last4, string? Brand, List<TokenizeError>? Errors);

    [Parameter] public string? EnvironmentKey { get; set; }
    [Parameter] public bool Simulated { get; set; }

    [Parameter, EditorRequired] public Func<SignupCardPaymentRequestDto, Task<SignupCardStepResult>> Start { get; set; } = default!;
    [Parameter, EditorRequired] public Func<string, Task<SignupCardStepResult>> CompleteThreeDs { get; set; } = default!;
    [Parameter, EditorRequired] public Func<string, Task<SignupCardStepResult>> Finish { get; set; } = default!;
    [Parameter, EditorRequired] public Func<string, Task<SignupCardStepResult>> GetStatus { get; set; } = default!;
    [Parameter, EditorRequired] public Func<string, Task> Cancel { get; set; } = default!;

    /// <summary>Solo en modo simulado: aprobar o rechazar el challenge del simulador.</summary>
    [Parameter] public Func<string, bool, Task>? SimulateChallenge { get; set; }

    /// <summary>El servidor dio el pago por hecho. true = el alta ya estaba pagada antes de este pago.</summary>
    [Parameter] public EventCallback<bool> OnPaid { get; set; }

    [Parameter] public EventCallback<string> OnFailed { get; set; }

    /// <summary>Pausa entre consultas del estado cuando el servidor aún está cobrando.</summary>
    [Parameter] public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(3);
    [Parameter] public int MaxPolls { get; set; } = 40;
    [Parameter] public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    [Parameter] public string NameLabel       { get; set; } = "Name on card";
    [Parameter] public string NumberLabel     { get; set; } = "Card number";
    [Parameter] public string CvvLabel        { get; set; } = "CVV";
    [Parameter] public string MonthLabel      { get; set; } = "Month";
    [Parameter] public string YearLabel       { get; set; } = "Year";
    [Parameter] public string VerifyingText   { get; set; } = "Verifying your card with your bank…";
    [Parameter] public string ChargingText    { get; set; } = "Processing your payment…";
    [Parameter] public string ChallengeText   { get; set; } = "Your bank needs to confirm this payment.";
    [Parameter] public string CancelText      { get; set; } = "Cancel and use another card";
    [Parameter] public string PaidText        { get; set; } = "Payment received.";
    [Parameter] public string InvalidFieldsText { get; set; } = "Please enter the name on the card and its expiration date.";
    [Parameter] public string GenericErrorText  { get; set; } = "We couldn't process the card. Please try another card or payment method.";
    [Parameter] public string CancelledText     { get; set; } = "The card payment was cancelled.";
    [Parameter] public string StillConfirmingText { get; set; } = "We're still confirming your payment. Please don't pay again; you can sign in once it's confirmed.";
    [Parameter] public string SimulatedText   { get; set; } = "Development simulator: no real card is used. Choose what the bank should do.";

    private readonly string _numberId      = "spreedly-number-" + Guid.NewGuid().ToString("N");
    private readonly string _cvvId         = "spreedly-cvv-" + Guid.NewGuid().ToString("N");
    private readonly string _fingerprintId = "spreedly-fp-" + Guid.NewGuid().ToString("N");
    private readonly string _challengeId   = "spreedly-challenge-" + Guid.NewGuid().ToString("N");

    private Phase   _phase = Phase.Idle;
    private string? _message;
    private string? _paymentRef;
    private SpreedlyThreeDsStepDto? _threeDs;
    private string  _scenario = SpreedlyFakeScenarios.Frictionless;
    private string  _fullName = string.Empty;
    private int     _month;
    private int     _year;

    private IJSObjectReference? _module;
    private DotNetObjectReference<SpreedlyCardPayment>? _self;

    /// <summary>Hay un pago en marcha: la página no debe empezar otro.</summary>
    public bool Busy => _phase is Phase.Tokenizing or Phase.Authenticating or Phase.Challenge or Phase.Charging or Phase.Paid;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || Simulated || string.IsNullOrWhiteSpace(EnvironmentKey))
            return;

        _self   = DotNetObjectReference.Create(this);
        _module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
        await _module.InvokeVoidAsync("init", _self, EnvironmentKey, _numberId, _cvvId);
    }

    /// <summary>Tokeniza (iFrame o escenario simulado) y empieza el pago. El final llega por OnPaid u OnFailed.</summary>
    public async Task PayAsync()
    {
        if (Busy) return;
        _message = null;

        SignupCardPaymentRequestDto request;
        if (Simulated)
        {
            request = new SignupCardPaymentRequestDto { PaymentMethodToken = SpreedlyFakeScenarios.Token(_scenario), BrowserInfo = "simulated" };
        }
        else
        {
            if (_module is null || string.IsNullOrWhiteSpace(_fullName) || _month is < 1 or > 12 || _year < 1)
            {
                _message = InvalidFieldsText;
                await InvokeAsync(StateHasChanged);
                return;
            }

            SetPhase(Phase.Tokenizing);
            var tokenized = await _module.InvokeAsync<TokenizeResult>("tokenize", _fullName.Trim(), _month, _year);
            if (string.IsNullOrEmpty(tokenized.Token) || tokenized.Errors is { Count: > 0 })
            {
                await FailAsync(tokenized.Errors?.FirstOrDefault()?.Message is { Length: > 0 } m ? m : GenericErrorText);
                return;
            }

            var browserInfo = await _module.InvokeAsync<string>("browserInfo");
            request = new SignupCardPaymentRequestDto { PaymentMethodToken = tokenized.Token, BrowserInfo = browserInfo };
        }

        SetPhase(Phase.Authenticating);
        await HandleAsync(await SafeAsync(() => Start(request)));
    }

    private async Task HandleAsync(SignupCardStepResult result)
    {
        if (result.AlreadySettled)
        {
            SetPhase(Phase.Paid);
            await OnPaid.InvokeAsync(true);
            return;
        }

        var step = result.Step;
        if (step is null)
        {
            await FailAsync(result.Error ?? GenericErrorText);
            return;
        }

        _paymentRef = step.PaymentRef;
        switch (step.Status)
        {
            case SignupCardStatuses.Paid:
                SetPhase(Phase.Paid);
                await OnPaid.InvokeAsync(false);
                break;

            case SignupCardStatuses.Failed or SignupCardStatuses.ToReview:
                await FailAsync(step.Message ?? GenericErrorText);
                break;

            case SignupCardStatuses.Charging or SignupCardStatuses.Charged:
                SetPhase(Phase.Charging);
                await PollAsync();
                break;

            default:
                if (step.ThreeDs is not null)
                    await StartLifecycleAsync(step.ThreeDs);
                else
                    await PollAsync();
                break;
        }
    }

    private async Task StartLifecycleAsync(SpreedlyThreeDsStepDto threeDs)
    {
        _threeDs = threeDs;
        var challenge = string.Equals(threeDs.RequiredAction, "challenge", StringComparison.OrdinalIgnoreCase);

        if (Simulated)
        {
            if (challenge) SetPhase(Phase.Challenge);
            else await FinishAsync();
            return;
        }

        SetPhase(challenge ? Phase.Challenge : Phase.Authenticating);
        await _module!.InvokeVoidAsync("startLifecycle", EnvironmentKey, threeDs, _fingerprintId, _challengeId);
    }

    /// <summary>Lo llama spreedlyCard.js con cada evento "3ds:status" del Lifecycle.</summary>
    [JSInvokable]
    public async Task OnThreeDsStatus(string action)
    {
        switch (action)
        {
            case "challenge":
                SetPhase(Phase.Challenge);
                break;

            case "trigger-completion":
                var completed = await SafeAsync(() => CompleteThreeDs(_paymentRef!));
                if (completed.Step is { Status: SignupCardStatuses.Authenticating, ThreeDs: not null } next)
                    await _module!.InvokeVoidAsync("finalize", next.ThreeDs);
                else
                    await HandleAsync(completed);
                break;

            default:
                // "succeeded", "error", "finalization-timeout": decida el servidor, que vuelve a consultar a Spreedly.
                await FinishAsync();
                break;
        }
    }

    [JSInvokable]
    public Task OnSpreedlyReady() => InvokeAsync(StateHasChanged);

    private async Task SimulatedDecisionAsync(bool approve)
    {
        if (_threeDs is null || SimulateChallenge is null) return;
        await SimulateChallenge(_threeDs.TransactionToken, approve);
        await FinishAsync();
    }

    private async Task FinishAsync()
    {
        SetPhase(Phase.Charging);
        try
        {
            await HandleAsync(await Finish(_paymentRef!));
        }
        catch
        {
            // Se perdió la respuesta: el servidor pudo cobrar igualmente. Se pregunta el estado, nunca se repite el pago.
            await PollAsync();
        }
    }

    private async Task PollAsync()
    {
        for (var i = 0; i < MaxPolls; i++)
        {
            if (PollInterval > TimeSpan.Zero)
                await Task.Delay(PollInterval, TimeProvider);

            SignupCardStepResult r;
            try { r = await GetStatus(_paymentRef!); }
            catch { continue; }

            var still = r.Step is { Status: SignupCardStatuses.Charging or SignupCardStatuses.Charged }
                        || r.Step is { Status: SignupCardStatuses.Authenticating, ThreeDs: null };
            if (!still)
            {
                await HandleAsync(r);
                return;
            }
        }

        _message = StillConfirmingText;
        await InvokeAsync(StateHasChanged);
    }

    private async Task CancelAsync()
    {
        if (_paymentRef is not null)
        {
            try { await Cancel(_paymentRef); } catch { /* el servidor cierra lo abandonado por su cuenta */ }
        }
        await FailAsync(CancelledText);
    }

    private async Task FailAsync(string message)
    {
        _message = message;
        SetPhase(Phase.Failed);
        if (_module is not null)
        {
            try { await _module.InvokeVoidAsync("reload"); } catch (JSDisconnectedException) { }
        }
        await OnFailed.InvokeAsync(message);
    }

    private async Task<SignupCardStepResult> SafeAsync(Func<Task<SignupCardStepResult>> call)
    {
        try { return await call(); }
        catch { return SignupCardStepResult.Fail(GenericErrorText); }
    }

    private void SetPhase(Phase phase)
    {
        _phase = phase;
        _ = InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("dispose");
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException) { }
        }
        _self?.Dispose();
    }
}
```

- [ ] **Step 4: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter SpreedlyCardPaymentTests -nologo`
Expected: PASS.

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter SignupsClientLigeroTests -nologo`
Expected: PASS (la biblioteca ligera sigue sin dependencias pesadas: solo `Microsoft.AspNetCore.Components.Web`, que trae JSInterop).

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedComponents.Lite/Payments/SpreedlyCardPayment.razor \
        MLMConquerorGlobalEdition.BizCenter.Tests/SpreedlyCardPaymentTests.cs
git commit -m "Componente unico de pago con tarjeta y 3DS en SharedComponents.Lite: iFrame de Spreedly sin inputs de numero ni CVV, Lifecycle por JSInterop, estados de verificar, challenge, cobrar, pagado y fallido, y modo simulado sin JS"
```

---

## Fase 3 (cont.) — La pantalla del alta paga con el componente

### Task 23: Las llamadas del alta con tarjeta y su flujo (`SpreedlySignupApi`, `SpreedlySignupFlow`)

**Files:**
- Create: `MLMConquerorGlobalEdition.Signups.Client/Payments/SpreedlySignupApi.cs`
- Create: `MLMConquerorGlobalEdition.Signups.Client/Payments/SpreedlySignupFlow.razor`
- Modify: `MLMConquerorGlobalEdition.Signups.Client/Payments/SignupPaymentOptionsApi.cs` (copiar `CardEnvironmentKey` y `CardSimulated`)
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Card/SpreedlySignupApiTests.cs`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Card/SpreedlySignupFlowTests.cs`

- [ ] **Step 1: Escribir las pruebas que fallan**

`SpreedlySignupApiTests.cs`:

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.Signups.Client.Payments;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Services.Card;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Card;

/// <summary>Las llamadas del navegador al pago con tarjeta del alta, escritas una vez para las dos páginas.</summary>
public class SpreedlySignupApiTests
{
    private sealed class Manejador(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public readonly List<(HttpRequestMessage Peticion, string Cuerpo)> Llamadas = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Llamadas.Add((r, r.Content is null ? "" : await r.Content.ReadAsStringAsync(ct)));
            return responder(r);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode c, string j) => new(c) { Content = new StringContent(j, Encoding.UTF8, "application/json") };
    private static HttpClient Api(Manejador h) => new(h) { BaseAddress = new Uri("https://signup.pruebas/") };

    [Fact]
    public async Task Empezar_manda_solo_el_token_y_los_datos_del_navegador()
    {
        var h = new Manejador(_ => Json(HttpStatusCode.OK, """{"success":true,"data":{"signupId":"ORD-1","card":{"paymentRef":"r1","status":"Authenticating","threeDs":{"transactionToken":"T","state":"pending","requiredAction":"challenge"}}}}"""));

        var r = await SpreedlySignupApi.StartAsync(Api(h), "ORD-1", new SignupCardPaymentRequestDto { PaymentMethodToken = "fake_challenge_1", BrowserInfo = "B" });

        r.Step!.ThreeDs!.RequiredAction.Should().Be("challenge");
        var (peticion, cuerpo) = h.Llamadas.Single();
        peticion.RequestUri!.AbsolutePath.Should().Be("/api/v1/signups/ORD-1/complete");
        using var doc = JsonDocument.Parse(cuerpo);
        doc.RootElement.GetProperty("paymentMethod").GetString().Should().Be("CreditCard");
        doc.RootElement.GetProperty("card").GetProperty("paymentMethodToken").GetString().Should().Be("fake_challenge_1");
        doc.RootElement.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("paymentMethod", "card");
    }

    [Fact]
    public async Task Un_alta_ya_pagada_se_reconoce()
    {
        var h = new Manejador(_ => Json(HttpStatusCode.Conflict, $$"""{"success":false,"errorCode":"{{SignupOrderClaim.SignupAlreadySettledCode}}","message":"x"}"""));
        (await SpreedlySignupApi.StartAsync(Api(h), "ORD-1", new())).AlreadySettled.Should().BeTrue();
    }

    [Fact]
    public async Task Un_error_trae_el_texto_del_servidor()
    {
        var h = new Manejador(_ => Json(HttpStatusCode.ServiceUnavailable, $$"""{"success":false,"errorCode":"{{SignupCardErrors.NotConfigured}}","message":"{{SignupCardMessages.NotConfigured}}"}"""));
        (await SpreedlySignupApi.StartAsync(Api(h), "ORD-1", new())).Error.Should().Be(SignupCardMessages.NotConfigured);
    }

    [Fact]
    public async Task Una_respuesta_que_no_es_json_es_un_error_generico()
    {
        var h = new Manejador(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("slow down") });
        (await SpreedlySignupApi.FinishAsync(Api(h), "ORD-1", "r1")).Error.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("finish")]
    [InlineData("cancel")]
    public async Task Los_pasos_van_a_la_ruta_del_intento(string paso)
    {
        var h = new Manejador(_ => Json(HttpStatusCode.OK, """{"success":true,"data":{"paymentRef":"r1","status":"Paid"}}"""));
        var api = Api(h);

        var r = paso switch
        {
            "complete" => await SpreedlySignupApi.CompleteThreeDsAsync(api, "ORD-1", "r1"),
            "finish"   => await SpreedlySignupApi.FinishAsync(api, "ORD-1", "r1"),
            _          => await SpreedlySignupApi.CancelAsync(api, "ORD-1", "r1")
        };

        r.Step!.Status.Should().Be("Paid");
        h.Llamadas.Single().Peticion.RequestUri!.AbsolutePath.Should().Be($"/api/v1/signups/ORD-1/card/r1/{paso}");
    }

    [Fact]
    public void Los_codigos_del_navegador_son_los_del_servidor()
        => SpreedlySignupApi.AlreadySettledCode.Should().Be(SignupOrderClaim.SignupAlreadySettledCode);
}
```

`SpreedlySignupFlowTests.cs`:

```csharp
using System.Net;
using System.Text;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.Signups.Client.Payments;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Card;

/// <summary>El flujo de la tarjeta que comparten las dos páginas: con el simulador, de principio a fin contra la API.</summary>
public class SpreedlySignupFlowTests : BunitContext
{
    private sealed class Fabrica(Func<HttpRequestMessage, HttpResponseMessage> responder) : IHttpClientFactory
    {
        public readonly List<string> Rutas = [];
        private sealed class M(Fabrica f, Func<HttpRequestMessage, HttpResponseMessage> r) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
            {
                lock (f.Rutas) f.Rutas.Add($"{req.Method} {req.RequestUri!.AbsolutePath}");
                return Task.FromResult(r(req));
            }
        }
        public HttpClient CreateClient(string name) => new(new M(this, responder)) { BaseAddress = new Uri("https://signup.pruebas/") };
    }

    private static HttpResponseMessage Json(string j) => new(HttpStatusCode.OK) { Content = new StringContent(j, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Con_challenge_aprobado_en_el_simulador_avisa_a_la_pagina_de_que_esta_pagado()
    {
        var http = new Fabrica(req => req.RequestUri!.AbsolutePath switch
        {
            "/api/v1/signups/ORD-1/complete" => Json("""{"success":true,"data":{"card":{"paymentRef":"r1","status":"Authenticating","threeDs":{"transactionToken":"fake_auth_1","state":"pending","requiredAction":"challenge"}}}}"""),
            "/api/v1/dev/spreedly/fake_auth_1/approve" => Json("""{"transactionToken":"fake_auth_1","decision":"approve"}"""),
            "/api/v1/signups/ORD-1/card/r1/finish" => Json("""{"success":true,"data":{"paymentRef":"r1","status":"Paid"}}"""),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });
        Services.AddSingleton<IHttpClientFactory>(http);
        var completadas = new List<bool>();

        var cut = Render<SpreedlySignupFlow>(p => p
            .Add(x => x.Simulated, true)
            .Add(x => x.OnCompleted, (bool ya) => completadas.Add(ya)));

        await cut.InvokeAsync(() => cut.Instance.PayAsync("ORD-1"));
        await cut.Find("button.spreedly-approve").ClickAsync(new MouseEventArgs());

        completadas.Should().Equal(false);
        http.Rutas.Should().Equal(
            "POST /api/v1/signups/ORD-1/complete",
            "POST /api/v1/dev/spreedly/fake_auth_1/approve",
            "POST /api/v1/signups/ORD-1/card/r1/finish");
    }

    [Fact]
    public async Task Un_error_al_empezar_llega_a_la_pagina()
    {
        Services.AddSingleton<IHttpClientFactory>(new Fabrica(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("""{"success":false,"errorCode":"CARD_NOT_CONFIGURED","message":"Card payments are temporarily unavailable."}""", Encoding.UTF8, "application/json")
            }));
        var errores = new List<string>();
        var cut = Render<SpreedlySignupFlow>(p => p.Add(x => x.Simulated, true).Add(x => x.OnError, (string e) => errores.Add(e)));

        await cut.InvokeAsync(() => cut.Instance.PayAsync("ORD-1"));

        errores.Should().Equal("Card payments are temporarily unavailable.");
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter "SpreedlySignupApiTests|SpreedlySignupFlowTests" -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `SpreedlySignupApi.cs`**

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Signups.Client.Payments;

/// <summary>
/// Las llamadas del pago con tarjeta del alta (Plan I) que comparten Signup.razor y MemberJoin.razor, escritas
/// una vez. Corren en el navegador (WebAssembly) contra SignupAPI. Nada de lo que mandan describe la tarjeta:
/// solo el token del iFrame y los datos del navegador para 3DS.
/// </summary>
public static class SpreedlySignupApi
{
    // SignupAPI no se puede referenciar desde el navegador; SpreedlySignupApiTests vigila que no se separen.
    public const string AlreadySettledCode = "SIGNUP_ALREADY_SETTLED";

    private const string GenericError = "We couldn't process the card. Please try another card or payment method.";

    public static async Task<SignupCardStepResult> StartAsync(
        HttpClient api, string signupId, SignupCardPaymentRequestDto card, CancellationToken ct = default)
    {
        using var r = await api.PostAsJsonAsync($"api/v1/signups/{Uri.EscapeDataString(signupId)}/complete",
            new { PaymentMethod = "CreditCard", Card = card }, ct);
        var b = await ReadAsync<StartData>(r, ct);

        if (b?.Success == true && b.Data?.Card is not null)
            return SignupCardStepResult.Ok(b.Data.Card);

        return b?.ErrorCode == AlreadySettledCode
            ? SignupCardStepResult.Settled()
            : SignupCardStepResult.Fail(b?.Message ?? b?.Errors?.FirstOrDefault() ?? GenericError);
    }

    public static Task<SignupCardStepResult> CompleteThreeDsAsync(HttpClient api, string signupId, string paymentRef, CancellationToken ct = default)
        => StepAsync(api, HttpMethod.Post, signupId, paymentRef, "complete", ct);

    public static Task<SignupCardStepResult> FinishAsync(HttpClient api, string signupId, string paymentRef, CancellationToken ct = default)
        => StepAsync(api, HttpMethod.Post, signupId, paymentRef, "finish", ct);

    public static Task<SignupCardStepResult> GetStatusAsync(HttpClient api, string signupId, string paymentRef, CancellationToken ct = default)
        => StepAsync(api, HttpMethod.Get, signupId, paymentRef, "status", ct);

    public static Task<SignupCardStepResult> CancelAsync(HttpClient api, string signupId, string paymentRef, CancellationToken ct = default)
        => StepAsync(api, HttpMethod.Post, signupId, paymentRef, "cancel", ct);

    /// <summary>Solo Development con el simulador: el "banco" aprueba o rechaza el challenge.</summary>
    public static async Task SimulateChallengeAsync(HttpClient api, string transactionToken, bool approve, CancellationToken ct = default)
    {
        using var r = await api.PostAsync(
            $"api/v1/dev/spreedly/{Uri.EscapeDataString(transactionToken)}/{(approve ? "approve" : "reject")}", null, ct);
    }

    private static async Task<SignupCardStepResult> StepAsync(
        HttpClient api, HttpMethod method, string signupId, string paymentRef, string step, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method,
            $"api/v1/signups/{Uri.EscapeDataString(signupId)}/card/{Uri.EscapeDataString(paymentRef)}/{step}");
        using var r = await api.SendAsync(request, ct);
        var b = await ReadAsync<SignupCardStepDto>(r, ct);

        return b?.Success == true && b.Data is not null
            ? SignupCardStepResult.Ok(b.Data)
            : SignupCardStepResult.Fail(b?.Message ?? b?.Errors?.FirstOrDefault() ?? GenericError);
    }

    private static async Task<Wrapper<T>?> ReadAsync<T>(HttpResponseMessage r, CancellationToken ct)
    {
        try
        {
            return await r.Content.ReadFromJsonAsync<Wrapper<T>>(ct);
        }
        catch (JsonException)
        {
            // Un 429 del limitador o un 502 de un proxy no vienen como ApiResponse.
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private sealed record Wrapper<T>(bool Success, T? Data, string? Message, List<string>? Errors, string? ErrorCode);

    private sealed record StartData(SignupCardStepDto? Card);
}
```

- [ ] **Step 4: Crear `SpreedlySignupFlow.razor`**

```razor
@using MLMConquerorGlobalEdition.SharedKernel.Billing
@using MLMConquerorGlobalEdition.SharedComponents.Lite.Payments
@inject IHttpClientFactory HttpFactory
@*
    El pago con tarjeta del alta (Plan I), escrito UNA vez para Signup.razor y MemberJoin.razor: pone el
    componente único SpreedlyCardPayment y le da las llamadas de SignupAPI. La página lo pinta en la pestaña de
    tarjeta y, al pulsar "Join Now", llama a PayAsync(signupId) cuando el alta ya existe. Escucha:
      - OnCompleted(alreadyPaid): el servidor cobró y activó (o el alta ya estaba pagada); la página enseña
        "bienvenido, inicia sesión". No hay tokens (I12).
      - OnError(texto): no se cobró; la página lo enseña y la persona puede probar otra tarjeta u otro método.
*@

<SpreedlyCardPayment @ref="_card"
                     EnvironmentKey="@EnvironmentKey"
                     Simulated="Simulated"
                     Start="StartAsync"
                     CompleteThreeDs="CompleteAsync"
                     Finish="FinishAsync"
                     GetStatus="StatusAsync"
                     Cancel="CancelAsync"
                     SimulateChallenge="SimulateAsync"
                     OnPaid="OnCompleted"
                     OnFailed="OnError" />

@code {
    public const string DefaultClientName = "SignupsInternal";

    [Parameter] public string? EnvironmentKey { get; set; }
    [Parameter] public bool Simulated { get; set; }
    [Parameter] public string ClientName { get; set; } = DefaultClientName;
    [Parameter] public EventCallback<bool> OnCompleted { get; set; }
    [Parameter] public EventCallback<string> OnError { get; set; }

    private SpreedlyCardPayment? _card;
    private string? _signupId;

    /// <summary>Hay un pago con tarjeta en marcha: la página no debe empezar otro (ni con otro método).</summary>
    public bool Busy => _card?.Busy == true;

    /// <summary>Tokeniza y empieza el pago del alta ya creada. El final llega por OnCompleted u OnError.</summary>
    public Task PayAsync(string signupId)
    {
        _signupId = signupId;
        return _card is null ? Task.CompletedTask : _card.PayAsync();
    }

    private HttpClient Api => HttpFactory.CreateClient(ClientName);

    private Task<SignupCardStepResult> StartAsync(SignupCardPaymentRequestDto card) => SpreedlySignupApi.StartAsync(Api, _signupId!, card);
    private Task<SignupCardStepResult> CompleteAsync(string paymentRef) => SpreedlySignupApi.CompleteThreeDsAsync(Api, _signupId!, paymentRef);
    private Task<SignupCardStepResult> FinishAsync(string paymentRef) => SpreedlySignupApi.FinishAsync(Api, _signupId!, paymentRef);
    private Task<SignupCardStepResult> StatusAsync(string paymentRef) => SpreedlySignupApi.GetStatusAsync(Api, _signupId!, paymentRef);
    private Task CancelAsync(string paymentRef) => SpreedlySignupApi.CancelAsync(Api, _signupId!, paymentRef);
    private Task SimulateAsync(string transactionToken, bool approve) => SpreedlySignupApi.SimulateChallengeAsync(Api, transactionToken, approve);
}
```

- [ ] **Step 5: `SignupPaymentOptionsApi.GetAsync` copia la clave y el modo simulado**

En `SignupPaymentOptionsApi.cs`, en el objeto que devuelve `GetAsync`:

```csharp
                return new SignupPaymentOptionsDto
                {
                    EntraxCountries    = r.Data.EntraxCountries ?? [],
                    CardEnabled        = r.Data.CardEnabled && !string.IsNullOrWhiteSpace(r.Data.CardEnvironmentKey),
                    CardEnvironmentKey = r.Data.CardEnvironmentKey,
                    CardSimulated      = r.Data.CardSimulated
                };
```

(Sin clave no hay iFrame que inicializar: la pestaña se esconde, falla cerrado.)

- [ ] **Step 6: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter "SpreedlySignupApiTests|SpreedlySignupFlowTests|OpcionesDePagoDelAltaTests|PestanaDeTarjetaDelAltaTests" -nologo`
Expected: PASS. (Si alguna prueba de `cerrar-tarjeta-en-el-alta` esperaba `CardEnabled = true` con solo `cardEnabled: true` en el JSON, añadirle `"cardEnvironmentKey":"ENVKEY1"` al JSON de la respuesta simulada.)

- [ ] **Step 7: Commit**

```bash
git add MLMConquerorGlobalEdition.Signups.Client/Payments/SpreedlySignupApi.cs \
        MLMConquerorGlobalEdition.Signups.Client/Payments/SpreedlySignupFlow.razor \
        MLMConquerorGlobalEdition.Signups.Client/Payments/SignupPaymentOptionsApi.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Card/SpreedlySignupApiTests.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Card/SpreedlySignupFlowTests.cs
git commit -m "Llamadas y flujo del pago con tarjeta del alta escritos una vez: solo el token del iFrame y los datos del navegador, pasos de 3DS por la ruta del intento y simulador en Development"
```

---

### Task 24: `Signup.razor` y `MemberJoin.razor` sin campos de número ni CVV

**Files:**
- Modify: `MLMConquerorGlobalEdition.Signups.Client/Pages/Signup.razor`
- Modify: `MLMConquerorGlobalEdition.Signups.Client/Pages/MemberJoin.razor`
- Modify: `MLMConquerorGlobalEdition.Signups.Client/Program.cs`
- Modify: `MLMConquerorGlobalEdition.Signups.Tests/Billing/ElAsistenteNoEscribeLaPasarelaAFuegoTests.cs`

Los cambios son los mismos en las dos páginas (los nombres de campo coinciden: `_ccFirst`, `_ccLast`, `_ccNumber`, `_ccCvv`, `_ccExpMonth`, `_ccExpYear`, `_showCcNum`, `_showCcv`, `DetectCardBrand()`, `_cardEnabled`).

- [ ] **Step 1: Cambiar la prueba guardiana (falla)**

En `ElAsistenteNoEscribeLaPasarelaAFuegoTests.cs`, sustituir `ElAsistente_ConstruyeElPagoConTarjetaDondeSeDecidio` por:

```csharp
    /// <summary>
    /// Plan I: la tarjeta se paga SOLO por el iFrame de Spreedly (SpreedlySignupFlow → SpreedlyCardPayment). En las
    /// pantallas de alta no queda ningún campo propio de número ni de CVV, ni la tokenización simulada.
    /// </summary>
    [Theory]
    [MemberData(nameof(Asistentes))]
    public void ElAsistente_PagaConTarjetaSoloPorElIframeDeSpreedly(string ruta)
    {
        var codigo = SinComentarios(LeerAsistente(ruta));

        codigo.Should().Contain("<SpreedlySignupFlow", $"{ruta} paga con tarjeta por el componente único");
        foreach (var prohibido in new[] { "_ccNumber", "_ccCvv", "CardPayment.PrepareAsync", "ICardTokenizationService", "DetectCardBrand" })
            codigo.Should().NotContain(prohibido, $"{ruta} no puede tener campos propios de número/CVV ni tokenizar fuera del iFrame");
    }
```

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter ElAsistenteNoEscribeLaPasarelaAFuegoTests -nologo`
Expected: FAIL (las páginas aún tienen los campos).

- [ ] **Step 2: Cabecera de las dos páginas**

Borrar la línea `@inject ICardTokenizationService Tokenizer`.

- [ ] **Step 3: La pestaña de tarjeta**

Sustituir todo el bloque `@if (_paymentTab == SignupPaymentTabs.Card && _cardEnabled) { <div class="row g-3"> … </div> }` (nombre, número con el ojo, CVV, mes y año) por:

```razor
                @* Plan I: número y CVV en el iFrame de Spreedly. El componente está pintado siempre que la tarjeta se
                   ofrezca (para que el iFrame no se reinicie al cambiar de pestaña) y solo se ve en la de tarjeta. *@
                @if (_cardEnabled)
                {
                    <div class="@(_paymentTab == SignupPaymentTabs.Card ? "" : "d-none")">
                        <SpreedlySignupFlow @ref="_cardFlow"
                                            EnvironmentKey="@_cardEnvironmentKey"
                                            Simulated="_cardSimulated"
                                            OnCompleted="OnCardCompleted"
                                            OnError="OnCardError" />
                    </div>
                }
```

- [ ] **Step 4: El botón "Join Now" no se pulsa con un pago con tarjeta en marcha**

En el `disabled="@(…)"` del botón, añadir al principio de la condición `(_cardFlow?.Busy == true) ||`.

- [ ] **Step 5: Los campos y la carga de opciones**

Borrar las declaraciones `_ccFirst`, `_ccLast`, `_ccNumber`, `_ccCvv`, `_ccExpMonth`, `_ccExpYear`, `_showCcNum`, `_showCcv` y el método `DetectCardBrand()` con su comentario. Añadir junto a `_cardEnabled`:

```csharp
    private SpreedlySignupFlow? _cardFlow;
    private string? _cardEnvironmentKey;
    private bool    _cardSimulated;
```

Y donde se leen las opciones (`var paymentOptions = await SignupPaymentOptionsApi.GetAsync(api);`), debajo de `_cardEnabled = paymentOptions.CardEnabled;`:

```csharp
        _cardEnvironmentKey = paymentOptions.CardEnvironmentKey;
        _cardSimulated      = paymentOptions.CardSimulated;
```

- [ ] **Step 6: `HandleJoinNow`: la tarjeta paga por el componente**

Sustituir desde `object payment;` hasta el final del `if/else` que construye `payment` (la rama de tarjeta con `CardPayment.Validate`/`PrepareAsync` y el `switch` con su caso por defecto `_ => (object)new { PaymentMethod = "CreditCard" }`) por:

```csharp
            if (paymentTab == SignupPaymentTabs.Card)
            {
                // Plan I: el número y el CVV están en el iFrame de Spreedly. El componente tokeniza, autentica con
                // 3DS y espera al servidor, que cobra y activa; el final llega por OnCardCompleted / OnCardError.
                await _cardFlow!.PayAsync(_signupId!);
                return;
            }

            object? payment = paymentTab switch
            {
                "token"  => (object)new { PaymentMethod = "Token",  TokenCode = _payToken },
                "crypto" => (object)new { PaymentMethod = "Crypto", CryptoCurrency = _cryptoCurrency },
                _        => null
            };
            if (payment is null) { _error = "Please choose a payment method."; return; }
```

Y quitar el comentario largo "La vía de tarjeta se TOKENIZA aquí, en el navegador…" que precede (ya no aplica); dejar el de "El método se fija aquí…".

- [ ] **Step 7: Los avisos del componente**

Junto a `OnEntraxCompleted` / `OnEntraxError`:

```csharp
    // ── Tarjeta con 3DS (Plan I) ─────────────────────────────────────────────
    // Tokenizar, autenticar, el challenge y esperar el cobro viven en SpreedlySignupFlow. Aquí solo se decide qué
    // enseña la página: activada (sin tokens: la persona inicia sesión) o el error para elegir otra tarjeta o método.

    private void OnCardCompleted(bool alreadyPaid) => ShowSettled(alreadyPaid);

    private void OnCardError(string message) => _error = message;
```

- [ ] **Step 8: `Signups.Client/Program.cs` deja de registrar la tokenización simulada**

Borrar el bloque `// ── TOKENIZACIÓN DE TARJETA ──…` entero y la línea `builder.Services.AddScoped<ICardTokenizationService, SimulatedCardTokenizationService>();`, y sustituirlos por:

```csharp
// La tarjeta del alta se paga por el iFrame de Spreedly (SharedComponents.Lite/Payments/SpreedlyCardPayment.razor,
// Plan I): el número no pasa por Blazor ni por nuestros servidores. Ya no hay tokenización en .NET que registrar.
```

Si queda sin uso, quitar `using MLMConquerorGlobalEdition.SharedKernel.Billing;` de ese archivo. (`Signups/Program.cs` sigue registrando `ServerSideCardTokenizationGuard`: es inofensivo y su prueba sigue; se retira con el Plan L.)

- [ ] **Step 9: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter "ElAsistenteNoEscribeLaPasarelaAFuegoTests|PestanaDeTarjetaDelAltaTests|SignupsClientLigeroTests" -nologo`
Expected: PASS.

Run: `dotnet build MLMConquerorGlobalEdition.Signups -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 10: Commit**

```bash
git add MLMConquerorGlobalEdition.Signups.Client/Pages/Signup.razor \
        MLMConquerorGlobalEdition.Signups.Client/Pages/MemberJoin.razor \
        MLMConquerorGlobalEdition.Signups.Client/Program.cs \
        MLMConquerorGlobalEdition.Signups.Tests/Billing/ElAsistenteNoEscribeLaPasarelaAFuegoTests.cs
git commit -m "Las dos pantallas de alta pagan con tarjeta por el iFrame de Spreedly: sin campos de numero ni CVV, sin tokenizacion simulada, y el alta se activa cuando el servidor cobra"
```

---

## Fase 4 — Admin

### Task 25: Lista de pagos con tarjeta del alta y *Re-check* (AdminAPI)

**Files:**
- Create: `MLMConquerorGlobalEdition.AdminAPI/Services/SignupApiRecheckForwarder.cs`
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminEntraxSignupsController.cs` (acción `Recheck` y sus privados `Unavailable`, `IsJsonObject`, `AuditAsync`, `OutcomeOf`)
- Create: `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminSignupCardPaymentsController.cs`
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Features/CardSignups/AdminSignupCardPaymentsControllerTests.cs`

El reenvío del *Re-check* (Bearer, tope de tiempo, auditoría, 403 sin cuerpo, 502 si SignupAPI no contesta) se escribe una vez (I22). `AdminEntraxSignupsController` pasa a usarlo sin cambiar su constructor ni sus constantes públicas: sus pruebas no cambian y hacen de regresión.

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.CardSignups;

/// <summary>
/// Pagos con tarjeta del alta en el admin: los abiertos (autenticando, cobrando, cobrados sin activar), los que
/// hay que revisar (dinero que pudo moverse y no se aplicó) y los recientes. Re-check se reenvía a SignupAPI con
/// el Bearer del administrador y queda auditado. Nada de lo que sale permite cobrar: ni el token de la tarjeta
/// ni el de 3DS.
/// </summary>
public class AdminSignupCardPaymentsControllerTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 10, 0, 0);

    private sealed class Captura(HttpStatusCode codigo = HttpStatusCode.OK,
        string cuerpo = "{\"success\":true,\"data\":{\"outcome\":\"Paid\"}}") : HttpMessageHandler
    {
        public HttpRequestMessage? Peticion;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Peticion = r;
            return Task.FromResult(new HttpResponseMessage(codigo) { Content = new StringContent(cuerpo, System.Text.Encoding.UTF8, "application/json") });
        }
    }

    private sealed class Caido : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
            => Task.FromException<HttpResponseMessage>(new HttpRequestException("caído"));
    }

    private static Mock<IHttpClientFactory> Fabrica(HttpMessageHandler h)
    {
        var f = new Mock<IHttpClientFactory>();
        f.Setup(x => x.CreateClient("SignupApi")).Returns(new HttpClient(h) { BaseAddress = new Uri("https://signup.pruebas/") });
        return f;
    }

    private static AdminSignupCardPaymentsController Build(AppDbContext db, IHttpClientFactory? f = null, string? bearer = null)
    {
        var ctx = new DefaultHttpContext();
        if (bearer is not null) ctx.Request.Headers.Authorization = bearer;
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.UserId).Returns("admin-uid-1");
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.Now).Returns(Now);

        return new AdminSignupCardPaymentsController(db, f ?? Mock.Of<IHttpClientFactory>(), user.Object, clock.Object,
            NullLogger<AdminSignupCardPaymentsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = ctx }
        };
    }

    private static async Task<SignupCardPayment> SembrarAsync(AppDbContext db, SignupCardPaymentStatus estado, string? motivo = null, int intento = 1)
    {
        if (!db.MemberProfiles.Any())
            db.MemberProfiles.Add(new MemberProfile
            {
                MemberId = "AMB-1", Email = "ana@example.com", FirstName = "Ana", LastName = "Gil", Country = "US",
                DateOfBirth = new DateTime(1990, 1, 1), MemberType = MemberType.Ambassador, Status = MemberAccountStatus.Pending,
                EnrollDate = Now, CreatedBy = "t", CreationDate = Now, LastUpdateDate = Now
            });
        var p = new SignupCardPayment
        {
            OrderId = "ORD-1", MemberId = "AMB-1", ExternalOrderId = SignupCardPayment.BuildExternalOrderId("ORD-1", intento),
            Status = estado, AmountUsd = 80m, ChargeAmount = 80m, ChargeCurrency = "USD", RouteBucketKey = "b",
            PlannedProcessors = "1", PaymentMethodToken = "PMSECRETO123", CardBrand = "Visa", First6 = "411111", Last4 = "1111",
            ScaTransactionToken = $"SCATX{intento:D5}", ScaAuthenticationToken = "SCATOKEN-SECRETO", Eci = "05", TransStatus = "Y",
            LiabilityShift = true, FailureCode = motivo, StartedAt = Now, CreatedBy = "t", CreationDate = Now, LastUpdateDate = Now
        };
        db.SignupCardPayments.Add(p);
        await db.SaveChangesAsync();
        return p;
    }

    [Fact]
    public void Exige_los_cuatro_roles_de_cobro()
        => typeof(AdminSignupCardPaymentsController).GetCustomAttribute<AuthorizeAttribute>()!.Roles
            .Should().Be(AppRoles.CryptoPaymentApprovers);

    [Fact]
    public async Task Abiertos_son_los_que_se_autentican_cobran_o_esperan_activarse()
    {
        await using var db = InMemoryDbHelper.Create();
        await SembrarAsync(db, SignupCardPaymentStatus.Authenticating, intento: 1);
        await SembrarAsync(db, SignupCardPaymentStatus.Charging, intento: 2);
        await SembrarAsync(db, SignupCardPaymentStatus.Charged, intento: 3);
        await SembrarAsync(db, SignupCardPaymentStatus.Paid, intento: 4);
        await SembrarAsync(db, SignupCardPaymentStatus.ToReview, "CHARGE_OUTCOME_UNKNOWN", intento: 5);

        var abiertos = (PagedResult<AdminSignupCardPaymentsController.SignupCardPaymentRowDto>)
            ((ApiResponse<PagedResult<AdminSignupCardPaymentsController.SignupCardPaymentRowDto>>)((OkObjectResult)await Build(db).List("open")).Value!).Data!;
        abiertos.Items.Select(r => r.Status).Should().BeEquivalentTo("Authenticating", "Charging", "Charged");

        var revisar = ((ApiResponse<PagedResult<AdminSignupCardPaymentsController.SignupCardPaymentRowDto>>)((OkObjectResult)await Build(db).List("review")).Value!).Data!;
        revisar.Items.Single().Reason.Should().Be("CHARGE_OUTCOME_UNKNOWN");
    }

    [Fact]
    public void La_fila_no_lleva_nada_con_lo_que_se_pueda_cobrar()
        => typeof(AdminSignupCardPaymentsController.SignupCardPaymentRowDto).GetProperties().Select(p => p.Name)
            .Should().NotContain(new[] { "PaymentMethodToken", "ScaAuthenticationToken", "ScaTransactionToken", "Notes" });

    [Fact]
    public async Task Re_check_reenvia_con_el_bearer_y_queda_auditado()
    {
        await using var db = InMemoryDbHelper.Create();
        var p = await SembrarAsync(db, SignupCardPaymentStatus.Charging);
        var h = new Captura();

        var r = await Build(db, Fabrica(h).Object, "Bearer token-admin").Recheck(p.Id);

        r.Should().BeOfType<ContentResult>().Which.StatusCode.Should().Be(200);
        h.Peticion!.RequestUri!.AbsolutePath.Should().Be($"/api/v1/signups/card/{p.Id}/recheck");
        h.Peticion.Headers.Authorization!.ToString().Should().Be("Bearer token-admin");

        var fila = db.AuditTracking.Single();
        fila.Action.Should().Be(AdminSignupCardPaymentsController.AuditRecheck);
        fila.EntityName.Should().Be(nameof(SignupCardPayment));
        fila.OldValues.Should().Contain("Charging");
        using var nuevo = JsonDocument.Parse(fila.NewValues!);
        nuevo.RootElement.GetProperty("Outcome").GetString().Should().Be("Paid");
        fila.NewValues.Should().NotContain("token-admin");
    }

    [Fact]
    public async Task Re_check_de_algo_que_no_existe_es_404()
    {
        await using var db = InMemoryDbHelper.Create();
        (await Build(db).Recheck("no-existe")).Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Con_SignupAPI_caida_es_502_y_queda_auditado()
    {
        await using var db = InMemoryDbHelper.Create();
        var p = await SembrarAsync(db, SignupCardPaymentStatus.Charging);

        var r = await Build(db, Fabrica(new Caido()).Object).Recheck(p.Id);

        r.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
        db.AuditTracking.Single().NewValues.Should().Contain(AdminSignupCardPaymentsController.SignupApiUnavailable);
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter AdminSignupCardPaymentsControllerTests -nologo`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `SignupApiRecheckForwarder.cs`** (el código es el que hoy vive dentro de `AdminEntraxSignupsController.Recheck`, generalizado)

```csharp
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Services;

/// <summary>
/// Reenvía un Re-check del admin a SignupAPI (que es quien liquida, por su camino único) con el Bearer del
/// administrador, con tope de tiempo, y deja una fila en AuditTracking con quién lo pidió, el estado anterior y
/// lo que contestó SignupAPI — también si no contestó. El Bearer se reenvía y no se guarda.
///
/// Lo usan Entrax y la tarjeta del alta (Plan I, I22): una sola versión del reenvío auditado.
/// </summary>
public sealed class SignupApiRecheckForwarder
{
    public const string SignupApiUnavailable = "SIGNUP_API_UNAVAILABLE";
    public const string ClientCancelled      = "CLIENT_CANCELLED";

    private readonly AppDbContext         _db;
    private readonly IHttpClientFactory   _httpFactory;
    private readonly ICurrentUserService  _user;
    private readonly IDateTimeProvider    _clock;
    private readonly ILogger              _logger;

    public SignupApiRecheckForwarder(
        AppDbContext db, IHttpClientFactory httpFactory, ICurrentUserService user, IDateTimeProvider clock, ILogger logger)
    {
        _db          = db;
        _httpFactory = httpFactory;
        _user        = user;
        _clock       = clock;
        _logger      = logger;
    }

    public async Task<IActionResult> ForwardAsync(
        HttpRequest incoming, string signupApiPath, string entityName, string entityId, string beforeStatus,
        string auditAction, string forbiddenMessage, TimeSpan timeout, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient(SignupApiRegistration.HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, signupApiPath);

        var bearer = incoming.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(bearer) && AuthenticationHeaderValue.TryParse(bearer, out var parsed))
            request.Headers.Authorization = parsed;

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(timeout);

        int    status;
        string body;
        try
        {
            using var resp = await http.SendAsync(request, limit.Token);
            status = (int)resp.StatusCode;
            body   = await resp.Content.ReadAsStringAsync(CancellationToken.None);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await AuditAsync(entityName, entityId, auditAction, beforeStatus, 0, ClientCancelled);
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            await AuditAsync(entityName, entityId, auditAction, beforeStatus, 0, SignupApiUnavailable);
            return Unavailable();
        }

        if (!IsJsonObject(body))
        {
            if (status is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden)
            {
                await AuditAsync(entityName, entityId, auditAction, beforeStatus, status, "FORBIDDEN");
                return new ObjectResult(ApiResponse<object>.Fail("FORBIDDEN", forbiddenMessage)) { StatusCode = status };
            }

            await AuditAsync(entityName, entityId, auditAction, beforeStatus, status, SignupApiUnavailable);
            return Unavailable();
        }

        await AuditAsync(entityName, entityId, auditAction, beforeStatus, status, OutcomeOf(body));
        return new ContentResult { Content = body, ContentType = "application/json", StatusCode = status };
    }

    private static ObjectResult Unavailable() =>
        new(ApiResponse<object>.Fail(SignupApiUnavailable, "The signup service is not reachable. Try again."))
        {
            StatusCode = StatusCodes.Status502BadGateway
        };

    private static bool IsJsonObject(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return false;
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task AuditAsync(string entityName, string entityId, string action, string before, int httpStatus, string? outcome)
    {
        try
        {
            _db.AuditTracking.Add(new GeneralAuditTracking
            {
                EntityName = entityName,
                EntityId   = entityId,
                Action     = action,
                OldValues  = JsonSerializer.Serialize(new { Status = before }),
                NewValues  = JsonSerializer.Serialize(new { HttpStatus = httpStatus, Outcome = outcome }),
                ChangedBy  = string.IsNullOrWhiteSpace(_user.UserId) ? (_user.Email ?? "unknown") : _user.UserId,
                ChangedAt  = _clock.Now
            });
            await _db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Re-check de {EntityName} {EntityId} por {UserId}: no se pudo guardar la auditoría ({HttpStatus}, {Outcome}).",
                entityName, entityId, _user.UserId, httpStatus, outcome);
        }
    }

    /// <summary>data.outcome si fue bien, errorCode si no; null si el cuerpo no es el ApiResponse esperado.</summary>
    private static string? OutcomeOf(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            string? Prop(JsonElement e, string name) =>
                e.EnumerateObject().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                 .Value is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;

            var data = root.EnumerateObject()
                .FirstOrDefault(p => string.Equals(p.Name, "data", StringComparison.OrdinalIgnoreCase)).Value;
            var outcome = data.ValueKind == JsonValueKind.Object ? Prop(data, "outcome") : null;
            var result  = outcome ?? Prop(root, "errorCode");
            return result is { Length: > 64 } ? result[..64] : result;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
```

- [ ] **Step 4: `AdminEntraxSignupsController.Recheck` usa el reenvío**

Sustituir el cuerpo de `Recheck` (desde `var http = _httpFactory.CreateClient(…)` hasta su `return new ContentResult …`) por:

```csharp
        var forwarder = new SignupApiRecheckForwarder(_db, _httpFactory, _user, _clock, _logger);
        return await forwarder.ForwardAsync(
            Request, $"api/v1/signups/entrax/{Uri.EscapeDataString(id)}/recheck",
            nameof(EntraxPayment), id, before.Value.ToString(), AuditRecheck,
            "You do not have permission to re-check Entrax payments.", RecheckTimeout, ct);
```

Borrar los privados que quedan sin uso (`Unavailable`, `IsJsonObject`, `AuditAsync`, `OutcomeOf`) y hacer que las constantes públicas apunten a las del reenvío, para no romper a quien las usa:

```csharp
    public const string SignupApiUnavailable = SignupApiRecheckForwarder.SignupApiUnavailable;
    public const string ClientCancelled      = SignupApiRecheckForwarder.ClientCancelled;
```

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter AdminEntraxSignupsControllerTests -nologo`
Expected: PASS (sin tocar las pruebas: es la regresión del reenvío).

- [ ] **Step 5: Crear `AdminSignupCardPaymentsController.cs`**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Altas pagadas con tarjeta y 3DS (Plan I): las abiertas (autenticando, cobrando o cobradas sin activar), las
/// que hay que revisar (dinero que pudo moverse y no se aplicó: resultado desconocido, doble cobro) y las
/// recientes. Re-check reenvía a SignupAPI, que concilia o liquida por su camino único (SignupCardSettlement),
/// con el Bearer del administrador; los mismos cuatro roles que la cripto y Entrax, comprobados aquí y allí.
///
/// Lo que sale de aquí no sirve para cobrar: ni el token de la tarjeta, ni el de 3DS, ni las notas internas. Sí
/// el token de la compra en Spreedly, que es lo que soporte busca en la consola para devolver.
/// </summary>
[ApiController]
[Route("api/v1/admin/signup-card-payments")]
[Authorize(Roles = AppRoles.CryptoPaymentApprovers)]
public class AdminSignupCardPaymentsController : ControllerBase
{
    public const string ViewOpen   = "open";
    public const string ViewReview = "review";
    public const string ViewRecent = "recent";

    public const int DefaultPageSize = 50;
    public const int MaxPageSize     = 200;
    public const int RecentDays      = 30;

    public static readonly TimeSpan DefaultRecheckTimeout = TimeSpan.FromSeconds(45);

    public const string AuditRecheck         = "CardRecheck";
    public const string SignupApiUnavailable = SignupApiRecheckForwarder.SignupApiUnavailable;

    private readonly AppDbContext                                   _db;
    private readonly IHttpClientFactory                             _httpFactory;
    private readonly ICurrentUserService                            _user;
    private readonly IDateTimeProvider                              _clock;
    private readonly ILogger<AdminSignupCardPaymentsController>     _logger;

    public AdminSignupCardPaymentsController(
        AppDbContext db, IHttpClientFactory httpFactory, ICurrentUserService user, IDateTimeProvider clock,
        ILogger<AdminSignupCardPaymentsController> logger)
    {
        _db          = db;
        _httpFactory = httpFactory;
        _user        = user;
        _clock       = clock;
        _logger      = logger;
    }

    /// <summary>
    /// Tope del Re-check. Más largo que el de Entrax: una conciliación puede listar transacciones en Spreedly y
    /// activar el alta. Si no contesta, 502 y se puede repetir (es idempotente).
    /// </summary>
    public TimeSpan RecheckTimeout { get; init; } = DefaultRecheckTimeout;

    /// <summary>GET api/v1/admin/signup-card-payments?view=open|review|recent&amp;page=1&amp;pageSize=50</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? view = ViewOpen, [FromQuery] int page = 1, [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken ct = default)
    {
        pageSize = pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        page     = Math.Clamp(page, 1, int.MaxValue / pageSize);

        var recentFrom = _clock.Now.AddDays(-RecentDays);
        var payments   = _db.SignupCardPayments.AsNoTracking();
        payments = view switch
        {
            ViewReview => payments.Where(p => p.Status == SignupCardPaymentStatus.ToReview),
            ViewRecent => payments.Where(p => p.StartedAt >= recentFrom),
            _          => payments.Where(p => p.Status == SignupCardPaymentStatus.Authenticating
                                           || p.Status == SignupCardPaymentStatus.Charging
                                           || p.Status == SignupCardPaymentStatus.Charged)
        };

        var total = await payments.CountAsync(ct);

        // Uniones a la izquierda: un pago a revisar tiene dinero en juego y no puede desaparecer por un perfil o pedido que falte.
        var raw = await (
            from p in payments
            join m0 in _db.MemberProfiles.AsNoTracking() on p.MemberId equals m0.MemberId into ms
            from m in ms.DefaultIfEmpty()
            join o0 in _db.Orders.AsNoTracking() on p.OrderId equals o0.Id into os
            from o in os.DefaultIfEmpty()
            orderby p.StartedAt descending, p.Id
            select new
            {
                Payment  = p,
                OrderNo  = o != null ? o.OrderNo : null,
                FullName = m != null ? (m.FirstName + " " + m.LastName).Trim() : null,
                Email    = m != null ? m.Email : null,
                Country  = m != null ? m.Country : null
            }
        ).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var rows = raw.Select(x => new SignupCardPaymentRowDto(
            x.Payment.Id, x.Payment.OrderId, x.OrderNo, x.Payment.MemberId, x.FullName, x.Email, x.Country,
            x.Payment.Status.ToString(), x.Payment.AmountUsd, x.Payment.ChargeAmount, x.Payment.ChargeCurrency,
            x.Payment.CardBrand, x.Payment.Last4, x.Payment.Eci, x.Payment.TransStatus, x.Payment.LiabilityShift,
            x.Payment.FlowPerformed, x.Payment.ProcessorUsed, x.Payment.ExternalOrderId, x.Payment.PurchaseTransactionToken,
            x.Payment.FailureCode, x.Payment.StartedAt, x.Payment.ChargedAt, x.Payment.LastCheckedAt)).ToList();

        return Ok(ApiResponse<PagedResult<SignupCardPaymentRowDto>>.Ok(new PagedResult<SignupCardPaymentRowDto>
        {
            Items = rows, TotalCount = total, Page = page, PageSize = pageSize
        }));
    }

    /// <summary>POST api/v1/admin/signup-card-payments/{id}/recheck — reconsulta Spreedly y liquida o concilia.</summary>
    [HttpPost("{id}/recheck")]
    public async Task<IActionResult> Recheck(string id, CancellationToken ct = default)
    {
        var before = await _db.SignupCardPayments.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => (SignupCardPaymentStatus?)p.Status)
            .FirstOrDefaultAsync(ct);
        if (before is null)
            return NotFound(ApiResponse<object>.Fail("CARD_PAYMENT_NOT_FOUND", "Payment not found."));

        var forwarder = new SignupApiRecheckForwarder(_db, _httpFactory, _user, _clock, _logger);
        return await forwarder.ForwardAsync(
            Request, $"api/v1/signups/card/{Uri.EscapeDataString(id)}/recheck",
            nameof(SignupCardPayment), id, before.Value.ToString(), AuditRecheck,
            "You do not have permission to re-check card payments.", RecheckTimeout, ct);
    }

    public sealed record SignupCardPaymentRowDto(
        string    Id,
        string    OrderId,
        string?   OrderNo,
        string    MemberId,
        string?   FullName,
        string?   Email,
        string?   Country,
        string    Status,
        decimal   AmountUsd,
        decimal   ChargeAmount,
        string    ChargeCurrency,
        string    CardBrand,
        string    Last4,
        string?   Eci,
        string?   TransStatus,
        bool?     LiabilityShift,
        string?   FlowPerformed,
        string?   ProcessorUsed,
        string    ExternalOrderId,
        string?   PurchaseTransactionToken,
        string?   Reason,
        DateTime  StartedAt,
        DateTime? ChargedAt,
        DateTime? LastCheckedAt);
}
```

- [ ] **Step 6: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "AdminSignupCardPaymentsControllerTests|AdminEntraxSignupsControllerTests" -nologo`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add MLMConquerorGlobalEdition.AdminAPI/Services/SignupApiRecheckForwarder.cs \
        MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminEntraxSignupsController.cs \
        MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminSignupCardPaymentsController.cs \
        MLMConquerorGlobalEdition.AdminAPI.Tests/Features/CardSignups/AdminSignupCardPaymentsControllerTests.cs
git commit -m "Admin de pagos con tarjeta del alta: abiertos, a revisar y recientes sin tokens con los que se pueda cobrar, y Re-check reenviado a SignupAPI y auditado con el reenvio que ahora comparte Entrax"
```

---

### Task 26: El *gateway token* de Spreedly en las credenciales (AdminAPI)

**Files:**
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminBillingGatewayController.cs` (`UpsertCredential`, `GetCredential`, `ApiCredentialMetadataDto`, `UpsertCredentialRequest`)
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Features/Billing/SpreedlyGatewayTokenCredentialTests.cs`

Hoy la API de credenciales no deja guardar `SpreedlyGatewayTokenEncrypted`: sin él, ningún procesador puede cobrar por Spreedly.

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Billing;

/// <summary>El gateway token de Spreedly de cada procesador se guarda cifrado y solo se informa si está o no.</summary>
public class SpreedlyGatewayTokenCredentialTests
{
    private static AdminBillingGatewayController Build(MLMConquerorGlobalEdition.Repository.Context.AppDbContext db)
    {
        var crypto = new Mock<IEncryptionService>();
        crypto.Setup(c => c.Encrypt(It.IsAny<string>())).Returns<string>(s => "ENC:CfDJ8" + s.Length);
        return new AdminBillingGatewayController(db, Mock.Of<ICacheService>(), crypto.Object)
        {
            // UpsertCredential lee User.Identity: sin HttpContext sería una NullReferenceException.
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Fact]
    public async Task Guarda_el_gateway_token_cifrado_y_solo_dice_que_esta()
    {
        await using var db = InMemoryDbHelper.Create();
        var c = Build(db);

        await c.UpsertCredential("NmiSpreedly", new AdminBillingGatewayController.UpsertCredentialRequest(
            "Production", null, true, null, null, null, SpreedlyGatewayToken: "GWTOKEN-EN-CLARO"));

        var fila = await db.ApiCredentials.SingleAsync();
        fila.SpreedlyGatewayTokenEncrypted.Should().StartWith("ENC:").And.NotContain("GWTOKEN-EN-CLARO");

        var meta = ((ApiResponse<AdminBillingGatewayController.ApiCredentialMetadataDto>)
            ((OkObjectResult)await c.GetCredential("NmiSpreedly", "Production")).Value!).Data!;
        meta.HasSpreedlyGatewayToken.Should().BeTrue();
    }

    [Fact]
    public async Task Vacio_no_borra_el_que_habia()
    {
        await using var db = InMemoryDbHelper.Create();
        var c = Build(db);
        await c.UpsertCredential("NmiSpreedly", new("Production", null, true, null, null, null, SpreedlyGatewayToken: "GW1"));
        var antes = (await db.ApiCredentials.SingleAsync()).SpreedlyGatewayTokenEncrypted;

        await c.UpsertCredential("NmiSpreedly", new("Production", "https://x", true, null, null, null, SpreedlyGatewayToken: ""));

        (await db.ApiCredentials.SingleAsync()).SpreedlyGatewayTokenEncrypted.Should().Be(antes);
    }

    [Fact]
    public async Task Con_el_prefijo_ENC_a_mano_se_rechaza()
    {
        await using var db = InMemoryDbHelper.Create();
        var r = await Build(db).UpsertCredential("NmiSpreedly", new("Production", null, true, null, null, null, SpreedlyGatewayToken: "ENC:abc"));
        r.Should().BeOfType<BadRequestObjectResult>();
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter SpreedlyGatewayTokenCredentialTests -nologo`
Expected: FAIL de compilación (`SpreedlyGatewayToken`, `HasSpreedlyGatewayToken`).

- [ ] **Step 3: Ampliar los contratos y el upsert**

En `ApiCredentialMetadataDto`, añadir al final: `bool HasSpreedlyGatewayToken = false`.

En `UpsertCredentialRequest`, añadir al final: `string? SpreedlyGatewayToken = null`.

En `GetCredential`, en el `new ApiCredentialMetadataDto(…)`, añadir como último argumento:

```csharp
            entity.AdditionalSecretEncrypted is not null,
            entity.SpreedlyGatewayTokenEncrypted is not null)));
```

En `UpsertCredential`:
- en el array de la comprobación del prefijo, añadir `(nameof(request.SpreedlyGatewayToken), request.SpreedlyGatewayToken),`;
- al crear la fila, debajo de `entity.AdditionalSecretEncrypted = Protect(request.AdditionalSecret);`: `entity.SpreedlyGatewayTokenEncrypted = Protect(request.SpreedlyGatewayToken);`;
- al actualizarla, debajo de la línea de `AdditionalSecret`: `if (Protect(request.SpreedlyGatewayToken) is { } gatewayToken) entity.SpreedlyGatewayTokenEncrypted = gatewayToken;`.

- [ ] **Step 4: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter SpreedlyGatewayTokenCredentialTests -nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminBillingGatewayController.cs \
        MLMConquerorGlobalEdition.AdminAPI.Tests/Features/Billing/SpreedlyGatewayTokenCredentialTests.cs
git commit -m "Credenciales: el gateway token de Spreedly de cada procesador se guarda cifrado por el servidor y la metadata solo dice si esta"
```

---

### Task 27: Pantallas de admin: pagos con tarjeta del alta, menú y credenciales de Spreedly

**Files:**
- Create: `MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminSignupCardPayments.razor`
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Components/Layout/AdminSidebarMenu.razor` (debajo de *Entrax Signups*)
- Modify: `MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminBillingCredentials.razor`
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Features/CardSignups/PantallaAdminSignupCardPaymentsTests.cs`

- [ ] **Step 1: Escribir la prueba que falla**

```csharp
using System.Text.RegularExpressions;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.CardSignups;

/// <summary>
/// AdminWeb no referencia AdminAPI: la pantalla duplica el contrato por la forma del JSON. Aquí se lee su
/// marcado: rutas y vistas del controlador, la fila con los mismos campos en el mismo orden, los cuatro roles en
/// la página y en el menú; y en credenciales, la fila "Spreedly" y el campo del gateway token.
/// </summary>
public class PantallaAdminSignupCardPaymentsTests
{
    private const string Ruta = "/admin/billing/card-signups";

    private static string Fichero(params string[] partes)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MLMConquerorGlobalEdition.slnx"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine([dir!.FullName, .. partes]));
    }

    private static string Pagina() => Fichero("MLMConquerorGlobalEdition.AdminWeb", "Components", "Pages", "AdminSignupCardPayments.razor");
    private static string Menu() => Fichero("MLMConquerorGlobalEdition.SharedComponents", "Components", "Layout", "AdminSidebarMenu.razor");
    private static string Credenciales() => Fichero("MLMConquerorGlobalEdition.AdminWeb", "Components", "Pages", "AdminBillingCredentials.razor");

    [Fact]
    public void La_pagina_exige_los_cuatro_roles_y_llama_a_las_rutas_del_controlador()
    {
        var p = Pagina();
        p.Should().Contain($"@page \"{Ruta}\"");
        p.Should().Contain("[Microsoft.AspNetCore.Authorization.Authorize(Roles = AppRoles.CryptoPaymentApprovers)]");
        p.Should().Contain("api/v1/admin/signup-card-payments?view=");
        p.Should().Contain("api/v1/admin/signup-card-payments/{Uri.EscapeDataString(row.Id)}/recheck");
        foreach (var v in new[] { AdminSignupCardPaymentsController.ViewOpen, AdminSignupCardPaymentsController.ViewReview, AdminSignupCardPaymentsController.ViewRecent })
            p.Should().Contain($"\"{v}\"");
    }

    [Fact]
    public void La_fila_de_la_pagina_tiene_los_campos_del_controlador_en_el_mismo_orden()
    {
        var m = Regex.Match(Pagina(), @"private record SignupCardPaymentRowDto\((?<campos>[^)]*)\)", RegexOptions.Singleline);
        m.Success.Should().BeTrue();
        var deLaPagina = m.Groups["campos"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(c => c.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1]);
        var delControlador = typeof(AdminSignupCardPaymentsController.SignupCardPaymentRowDto)
            .GetConstructors().Single().GetParameters().Select(x => x.Name);
        deLaPagina.Should().Equal(delControlador);
    }

    [Fact]
    public void El_menu_la_enseña_a_los_mismos_roles_que_Entrax()
    {
        var menu = Menu();
        var entrax = menu.IndexOf("/admin/billing/entrax-signups", StringComparison.Ordinal);
        var tarjeta = menu.IndexOf(Ruta, StringComparison.Ordinal);
        tarjeta.Should().BeGreaterThan(entrax, "va en el mismo bloque, debajo de Entrax Signups");
        menu[entrax..tarjeta].Should().NotContain("</AuthorizeView>", "dentro del mismo AuthorizeView de los cuatro roles");
    }

    [Fact]
    public void Credenciales_ofrece_Spreedly_y_el_gateway_token()
    {
        var c = Credenciales();
        c.Should().Contain("\"Spreedly\"");
        c.Should().Contain("SpreedlyGatewayToken");
        c.Should().Contain("HasSpreedlyGatewayToken");
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter PantallaAdminSignupCardPaymentsTests -nologo`
Expected: FAIL (la página no existe).

- [ ] **Step 3: Crear `AdminSignupCardPayments.razor`**

```razor
@page "/admin/billing/card-signups"
@rendermode @(new InteractiveServerRenderMode(prerender: false))
@attribute [Microsoft.AspNetCore.Authorization.Authorize(Roles = AppRoles.CryptoPaymentApprovers)]
@inject HttpClient Http

@*
    Altas pagadas con tarjeta y 3DS (Plan I). Re-check vuelve a preguntar a Spreedly: cierra autenticaciones
    abandonadas, concilia un cobro dudoso y activa un cobrado que no se activó (lo hace SignupAPI, por el
    camino único). "To review" tiene dinero en juego: se busca la compra en la consola de Spreedly por su token
    y se aplica o se devuelve a mano.

    El atributo solo esconde la pantalla: AdminSignupCardPaymentsController y SignupAPI comprueban los mismos
    cuatro roles. Cada Re-check queda en AuditTracking. Las fechas son hora de servidor.
*@

<PageTitle>Card Signups — Admin</PageTitle>

<BreadcrumbNav Items="@(new List<BreadcrumbNav.BreadcrumbItem> {
    new("Admin Dashboard", "/admin"), new("Billing"), new("Card Signups") })" />

@if (_alertMsg is not null)
{
    <div class="alert @(_alertIsError ? "alert-danger" : "alert-success") alert-dismissible mb-3">
        @_alertMsg <button type="button" class="btn-close" @onclick="() => _alertMsg = null"></button>
    </div>
}

<div class="card border-0 shadow-sm">
    <div class="card-header py-3 d-flex justify-content-between align-items-center flex-wrap gap-2">
        <div>
            <h5 class="mb-0 fw-bold">Card signups — 3-D Secure</h5>
            <small class="text-muted">Charged signups activate automatically. "To review" means money may have been taken but was not applied.</small>
        </div>
        <div class="d-flex gap-2">
            <div class="btn-group btn-group-sm">
                <button class="btn @(_view == "open" ? "btn-primary" : "btn-outline-primary")" @onclick='() => ChangeView("open")'>In progress</button>
                <button class="btn @(_view == "review" ? "btn-primary" : "btn-outline-primary")" @onclick='() => ChangeView("review")'>To review</button>
                <button class="btn @(_view == "recent" ? "btn-primary" : "btn-outline-primary")" @onclick='() => ChangeView("recent")'>Recent (30 days)</button>
            </div>
            <button class="btn btn-sm btn-outline-secondary" @onclick="LoadAsync" disabled="@_loading"><i class="ri-refresh-line"></i> Refresh</button>
        </div>
    </div>
    <div class="card-body p-0">
        @if (_loading)
        {
            <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
        }
        else if (_rows.Count == 0)
        {
            <div class="text-center py-5 text-muted">
                <i class="ri-bank-card-line" style="font-size:2.5rem;opacity:.4"></i>
                <p class="mt-2 mb-0">Nothing here.</p>
            </div>
        }
        else
        {
            <SfGrid TValue="SignupCardPaymentRowDto" DataSource="@_rows" AllowResizing="true" GridLines="GridLine.Horizontal">
                <GridColumns>
                    <GridColumn HeaderText="Started" Width="140">
                        <Template><span class="small text-muted">@(((SignupCardPaymentRowDto)context).StartedAt.ToString("yyyy-MM-dd HH:mm"))</span></Template>
                    </GridColumn>
                    <GridColumn Field="@nameof(SignupCardPaymentRowDto.MemberId)" HeaderText="Member ID" Width="120" />
                    <GridColumn HeaderText="Order" Width="120"><Template>@(((SignupCardPaymentRowDto)context).OrderNo ?? "—")</Template></GridColumn>
                    <GridColumn HeaderText="Name" Width="170"><Template>@(((SignupCardPaymentRowDto)context).FullName ?? "(profile not found)")</Template></GridColumn>
                    <GridColumn HeaderText="Email" Width="200"><Template>@(((SignupCardPaymentRowDto)context).Email ?? "—")</Template></GridColumn>
                    <GridColumn HeaderText="Status" Width="130" TextAlign="TextAlign.Center">
                        <Template>@{ var s = ((SignupCardPaymentRowDto)context).Status; }<span class="badge @StatusBadge(s)">@s</span></Template>
                    </GridColumn>
                    <GridColumn HeaderText="Amount" Width="120" TextAlign="TextAlign.Right">
                        <Template>@{ var r = (SignupCardPaymentRowDto)context; }@r.ChargeAmount.ToString("N2") @r.ChargeCurrency</Template>
                    </GridColumn>
                    <GridColumn HeaderText="Card" Width="120"><Template>@{ var r = (SignupCardPaymentRowDto)context; }@r.CardBrand ••@r.Last4</Template></GridColumn>
                    <GridColumn HeaderText="3DS" Width="140">
                        <Template>@{ var r = (SignupCardPaymentRowDto)context; }<span class="small">ECI @(r.Eci ?? "—") · @(r.TransStatus ?? "—") · @(r.LiabilityShift == true ? "shift" : "no shift")</span></Template>
                    </GridColumn>
                    <GridColumn HeaderText="Processor" Width="120"><Template>@(((SignupCardPaymentRowDto)context).ProcessorUsed ?? "—")</Template></GridColumn>
                    <GridColumn HeaderText="Reason" Width="190"><Template><code class="small">@(((SignupCardPaymentRowDto)context).Reason ?? "—")</code></Template></GridColumn>
                    <GridColumn HeaderText="Spreedly order / purchase" Width="260">
                        <Template>@{ var r = (SignupCardPaymentRowDto)context; }<code class="small d-block">@r.ExternalOrderId</code><code class="small d-block text-muted">@(r.PurchaseTransactionToken ?? "—")</code></Template>
                    </GridColumn>
                    <GridColumn HeaderText="Last check" Width="140">
                        <Template><span class="small text-muted">@(((SignupCardPaymentRowDto)context).LastCheckedAt?.ToString("yyyy-MM-dd HH:mm") ?? "—")</span></Template>
                    </GridColumn>
                    <GridColumn HeaderText="Actions" Width="120" TextAlign="TextAlign.Center">
                        <Template>
                            @{ var row = (SignupCardPaymentRowDto)context; }
                            <button class="btn btn-sm btn-outline-primary py-0" disabled="@(_busyId is not null)" @onclick="() => RecheckAsync(row)" title="Ask Spreedly again and settle">
                                @if (_busyId == row.Id) { <span class="spinner-border spinner-border-sm me-1"></span> }
                                Re-check
                            </button>
                        </Template>
                    </GridColumn>
                </GridColumns>
            </SfGrid>

            @if (_totalPages > 1)
            {
                <div class="d-flex justify-content-between align-items-center px-3 py-2 border-top bg-light">
                    <small class="text-muted">@_totalCount payments — Page @_page of @_totalPages</small>
                    <nav>
                        <ul class="pagination pagination-sm mb-0">
                            <li class="page-item @(_page == 1 ? "disabled" : "")"><button class="page-link" @onclick="() => ChangePage(_page - 1)">‹</button></li>
                            <li class="page-item @(_page == _totalPages ? "disabled" : "")"><button class="page-link" @onclick="() => ChangePage(_page + 1)">›</button></li>
                        </ul>
                    </nav>
                </div>
            }
        }
    </div>
    <div class="card-footer bg-light small text-muted">
        Re-check asks Spreedly for the real status; it never marks anything as paid on its own. Every Re-check is audited.
        Refunds of "To review" payments are done manually in the Spreedly console (search by the purchase token).
    </div>
</div>

@code {
    private const int PageSize = 50;

    private List<SignupCardPaymentRowDto> _rows = new();
    private string  _view = "open";
    private int     _page = 1;
    private int     _totalCount;
    private int     _totalPages = 1;
    private bool    _loading = true;
    private string? _busyId;
    private string? _alertMsg;
    private bool    _alertIsError;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task ChangeView(string view) { _view = view; _page = 1; await LoadAsync(); }

    private async Task ChangePage(int p)
    {
        if (p < 1 || p > _totalPages) return;
        _page = p;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        StateHasChanged();
        try
        {
            var r = await Http.GetFromJsonAsync<ApiResp<Paged<SignupCardPaymentRowDto>>>(
                $"api/v1/admin/signup-card-payments?view={_view}&page={_page}&pageSize={PageSize}");
            _rows       = r?.Data?.Items.ToList() ?? new();
            _totalCount = r?.Data?.TotalCount ?? 0;
            _totalPages = Math.Max(1, r?.Data?.TotalPages ?? 1);
        }
        catch (Exception ex)
        {
            _rows = new(); _totalCount = 0; _totalPages = 1;
            _alertMsg = $"Error: {ex.Message}"; _alertIsError = true;
        }
        finally { _loading = false; }
    }

    private async Task RecheckAsync(SignupCardPaymentRowDto row)
    {
        if (_busyId is not null) return;
        _busyId = row.Id;
        try
        {
            using var resp = await Http.PostAsync($"api/v1/admin/signup-card-payments/{Uri.EscapeDataString(row.Id)}/recheck", null);
            ApiResp<RecheckResult>? payload = null;
            try { payload = await resp.Content.ReadFromJsonAsync<ApiResp<RecheckResult>>(); } catch { /* 403 sin cuerpo */ }

            if (resp.IsSuccessStatusCode && payload?.Data is not null)
            {
                _alertMsg     = $"{row.MemberId}: {OutcomeText(payload.Data.Outcome)}";
                _alertIsError = payload.Data.Outcome is "ToReview" or "ProviderUnavailable" or "Failed" or "Charging";
                await LoadAsync();
            }
            else if (resp.StatusCode is System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.Unauthorized)
            {
                _alertMsg = "You do not have permission to re-check card payments."; _alertIsError = true;
            }
            else
            {
                _alertMsg = payload?.ErrorCode switch
                {
                    "CARD_PAYMENT_NOT_FOUND" => "This payment no longer exists. Refresh the list.",
                    "SIGNUP_API_UNAVAILABLE" => "The signup service did not respond. Try again in a moment.",
                    _ => payload?.Errors?.FirstOrDefault() ?? payload?.Message ?? $"Re-check failed ({(int)resp.StatusCode})."
                };
                _alertIsError = true;
            }
        }
        catch (Exception ex) { _alertMsg = $"Error: {ex.Message}"; _alertIsError = true; }
        finally { _busyId = null; }
    }

    private static string OutcomeText(string outcome) => outcome switch
    {
        "Paid"                => "charged and activated.",
        "AlreadyResolved"     => "already settled; nothing changed.",
        "StillAuthenticating" => "the cardholder is still verifying with the bank.",
        "Charging"            => "the charge outcome is still unknown; it will be reconciled.",
        "Failed"              => "no money was taken. The order is open again.",
        "ToReview"            => "money may have been taken but it was not applied. Check the purchase in Spreedly.",
        "ProviderUnavailable" => "Spreedly did not answer. Try again later.",
        _                     => outcome
    };

    private static string StatusBadge(string status) => status switch
    {
        "Paid"           => "bg-success",
        "ToReview"       => "bg-danger",
        "Charging"       => "bg-warning text-dark",
        "Charged"        => "bg-info text-dark",
        "Authenticating" => "bg-secondary",
        _                => "bg-light text-dark border"
    };

    // AdminWeb no referencia AdminAPI: el contrato se duplica por forma del JSON; PantallaAdminSignupCardPaymentsTests lo vigila.
    private record ApiResp<T>(bool Success, T? Data, string? Message, string? ErrorCode, IEnumerable<string>? Errors);
    private record Paged<T>(IEnumerable<T> Items, int TotalCount, int Page, int PageSize, int TotalPages);
    private record RecheckResult(string Outcome);

    private record SignupCardPaymentRowDto(
        string Id, string OrderId, string? OrderNo, string MemberId, string? FullName, string? Email, string? Country,
        string Status, decimal AmountUsd, decimal ChargeAmount, string ChargeCurrency, string CardBrand, string Last4,
        string? Eci, string? TransStatus, bool? LiabilityShift, string? FlowPerformed, string? ProcessorUsed,
        string ExternalOrderId, string? PurchaseTransactionToken, string? Reason, DateTime StartedAt, DateTime? ChargedAt,
        DateTime? LastCheckedAt);
}
```

- [ ] **Step 4: Entrada del menú**

En `AdminSidebarMenu.razor`, justo debajo del `<li>` de *Entrax Signups* y dentro del mismo `<Authorized>`:

```razor
                @* Altas pagadas con tarjeta y 3DS (Plan I): mismos cuatro roles que la cripto y Entrax. *@
                <li class="menu-item @(IsActive("/admin/billing/card-signups") == "active" ? "active" : "")">
                    <a href="/admin/billing/card-signups"
                       class="menu-link @IsActive("/admin/billing/card-signups")">
                        <i class="ri-bank-card-line menu-icon"></i>
                        <span class="title">Card Signups</span>
                    </a>
                </li>
```

- [ ] **Step 5: `AdminBillingCredentials.razor`: la fila "Spreedly" y el gateway token**

1. En `ServiceKeys`, añadir `"Spreedly"` al principio de la lista (antes de `"NmiSpreedly"`), con un comentario: `// Spreedly (Plan I): environment key, access secret, SCA provider key y signing secret del entorno.`
2. En `SecretLabels()`, añadir antes del caso por defecto:

```csharp
                    : _selectedKey.Equals("Spreedly", StringComparison.OrdinalIgnoreCase)
                        ? ("Environment key", "Access secret", "SCA provider key", "Signing secret")
```

(el operador `?:` se encadena igual que los de `EWallet` y `Volet`).
3. En `CredentialFormModel`, añadir `public string? SpreedlyGatewayToken { get; set; }`; en `CredentialMetadataDto`, añadir al final `bool HasSpreedlyGatewayToken`; en el `payload` de `Save()`, `SpreedlyGatewayToken = Trimmed(_form.SpreedlyGatewayToken),`; y al limpiar tras guardar, `_form.SpreedlyGatewayToken = string.Empty;`.
4. Debajo del bloque de `@labels.Additional`, el campo solo para los procesadores de tarjeta:

```razor
                        @if (IsCardProcessor(_selectedKey))
                        {
                            <div class="col-md-4">
                                <label class="form-label fw-semibold">Spreedly gateway token</label>
                                <input type="password" class="form-control" @bind="_form.SpreedlyGatewayToken"
                                       placeholder="@(_metadata?.HasSpreedlyGatewayToken == true ? "•••• (unchanged)" : "not set")" autocomplete="new-password" />
                                <div class="form-text">The gateway_token Spreedly assigned to this processor in the same environment.</div>
                            </div>
                        }
```

con, en `@code`:

```csharp
    // Los procesadores de tarjeta que el router puede elegir: cada uno cobra por Spreedly con su gateway token.
    private static bool IsCardProcessor(string key) => Enum.TryParse<MLMConquerorGlobalEdition.Domain.Enums.CardProcessor>(key, out _);
```

- [ ] **Step 6: Ejecutar y ver que pasa**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "PantallaAdminSignupCardPaymentsTests|PantallaAdminEntraxSignupsTests" -nologo`
Expected: PASS.

Run: `dotnet build MLMConquerorGlobalEdition.AdminWeb -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 7: Commit**

```bash
git add MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminSignupCardPayments.razor \
        MLMConquerorGlobalEdition.SharedComponents/Components/Layout/AdminSidebarMenu.razor \
        MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminBillingCredentials.razor \
        MLMConquerorGlobalEdition.AdminAPI.Tests/Features/CardSignups/PantallaAdminSignupCardPaymentsTests.cs
git commit -m "Admin: pantalla Card Signups con Re-check, entrada de menu para los roles de cobro, y la credencial Spreedly con sus cuatro secretos y el gateway token de cada procesador"
```

---

## Fase 5 — Documentación y prueba de punta a punta

### Task 28: §11 de `pruebas-de-seguridad.md` y condiciones de despliegue

**Files:**
- Modify: `docs/deployment/pruebas-de-seguridad.md` (sección nueva al final)

- [ ] **Step 1: Añadir la sección 11 al final del archivo**

Antes de pegarla, contrastar cada fila con el código que quedó (nombres de rutas, códigos y tiempos), como se hizo con la §10 del Plan H.

````markdown

## 11. Tarjeta con 3D Secure en el alta (Plan I, 3DS2 Global de Spreedly)

Qué protege: el alta pagada con tarjeta. El número y el CVV se teclean en el iFrame de Spreedly
(`core.spreedly.com`) y no pasan por nuestros servidores. El servidor pregunta a Spreedly qué tarjeta es, fija
el plan del router, autentica con 3DS2 Global por el importe del pedido y **solo cobra con liability shift**.
**El alta solo la activa el servidor, y solo después de cobrar**, por un único camino (`SignupCardSettlement`)
al que llegan la pantalla, el callback de Spreedly, el barrido `signup-card-sweep` y el *Re-check* del admin.
Ese camino **siempre** vuelve a consultar la autenticación a Spreedly (`GET /v1/transactions/{token}`): lo
que diga el navegador o el callback no decide nada. Un cobro de resultado desconocido nunca se da por no
cobrado: se concilia o va a *To review*. Plan: `docs/superpowers/plans/2026-10-16-plan-i-3ds-con-spreedly.md`.

Endpoints (SignupAPI, `https://localhost:7005` en Development, salvo los de AdminAPI):

| Endpoint | Acceso | Qué hace |
|---|---|---|
| `GET api/v1/signups/payment-options` | anónimo | `cardEnabled`, `cardEnvironmentKey` (pública) y `cardSimulated` |
| `POST api/v1/signups/{signupId}/complete` con `"paymentMethod": "CreditCard"` y `card` | anónimo | pregunta a Spreedly la tarjeta, fija el plan, reserva el pedido (`Processing`) y autentica |
| `POST api/v1/signups/{signupId}/card/{paymentRef}/complete` | anónimo, 10/min por IP | "trigger-completion" del Lifecycle: completa la autenticación con el token de la fila |
| `POST api/v1/signups/{signupId}/card/{paymentRef}/finish` | anónimo, 10/min por IP | el Lifecycle terminó: liquida YA re-consultando a Spreedly |
| `GET api/v1/signups/{signupId}/card/{paymentRef}/status` | anónimo, 30/min por IP | estado; si hace ≥ 10 s de la última consulta, liquida |
| `POST api/v1/signups/{signupId}/card/{paymentRef}/cancel` | anónimo, 10/min por IP | cierra un intento que SOLO se estaba autenticando |
| `POST api/v1/signups/spreedly/callback` | anónimo, solo actúa con firma válida, 64 KB | el aviso de Spreedly |
| `POST api/v1/signups/card/{paymentRef}/recheck` | `SuperAdmin`, `Admin`, `BillingManager`, `SupportManager` | re-consulta y liquida o concilia |
| `GET api/v1/admin/signup-card-payments?view=open\|review\|recent` (AdminAPI) | los mismos cuatro roles | la lista del admin |
| `POST api/v1/admin/signup-card-payments/{id}/recheck` (AdminAPI) | los mismos cuatro roles | reenvía a SignupAPI con el Bearer y audita |

### Condiciones de despliegue

- **Credenciales en `ApiCredential`, nunca en `appsettings`.** Fila `Spreedly` del entorno
  `Spreedly:CredentialEnvironment` (`Production`): *environment key* (API Key), *access secret* (Secret Key),
  *SCA provider key* (Merchant ID) y *signing secret* (Additional). Cada procesador que el router pueda elegir
  (`NmiSpreedly`, `CheckoutEUR`, …) con su *Spreedly gateway token* **del mismo entorno**. Se cargan en
  *Admin → Billing → API Credentials*. Sin ellas, `payment-options` esconde la tarjeta y el alta responde
  `503 CARD_NOT_CONFIGURED`.
- **SignupAPI con el certificado del key ring de credenciales** (`DataProtection:Certificate:*`), el MISMO
  que AdminAPI y Billing. Sin él SignupAPI no arranca (con `Spreedly:UseFake` no hace falta). Los enlaces de
  correo de confirmación y recuperación emitidos antes del despliegue dejan de valer (viven 15 min).
- **`Spreedly:Enabled` solo en SignupAPI.** En Billing se queda en `false` hasta el plan de recurrente MIT: con
  Spreedly encendido un cobro sin 3DS falla cerrado (`SPREEDLY_SCA_REQUIRED`) y las renovaciones con tarjeta
  fallarían. Apagado, Billing sigue con el stub (que no cobra): es un problema conocido y anterior a este plan.
- **`Spreedly:UseFake` nunca fuera de Development** (si está puesto, no arranca).
- **`Signup:CardEnabled = true` fuera de Development exige `Spreedly:Enabled = true`** (si no, no arranca).
- **Apagar la tarjeta es `Signup:CardEnabled = false`, no quitar la credencial**: el barrido sigue cerrando los
  cobros en vuelo con ella. Con Spreedly apagado y cobros abiertos, el log lleva un `Critical`.
- **El barrido vivo.** En `/hangfire` de SignupAPI debe existir `signup-card-sweep` (cada 2 minutos, cola `signups`).
- **Regla de ruteo para `Payment`** (*Admin → Billing → Routing Rules*): sin una regla activa que cubra el país
  y la marca, el alta responde `503 CARD_NO_GATEWAY`.
- **CSP.** Si algún día el host `Signups` emite CSP, tiene que permitir `script-src https://core.spreedly.com`,
  `frame-src https://core.spreedly.com https:` (el challenge abre iframes del ACS de cada banco) y
  `connect-src https://core.spreedly.com`. Hoy no emite CSP; `BizCenterWeb` sí y la ajusta el Plan L.
- **Prueba real con Spreedly antes de encender la tarjeta en producción** (no hay sandbox), con importe mínimo
  y aprobación de Nick, en este orden:
  1. el iFrame carga con la *environment key* y tokeniza (el número no aparece en ninguna petición a nuestros hosts:
     comprobarlo en las herramientas del navegador);
  2. una tarjeta **sin fricción** y otra **con challenge**: los campos `sca_authentication` (`eci`,
     `trans_status`, `token`, `authentication_value`) llegan con esos nombres o con los alias que lee
     `SpreedlyClient` (R1); si no, ajustar el lector **antes** de seguir;
  3. el cobro con `sca_authentication_token` en el gateway del router, `PaymentHistory` capturado y el alta activa;
  4. el **callback**: su forma (XML o JSON) y su firma (`signed`, HMAC-SHA1) validan (R2); si llega con 401,
     revisar con Spreedly qué firman — ninguna alta se pierde mientras tanto;
  5. `GET /v1/payment_methods/{token}/transactions.json` lista la compra con nuestro `order_id` (R3).
- **Revisar el log de SignupAPI por `Critical` de tarjeta** tras el despliegue: cada uno es un cobro que quedó
  *To review* (resultado desconocido, doble cobro, cobro simulado). Se resuelven en *Admin → Billing → Card
  Signups → To review*, buscando la compra por su token en la consola de Spreedly.

### Pruebas

Preparación (Development, con el simulador): arrancar SignupAPI con `Signup__CardEnabled=true`,
`Spreedly__Enabled=true` y `Spreedly__UseFake=true`. Los tokens del simulador son `fake_{escenario}_{guid}`
con `frictionless`, `challenge`, `failed`, `noshift`, `declined` y `unknown`.

#### 11.1 El número de la tarjeta

| # | Qué se prueba | Cómo | Resultado esperado | ✔ |
|---|---|---|---|---|
| 11.1 | El PAN no pasa por nosotros | con credenciales reales: pagar y revisar en las herramientas del navegador todas las peticiones a `localhost:7005`/`7147` | ninguna lleva el número ni el CVV; solo `paymentMethodToken` y `browserInfo` | ☐ |
| 11.2 | Sin campos propios | revisar el HTML de `/ambassador-join/…` y `/join/…` con la pestaña de tarjeta | no hay `input` de número ni de CVV: dos `div` donde Spreedly pone sus iframes | ☐ |
| 11.3 | El cliente no describe la tarjeta | `complete` con `"card": {"paymentMethodToken": "fake_frictionless_x", "browserInfo": "x"}` y añadir `"last4"`, `"cardBrand"`, `"amount"` | se ignoran: la fila `SignupCardPayments` tiene lo que dice Spreedly y `ChargeAmount` = `Orders.TotalAmount` | ☐ |
| 11.4 | Token de otro entorno | `complete` con un `paymentMethodToken` inventado | `400 CARD_INVALID`, sin fila ni cambios en el pedido | ☐ |

#### 11.2 3D Secure

| # | Qué se prueba | Cómo | Resultado esperado | ✔ |
|---|---|---|---|---|
| 11.5 | Sin fricción | escenario `frictionless` | `Paid`, ECI `05`, `TransStatus` `Y`, `LiabilityShift` 1, alta activa, `PaymentHistory` capturado | ☐ |
| 11.6 | Con challenge | escenario `challenge`, aprobar en la pantalla | la pantalla enseña el challenge; al aprobar, `Paid`; `FlowPerformed` = `challenge` | ☐ |
| 11.7 | Challenge rechazado | escenario `challenge`, rechazar | `Failed` `AUTH_FAILED`, sin `PaymentHistory`, pedido en `Pending` | ☐ |
| 11.8 | **Sin liability shift no se cobra** | escenario `noshift` (ECI 07) | `Failed` `NO_LIABILITY_SHIFT`, el orquestador no se llamó (sin `GatewayChargeAttempts` del pedido) | ☐ |
| 11.9 | 3DS fallido | escenario `failed` | `Failed`, sin cobro | ☐ |
| 11.10 | El navegador no decide | con un challenge pendiente, `POST …/finish` | sigue `Authenticating`: la liquidación preguntó a Spreedly y aún está pendiente | ☐ |
| 11.11 | Sin 3DS no hay cobro en el gateway | revisar `SpreedlyCardGatewayService` y su prueba `Sin_token_de_3ds_no_cobra…` | `SPREEDLY_SCA_REQUIRED` antes de llamar | ☐ |

#### 11.3 El cobro y la activación

| # | Qué se prueba | Cómo | Resultado esperado | ✔ |
|---|---|---|---|---|
| 11.12 | Primero se cobra | escenario `frictionless`; en SQL, orden de `SignupCardPayments.ChargedAt` y `ActivatedAt` | `ChargedAt` ≤ `ActivatedAt`; un intento `Charged` sin activar lo activa el barrido | ☐ |
| 11.13 | Rechazo del emisor | escenario `declined` | `Failed` `DECLINED:SPREEDLY_DECLINED`, pedido en `Pending`, miembro `Pending` | ☐ |
| 11.14 | **Resultado desconocido no se da por no cobrado** | escenario `unknown` | queda `Charging`, pedido en `Processing`; `POST /api/v1/dev/spreedly/{purchaseToken}/approve` + `POST /api/v1/dev/spreedly/sweep` → `Paid` | ☐ |
| 11.15 | Desconocido rechazado | como 11.14 pero `…/reject` y barrido | `Failed` `CHARGE_DECLINED`, pedido en `Pending` | ☐ |
| 11.16 | Desconocido sin noticias | como 11.14, sin resolver, y barrido pasados 30 min (o `Spreedly__ChargeUnknownGraceMinutes=10` y esperar) | `ToReview` `CHARGE_OUTCOME_UNKNOWN`, `Critical` en el log, visible en *To review* | ☐ |
| 11.17 | Un cobro, aunque lleguen varios | 4 `finish` simultáneos del mismo intento + barrido (contra SQL Server) | un solo `PaymentHistory`, un solo `GatewayChargeAttempts` `Success`, un solo bono por `SourceOrderId` | ☐ |
| 11.18 | Sin fallback tras un fallo dudoso | prueba `ExecuteAsync_TrasUnFalloDudoso_NoPruebaElSiguienteGateway` | el segundo gateway no se llama | ☐ |
| 11.19 | Doble cobro | activar el alta con cripto y luego liquidar un intento `Charged` del mismo pedido | `ToReview` `ORDER_ALREADY_SETTLED`, `Critical` | ☐ |
| 11.20 | La tarjeta guardada | tras 11.5, `MemberCreditCards` del miembro | `SpreedlyPaymentMethodToken` puesto, `EncryptedPan` vacío, `EncryptedCvv` NULL, `Gateway` `spreedly` | ☐ |
| 11.21 | Sin sesión desde un anónimo | la respuesta de `complete`/`finish` con tarjeta | sin `accessToken` ni `refreshToken`; la pantalla manda a iniciar sesión | ☐ |

#### 11.4 Abandono, otros métodos y endpoints

| # | Qué se prueba | Cómo | Resultado esperado | ✔ |
|---|---|---|---|---|
| 11.22 | Challenge abandonado | escenario `challenge`, cerrar la pestaña, barrido pasados 20 min | `Failed` `AUTH_ABANDONED`, pedido en `Pending` | ☐ |
| 11.23 | Cancelar | escenario `challenge` y *Cancel and use another card* | `Failed` `CANCELLED`, pedido en `Pending`; `cancel` sobre un intento `Charging` no hace nada | ☐ |
| 11.24 | Otro método con la tarjeta en curso | con un challenge pendiente, `complete` con token o cripto | `409 CARD_IN_PROGRESS` | ☐ |
| 11.25 | Endpoints de la pantalla | `…/status`, `…/finish`, `…/complete`, `…/cancel` con un `paymentRef` de otra alta o un `signupId` inventado | `404 CARD_PAYMENT_NOT_FOUND` sin más datos | ☐ |
| 11.26 | Rate limit | 11 `finish` en un minuto desde la misma IP | el 11.º `429` | ☐ |
| 11.27 | Tarjeta apagada | `Signup__CardEnabled=false` | `payment-options` sin tarjeta y `complete` con tarjeta `400 CARD_PAYMENTS_DISABLED` sin tocar nada | ☐ |

#### 11.5 Callback

| # | Qué se prueba | Cómo | Resultado esperado | ✔ |
|---|---|---|---|---|
| 11.28 | Sin firma | `POST api/v1/signups/spreedly/callback` con una transacción sin `signed` | `401`, sin cambios | ☐ |
| 11.29 | Firma de otro secreto o un valor cambiado | firmar con otro secreto, o cambiar `state` tras firmar | `401` | ☐ |
| 11.30 | Firmado | firmar con `fake-spreedly-signing-secret` (simulador) los campos `amount state token` de una autenticación pendiente | `200`; la liquidación re-consulta (no cambia nada si sigue pendiente) | ☐ |
| 11.31 | XXE | cuerpo XML con `<!DOCTYPE … SYSTEM "file:///…">` | `400 INVALID_CALLBACK`, sin leer ningún fichero | ☐ |
| 11.32 | Tamaño | cuerpo de 65 KB (con `Content-Length` y chunked) | `413`, sin fila en `ErrorLogs` | ☐ |

#### 11.6 Admin y configuración

| # | Qué se prueba | Cómo | Resultado esperado | ✔ |
|---|---|---|---|---|
| 11.33 | Roles | *Card Signups* y `POST …/recheck` con `SupportLevel1` | sin entrada de menú y `403` | ☐ |
| 11.34 | La lista no expone tokens de cobro | respuesta de `GET …/admin/signup-card-payments` | sin `paymentMethodToken`, `scaAuthenticationToken`, `scaTransactionToken` ni `notes` | ☐ |
| 11.35 | Re-check auditado | *Re-check* sobre un intento | fila en `AuditTracking` (`Action = CardRecheck`) con quién, estado anterior, HTTP y desenlace; el Bearer no se guarda | ☐ |
| 11.36 | Credenciales | guardar la fila `Spreedly` y el gateway token de `NmiSpreedly` | en la base solo `ENC:CfDJ8…`; la pantalla solo dice *set* / *not set* | ☐ |
| 11.37 | Simulador fuera de Development | arrancar SignupAPI en `Production` con `Spreedly__UseFake=true` | no arranca | ☐ |
| 11.38 | Tarjeta sin Spreedly fuera de Development | `Production` con `Signup__CardEnabled=true` y `Spreedly__Enabled=false` | no arranca | ☐ |
| 11.39 | Endpoints de desarrollo | `POST api/v1/dev/spreedly/sweep` en Development sin `UseFake` | `404` | ☐ |
| 11.40 | Secretos en el log | buscar el *access secret*, el *signing secret* y un `browser_info` en el log de SignupAPI y en `SpreedlyApiCalls` | no aparecen | ☐ |
| 11.41 | Credenciales por entorno | fila `Spreedly` en `Sandbox` y en `Production`, `Spreedly:CredentialEnvironment=Production` | se usan las de `Production` (comprobar con la *environment key* que llega en `payment-options`) | ☐ |

#### 11.7 Fallos del legado (MWR Life) que no se copiaron

| # | Fallo del legado | Cómo se prueba | Resultado esperado | ✔ |
|---|---|---|---|---|
| 11.42 | El PAN pasaba por el servidor | 11.1 y 11.2 | no pasa | ☐ |
| 11.43 | Importe, gateway y token los mandaba el cliente | 11.3; añadir `"gateway"`/`"gatewayToken"` al cuerpo | se ignoran: el plan es del router y el token de gateway sale de `ApiCredential` | ☐ |
| 11.44 | 3DS que el cliente podía saltarse (`NONTHREEDS`, `fullfr33dom`) | buscar esas cadenas en el código nuevo; mandar `browserInfo` vacío | no existen; sin `browserInfo` el validador rechaza; no hay camino de cobro sin `sca_authentication_token` | ☐ |
| 11.45 | Cobro omitido según el dominio del correo | revisar `SignupCardCheckout`/`Settlement` | nada depende del correo | ☐ |
| 11.46 | Rebaja a cobro sin 3DS | 11.8, 11.9 y 11.11 | nunca se cobra sin shift | ☐ |
| 11.47 | `pending` tratado como pagado | 11.10 | `pending` es `Authenticating` | ☐ |
| 11.48 | Endpoints sin auth que completaban cualquier token | `…/complete` con un `transactionToken` en el cuerpo o la query | se ignora: se completa el token **de la fila**, y solo con `signupId` + `paymentRef` | ☐ |
| 11.49 | `callback_url` a una página de usuario sin firma | 11.28-11.30 | el callback es de la API, firmado, y no decide nada | ☐ |
| 11.50 | Cobro de 1 USD en vez del total | 11.3 | `ChargeAmount` = total del pedido (o su conversión del router) | ☐ |
| 11.51 | TLS 1.0 y `Thread.Sleep` en la petición | revisar `SpreedlyClient` | `HttpClient` del sistema (TLS del SO) y ningún `Thread.Sleep` | ☐ |
| 11.52 | Credenciales comentadas en el código | buscar la *environment key* y el *access secret* en el repositorio | no están: viven cifradas en `ApiCredential` | ☐ |
````

- [ ] **Step 2: Commit**

```bash
git add docs/deployment/pruebas-de-seguridad.md
git commit -m "Pruebas de seguridad de la tarjeta con 3DS en el alta (PAN fuera de nuestros servidores, sin cobro sin liability shift, cobro antes de activar, resultado dudoso conciliado, callback firmado) y sus condiciones de despliegue"
```

---

### Task 29: Prueba completa y de punta a punta con el simulador, con la tarjeta encendida solo en Development

**Files:** ninguno (verificación).

- [ ] **Step 1: Compilar la solución** (cuando ningún otro agente esté compilando)

Run: `dotnet build MLMConquerorGlobalEdition.slnx -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 2: Todas las pruebas de los proyectos tocados**

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests -nologo`
Expected: PASS (todas, también las anteriores al plan).

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests -nologo`
Expected: PASS.

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests -nologo`
Expected: PASS.

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests -nologo`
Expected: PASS.

- [ ] **Step 3: Una regla de ruteo para `Payment`**

En SQL (base de Development): `SELECT Id, OperationType, IsoCountryCode, CountryGroupId, IsCatchAll, IsActive FROM GatewayRoutingRules WHERE OperationType = 2 AND IsActive = 1`. Si no hay ninguna que cubra `US`, crearla en AdminWeb → *Billing → Routing Rules*: operación *Payment*, catch-all global, reparto 100 % `NmiSpreedly`, sin política de moneda. (El simulador no necesita gateway tokens: `FakeSpreedlyCredentialSource` da uno por procesador.)

- [ ] **Step 4: Arrancar con la tarjeta encendida y el simulador** (dos terminales PowerShell; SignupAPI aplica la migración `SpreedlyTarjetaEnElAlta` al arrancar)

```powershell
$env:Signup__CardEnabled = "true"; $env:Spreedly__Enabled = "true"; $env:Spreedly__UseFake = "true"
dotnet run --project MLMConquerorGlobalEdition.SignupAPI --launch-profile https
```

```powershell
dotnet run --project MLMConquerorGlobalEdition.Signups --launch-profile https
```

Expected: en la consola de SignupAPI no sale ningún `[Spreedly] …`; `https://localhost:7005/api/v1/signups/payment-options` devuelve `cardEnabled: true`, `cardEnvironmentKey: "fake-spreedly-environment"`, `cardSimulated: true`; `/hangfire` → *Recurring jobs* muestra `signup-card-sweep`.

Así queda encendida la tarjeta **solo en Development con el simulador**: el `appsettings.json` base sigue con `Signup:CardEnabled = false` y `Spreedly:Enabled = false`, y fuera de Development la guarda no deja arrancar con el simulador.

- [ ] **Step 5: Alta pagada sin fricción**

1. Abrir `https://localhost:7147/ambassador-join/{slug-de-un-patrocinador}`, país **United States**, rellenar con un correo nuevo y elegir un plan.
2. Pestaña **Credit Card**: se ve el aviso del simulador y el selector de escenario (no hay campos de número ni de CVV). Elegir `frictionless`, aceptar términos y *Join Now*.
3. La pantalla pasa por "Verifying…" y "Processing…" y acaba en "Welcome… Go to BizCenter". Iniciar sesión con el correo y la contraseña del alta: **entra**.
4. En SQL: `SignupCardPayments` del pedido `Paid`, `Consumed = 1`, `Eci = '05'`, `TransStatus = 'Y'`, `LiabilityShift = 1`, `ChargedAt` ≤ `ActivatedAt`; `Orders.Status = 4`; un `PaymentHistories` `NmiSpreedly` capturado con `GatewayTransactionId` `fake_purchase_…`; `MemberCreditCards` con `SpreedlyPaymentMethodToken` y `EncryptedPan = ''`; `SpreedlyApiCalls` con `GetPaymentMethod`, `Authenticate`, `GetTransaction` y `Purchase`, sin cuerpos.

- [ ] **Step 6: Challenge, rechazo, sin shift y desconocido**

1. Otra alta, escenario `challenge`: aparece *Approve / Reject (simulated bank)*. *Reject* → aviso de que el banco no verificó la tarjeta; el pedido vuelve a `Pending`. Elegir `challenge` otra vez y *Approve* → alta activa.
2. Otra alta con `noshift` → aviso de verificación fallida; en SQL `FailureCode = 'NO_LIABILITY_SHIFT'` y **ningún** `GatewayChargeAttempts` del pedido.
3. Otra alta con `declined` → aviso de tarjeta rechazada; `FailureCode = 'DECLINED:SPREEDLY_DECLINED'`.
4. Otra alta con `unknown` → la pantalla se queda en "Processing…" y acaba en "We're still confirming your payment…". En SQL el intento sigue `Charging`. Tomar el token de la compra de `SpreedlyApiCalls` (`Operation = 'Purchase'`, `TransactionToken`) y: `curl.exe -k -X POST https://localhost:7005/api/v1/dev/spreedly/{token}/approve` y `curl.exe -k -X POST https://localhost:7005/api/v1/dev/spreedly/sweep` → el intento pasa a `Paid` y el alta se activa con un `PaymentHistory` `Spreedly`.

- [ ] **Step 7: Seguridad con el simulador**

Ejecutar las pruebas de `docs/deployment/pruebas-de-seguridad.md` §11 que se pueden hacer con el simulador (todas salvo 11.1 y las que piden `Production` o credenciales reales: 11.37 y 11.38 arrancan en `Production`; 11.41 necesita filas reales) y marcar las que pasen con fecha y cómo.

- [ ] **Step 8: Carrera contra SQL Server (R9)**

Con un intento `challenge` aprobado en el simulador pero aún sin terminar, lanzar a la vez:

```powershell
1..4 | ForEach-Object -Parallel { curl.exe -k -s -X POST "https://localhost:7005/api/v1/signups/$using:signupId/card/$using:paymentRef/finish" } -ThrottleLimit 4
curl.exe -k -X POST https://localhost:7005/api/v1/dev/spreedly/sweep
```

(`ForEach-Object -Parallel` es de PowerShell 7; en Windows PowerShell 5.1, abrir cuatro consolas.)

Expected: un solo `PaymentHistory` del pedido, un solo `GatewayChargeAttempts` `Success`, una sola fila de cada bono en `CommissionEarnings` con ese `SourceOrderId`, y el intento `Paid` una vez.

- [ ] **Step 9: Admin**

Entrar a AdminWeb con un usuario `BillingManager` → *Card Signups* (debajo de *Entrax Signups*): las tres vistas cargan; el intento de 11.16 aparece en *To review* con `CHARGE_OUTCOME_UNKNOWN`; *Re-check* sobre un intento `Charging` resuelto en el simulador lo activa. *API Credentials* ofrece `Spreedly` con sus cuatro etiquetas y, en `NmiSpreedly`, el campo *Spreedly gateway token*. Con `SupportLevel1` la entrada del menú no aparece y la API devuelve `403`.

- [ ] **Step 10: Cerrar**

Parar los dos procesos y borrar las variables de entorno de la consola (`Remove-Item Env:Signup__CardEnabled, Env:Spreedly__*`). No hay nada que commitear en esta tarea. **No** se enciende `Signup:CardEnabled` en ningún `appsettings` del repositorio: en producción se enciende por configuración del entorno solo después de la prueba real de §11 (Condiciones de despliegue).

---

## Autorrevisión

- **Cobertura de las decisiones de Nick:** (1) 3DS2 Global con SCA provider y compra con `sca_authentication_token` por el router: Tareas 2, 4, 9, 10, 13, 14; (2) nunca sin 3DS ni sin *liability shift*, sin rebaja: Tareas 2 (`ThreeDsLiabilityPolicy`), 9 (`SPREEDLY_SCA_REQUIRED`), 13 (`NO_LIABILITY_SHIFT`), 10 (sin pasos diferidos con cliente presente); (3) simulador solo en Development con variable de entorno, `.invalid` y endpoints dev: Tareas 1, 3, 5, 6, 20, 29; (4) primero se cobra y después se activa: Tarea 13 (`Charged` guardado antes de `ActivateAsync`); (5) el PAN no pasa por nuestros servidores: Tareas 21, 22, 24; (6) `Signup:CardEnabled` dado por hecho y encendido al final solo en Development: Tareas 0, 15, 18, 20, 29; (7) componente único en `SharedComponents.Lite`: Tareas 21, 22 (y `SpreedlySignupFlow`, Tarea 23); (8) hora de servidor: `IDateTimeProvider.Now` en todo lo nuevo, ningún `UtcNow`; (9) secretos cifrados en `ApiCredential`, nunca en `appsettings`: Tareas 3, 26, 27 y la prueba `No_hay_ningun_secreto_de_Spreedly_en_el_appsettings_base` (Tarea 20).
- **Cobertura del alcance propuesto:** Fase 1 — cliente con Basic auth, authenticate, purchase, GET, void, crédito, log sin cuerpos, simulador, credenciales por entorno, arreglo del orquestador (Tareas 1-11); Fase 2 — componente, `browser_info`, Lifecycle, estados, JS por JSInterop cargado bajo demanda, CSP documentada (Tareas 21, 22, 28); Fase 3 — estado intermedio con el pedido `Pending → Processing → Completed`, re-consulta, *liability shift*, cobro por el router, `SignupOrderClaim`, callback firmado, barrido, columnas ECI/estado/flujo/tokens/NTID y CAVV solo como indicador (Tareas 8, 12-20, 23, 24); Fase 4 — lista, *Re-check*, `Spreedly` y gateway token en credenciales (Tareas 25-27); Fase 5 — §11 y prueba de punta a punta (Tareas 28, 29). Lo que no entra está en *Fuera de alcance*.
- **Marcadores:** sin `TBD` ni "similar a": el stub que se conserva en la Tarea 9 va escrito entero; las pruebas existentes que cambian en la Tarea 15 tienen su código o la regla exacta de sustitución.
- **Nombres usados en varias tareas:** `SpreedlyOptions` (`Timeout`, `AuthenticationTimeout`, `ChargeUnknownGrace`, `PollThrottle`), `SpreedlyOptionsGuard`, `ISpreedlyClient`, `SpreedlyClient`, `FakeSpreedlyClient.Resolve`, `SpreedlyCall<T>`, `SpreedlyOutcome`, `SpreedlyErrorCodes`, `SpreedlyStates`, `SpreedlyTransaction`, `SpreedlyScaAuthentication`, `SpreedlyPaymentMethod`, `SpreedlyAuthenticateRequest`, `SpreedlyPurchaseRequest`, `SpreedlyCurrency.ToMinorUnits`, `SpreedlySignature`, `SpreedlyCardBrands`, `ThreeDsLiabilityPolicy`, `ISpreedlyCredentialSource`/`SpreedlyCredentials`, `FakeSpreedlyCredentialSource`, `SpreedlyCallLog`, `AddSpreedly`, `AddCardCharging`, `CardChargeOutcomes`, `SignupCardPayment` (`StartedAt`, `SetPlannedProcessors`, `GetPlannedProcessors`, `BuildExternalOrderId`), `SignupCardPaymentStatus`, `SpreedlyApiCall`, `SignupCardStatuses`, `SignupCardPaymentRequestDto`, `SignupCardStepDto`, `SpreedlyThreeDsStepDto`, `SignupCardStepResult`, `SpreedlyFakeScenarios`, `SignupCardErrors`, `SignupCardMessages`, `SignupCardSteps`, `SignupCardRouting`, `ISignupCardOptions`/`SignupCardAvailability`, `ISignupCardCheckout.StartAsync`, `ISignupCardSettlement.SettleAsync/CancelAsync`, `SignupCardSettlementSource`, `SignupCardSettlementOutcome`, `ISignupCardStatusService`, `ISpreedlyCallbackProcessor`/`SpreedlyCallbackOutcome`, `SignupCardController`, `SignupCardSweepJob`, `SpreedlyCardPayment` (`PayAsync`, `Busy`, `TokenizeResult`), `SpreedlySignupApi`, `SpreedlySignupFlow`, `SignupApiRecheckForwarder`, `AdminSignupCardPaymentsController`.
- **Riesgo de ejecución:** la Tarea 15 toca pruebas de la rama `cerrar-tarjeta-en-el-alta`; si al ejecutar este plan sus nombres cambiaron, ajustar los pasos 8a-8f a los nombres reales antes de seguir (la regla de fondo no cambia: la tarjeta deja de ser un camino inmediato).

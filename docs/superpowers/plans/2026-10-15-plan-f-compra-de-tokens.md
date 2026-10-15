# Plan F — Cobro desde el BizCenter y compra de tokens

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que un miembro pueda comprar tokens desde el BizCenter pagando con su saldo de comisiones, su saldo real de eWallet (i-Payout), una tarjeta guardada o cripto (con aprobación manual y dirección de depósito configurada por el admin), con un tope de 50 tokens por mes calendario, step-up cuando paga con saldo, sin cobros dobles en reintentos, recibo por correo y con cualquier error a la vista. Cierra las filas "Compra con comisiones, eWallet, tarjeta o crypto", "Límite de 50 tokens por mes, no reembolsables" y "Distribución a equipo y a cliente externo" de la matriz de paridad.

**Architecture:** Billing es el dueño del dinero. Expone `api/v1/billing/tokens/*` (catálogo con precio, fondos, compra, confirmación de cripto y conciliación) y hace todo el trabajo: precio, cupo, candado por miembro, cobro, emisión de códigos y recibo. El BizCenter es un paso: reenvía el JWT del miembro con un `HttpClient` con nombre (`"billing"`, como `"rankengine"`) y nunca manda un `MemberId`. La compra con saldo entra por un endpoint propio con `[RequiresStepUp]` (Plan E); Billing vuelve a validar el permiso (sin quemarlo) para que nadie se salte el BizCenter. AdminAPI administra los parámetros globales (direcciones de depósito cripto incluidas), lista las compras en cripto y las que hay que conciliar, y reenvía las acciones a Billing con el Bearer del administrador. La pantalla compartida `TokensPage.razor` habla el contrato real, que vive una vez en `SharedKernel/Billing`.

**Tech Stack:** .NET 10, ASP.NET Core MVC, EF Core 10 (SQL Server, `sp_getapplock`), MediatR (BizCenter), Blazor Server/WASM (SharedComponents, Signups.Client, AdminWeb), xUnit + FluentAssertions + Moq + bUnit, EF InMemory. Rama: se crea desde `master`, que ya incluye el Plan E.

---

## Hoja de ruta de la ronda

| Plan | Alcance | Estado |
|---|---|---|
| E | OTP / step-up al cambiar el método de cobro | En `master` (`95028d3`). |
| **F (este)** | Cobro desde el BizCenter + compra de tokens | Este documento (revisado 2026-10-15 con las respuestas del dueño). |
| G | Upgrade de membresía | Reutiliza de F: `IMemberFundsGate`, la vía de tarjeta con pasos inmediatos, `IEWalletFunds`, la confirmación de cripto con `CryptoPaymentPurpose` y las direcciones de depósito. |

## Decisiones

**Del dueño (vinculantes):**

1. **Billing es el dueño del dinero.** El BizCenter llama a Billing por HTTP con el JWT del miembro; no toca saldos ni crea pedidos.
2. **Métodos:** saldo de comisiones, saldo de eWallet, tarjeta guardada y cripto. **SafetyPay no.** Cripto es aprobación manual, como el alta: `CryptoPaymentConfirmation` + pedido en `Processing`; los tokens se emiten solo cuando un administrador confirma.
3. **Precio = SUMA de los productos otorgados** (`TokenTypeProduct.Role == Granted`), **una vez cada uno**: `QuantityGranted` no multiplica, porque al canjear el token se entrega un producto de cada uno. Se vende si tiene al menos un producto otorgado y el total es > 0. Los de upgrade (sin `Granted`) no se venden. Las variantes promocionales o gratuitas se excluyen con una regla de datos (D1).
4. **Límite: 50 tokens por miembro y mes calendario**, hora de servidor (`Now`), contado sobre `TokenTransaction` con `TransactionType == Purchased` **imputados al mes de su compra** (`TokenPurchase.PeriodKey`, D3). **No reembolsable.**
5. **Sin comisiones, volumen ni puntos por comprar**: llegan al canjear el token. Pedidos sin `OrderDetails`.
6. **Direcciones de depósito cripto como parámetros** administrables: `Crypto.DepositAddress.{BTC|ETH|USDT}` (+ `Crypto.DepositNetwork.{…}`). CRUD de `GlobalParameter` en AdminAPI + página en AdminWeb. La compra muestra dirección, importe y moneda; una moneda sin dirección no se ofrece. El alta muestra también la dirección configurada.
7. **eWallet = saldo REAL de i-Payout.** Saldo con `eWallet_GetCurrencyBalance` (USD); débito con `eWallet_AddCheckoutItems` exactamente como el legado (`EWallet_AddDeductionFromBalance`). Se abandona el libro interno de `WalletHistory` para las compras. `MerchantReferenceID` = id de la compra. Resultado dudoso → la compra queda **a conciliar a mano**, nunca se reintenta.
8. **Ninguna compra bajo suplantación**, sea o no de solo lectura.
9. **Step-up al pagar con saldo** (comisiones o eWallet): operación nueva `StepUpOperations.TokenPurchaseFromBalance`. Tarjeta y cripto no lo piden.
10. **Recibo por correo** al completar y al confirmar cripto: plantilla `TOKEN_PURCHASE_RECEIPT` en/es.
11. **El miembro sale siempre del token**; idempotencia por clave del cliente; el tope aguanta compras simultáneas; componentes unificados.

**Tomadas aquí (con su porqué):**

| # | Decisión | Por qué |
|---|---|---|
| D1 | **Regla de "no se vende": columna `TokenType.IsPurchasable`** (por defecto `false`), que el admin cambia en la pantalla de tipos de token. La migración la inicializa a `true` para los tipos activos que no son de upgrade y cuyo nombre no lleva marca promocional (`FREE`, `Coupon`, `SpecialPromo`, `Help a Friend`). | Revisado: **nada en los datos distingue las variantes FREE** salvo el nombre. Otorgan los mismos productos que las de pago (p. ej. 81 y 5 = Ambassador + Elite), con los mismos precios; `Product` no tiene marca de gratuidad y `MembershipLevel.IsFree` describe niveles, no variantes de token (y marcaría como no vendible al Guest). Un flag explícito es la única regla de datos posible; el nombre solo se usa una vez para el valor inicial, y desde ahí manda el dato. Los tipos nuevos nacen sin vender hasta que alguien lo decide. |
| D2 | Precio de cada producto = `SetupFee + MonthlyFee`; precio del token = suma de los productos otorgados, **sin multiplicar por `QuantityGranted`**. Sin precios promo. | Es lo que el alta escribe en `OrderDetail.UnitPrice` (`SelectProductsHandler.cs:92`). Un token sustituye ese cobro, y al canjearlo se entrega un producto de cada uno (`QuantityGranted` no se usa al canjear): cobrar por cantidad sería cobrar lo que no se entrega. |
| D3 | Cupo: instancias `Purchased` con código cuyo `OriginalOwnerMemberId` es el miembro y cuya **compra dueña** (`TokenTransaction.TokenPurchaseId` → `TokenPurchase`) tiene el `PeriodKey` del mes, **más** las compras en vuelo (`Pending`, `AwaitingCryptoConfirmation`, `NeedsReconciliation`) del mismo `PeriodKey`. Las filas `Purchased` sin compra (anteriores a F) cuentan por `CreationDate`. **Una compra cuenta en un solo mes, desde que reserva hasta que emite**; confirmar una cripto o cerrar una conciliación **no** vuelve a comprobar el tope. | Contar solo lo emitido dejaría pasar compras simultáneas o pedidos de cripto sin confirmar. Contar lo emitido por `CreationDate` dejaba pasar el tope entre meses: 50 en cripto reservados el 30/09 no ocupaban octubre, el miembro compraba 50 más en octubre y al confirmar la de septiembre octubre quedaba con 100. Por dueño original porque `TransferTokenInstance` cambia `MemberId`. |
| D4 | Concurrencia: `sp_getapplock` exclusivo por miembro, **el candado de fondos** `member-funds:{memberId}` (`IMemberFundsGate` en `Billing/Services/MemberFundsGate.cs`, un solo nombre en `MemberFundsLock.Resource`). Lo toman la compra de tokens, `PayoutOrchestrator` (alrededor de la lectura y reserva de comisiones) y el lote CSV (`PayoutBatchExportService`, un candado por miembro en orden ordinal y saltando al ocupado). Lo registra `AddPayoutServices`. | `TokenBalance` no tiene índice único ni `RowVersion` real. Serializa solo lo del mismo miembro. Compartido con los pagos porque, si no, un pago podía reservar las mismas comisiones que una compra acababa de gastar. |
| D5 | Idempotencia: `IdempotencyKey` de la pantalla; índice único `(MemberId, IdempotencyKey)`. | Un timeout no se convierte en dos cobros. |
| D6 | Pedido de compra de tokens en `Paid` y **sin `OrderDetails`**. | `EnrollmentTeamPointsService` y `PersonalCustomerPointsService` suman puntos de los `OrderDetails` de pedidos `Completed` (decisión 5 del dueño). |
| D7 | Tarjeta: solo los pasos del plan de ruteo **sin espera** (`DelayMinutes == 0`); token de cobro = `SpreedlyPaymentMethodToken` o, si falta, `CardToken`. | Un paso con espera lo cobraría `DelayedFallbackChargeJob`, que no emite tokens. |
| D8 | Saldo de comisiones gastable = pendientes con `PaymentDate <= Now` no reservadas por un `PayoutAttempt` no fallido + residual consolidable. | `GetAvailableAsync` incluye lo que vence después y lo que está en un pago en curso. |
| D9 | Cripto: `CryptoPaymentConfirmation.Purpose` (`Signup = 1`, `TokenPurchase = 2`). La compra la confirma **Billing**; AdminAPI reenvía. | Sin el discriminador, una compra saldría en la pantalla de altas y su "Confirmar" iría a SignupAPI. |
| D10 | **Usuario de i-Payout = `AccountIdentifier` de la wallet eWallet del miembro** (la preferida primero). | `EWalletPayoutGatewayService` propone el `MemberId` como `UserName` al dar de alta, pero guarda en `AccountIdentifier` lo que el gateway confirma o reasigna. Leer el dato guardado coincide con el `MemberId` cuando el gateway lo aceptó y sigue siendo correcto cuando no. Sin wallet eWallet con identificador no se puede pagar con eWallet. |
| D11 | **Débito eWallet** como el legado: `eWallet_AddCheckoutItems` con `AutoChargeAccount = "true"` y un ítem. Se da por **cobrado** solo si el envoltorio y el ítem tienen `m_Code >= 0` **y** el saldo releído bajó al menos el importe. Si los códigos están bien pero el saldo no bajó (PROCESSING/PENDING), o no hubo respuesta clara (timeout, 5xx, JSON roto), la compra queda `NeedsReconciliation`: no se emiten tokens, no se reintenta y aparece en el panel para que tesorería la cierre. | Es exactamente lo que hace el legado: `BuyItemsWithEWalletBalance` (`DailyRealTimeCommissionEntity.cs:17160`) y la deducción de tokens (`:16880-16960`) solo dan por buena la deducción si `_balanceAfterDeduction < _balance`; el texto del ítem vacío es secundario y PROCESSING/PENDING devuelven código 0. No existe en i-Payout ni en nuestro cliente una consulta por `MerchantReferenceID`, así que ante la duda no hay forma segura de reintentar. |
| D12 | El débito eWallet ocurre **con el candado del miembro tomado** (fase 2 en su propia transacción). | La verificación por saldo solo es fiable si no hay otro débito nuestro del mismo miembro a la vez. |
| D13 | Step-up: dos endpoints en el BizCenter. `POST tokens/purchase/balance` lleva `[RequiresStepUp(TokenPurchaseFromBalance)]` (valida y **quema** el permiso) y solo acepta comisiones o eWallet; `POST tokens/purchase` solo acepta tarjeta o cripto. Billing exige y valida el mismo permiso para los métodos de saldo **sin quemarlo** (ya lo quemó el BizCenter). | El filtro de Plan E es incondicional por acción: la condición la pone la ruta. Validar también en Billing cierra la puerta de llamar a Billing directamente con el JWT; no se quema allí porque el permiso ya se marcó como usado en la caché compartida. |
| D14 | La distribución mueve **los mismos códigos** y exige equipo de inscripción. | `DistributeTokenHandler` acuñaba códigos nuevos y dejaba canjeables los del que enviaba. |
| D15 | "Cliente externo" = compartir un código disponible (botón Copiar). | Los códigos son al portador: el alta no exige relación entre dueño y quien se inscribe. |
| D16 | Cripto candidata: `BTC`, `ETH`, `USDT`; se ofrece la que tenga dirección configurada. El importe se muestra en USD ("importe a pagar en {moneda}"). | No hay tipo de cambio en el sistema; el admin coteja el equivalente al confirmar. |
| D17 | Recibo sin códigos de token. | Los códigos son al portador: quien lea el buzón podría usarlos. La pantalla los muestra al comprar. |
| D18 | **Salidas de una cripto sin pagar**: `AwaitingCryptoConfirmation` → `Cancelled` (el miembro, `POST tokens/purchases/{id}/cancel`), `Rejected` (un aprobador de cripto, con motivo) o `Expired` (trabajo `TokenPurchaseCryptoExpiryJob` cada hora, a los `Billing:TokenPurchase:CryptoExpiryDays` días, 3 por defecto). Las tres liberan el cupo, cancelan el pedido y dejan la `CryptoPaymentConfirmation` en `Cancelled` (ya no se puede confirmar). Estados nuevos en el enum (`int`, sin migración). | Sin salida, una cripto abandonada ocupaba cupo y el panel acumulaba pedidos muertos. Billing ya tiene Hangfire con trabajos recurrentes, así que la caducidad es un trabajo y no algo perezoso al listar. |

## Fuera de alcance

- **3D Secure** (Spreedly): plan posterior. La tarjeta se cobra con tarjetas guardadas por el orquestador existente.
- **Cobro real con Spreedly**: `SpreedlyCardGatewayService` es un stub que devuelve un éxito simulado. Por eso la tarjeta va **apagada** (`Billing:TokenPurchase:CardEnabled = false`, R13) y encenderla es un bloqueante de producción.
- **Devoluciones**: no reembolsable.
- **Tipo de cambio cripto**: se muestra el importe en USD; el admin coteja.
- **Pestaña de tokens del panel admin** (`AdminMemberProfile`, `AdminMemberView`): llama a rutas `api/v1/admin/members/{id}/tokens/*` que no existen. Aquí solo se garantiza que no compra ni distribuye.
- **Búsqueda y fechas del historial**: el endpoint no los acepta; la pantalla los quita.
- **Validar en el servidor del alta que la moneda cripto tenga dirección**: el alta solo la muestra (alcance acotado por el dueño). `MemberJoin.razor` tiene el mismo texto falso ("te redirigiremos a la pasarela") y queda anotado.
- **Unificar el candado con el cobro recurrente** (R4).

## Riesgos conocidos

| # | Riesgo | Mitigación en este plan |
|---|---|---|
| R1 | Débito eWallet en PROCESSING/PENDING o sin respuesta: el dinero pudo salir o no. | Compra `NeedsReconciliation`, sin tokens, reserva el cupo, aparece en *Admin → Billing → Token Purchases → To review*; tesorería consulta i-Payout y la cierra como cobrada (emite) o no cobrada (libera). La pantalla dice "no lo intentes de nuevo" y la clave repetida devuelve `PURCHASE_NEEDS_REVIEW`. |
| R2 | La verificación por saldo puede engañarse si i-Payout acredita (un payout `eWallet_Load`) entre la lectura previa y la posterior: el saldo no baja y una compra cobrada queda a conciliar. | Va al lado seguro (revisión manual, nunca tokens sin cobro). Nuestros débitos del mismo miembro están serializados (D12). |
| R3 | En desarrollo los `AccountIdentifier` de eWallet son correos sintéticos (`AMB-…@payout.dev`): i-Payout no los reconoce y el saldo falla. | La pantalla muestra el saldo como "—" y el error; probar el débito real exige sandbox de i-Payout con un usuario creado. |
| R4 | `RecurringBillingProcessor` también debita comisiones sin pasar por el candado de fondos (los pagos sí lo toman, D4). | Documentado; unificar en G o posterior. |
| R5 | Caída del proceso, o de la base, entre un cobro (tarjeta o eWallet) y la emisión. | Ninguna compra queda en `Pending` por un fallo nuestro: lo que falla **antes** de mover dinero (ruteo, candado, lectura del saldo) abandona la compra y suelta el cupo; lo que falla **después** (emitir, guardar, pasar a conciliar) relee la compra con el contexto limpio y la deja `NeedsReconciliation` con `CancellationToken.None`, y si ni eso se guarda queda un `Critical` en el log con el id. Red de seguridad: `TokenPurchaseStalePendingJob` (cada hora) pasa a conciliar las compras con tarjeta o eWallet que llevan `Billing:TokenPurchase:StalePendingMinutes` (30) en `Pending`. **Nunca libera**: el dinero pudo moverse. |
| R5b | **Contrato del orquestador de tarjeta.** `GatewayChargeOrchestrator` pasa al siguiente gateway ante **cualquier** fallo (incluido un timeout en el que el emisor pudo cargar) y devuelve solo el código del último: con varios pasos de una vez, un timeout en el primero y un cobro en el segundo son **dos cobros**, y un fallo final no dice si hubo cargo. Además no distingue un rechazo del emisor de un resultado desconocido. | La compra de tokens llama al orquestador **un paso cada vez** y solo sigue al siguiente cuando el fallo prueba que no hubo cobro: lista explícita `CardChargeOutcomes.ExplicitNoChargeCodes` (hoy solo comprobaciones previas que no llegan a llamar al gateway: `GATEWAY_NOT_REGISTERED`, `SPREEDLY_MEMBER_TOKEN_MISSING`, `SPREEDLY_CREDENTIAL_MISSING`, `SPREEDLY_CREDENTIAL_INCOMPLETE`, `SPREEDLY_DOWNSTREAM_TOKEN_MISSING`, `SPREEDLY_DOWNSTREAM_TOKEN_NOT_SET`, `CREDENTIAL_NOT_FOUND`, `CREDENTIAL_INACTIVE`, `CREDENTIAL_INCOMPLETE`). Cualquier otro fallo va a conciliar y no libera el cupo. Los demás usuarios del orquestador (cobro recurrente, `DelayedFallbackChargeJob`) siguen expuestos: arreglarlo en el orquestador cuando el cobro con Spreedly sea real, y añadir a la lista sus rechazos explícitos (nunca un timeout ni un 5xx). |
| R6 | `TokenCodeGenerator` puede repetir (el espacio es pequeño). | Ya usa `RandomNumberGenerator` (impredecible y seguro entre hilos); `TokenIssuer` descarta los que ya existen. |
| R7 | `TokenBalance` sin control de concurrencia. | Toda emisión de la compra va bajo el candado del miembro: comisiones (una transacción), eWallet (fase 2) y tarjeta (fase 3, con `CancellationToken.None`; si el candado está ocupado la compra, ya cobrada, queda a conciliar). Confirmar una cripto y conciliar **no** lo toman: su `RowVersion` evita emitir dos veces la misma compra, pero el `TokenBalance` se puede pisar con otra emisión simultánea del mismo miembro. Mismo riesgo que `GrantTokens`. |
| R8 | **Cambiar una dirección de depósito desvía dinero.** El CRUD de parámetros lo pueden usar SuperAdmin y Admin sin segundo factor (el step-up del panel admin no existe todavía) y `GlobalParameter` no guarda historial. | Se registra un aviso en el log con el valor viejo y el nuevo y quién lo cambió; las direcciones se validan por formato. Pedir step-up en el admin queda pendiente. |
| R9 | La plantilla de código de step-up (Plan E) dice "un cambio sensible, como a dónde se pagan tus comisiones". Para una compra se lee raro pero no es falso. | El diálogo muestra un texto propio por operación (`StepUp.WhyTokenPurchase`). La plantilla de correo no se toca. |
| R10 | La espera de 24 h tras cambiar un factor (Plan E, `STEP_UP_COOLDOWN`) también bloquea comprar con saldo. | Es lo deseable; tarjeta y cripto siguen disponibles. |
| R11 | `IsPurchasable` se inicializa por nombre en la migración `CompraDeTokens`; un tipo promocional con un nombre sin marca quedaría vendible. | Tras aplicar la migración en producción, revisar a mano `SELECT Id, Name, IsPurchasable FROM TokenTypes WHERE IsActive = 1` (Tarea 16) y corregir en *Token Types*. |
| R13 | **BLOQUEANTE DE PRODUCCIÓN — la tarjeta no cobra de verdad.** `SpreedlyCardGatewayService` es un stub: valida credenciales y devuelve `simulated_success` con un id `simulated-spreedly-txn-…` (y `StubGatewayBase` uno `STUB-…`). Además el orquestador no rellena `SpreedlyPaymentMethodToken` ni `DownstreamProcessor` en la petición al gateway. Encender la tarjeta así emitiría tokens sin cobrar. | `Billing:TokenPurchase:CardEnabled` (por defecto `false`): apagada, una compra con tarjeta se rechaza al principio con `CARD_PAYMENTS_DISABLED` (sin reserva ni cobro) y el catálogo y los fondos traen `CardEnabled = false` para que la pantalla no muestre la pestaña. Aunque se encienda por error, un id que empieza por `simulated` (o `STUB-`) nunca emite: la compra queda `NeedsReconciliation` con `CARD_SIMULATED_CHARGE` y un `Critical` en el log. **No encender en producción** hasta que el cobro con Spreedly sea real y probado (Tarea 16, paso 9; `docs/deployment/pruebas-de-seguridad.md`, condiciones de despliegue). |
| R12 | Una compra en cripto que nadie paga reservaría el cupo para siempre. | D18: el miembro la cancela, un administrador la rechaza o caduca a los `Billing:TokenPurchase:CryptoExpiryDays` días (3 por defecto) con un trabajo recurrente de Hangfire. |

## Mapa de archivos

| Archivo | Responsabilidad |
|---|---|
| `SharedKernel/Billing/TokenPurchaseContracts.cs` (nuevo) | Reglas, métodos, estados, códigos de error y DTOs de la compra. |
| `SharedKernel/Billing/CryptoDeposit.cs` (nuevo) | Claves `Crypto.Deposit*` y `CryptoDepositAddressDto`. |
| `SharedKernel/Security/StepUpOperations.cs` | `TokenPurchaseFromBalance`. |
| `SharedKernel/NotificationEvents.cs` | `TokenPurchaseReceipt`. |
| `Domain/Enums/TokenPaymentMethod.cs`, `TokenPurchaseStatus.cs`, `Domain/Entities/Tokens/TokenPurchase.cs`, `Domain/Entities/Orders/CryptoPaymentPurpose.cs` (nuevos) | Dominio de la compra. |
| `Domain/Entities/Tokens/TokenType.cs`, `TokenTransaction.cs`, `Domain/Entities/Orders/CryptoPaymentConfirmation.cs` | `IsPurchasable`, `TokenPurchaseId`, `Purpose`. |
| `Repository/Configurations/*`, `Context/AppDbContext.cs`, `Migrations/*_CompraDeTokens.cs`, `Migrations/*_SiembraPlantillaReciboCompraTokens.cs` | Esquema y plantilla. |
| `Repository/Services/Crypto/CryptoDepositAddressReader.cs` (nuevo) | Lee las direcciones configuradas (Billing y SignupAPI). |
| `Repository/Services/Payout/EWallet/EWalletClient.cs`, `EWalletContracts.cs` | `DeductAsync` (`eWallet_AddCheckoutItems`). |
| `AdminAPI/Controllers/AdminGlobalParametersController.cs`, `AdminAPI/DTOs/GlobalParameters/*` (nuevos) | CRUD de parámetros. |
| `AdminWeb/Components/Pages/AdminGlobalParameters.razor` (nuevo) | Pantalla de parámetros. |
| `Billing/Services/CurrentUserService.cs`, `Billing/Services/SharedClockAdapter.cs` (nuevo) | `memberId` correcto; reloj para el validador de step-up. |
| `Billing/Services/Tokens/*` (nuevo) | Precio, cupo, emisor, candado, fondos eWallet, recibos, comprobación de step-up y servicio de compra. |
| `Billing/Services/Recurring/ICommissionBalanceService.cs`, `CommissionBalanceService.cs` | `GetSpendableAsync`, `FundTokenPurchaseAsync`. |
| `Billing/Controllers/TokenPurchasesController.cs` (nuevo), `Billing/Program.cs` | Endpoints y DI. |
| `SignupAPI/.../ConfirmCryptoPaymentHandler.cs`, `SignupAPI/Controllers/SignupsController.cs`, `SignupAPI/Program.cs`, `Signups.Client/Pages/Signup.razor` | Solo altas; dirección de depósito en el alta. |
| `AdminAPI/Controllers/AdminCryptoSignupsController.cs` | Filtra `Purpose == Signup`. |
| `AdminAPI/Controllers/AdminTokenPurchasesController.cs` (nuevo), `AdminAPI/Program.cs` | Cripto pendiente y compras a conciliar; reenvía a Billing. |
| `AdminAPI/DTOs/Tokens/*.cs`, `AdminAPI/Mappings/AdminMappingExtensions.cs`, `AdminWeb/Components/Pages/AdminTokenTypes.razor` | `IsPurchasable` editable. |
| `AdminWeb/Components/Pages/AdminTokenPurchases.razor` (nuevo), `SharedComponents/Components/Layout/AdminSidebarMenu.razor` | Pantallas admin y menú. |
| `BizCenter/Services/BearerToken.cs`, `IBillingClient.cs`, `BillingClient.cs` (nuevos), `BizCenter/Program.cs` | Cliente a Billing. |
| `BizCenter/Controllers/TokensController.cs`, `WalletController.cs`, `RanksController.cs` | `tokens/types`, `tokens/summary`, `tokens/purchase`, `tokens/purchase/balance`, `wallet/balances`. |
| `BizCenter/Features/Tokens/GetTokenSummary/*`, `DTOs/Tokens/Validators/PurchaseTokensRequestValidator.cs` (nuevos), `DTOs/Tokens/TokenBalanceDto.cs`, `Features/Tokens/GetTokenBalances/GetTokenBalancesHandler.cs` | Resumen, validación, `TokenTypeId` en saldos. |
| `BizCenter/Features/Tokens/DistributeToken/DistributeTokenHandler.cs` | Mueve los códigos. |
| `SharedComponents/Components/Tokens/TokensPage.razor`, `TokenPurchaseMessages.cs` (nuevo), `Components/Account/StepUp/StepUpDialog.razor`, `StepUpMessages.cs` | Pantalla, step-up y texto por operación. |
| `SharedComponents/Resources/SharedResources*.resx` (9) | Textos `Tokens.*` y `StepUp.WhyTokenPurchase`; la nota pasa de 10 a 50. |
| `BizCenterWeb/Components/Pages/Tokens.razor`, `BizCenterApp/Components/Pages/Tokens.razor` | Encienden compra y distribución. |

---

### Task 1: Contrato compartido (SharedKernel)

**Files:**
- Create: `MLMConquerorGlobalEdition.SharedKernel/Billing/TokenPurchaseContracts.cs`
- Create: `MLMConquerorGlobalEdition.SharedKernel/Billing/CryptoDeposit.cs`
- Modify: `MLMConquerorGlobalEdition.SharedKernel/Security/StepUpOperations.cs`
- Modify: `MLMConquerorGlobalEdition.SharedKernel/NotificationEvents.cs`

> `ImpersonationScope.IsImpersonating` ya existe en `master` (Plan E): se usa tal cual.

- [ ] **Step 1: Crear `TokenPurchaseContracts.cs`**

```csharp
namespace MLMConquerorGlobalEdition.SharedKernel.Billing;

/// <summary>
/// Reglas de la compra de tokens. Las aplica Billing, las reenvía el BizCenter y las muestra la
/// pantalla; viven aquí para que las tres partes no diverjan (la nota de la pantalla decía "10 por
/// mes" mientras el negocio pedía 50).
/// </summary>
public static class TokenPurchaseRules
{
    /// <summary>Tokens que un miembro puede comprar por mes calendario, en hora de servidor.</summary>
    public const int MonthlyLimit = 50;

    public const int IdempotencyKeyMaxLength = 64;

    /// <summary>
    /// Monedas candidatas. Solo se ofrecen las que tienen dirección de depósito configurada
    /// (<see cref="CryptoDepositKeys"/>).
    /// </summary>
    public static readonly IReadOnlyList<string> CryptoCurrencies = ["BTC", "ETH", "USDT"];

    /// <summary>La clave la genera la pantalla (un GUID sin guiones).</summary>
    public static bool IsValidIdempotencyKey(string? key) =>
        !string.IsNullOrWhiteSpace(key)
        && key.Length <= IdempotencyKeyMaxLength
        && key.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    /// <summary>yyyyMM del mes al que se imputa una reserva.</summary>
    public static int PeriodKey(DateTime now) => now.Year * 100 + now.Month;

    public static DateTime MonthStart(DateTime now) => new(now.Year, now.Month, 1, 0, 0, 0, now.Kind);
}

/// <summary>Valores de <see cref="PurchaseTokensRequest.PaymentMethod"/>. Coinciden con el enum de dominio.</summary>
public static class TokenPaymentMethods
{
    public const string CommissionBalance = "CommissionBalance";
    public const string EWallet           = "EWallet";
    public const string CreditCard        = "CreditCard";
    public const string Crypto            = "Crypto";

    /// <summary>
    /// Pagar con saldo propio (comisiones o eWallet) exige step-up: es dinero que ya es del miembro y
    /// sale sin que nadie más lo autorice. Tarjeta (emisor) y cripto (admin) tienen su propio control.
    /// </summary>
    public static bool IsBalance(string? method) =>
        string.Equals(method, CommissionBalance, StringComparison.OrdinalIgnoreCase)
        || string.Equals(method, EWallet, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Valores de <see cref="TokenPurchaseResultDto.Status"/>.</summary>
public static class TokenPurchaseStatuses
{
    public const string Pending                    = "Pending";
    public const string Completed                  = "Completed";
    public const string Failed                     = "Failed";
    public const string AwaitingCryptoConfirmation = "AwaitingCryptoConfirmation";
    public const string NeedsReconciliation        = "NeedsReconciliation";
}

/// <summary>Códigos de error de la compra. La pantalla los traduce con <c>TokenPurchaseMessages</c>.</summary>
public static class TokenPurchaseErrors
{
    public const string NotAMember                    = "NOT_A_MEMBER";
    public const string Impersonation                 = "IMPERSONATION_CANNOT_PURCHASE";
    public const string InvalidQuantity               = "INVALID_QUANTITY";
    public const string IdempotencyKeyRequired        = "IDEMPOTENCY_KEY_REQUIRED";
    public const string PaymentMethodNotSupported     = "PAYMENT_METHOD_NOT_SUPPORTED";

    /// <summary>Un método de saldo por la ruta sin step-up, o tarjeta/cripto por la de saldo.</summary>
    public const string WrongEndpoint                 = "PAYMENT_METHOD_WRONG_ENDPOINT";
    public const string MemberNotFound                = "MEMBER_NOT_FOUND";
    public const string MemberInactive                = "MEMBER_INACTIVE";
    public const string TokenTypeNotFound             = "TOKEN_TYPE_NOT_FOUND";
    public const string NotForSale                    = "TOKEN_TYPE_NOT_FOR_SALE";
    public const string NoGrantedProduct              = "TOKEN_TYPE_NO_PRODUCT";
    public const string ProductInactive               = "TOKEN_PRODUCT_INACTIVE";
    public const string PriceNotSet                   = "TOKEN_PRICE_NOT_SET";
    public const string MonthlyLimitReached           = "TOKEN_MONTHLY_LIMIT_REACHED";
    public const string InsufficientCommissionBalance = "INSUFFICIENT_COMMISSION_BALANCE";
    public const string InsufficientEWalletBalance    = "INSUFFICIENT_EWALLET_BALANCE";
    public const string EWalletNotFound               = "EWALLET_NOT_FOUND";
    public const string EWalletUnavailable            = "EWALLET_UNAVAILABLE";
    public const string EWalletRejected               = "EWALLET_DEBIT_REJECTED";
    public const string NeedsReview                   = "PURCHASE_NEEDS_REVIEW";
    public const string CreditCardNotFound            = "CREDIT_CARD_NOT_FOUND";
    public const string CreditCardExpired             = "CREDIT_CARD_EXPIRED";
    public const string CreditCardNotChargeable       = "CREDIT_CARD_NOT_CHARGEABLE";
    public const string CardChargeFailed              = "CARD_CHARGE_FAILED";
    public const string NoImmediateGateway            = "NO_IMMEDIATE_GATEWAY";
    public const string CryptoCurrencyNotSupported    = "CRYPTO_CURRENCY_NOT_SUPPORTED";
    public const string CryptoAddressNotConfigured    = "CRYPTO_ADDRESS_NOT_CONFIGURED";
    public const string InProgress                    = "PURCHASE_IN_PROGRESS";
    public const string Busy                          = "PURCHASE_BUSY";
    public const string BillingUnavailable            = "BILLING_UNAVAILABLE";
    public const string CryptoPaymentNotFound         = "CRYPTO_PAYMENT_NOT_FOUND";
    public const string CryptoAlreadyConfirmed        = "CRYPTO_PAYMENT_ALREADY_CONFIRMED";
    public const string CryptoTransactionIdRequired   = "CRYPTO_TRANSACTION_ID_REQUIRED";
    public const string PurchaseNotFound              = "TOKEN_PURCHASE_NOT_FOUND";
    public const string NotUnderReview                = "TOKEN_PURCHASE_NOT_UNDER_REVIEW";
}

/// <summary>
/// Lo que manda la pantalla. NO lleva MemberId: el comprador sale del token en el BizCenter y otra
/// vez en Billing. Un campo que no existe no se puede falsificar.
/// </summary>
public sealed class PurchaseTokensRequest
{
    public int     TokenTypeId    { get; set; }
    public int     Quantity       { get; set; }
    public string  PaymentMethod  { get; set; } = string.Empty;
    public string? CreditCardId   { get; set; }
    public string? CryptoCurrency { get; set; }
    public string  IdempotencyKey { get; set; } = string.Empty;
}

public sealed class TokenPurchaseResultDto
{
    public string       PurchaseId           { get; set; } = string.Empty;
    public string       OrderId              { get; set; } = string.Empty;
    public string?      OrderNo              { get; set; }
    public string       Status               { get; set; } = string.Empty;
    public int          TokenTypeId          { get; set; }
    public int          Quantity             { get; set; }
    public decimal      UnitPrice            { get; set; }
    public decimal      TotalAmount          { get; set; }
    public string       PaymentMethod        { get; set; } = string.Empty;
    public List<string> TokenCodes           { get; set; } = new();
    public int          MonthlyRemaining     { get; set; }

    // Solo en cripto: a dónde y en qué moneda transferir.
    public string?      CryptoCurrency       { get; set; }
    public string?      CryptoDepositAddress { get; set; }
    public string?      CryptoNetwork        { get; set; }
}

public sealed class TokenCatalogDto
{
    public int MonthlyLimit       { get; set; }
    public int PurchasedThisMonth { get; set; }
    public int MonthlyRemaining   { get; set; }
    public List<TokenCatalogItemDto> Items { get; set; } = new();

    /// <summary>Monedas con dirección configurada. Vacío = no se ofrece cripto.</summary>
    public List<CryptoDepositAddressDto> CryptoDeposits { get; set; } = new();
}

public sealed class TokenCatalogItemDto
{
    public int      TokenTypeId          { get; set; }
    public string   Name                 { get; set; } = string.Empty;
    public string?  Description          { get; set; }
    public bool     IsGuestPass          { get; set; }
    public decimal? UnitPrice            { get; set; }
    public bool     Purchasable          { get; set; }
    public string?  NotPurchasableReason { get; set; }
}

public sealed class TokenPurchaseFundsDto
{
    public decimal  CommissionBalance { get; set; }

    /// <summary>Saldo real en i-Payout (USD). Null si no se pudo leer o no hay cuenta.</summary>
    public decimal? EWalletBalance    { get; set; }

    /// <summary>False si el miembro no tiene cuenta eWallet registrada: no puede pagar con ella.</summary>
    public bool     HasEWallet        { get; set; }
}

public sealed class TokenSummaryDto
{
    public int TotalTokens    { get; set; }
    public int TotalUsed      { get; set; }
    public int TotalAvailable { get; set; }
}

public sealed class ConfirmCryptoTokenPurchaseRequest
{
    public string  CryptoTransactionId { get; set; } = string.Empty;
    public string? Notes               { get; set; }
}

/// <summary>
/// Cierre manual de una compra que quedó a conciliar (débito eWallet dudoso). Lo decide tesorería
/// después de mirar i-Payout.
/// </summary>
public sealed class ReconcileTokenPurchaseRequest
{
    /// <summary>True: el dinero salió; se emiten los tokens. False: no salió; se libera el cupo.</summary>
    public bool    Charged          { get; set; }
    public string? GatewayReference { get; set; }
    public string? Notes            { get; set; }
}
```

- [ ] **Step 2: Crear `CryptoDeposit.cs`**

```csharp
namespace MLMConquerorGlobalEdition.SharedKernel.Billing;

/// <summary>
/// Claves de <c>GlobalParameter</c> con las direcciones de depósito cripto. Se administran en
/// Admin → Settings → Global Parameters. Una moneda sin dirección (o con dirección vacía) no se
/// ofrece ni en la compra de tokens ni en el alta.
/// </summary>
public static class CryptoDepositKeys
{
    public const string AddressPrefix = "Crypto.DepositAddress.";
    public const string NetworkPrefix = "Crypto.DepositNetwork.";

    public static string AddressKey(string currency) => AddressPrefix + currency.ToUpperInvariant();
    public static string NetworkKey(string currency) => NetworkPrefix + currency.ToUpperInvariant();
}

public sealed class CryptoDepositAddressDto
{
    public string  Currency { get; set; } = string.Empty;
    public string  Address  { get; set; } = string.Empty;

    /// <summary>Red o nota para el que transfiere (p. ej. "TRC-20"). Opcional.</summary>
    public string? Network  { get; set; }
}
```

- [ ] **Step 3: Nueva operación de step-up** — en `StepUpOperations.cs`, debajo de `PayoutMethodChange`:

```csharp
    /// <summary>Pagar tokens con saldo propio: comisiones o eWallet. Tarjeta y cripto no lo piden.</summary>
    public const string TokenPurchaseFromBalance = "member.tokens.purchase-from-balance";
```

y en el `HashSet` `Known`, añadir `TokenPurchaseFromBalance` después de `PayoutMethodChange,` (con coma en la línea anterior).

- [ ] **Step 4: Evento de correo** — en `NotificationEvents.cs`, antes del cierre de la clase:

```csharp
    /// <summary>
    /// Recibo de una compra de tokens completada (o de una compra en cripto confirmada). Sin los
    /// códigos: son al portador. Variables: { FullName, TokenType, Quantity, Amount, PaymentMethod,
    /// PurchaseDate, PurchaseId, OrderNo }. Un fallo de envío se registra y no deshace la compra.
    /// </summary>
    public const string TokenPurchaseReceipt = "TOKEN_PURCHASE_RECEIPT";
```

- [ ] **Step 5: Compilar**

Run: `dotnet build MLMConquerorGlobalEdition.SharedKernel -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedKernel
git commit -m "Contrato compartido de la compra de tokens: tope de 50 al mes, metodos, estados, codigos de error, direcciones de deposito cripto, operacion de step-up para pagar con saldo y evento del recibo"
```

---

### Task 2: Dominio y esquema

**Files:**
- Create: `MLMConquerorGlobalEdition.Domain/Enums/TokenPaymentMethod.cs`, `TokenPurchaseStatus.cs`
- Create: `MLMConquerorGlobalEdition.Domain/Entities/Tokens/TokenPurchase.cs`
- Create: `MLMConquerorGlobalEdition.Domain/Entities/Orders/CryptoPaymentPurpose.cs`
- Modify: `MLMConquerorGlobalEdition.Domain/Entities/Orders/CryptoPaymentConfirmation.cs`
- Modify: `MLMConquerorGlobalEdition.Domain/Entities/Tokens/TokenTransaction.cs`, `TokenType.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Configurations/TokenPurchaseConfiguration.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Configurations/CryptoPaymentConfirmationConfiguration.cs`, `TokenTransactionConfiguration.cs`, `TokenTypeConfiguration.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Context/AppDbContext.cs:105`
- Create: `MLMConquerorGlobalEdition.Repository/Migrations/*_CompraDeTokens.cs` (vía `dotnet ef`, más un `Sql` a mano)
- Test: `MLMConquerorGlobalEdition.Domain.Tests/TokenPurchaseTests.cs`

- [ ] **Step 1: Escribir la prueba (falla: los tipos no existen)**

```csharp
using FluentAssertions;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Tests;

/// <summary>
/// La máquina de estados de una compra de tokens: se completa una sola vez, lo dudoso queda a
/// conciliar sin emitir, y todo lo que está en vuelo ocupa cupo del mes.
/// </summary>
public class TokenPurchaseTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 10, 0, 0);

    [Theory]
    [InlineData(TokenPurchaseStatus.Pending)]
    [InlineData(TokenPurchaseStatus.AwaitingCryptoConfirmation)]
    [InlineData(TokenPurchaseStatus.NeedsReconciliation)]
    public void Lo_que_esta_en_vuelo_se_completa_y_guarda_el_pago(TokenPurchaseStatus desde)
    {
        var p = new TokenPurchase { Status = desde };

        p.Complete("ph-1", Now, "user-1");

        p.Status.Should().Be(TokenPurchaseStatus.Completed);
        p.PaymentHistoryId.Should().Be("ph-1");
        p.CompletedAt.Should().Be(Now);
        p.LastUpdateBy.Should().Be("user-1");
    }

    [Theory]
    [InlineData(TokenPurchaseStatus.Completed)]
    [InlineData(TokenPurchaseStatus.Failed)]
    public void Una_compra_cerrada_no_se_completa_otra_vez(TokenPurchaseStatus status)
    {
        var act = () => new TokenPurchase { Status = status }.Complete("ph-3", Now, "user-1");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Fallan_las_pendientes_y_las_que_estaban_a_conciliar_pero_no_las_de_cripto()
    {
        var pendiente = new TokenPurchase { Status = TokenPurchaseStatus.Pending };
        pendiente.Fail("CARD_CHARGE_FAILED", Now, "user-1");
        pendiente.Status.Should().Be(TokenPurchaseStatus.Failed);
        pendiente.FailureCode.Should().Be("CARD_CHARGE_FAILED");

        var aConciliar = new TokenPurchase { Status = TokenPurchaseStatus.NeedsReconciliation };
        aConciliar.Fail("NOT_CHARGED", Now, "tesoreria@x.com");
        aConciliar.Status.Should().Be(TokenPurchaseStatus.Failed);

        var cripto = new TokenPurchase { Status = TokenPurchaseStatus.AwaitingCryptoConfirmation };
        var act = () => cripto.Fail("X", Now, "user-1");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Solo_una_pendiente_pasa_a_conciliar()
    {
        var p = new TokenPurchase { Status = TokenPurchaseStatus.Pending };

        p.MarkForReconciliation("EWALLET_DEBIT_PENDING", "saldo sin cambios", Now, "user-1");

        p.Status.Should().Be(TokenPurchaseStatus.NeedsReconciliation);
        p.FailureCode.Should().Be("EWALLET_DEBIT_PENDING");
        p.Notes.Should().Be("saldo sin cambios");

        var act = () => new TokenPurchase { Status = TokenPurchaseStatus.Completed }
            .MarkForReconciliation("X", null, Now, "user-1");
        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(TokenPurchaseStatus.Pending, true)]
    [InlineData(TokenPurchaseStatus.AwaitingCryptoConfirmation, true)]
    [InlineData(TokenPurchaseStatus.NeedsReconciliation, true)]
    [InlineData(TokenPurchaseStatus.Completed, false)]
    [InlineData(TokenPurchaseStatus.Failed, false)]
    public void Solo_las_compras_en_vuelo_reservan_cupo(TokenPurchaseStatus status, bool reserva) =>
        new TokenPurchase { Status = status }.IsReservation.Should().Be(reserva);
}
```

Run: `dotnet test MLMConquerorGlobalEdition.Domain.Tests --filter TokenPurchaseTests -nologo`
Expected: FAIL — `TokenPurchase` no existe.

- [ ] **Step 2: Crear los enums**

`Domain/Enums/TokenPaymentMethod.cs`:

```csharp
namespace MLMConquerorGlobalEdition.Domain.Enums;

/// <summary>
/// Con qué se paga una compra de tokens. Los nombres coinciden con
/// <c>SharedKernel.Billing.TokenPaymentMethods</c>, que es lo que viaja por HTTP. Sin SafetyPay.
/// </summary>
public enum TokenPaymentMethod
{
    CommissionBalance = 1,
    EWallet           = 2,
    CreditCard        = 3,
    Crypto            = 4
}
```

`Domain/Enums/TokenPurchaseStatus.cs`:

```csharp
namespace MLMConquerorGlobalEdition.Domain.Enums;

public enum TokenPurchaseStatus
{
    /// <summary>Cupo reservado; el cobro está en curso.</summary>
    Pending = 1,

    /// <summary>Cobrado y con los tokens emitidos.</summary>
    Completed = 2,

    /// <summary>No se cobró. No emitió nada y libera el cupo.</summary>
    Failed = 3,

    /// <summary>Pago en cripto esperando que un administrador confirme la transferencia.</summary>
    AwaitingCryptoConfirmation = 4,

    /// <summary>
    /// No se sabe si el dinero salió (débito eWallet en PROCESSING/PENDING o sin respuesta clara).
    /// Sin tokens, con el cupo reservado, hasta que tesorería la cierre.
    /// </summary>
    NeedsReconciliation = 5
}
```

- [ ] **Step 3: Crear `Domain/Entities/Tokens/TokenPurchase.cs`**

```csharp
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Tokens;

/// <summary>
/// Una compra de tokens desde el BizCenter. Es a la vez la RESERVA de cupo del mes (mientras está
/// en vuelo cuenta contra el tope aunque no haya tokens), la IDEMPOTENCIA ((MemberId,
/// IdempotencyKey) es único) y el VÍNCULO entre pedido, pago y códigos emitidos
/// (TokenTransaction.TokenPurchaseId). Su Id es también el MerchantReferenceID del débito eWallet.
///
/// No reembolsable: Completed no tiene salida.
/// </summary>
public class TokenPurchase : AuditChangesStringKey
{
    public string MemberId { get; set; } = string.Empty;
    public int TokenTypeId { get; set; }

    /// <summary>Productos otorgados en el momento de la compra, separados por coma. De ahí salió el precio.</summary>
    public string GrantedProductIds { get; set; } = string.Empty;

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public TokenPaymentMethod PaymentMethod { get; set; }
    public TokenPurchaseStatus Status { get; set; } = TokenPurchaseStatus.Pending;
    public string OrderId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>yyyyMM del mes al que se imputa la reserva (hora de servidor).</summary>
    public int PeriodKey { get; set; }

    public string? CreditCardId { get; set; }
    public string? PaymentHistoryId { get; set; }
    public string? FailureCode { get; set; }

    /// <summary>Detalle para tesorería: por qué quedó a conciliar o cómo se cerró.</summary>
    public string? Notes { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>¿Ocupa cupo del mes sin haber emitido? No se mapea.</summary>
    public bool IsReservation => Status is TokenPurchaseStatus.Pending
                                        or TokenPurchaseStatus.AwaitingCryptoConfirmation
                                        or TokenPurchaseStatus.NeedsReconciliation;

    public void Complete(string paymentHistoryId, DateTime now, string actor)
    {
        if (!IsReservation)
            throw new InvalidOperationException($"La compra {Id} está en {Status} y no se puede completar.");

        Status           = TokenPurchaseStatus.Completed;
        PaymentHistoryId = paymentHistoryId;
        CompletedAt      = now;
        LastUpdateDate   = now;
        LastUpdateBy     = actor;
    }

    /// <summary>Falla lo que se intentó cobrar y no entró, o lo que tesorería da por no cobrado.</summary>
    public void Fail(string failureCode, DateTime now, string actor)
    {
        if (Status is not (TokenPurchaseStatus.Pending or TokenPurchaseStatus.NeedsReconciliation))
            throw new InvalidOperationException($"La compra {Id} está en {Status} y no puede marcarse fallida.");

        Status         = TokenPurchaseStatus.Failed;
        FailureCode    = failureCode;
        LastUpdateDate = now;
        LastUpdateBy   = actor;
    }

    /// <summary>El cobro tuvo un resultado dudoso: ni tokens ni reintento, a revisión.</summary>
    public void MarkForReconciliation(string reasonCode, string? detail, DateTime now, string actor)
    {
        if (Status != TokenPurchaseStatus.Pending)
            throw new InvalidOperationException($"La compra {Id} está en {Status} y no puede pasar a conciliación.");

        Status         = TokenPurchaseStatus.NeedsReconciliation;
        FailureCode    = reasonCode;
        Notes          = detail;
        LastUpdateDate = now;
        LastUpdateBy   = actor;
    }
}
```

- [ ] **Step 4: `CryptoPaymentPurpose`, `Purpose`, `TokenPurchaseId`, `IsPurchasable`**

`Domain/Entities/Orders/CryptoPaymentPurpose.cs`:

```csharp
namespace MLMConquerorGlobalEdition.Domain.Entities.Orders;

/// <summary>
/// Qué se cobra en cripto. El alta la confirma SignupAPI (activa la membresía y genera comisiones);
/// la compra de tokens la confirma Billing (emite los códigos).
/// </summary>
public enum CryptoPaymentPurpose
{
    Signup        = 1,
    TokenPurchase = 2
}
```

En `CryptoPaymentConfirmation.cs`, después de `AmountDue`:

```csharp
    /// <summary>Qué se cobra. Las filas anteriores a la compra de tokens son todas altas.</summary>
    public CryptoPaymentPurpose Purpose { get; set; } = CryptoPaymentPurpose.Signup;
```

En `TokenTransaction.cs`, después de `UsedOnOrderId`:

```csharp
    /// <summary>
    /// La compra que emitió esta instancia (Plan F). Null para las emitidas por alta, renovación o
    /// administración. Permite devolver los mismos códigos cuando una compra se repite con su clave.
    /// </summary>
    public string? TokenPurchaseId { get; set; }
```

En `TokenType.cs`, después de `IsActive`:

```csharp
    /// <summary>
    /// ¿Se vende en el BizCenter? Decisión de negocio por tipo, no derivable de otros datos: las
    /// variantes FREE / Coupon / SpecialPromo / Help a Friend otorgan los mismos productos, con los
    /// mismos precios, que las de pago. Por defecto false: un tipo nuevo no se vende hasta que un
    /// administrador lo decide en Admin → Tokens → Token Types.
    /// </summary>
    public bool IsPurchasable { get; set; }
```

- [ ] **Step 5: Correr la prueba de dominio**

Run: `dotnet test MLMConquerorGlobalEdition.Domain.Tests --filter TokenPurchaseTests -nologo`
Expected: PASS.

- [ ] **Step 6: Configuraciones**

`Repository/Configurations/TokenPurchaseConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class TokenPurchaseConfiguration : IEntityTypeConfiguration<TokenPurchase>
{
    public void Configure(EntityTypeBuilder<TokenPurchase> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.GrantedProductIds).IsRequired().HasMaxLength(400);
        builder.Property(x => x.OrderId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(64); // TokenPurchaseRules.IdempotencyKeyMaxLength
        builder.Property(x => x.UnitPrice).HasPrecision(18, 4);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 4);
        builder.Property(x => x.PaymentMethod).HasConversion<int>();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.CreditCardId).HasMaxLength(450);
        builder.Property(x => x.PaymentHistoryId).HasMaxLength(450);
        builder.Property(x => x.FailureCode).HasMaxLength(64);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Ignore(x => x.IsReservation);

        // La idempotencia vive en la base, no solo en el código.
        builder.HasIndex(x => new { x.MemberId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => x.OrderId).IsUnique();
        builder.HasIndex(x => new { x.MemberId, x.PeriodKey, x.Status });
        builder.HasIndex(x => x.Status);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
```

En `CryptoPaymentConfirmationConfiguration.cs`, después de `builder.Property(x => x.CreatedBy)...`:

```csharp
        // Las filas que ya existen son altas: el valor por defecto de la columna las deja así.
        builder.Property(x => x.Purpose)
               .HasConversion<int>()
               .HasDefaultValue(CryptoPaymentPurpose.Signup);
        builder.HasIndex(x => new { x.Purpose, x.Status, x.CreationDate });
```

En `TokenTransactionConfiguration.cs`, antes del cierre de `Configure`:

```csharp
        builder.Property(x => x.TokenPurchaseId).HasMaxLength(36);
        builder.HasIndex(x => x.TokenPurchaseId).HasFilter("[TokenPurchaseId] IS NOT NULL");

        // El tope de 50 al mes cuenta por dueño original, tipo de movimiento y fecha.
        builder.HasIndex(x => new { x.OriginalOwnerMemberId, x.TransactionType, x.CreationDate });
```

En `TokenTypeConfiguration.cs`, dentro de `Configure` antes del `HasData`:

```csharp
        builder.Property(x => x.IsPurchasable).HasDefaultValue(false);
```

`AppDbContext.cs`, debajo de `TokenTransactions` (~:105):

```csharp
    public DbSet<TokenPurchase> TokenPurchases => Set<TokenPurchase>();
```

- [ ] **Step 7: Generar la migración**

```bash
dotnet ef migrations add CompraDeTokens \
  --project MLMConquerorGlobalEdition.Repository \
  --startup-project MLMConquerorGlobalEdition.Billing
```

Revisar el `Up`: solo debe tener `CreateTable("TokenPurchases")`, `AddColumn<int>("Purpose", "CryptoPaymentConfirmations", defaultValue: 1)`, `AddColumn<string>("TokenPurchaseId", "TokenTransactions")`, `AddColumn<bool>("IsPurchasable", "TokenTypes", defaultValue: false)` y los índices. Si aparece otra cosa, el modelo traía deriva: parar y avisar.

Añadir **a mano al final del `Up`** la regla inicial de D1 (los `HasData` de `TokenTypeConfiguration` no se tocan: el valor inicial es de datos, no de modelo):

```csharp
            // Valor inicial de IsPurchasable (Plan F, decisión D1). Nada en los datos distingue las
            // variantes promocionales salvo el nombre, así que se usa UNA vez para inicializar; desde
            // aquí manda la columna, que el admin cambia en Token Types. Upgrade (Category = 2) no
            // otorga productos y no se vende de todas formas.
            migrationBuilder.Sql(@"
UPDATE TokenTypes
   SET IsPurchasable = 1
 WHERE IsActive = 1
   AND Category <> 2
   AND Name NOT LIKE '%FREE%'
   AND Name NOT LIKE '%Coupon%'
   AND Name NOT LIKE '%SpecialPromo%'
   AND Name NOT LIKE '%Help a Friend%';");
```

Verificar:

Run: `grep -c "TokenPurchases\|Purpose\|TokenPurchaseId\|IsPurchasable" MLMConquerorGlobalEdition.Repository/Migrations/*_CompraDeTokens.cs`
Expected: > 0.

- [ ] **Step 8: Compilar**

Run: `dotnet build MLMConquerorGlobalEdition.Repository -v q -nologo`
Expected: `0 Error(s)`

- [ ] **Step 9: Commit**

```bash
git add MLMConquerorGlobalEdition.Domain MLMConquerorGlobalEdition.Domain.Tests MLMConquerorGlobalEdition.Repository
git commit -m "Compra de tokens en el dominio: TokenPurchase reserva cupo, guarda la clave de idempotencia y puede quedar a conciliar; el cobro en cripto distingue alta de compra, cada token comprado apunta a su compra y TokenType.IsPurchasable decide que se vende, inicializado excluyendo las variantes promocionales"
```

---
### Task 3: Parámetros globales — CRUD en el admin y direcciones de depósito cripto

**Files:**
- Create: `MLMConquerorGlobalEdition.AdminAPI/DTOs/GlobalParameters/GlobalParameterDtos.cs`
- Create: `MLMConquerorGlobalEdition.AdminAPI/DTOs/GlobalParameters/Validators/GlobalParameterValidators.cs`
- Create: `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminGlobalParametersController.cs`
- Create: `MLMConquerorGlobalEdition.Repository/Services/Crypto/CryptoDepositAddressReader.cs`
- Create: `MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminGlobalParameters.razor`
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Components/Layout/AdminSidebarMenu.razor` (bloque `SuperAdmin,Admin` de ~:99)
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Features/GlobalParameters/AdminGlobalParametersControllerTests.cs`
- Test: `MLMConquerorGlobalEdition.Billing.Tests/Services/Tokens/CryptoDepositAddressReaderTests.cs`

> **Caché:** hoy nadie cachea `GlobalParameters` — `CommissionBalanceService`, `RecurringBillingPlanner`, `PayoutReceiptService`, `RankQualificationService` y `DailyResidualConsolidationJob` leen la tabla en cada uso (`AsNoTracking().FirstOrDefaultAsync(p => p.Key == …)`). No hay nada que invalidar; si algún día se cachea, el `PUT` de este controlador es el sitio donde hacerlo.

- [ ] **Step 1: Escribir las pruebas (fallan: el controlador y el lector no existen)**

`AdminAPI.Tests/Features/GlobalParameters/AdminGlobalParametersControllerTests.cs`:

```csharp
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.GlobalParameters;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.GlobalParameters;

/// <summary>
/// El CRUD de parámetros globales: solo SuperAdmin y Admin, rastro de quién y cuándo, la clave no
/// cambia una vez creada, y las direcciones de depósito cripto se validan porque cambiarlas desvía
/// dinero.
/// </summary>
public class AdminGlobalParametersControllerTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 10, 0, 0);

    private static AdminGlobalParametersController Build(AppDbContext db)
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.Email).Returns("admin@x.com");
        user.Setup(u => u.UserId).Returns("admin-1");
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.Now).Returns(Now);
        return new AdminGlobalParametersController(db, user.Object, clock.Object,
            NullLogger<AdminGlobalParametersController>.Instance);
    }

    [Fact]
    public void Solo_SuperAdmin_y_Admin()
        => typeof(AdminGlobalParametersController).GetCustomAttribute<AuthorizeAttribute>()!.Roles
            .Should().Be(AppRoles.SuperAdmin + "," + AppRoles.Admin);

    [Fact]
    public async Task Crear_deja_el_rastro_de_quien_y_cuando()
    {
        await using var db = InMemoryDbHelper.Create();

        var r = await Build(db).Create(new CreateGlobalParameterRequest
        {
            Key = "Crypto.DepositAddress.BTC", Value = " bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh ", Description = "Cold wallet"
        }, default);

        r.Should().BeOfType<CreatedAtActionResult>();
        var p = db.GlobalParameters.Single();
        p.Value.Should().Be("bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh");
        p.CreatedBy.Should().Be("admin@x.com");
        p.CreationDate.Should().Be(Now);
    }

    [Fact]
    public async Task Una_clave_repetida_es_409()
    {
        await using var db = InMemoryDbHelper.Create();
        db.GlobalParameters.Add(new GlobalParameter { Key = "Foo", Value = "1", CreatedBy = "seed", CreationDate = Now });
        await db.SaveChangesAsync();

        var r = await Build(db).Create(new CreateGlobalParameterRequest { Key = "Foo", Value = "2" }, default);

        r.Should().BeOfType<ConflictObjectResult>();
    }

    [Theory]
    [InlineData("Crypto.DepositAddress.BTC", "no es una direccion")]
    [InlineData("Crypto.DepositAddress.DOGE", "DH5yaieqoZN36fDVciNyRueRGvGLR3mr7L")]
    public async Task Una_direccion_cripto_invalida_o_de_moneda_no_admitida_es_400(string key, string value)
    {
        await using var db = InMemoryDbHelper.Create();

        var r = await Build(db).Create(new CreateGlobalParameterRequest { Key = key, Value = value }, default);

        r.Should().BeOfType<BadRequestObjectResult>();
        db.GlobalParameters.Should().BeEmpty();
    }

    [Fact]
    public async Task Actualizar_cambia_el_valor_y_el_rastro_pero_no_la_clave()
    {
        await using var db = InMemoryDbHelper.Create();
        var p = new GlobalParameter { Key = "DailyResidualConsolidationMinimum", Value = "100", CreatedBy = "seed", CreationDate = Now.AddDays(-5) };
        db.GlobalParameters.Add(p);
        await db.SaveChangesAsync();

        var r = await Build(db).Update(p.Id, new UpdateGlobalParameterRequest { Value = "150", Description = "subido" }, default);

        r.Should().BeOfType<OkObjectResult>();
        var saved = db.GlobalParameters.Single();
        saved.Key.Should().Be("DailyResidualConsolidationMinimum");
        saved.Value.Should().Be("150");
        saved.LastUpdateBy.Should().Be("admin@x.com");
        saved.LastUpdateDate.Should().Be(Now);
    }

    [Fact]
    public async Task Listar_filtra_por_prefijo()
    {
        await using var db = InMemoryDbHelper.Create();
        db.GlobalParameters.AddRange(
            new GlobalParameter { Key = "Crypto.DepositAddress.BTC", Value = "x", CreatedBy = "seed", CreationDate = Now },
            new GlobalParameter { Key = "Foo", Value = "1", CreatedBy = "seed", CreationDate = Now });
        await db.SaveChangesAsync();

        var r = await Build(db).List("Crypto.", default);

        ((OkObjectResult)r).Value.Should().BeOfType<ApiResponse<IEnumerable<GlobalParameterDto>>>()
            .Which.Data!.Select(d => d.Key).Should().Equal("Crypto.DepositAddress.BTC");
    }
}
```

`Billing.Tests/Services/Tokens/CryptoDepositAddressReaderTests.cs`:

```csharp
using FluentAssertions;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Repository.Services.Crypto;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Tokens;

/// <summary>Solo se ofrecen las monedas candidatas con dirección no vacía.</summary>
public class CryptoDepositAddressReaderTests
{
    [Fact]
    public async Task Devuelve_las_monedas_con_direccion_y_su_red()
    {
        var db = TestDbContextFactory.Create();
        GlobalParameter P(string k, string v) => new() { Key = k, Value = v, CreatedBy = "seed", CreationDate = DateTime.Now };
        db.GlobalParameters.AddRange(
            P("Crypto.DepositAddress.BTC", " bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh "),
            P("Crypto.DepositAddress.ETH", ""),
            P("Crypto.DepositAddress.USDT", "TQ5NkEQwK6x9C2iGBDbTj9JxF8tqmL7Ak5"),
            P("Crypto.DepositNetwork.USDT", "TRC-20"),
            P("Crypto.DepositAddress.DOGE", "DH5yaieqoZN36fDVciNyRueRGvGLR3mr7L"));
        await db.SaveChangesAsync();

        var list = await new CryptoDepositAddressReader(db).GetConfiguredAsync();

        list.Select(d => d.Currency).Should().Equal("BTC", "USDT");
        list[0].Address.Should().Be("bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh");
        list[0].Network.Should().BeNull();
        list[1].Network.Should().Be("TRC-20");
    }
}
```

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter AdminGlobalParametersControllerTests -nologo` y `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter CryptoDepositAddressReaderTests -nologo`
Expected: FAIL — los tipos no existen.

- [ ] **Step 2: DTOs, reglas y validadores**

`AdminAPI/DTOs/GlobalParameters/GlobalParameterDtos.cs`:

```csharp
using System.Text.RegularExpressions;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.GlobalParameters;

public record GlobalParameterDto(
    int Id, string Key, string Value, string? Description,
    DateTime CreationDate, string CreatedBy, DateTime? LastUpdateDate, string? LastUpdateBy);

public class CreateGlobalParameterRequest
{
    public string  Key         { get; set; } = string.Empty;
    public string  Value       { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>La clave no se cambia: se lee por nombre desde el código, y renombrarla la "borraría".</summary>
public class UpdateGlobalParameterRequest
{
    public string  Value       { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// Reglas de valor que dependen de la clave. Las direcciones de depósito se validan con cuidado
/// porque cambiar una desvía el dinero de los miembros a otra cartera.
/// </summary>
public static class GlobalParameterRules
{
    public const string KeyPattern = @"^[A-Za-z0-9._-]{1,200}$";
    public const int ValueMaxLength = 2000;

    // Bech32 (bc1…), base58 (1…/3…/T…) y hex con 0x quedan dentro de esto; lo que no, se rechaza.
    private static readonly Regex DepositAddress = new(@"^(0x)?[A-Za-z0-9]{20,128}$", RegexOptions.Compiled);

    public static string? ValueError(string key, string? value)
    {
        if (value is null) return "Value is required.";
        if (value.Length > ValueMaxLength) return $"Value must be at most {ValueMaxLength} characters.";

        if (key.StartsWith(CryptoDepositKeys.AddressPrefix, StringComparison.Ordinal))
        {
            var currency = key[CryptoDepositKeys.AddressPrefix.Length..];
            if (!TokenPurchaseRules.CryptoCurrencies.Contains(currency))
                return $"Unsupported currency '{currency}'. Use one of: {string.Join(", ", TokenPurchaseRules.CryptoCurrencies)}.";

            // Vacío está permitido: es la forma de dejar de ofrecer esa moneda.
            if (value.Trim().Length > 0 && !DepositAddress.IsMatch(value.Trim()))
                return "The value is not a valid deposit address.";
        }

        if (key.StartsWith(CryptoDepositKeys.NetworkPrefix, StringComparison.Ordinal) && value.Trim().Length > 50)
            return "The network note must be at most 50 characters.";

        return null;
    }
}
```

`AdminAPI/DTOs/GlobalParameters/Validators/GlobalParameterValidators.cs`:

```csharp
using FluentValidation;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.GlobalParameters.Validators;

public class CreateGlobalParameterRequestValidator : AbstractValidator<CreateGlobalParameterRequest>
{
    public CreateGlobalParameterRequestValidator()
    {
        RuleFor(x => x.Key).NotEmpty().Matches(GlobalParameterRules.KeyPattern)
            .WithMessage("Key may only contain letters, digits, dots, dashes and underscores (max 200).");
        RuleFor(x => x.Value).NotNull().MaximumLength(GlobalParameterRules.ValueMaxLength);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public class UpdateGlobalParameterRequestValidator : AbstractValidator<UpdateGlobalParameterRequest>
{
    public UpdateGlobalParameterRequestValidator()
    {
        RuleFor(x => x.Value).NotNull().MaximumLength(GlobalParameterRules.ValueMaxLength);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
```

- [ ] **Step 3: Crear `AdminAPI/Controllers/AdminGlobalParametersController.cs`**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.GlobalParameters;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Alta, consulta y edición de <see cref="GlobalParameter"/>. Hasta ahora cada parámetro tenía su
/// propia pantalla (o ninguna); este es el CRUD genérico. Sin borrado: los parámetros los lee el
/// código por nombre, y quitar uno es cambiar el comportamiento sin que nadie lo vea. Para "apagar"
/// una dirección de depósito se deja el valor vacío.
/// </summary>
/// <remarks>
/// Nadie cachea esta tabla hoy (todos los lectores consultan la base en cada uso), así que guardar
/// basta para que el cambio se vea. Los cambios de direcciones de depósito se registran como aviso
/// con el valor viejo y el nuevo: la tabla no guarda historial (riesgo R8 del Plan F).
/// </remarks>
[ApiController]
[Route("api/v1/admin/global-parameters")]
[Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.Admin)]
public class AdminGlobalParametersController : ControllerBase
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _user;
    private readonly IDateTimeProvider   _clock;
    private readonly ILogger<AdminGlobalParametersController> _logger;

    public AdminGlobalParametersController(
        AppDbContext db, ICurrentUserService user, IDateTimeProvider clock,
        ILogger<AdminGlobalParametersController> logger)
    {
        _db     = db;
        _user   = user;
        _clock  = clock;
        _logger = logger;
    }

    /// <summary>GET /api/v1/admin/global-parameters?prefix=Crypto.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? prefix, CancellationToken ct)
    {
        var query = _db.GlobalParameters.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(prefix))
            query = query.Where(p => p.Key.StartsWith(prefix));

        var rows = await query.OrderBy(p => p.Key).ToListAsync(ct);
        return Ok(ApiResponse<IEnumerable<GlobalParameterDto>>.Ok(rows.Select(ToDto)));
    }

    /// <summary>GET /api/v1/admin/global-parameters/{id}</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var p = await _db.GlobalParameters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return p is null
            ? NotFound(ApiResponse<GlobalParameterDto>.Fail("GLOBAL_PARAMETER_NOT_FOUND", $"Parameter {id} not found."))
            : Ok(ApiResponse<GlobalParameterDto>.Ok(ToDto(p)));
    }

    /// <summary>POST /api/v1/admin/global-parameters</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGlobalParameterRequest request, CancellationToken ct)
    {
        var key   = request.Key.Trim();
        var value = request.Value?.Trim() ?? string.Empty;

        var error = GlobalParameterRules.ValueError(key, value);
        if (error is not null)
            return BadRequest(ApiResponse<GlobalParameterDto>.Fail("GLOBAL_PARAMETER_INVALID", error));

        if (await _db.GlobalParameters.AnyAsync(p => p.Key == key, ct))
            return Conflict(ApiResponse<GlobalParameterDto>.Fail("GLOBAL_PARAMETER_EXISTS", $"Parameter '{key}' already exists."));

        var now = _clock.Now;
        var entity = new GlobalParameter
        {
            Key            = key,
            Value          = value,
            Description    = request.Description?.Trim(),
            CreatedBy      = Actor,
            CreationDate   = now,
            LastUpdateDate = now,
            LastUpdateBy   = Actor
        };
        _db.GlobalParameters.Add(entity);
        await _db.SaveChangesAsync(ct);

        WarnIfDeposit(key, oldValue: null, newValue: value);
        return CreatedAtAction(nameof(Get), new { id = entity.Id }, ApiResponse<GlobalParameterDto>.Ok(ToDto(entity)));
    }

    /// <summary>PUT /api/v1/admin/global-parameters/{id} — valor y descripción; la clave no cambia.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateGlobalParameterRequest request, CancellationToken ct)
    {
        var entity = await _db.GlobalParameters.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<GlobalParameterDto>.Fail("GLOBAL_PARAMETER_NOT_FOUND", $"Parameter {id} not found."));

        var value = request.Value?.Trim() ?? string.Empty;
        var error = GlobalParameterRules.ValueError(entity.Key, value);
        if (error is not null)
            return BadRequest(ApiResponse<GlobalParameterDto>.Fail("GLOBAL_PARAMETER_INVALID", error));

        var oldValue = entity.Value;
        entity.Value          = value;
        entity.Description    = request.Description?.Trim();
        entity.LastUpdateDate = _clock.Now;
        entity.LastUpdateBy   = Actor;
        await _db.SaveChangesAsync(ct);

        WarnIfDeposit(entity.Key, oldValue, value);
        return Ok(ApiResponse<GlobalParameterDto>.Ok(ToDto(entity)));
    }

    private string Actor
    {
        get
        {
            var who = !string.IsNullOrWhiteSpace(_user.Email) ? _user.Email : _user.UserId;
            return who.Length <= 100 ? who : who[..100];
        }
    }

    private void WarnIfDeposit(string key, string? oldValue, string newValue)
    {
        if (!key.StartsWith(CryptoDepositKeys.AddressPrefix, StringComparison.Ordinal)) return;
        _logger.LogWarning(
            "Dirección de depósito cripto cambiada: {Key} de '{Old}' a '{New}' por {Actor}.",
            key, oldValue ?? "(nueva)", newValue, Actor);
    }

    private static GlobalParameterDto ToDto(GlobalParameter p) =>
        new(p.Id, p.Key, p.Value, p.Description, p.CreationDate, p.CreatedBy, p.LastUpdateDate, p.LastUpdateBy);
}
```

- [ ] **Step 4: Crear el lector `Repository/Services/Crypto/CryptoDepositAddressReader.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Repository.Services.Crypto;

/// <summary>
/// Las direcciones de depósito configuradas en GlobalParameters. Lo usan Billing (compra de tokens)
/// y SignupAPI (alta): una sola lectura para que los dos ofrezcan las mismas monedas.
/// </summary>
public interface ICryptoDepositAddressReader
{
    /// <summary>Monedas candidatas con dirección no vacía, en el orden de TokenPurchaseRules.CryptoCurrencies.</summary>
    Task<IReadOnlyList<CryptoDepositAddressDto>> GetConfiguredAsync(CancellationToken ct = default);
}

public sealed class CryptoDepositAddressReader : ICryptoDepositAddressReader
{
    private readonly AppDbContext _db;

    public CryptoDepositAddressReader(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<CryptoDepositAddressDto>> GetConfiguredAsync(CancellationToken ct = default)
    {
        var keys = TokenPurchaseRules.CryptoCurrencies
            .SelectMany(c => new[] { CryptoDepositKeys.AddressKey(c), CryptoDepositKeys.NetworkKey(c) })
            .ToList();

        var values = await _db.GlobalParameters.AsNoTracking()
            .Where(p => keys.Contains(p.Key))
            .ToDictionaryAsync(p => p.Key, p => p.Value, ct);

        var result = new List<CryptoDepositAddressDto>();
        foreach (var currency in TokenPurchaseRules.CryptoCurrencies)
        {
            if (!values.TryGetValue(CryptoDepositKeys.AddressKey(currency), out var address)
                || string.IsNullOrWhiteSpace(address))
                continue;

            values.TryGetValue(CryptoDepositKeys.NetworkKey(currency), out var network);
            result.Add(new CryptoDepositAddressDto
            {
                Currency = currency,
                Address  = address.Trim(),
                Network  = string.IsNullOrWhiteSpace(network) ? null : network.Trim()
            });
        }
        return result;
    }
}
```

- [ ] **Step 5: Correr las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter AdminGlobalParametersControllerTests -nologo` → PASS.
Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter CryptoDepositAddressReaderTests -nologo` → PASS.

- [ ] **Step 6: Crear `AdminWeb/Components/Pages/AdminGlobalParameters.razor`**

```razor
@page "/admin/settings/global-parameters"
@rendermode @(new InteractiveServerRenderMode(prerender: false))
@attribute [Microsoft.AspNetCore.Authorization.Authorize(Roles = "SuperAdmin,Admin")]
@inject HttpClient Http

@*
    Parámetros globales del sistema. Incluye las direcciones de depósito cripto
    (Crypto.DepositAddress.BTC / ETH / USDT y Crypto.DepositNetwork.*): una moneda sin dirección no
    se ofrece ni en la compra de tokens ni en el alta. Cambiar una dirección desvía pagos: revisar dos
    veces. El servidor valida el formato y registra el cambio.
*@

<PageTitle>Global Parameters — Admin</PageTitle>

<BreadcrumbNav Items="@(new List<BreadcrumbNav.BreadcrumbItem> {
    new("Admin Dashboard", "/admin"), new("Settings"), new("Global Parameters") })" />

@if (_alertMsg is not null)
{
    <div class="alert @(_alertIsError ? "alert-danger" : "alert-success") alert-dismissible mb-3">
        @_alertMsg <button type="button" class="btn-close" @onclick="() => _alertMsg = null"></button>
    </div>
}

<div class="alert alert-info small">
    Crypto deposit addresses: <code>Crypto.DepositAddress.BTC</code>, <code>Crypto.DepositAddress.ETH</code>,
    <code>Crypto.DepositAddress.USDT</code>, with an optional network note in <code>Crypto.DepositNetwork.&lt;CURRENCY&gt;</code>
    (e.g. <em>TRC-20</em>). Leave an address empty to stop offering that currency.
</div>

<div class="card border-0 shadow-sm">
    <div class="card-header py-3 d-flex justify-content-between align-items-center">
        <h5 class="mb-0 fw-bold">Global parameters</h5>
        <button class="btn btn-sm btn-primary" @onclick="OpenCreate"><i class="ri-add-line"></i> New</button>
    </div>
    <div class="card-body p-0">
        @if (_loading)
        {
            <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
        }
        else
        {
            <div class="table-responsive">
                <table class="table align-middle mb-0">
                    <thead><tr><th>Key</th><th>Value</th><th>Description</th><th>Last update</th><th></th></tr></thead>
                    <tbody>
                        @foreach (var p in _items)
                        {
                            var row = p;
                            <tr>
                                <td class="font-monospace small">@row.Key</td>
                                <td class="font-monospace small text-break">@row.Value</td>
                                <td class="small">@row.Description</td>
                                <td class="small text-muted">@((row.LastUpdateDate ?? row.CreationDate).ToString("yyyy-MM-dd HH:mm")) · @(row.LastUpdateBy ?? row.CreatedBy)</td>
                                <td><button class="btn btn-sm btn-outline-secondary py-0" @onclick="() => OpenEdit(row)">Edit</button></td>
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
        }
    </div>
</div>

@if (_showForm)
{
    <div class="modal d-block" tabindex="-1" style="background:rgba(0,0,0,.45)">
        <div class="modal-dialog modal-dialog-centered">
            <div class="modal-content">
                <div class="modal-header">
                    <h5 class="modal-title">@(_editId is null ? "New parameter" : "Edit parameter")</h5>
                    <button type="button" class="btn-close" @onclick="CloseForm" disabled="@_saving"></button>
                </div>
                <div class="modal-body">
                    <label class="form-label small fw-semibold">Key</label>
                    <input class="form-control form-control-sm mb-3 font-monospace" @bind="_key" disabled="@(_editId is not null)" maxlength="200" />
                    <label class="form-label small fw-semibold">Value</label>
                    <textarea class="form-control form-control-sm mb-3 font-monospace" rows="2" @bind="_value" maxlength="2000"></textarea>
                    <label class="form-label small fw-semibold">Description</label>
                    <input class="form-control form-control-sm" @bind="_description" maxlength="500" />
                    @if (_key.StartsWith("Crypto.DepositAddress."))
                    {
                        <div class="alert alert-warning py-2 small mt-3 mb-0">
                            Members will send money to this address. Double-check it against the wallet before saving.
                        </div>
                    }
                </div>
                <div class="modal-footer">
                    <button class="btn btn-secondary" @onclick="CloseForm" disabled="@_saving">Cancel</button>
                    <button class="btn btn-primary" @onclick="SaveAsync" disabled="@(_saving || string.IsNullOrWhiteSpace(_key))">
                        @if (_saving) { <span class="spinner-border spinner-border-sm me-1"></span> }
                        Save
                    </button>
                </div>
            </div>
        </div>
    </div>
}

@code {
    private List<ParamDto> _items = new();
    private bool _loading = true, _saving, _showForm, _alertIsError;
    private string? _alertMsg;
    private int? _editId;
    private string _key = string.Empty, _value = string.Empty;
    private string? _description;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var resp = await Http.GetFromJsonAsync<ApiResp<List<ParamDto>>>("api/v1/admin/global-parameters");
            _items = resp?.Data ?? new();
        }
        catch (Exception ex) { _alertMsg = $"Error: {ex.Message}"; _alertIsError = true; }
        finally { _loading = false; }
    }

    private void OpenCreate() { _editId = null; _key = string.Empty; _value = string.Empty; _description = null; _showForm = true; }
    private void OpenEdit(ParamDto p) { _editId = p.Id; _key = p.Key; _value = p.Value; _description = p.Description; _showForm = true; }
    private void CloseForm() => _showForm = false;

    private async Task SaveAsync()
    {
        _saving = true;
        try
        {
            var resp = _editId is null
                ? await Http.PostAsJsonAsync("api/v1/admin/global-parameters", new { Key = _key.Trim(), Value = _value, Description = _description })
                : await Http.PutAsJsonAsync($"api/v1/admin/global-parameters/{_editId}", new { Value = _value, Description = _description });

            if (resp.IsSuccessStatusCode)
            {
                _alertMsg = "Parameter saved."; _alertIsError = false;
                CloseForm();
                await LoadAsync();
            }
            else
            {
                var body = await resp.Content.ReadFromJsonAsync<ApiResp<object>>();
                _alertMsg = body?.Errors?.FirstOrDefault() ?? body?.Message ?? $"Save failed ({(int)resp.StatusCode}).";
                _alertIsError = true;
            }
        }
        catch (Exception ex) { _alertMsg = $"Error: {ex.Message}"; _alertIsError = true; }
        finally { _saving = false; }
    }

    private record ApiResp<T>(bool Success, T? Data, string? Message, IEnumerable<string>? Errors);
    private record ParamDto(int Id, string Key, string Value, string? Description,
        DateTime CreationDate, string CreatedBy, DateTime? LastUpdateDate, string? LastUpdateBy);
}
```

- [ ] **Step 7: Menú** — en `AdminSidebarMenu.razor`, en el bloque `<AuthorizeView Roles="SuperAdmin,Admin">` de ~:99, reemplazar:

```razor
            <Authorized>
                @* ── Rank Reports ── *@
```

por:

```razor
            <Authorized>
                <li class="menu-item @(IsActive("/admin/settings/global-parameters") == "active" ? "active" : "")">
                    <a href="/admin/settings/global-parameters" class="menu-link @IsActive("/admin/settings/global-parameters")">
                        <i class="ri-settings-4-line menu-icon"></i>
                        <span class="title">Global Parameters</span>
                    </a>
                </li>
                @* ── Rank Reports ── *@
```

- [ ] **Step 8: Compilar**

Run: `dotnet build MLMConquerorGlobalEdition.AdminWeb -v q -nologo` → `0 Error(s)`.

- [ ] **Step 9: Commit**

```bash
git add MLMConquerorGlobalEdition.AdminAPI MLMConquerorGlobalEdition.AdminAPI.Tests MLMConquerorGlobalEdition.Repository MLMConquerorGlobalEdition.Billing.Tests MLMConquerorGlobalEdition.AdminWeb MLMConquerorGlobalEdition.SharedComponents/Components/Layout/AdminSidebarMenu.razor
git commit -m "CRUD de parametros globales en el panel (SuperAdmin y Admin) con rastro de quien y cuando, y lector de las direcciones de deposito cripto: una moneda sin direccion no se ofrece"
```

---

### Task 4: Billing — identidad del miembro, precio y cupo

**Files:**
- Modify: `MLMConquerorGlobalEdition.Billing/Services/CurrentUserService.cs`
- Create: `MLMConquerorGlobalEdition.Billing/Services/Tokens/TokenPricingService.cs`
- Create: `MLMConquerorGlobalEdition.Billing/Services/Tokens/TokenMonthlyQuota.cs`
- Test: `MLMConquerorGlobalEdition.Billing.Tests/Services/CurrentUserServiceTests.cs`
- Test: `MLMConquerorGlobalEdition.Billing.Tests/Services/Tokens/TokenPricingServiceTests.cs`
- Test: `MLMConquerorGlobalEdition.Billing.Tests/Services/Tokens/TokenMonthlyQuotaTests.cs`

- [ ] **Step 1: Pruebas de identidad (fallan: hoy lee `member_id`)**

```csharp
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services;

/// <summary>
/// Billing leía el claim "member_id", pero JwtService emite "memberId": MemberId salía siempre vacío.
/// </summary>
public class CurrentUserServiceTests
{
    private static CurrentUserService Con(params Claim[] claims) =>
        new(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "prueba")) }
        });

    [Fact]
    public void Lee_el_memberId_con_el_nombre_que_emite_JwtService()
    {
        var sut = Con(new Claim(ClaimTypes.NameIdentifier, "user-1"), new Claim(CallerIdentity.MemberIdClaim, "AMB-1"));

        sut.MemberId.Should().Be("AMB-1");
        sut.UserId.Should().Be("user-1");
    }

    [Fact]
    public void Sin_claim_de_miembro_es_cadena_vacia()
        => Con(new Claim(ClaimTypes.NameIdentifier, "staff-1")).MemberId.Should().BeEmpty();
}
```

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter CurrentUserServiceTests -nologo` → FAIL.

- [ ] **Step 2: Corregir `CurrentUserService.cs`** — reemplazar las dos propiedades:

```csharp
    public string UserId => CallerIdentity.UserIdOf(User) ?? string.Empty;
    public string MemberId => CallerIdentity.MemberIdOf(User) ?? string.Empty;
```

y añadir `using MLMConquerorGlobalEdition.SharedKernel.Security;`. Run → PASS.

- [ ] **Step 3: Pruebas de precio (fallan: el servicio no existe)**

```csharp
using FluentAssertions;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Tokens;

/// <summary>
/// Precio de un token = suma de lo que el alta cobraría por cada producto otorgado (SetupFee +
/// MonthlyFee), por QuantityGranted. Se vende si el tipo está marcado como vendible, tiene al menos
/// un producto otorgado, todos activos, y el total es mayor que cero.
/// </summary>
public class TokenPricingServiceTests
{
    private static readonly DateTime Seed = new(2026, 10, 1);

    private static Product Producto(string id, decimal setup, decimal monthly, bool activo = true) => new()
    {
        Id = id, Name = id, Description = id, ImageUrl = "", SetupFee = setup, MonthlyFee = monthly,
        IsActive = activo, CreatedBy = "seed", CreationDate = Seed
    };

    private static TokenType Tipo(int id, string nombre, bool vendible = true, bool activo = true,
        TokenCategory cat = TokenCategory.Enrollment) => new()
    {
        Id = id, Name = nombre, IsActive = activo, IsPurchasable = vendible, Category = cat,
        CreatedBy = "seed", CreationDate = Seed
    };

    private static TokenTypeProduct Vinculo(int tipo, string producto,
        TokenProductRole rol = TokenProductRole.Granted, int cantidad = 1) => new()
    {
        TokenTypeId = tipo, ProductId = producto, Role = rol, QuantityGranted = cantidad,
        CreatedBy = "seed", CreationDate = Seed
    };

    private static AppDbContext Catalogo()
    {
        var db = TestDbContextFactory.Create();
        db.Products.AddRange(
            Producto("prod-vip", 50m, 49.99m),
            Producto("prod-sub", 0m, 40m),
            Producto("prod-off", 10m, 10m, activo: false),
            Producto("prod-free", 0m, 0m),
            Producto("prod-guest", 0m, 10m));
        db.TokenTypes.AddRange(
            Tipo(13, "Enrollment: VIP Member"),
            Tipo(64, "Enrollment: Ambassador + VIP"),
            Tipo(86, "Enrollment: Ambassador + VIP FREE", vendible: false),
            Tipo(40, "Dos VIP"),
            Tipo(56, "Upgrade: Guest to VIP", cat: TokenCategory.Upgrade),
            Tipo(70, "Producto inactivo"),
            Tipo(71, "Sin precio"),
            Tipo(99, "Tipo inactivo", activo: false));
        db.TokenTypeProducts.AddRange(
            Vinculo(13, "prod-vip"),
            Vinculo(64, "prod-sub"), Vinculo(64, "prod-vip"),
            Vinculo(86, "prod-sub"), Vinculo(86, "prod-vip"),
            Vinculo(40, "prod-vip", cantidad: 2),
            Vinculo(56, "prod-guest", TokenProductRole.UpgradeFrom), Vinculo(56, "prod-vip", TokenProductRole.UpgradeTo),
            Vinculo(70, "prod-vip"), Vinculo(70, "prod-off"),
            Vinculo(71, "prod-free"),
            Vinculo(99, "prod-vip"));
        db.SaveChanges();
        return db;
    }

    [Theory]
    [InlineData(13, 99.99)]   // un producto
    [InlineData(64, 139.99)]  // Ambassador (0 + 40) + VIP (50 + 49.99)
    [InlineData(40, 199.98)]  // VIP × QuantityGranted 2
    public async Task El_precio_es_la_suma_de_los_productos_otorgados(int tipo, double precio)
    {
        var quote = await new TokenPricingService(Catalogo()).QuoteAsync(tipo);

        quote.IsSuccess.Should().BeTrue();
        quote.Value!.UnitPrice.Should().Be((decimal)precio);
    }

    [Fact]
    public async Task Guarda_los_productos_de_los_que_salio_el_precio()
        => (await new TokenPricingService(Catalogo()).QuoteAsync(64)).Value!.ProductIds
            .Should().BeEquivalentTo("prod-sub", "prod-vip");

    [Theory]
    [InlineData(86,    TokenPurchaseErrors.NotForSale)]
    [InlineData(56,    TokenPurchaseErrors.NoGrantedProduct)]
    [InlineData(70,    TokenPurchaseErrors.ProductInactive)]
    [InlineData(71,    TokenPurchaseErrors.PriceNotSet)]
    [InlineData(99,    TokenPurchaseErrors.TokenTypeNotFound)]
    [InlineData(12345, TokenPurchaseErrors.TokenTypeNotFound)]
    public async Task Lo_que_no_se_vende_lo_dice_con_su_codigo(int tipo, string codigo)
    {
        var quote = await new TokenPricingService(Catalogo()).QuoteAsync(tipo);

        quote.IsSuccess.Should().BeFalse();
        quote.ErrorCode.Should().Be(codigo);
    }

    [Fact]
    public async Task El_catalogo_lista_los_activos_y_marca_los_que_no_se_venden()
    {
        var items = await new TokenPricingService(Catalogo()).GetCatalogAsync();

        items.Should().NotContain(i => i.TokenTypeId == 99);
        items.Single(i => i.TokenTypeId == 64).Purchasable.Should().BeTrue();
        var free = items.Single(i => i.TokenTypeId == 86);
        free.Purchasable.Should().BeFalse();
        free.NotPurchasableReason.Should().Be(TokenPurchaseErrors.NotForSale);
    }
}
```

- [ ] **Step 4: Crear `Billing/Services/Tokens/TokenPricingService.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Services.Tokens;

/// <summary>Un tipo de token con su precio de venta, o con la razón por la que no se vende.</summary>
public sealed record TokenQuote(
    int                   TokenTypeId,
    string                Name,
    string?               Description,
    bool                  IsGuestPass,
    IReadOnlyList<string> ProductIds,
    decimal?              UnitPrice,
    bool                  Purchasable,
    string?               NotPurchasableReason);

public interface ITokenPricingService
{
    /// <summary>Todos los tipos activos, vendibles o no, para que la pantalla sepa qué ofrecer.</summary>
    Task<IReadOnlyList<TokenQuote>> GetCatalogAsync(CancellationToken ct = default);

    /// <summary>El precio de un tipo, o un fallo con el código de por qué no se vende.</summary>
    Task<Result<TokenQuote>> QuoteAsync(int tokenTypeId, CancellationToken ct = default);
}

/// <inheritdoc cref="ITokenPricingService"/>
public sealed class TokenPricingService : ITokenPricingService
{
    private readonly AppDbContext _db;

    public TokenPricingService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<TokenQuote>> GetCatalogAsync(CancellationToken ct = default)
    {
        var types = await _db.TokenTypes.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        var typeIds = types.Select(t => t.Id).ToList();
        var links = await _db.TokenTypeProducts.AsNoTracking()
            .Where(l => typeIds.Contains(l.TokenTypeId) && l.Role == TokenProductRole.Granted)
            .ToListAsync(ct);

        var products = await ProductsAsync(links, ct);

        return types
            .Select(t => Price(t, links.Where(l => l.TokenTypeId == t.Id).ToList(), products))
            .ToList();
    }

    public async Task<Result<TokenQuote>> QuoteAsync(int tokenTypeId, CancellationToken ct = default)
    {
        var type = await _db.TokenTypes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tokenTypeId && t.IsActive, ct);

        if (type is null)
            return Result<TokenQuote>.Failure(TokenPurchaseErrors.TokenTypeNotFound, "Token type not found or inactive.");

        var links = await _db.TokenTypeProducts.AsNoTracking()
            .Where(l => l.TokenTypeId == tokenTypeId && l.Role == TokenProductRole.Granted)
            .ToListAsync(ct);

        var quote = Price(type, links, await ProductsAsync(links, ct));

        return quote.Purchasable
            ? Result<TokenQuote>.Success(quote)
            : Result<TokenQuote>.Failure(quote.NotPurchasableReason!,
                $"Token type '{type.Name}' cannot be purchased ({quote.NotPurchasableReason}).");
    }

    private async Task<Dictionary<string, Product>> ProductsAsync(
        IReadOnlyCollection<TokenTypeProduct> links, CancellationToken ct)
    {
        var ids = links.Select(l => l.ProductId).Distinct().ToList();
        return await _db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);
    }

    /// <summary>
    /// Suma de lo que el alta cobraría por cada producto otorgado (SetupFee + MonthlyFee, como
    /// SelectProductsHandler), por QuantityGranted. Sin precios promo: el token sustituye al cobro
    /// normal. IsPurchasable va primero: las variantes FREE otorgan los mismos productos que las de
    /// pago, así que sin el flag se venderían a precio completo.
    /// </summary>
    private static TokenQuote Price(
        TokenType type, IReadOnlyCollection<TokenTypeProduct> granted, IReadOnlyDictionary<string, Product> products)
    {
        TokenQuote NotSold(string reason) =>
            new(type.Id, type.Name, type.Description, type.IsGuestPass, Array.Empty<string>(), null, false, reason);

        if (!type.IsPurchasable) return NotSold(TokenPurchaseErrors.NotForSale);
        if (granted.Count == 0)  return NotSold(TokenPurchaseErrors.NoGrantedProduct);

        var total = 0m;
        foreach (var link in granted)
        {
            if (!products.TryGetValue(link.ProductId, out var product) || !product.IsActive)
                return NotSold(TokenPurchaseErrors.ProductInactive);

            total += (product.SetupFee + product.MonthlyFee) * Math.Max(1, link.QuantityGranted);
        }

        if (total <= 0) return NotSold(TokenPurchaseErrors.PriceNotSet);

        var ids = granted.Select(g => g.ProductId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return new TokenQuote(type.Id, type.Name, type.Description, type.IsGuestPass, ids, total, true, null);
    }
}
```

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter TokenPricingServiceTests -nologo` → PASS.

- [ ] **Step 5: Pruebas del cupo (fallan: el servicio no existe)**

```csharp
using FluentAssertions;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Tokens;

/// <summary>
/// El tope de 50 al mes: comprados emitidos en el mes (por dueño ORIGINAL) más compras en vuelo
/// del mismo mes, incluidas las que esperan conciliación.
/// </summary>
public class TokenMonthlyQuotaTests
{
    private const string Yo   = "AMB-1";
    private const string Otro = "AMB-2";
    private static readonly DateTime Now = new(2026, 10, 15, 10, 0, 0);

    private static TokenTransaction Instancia(
        string code, DateTime at, TokenTransactionType tipo = TokenTransactionType.Purchased,
        string? dueño = null, bool conCodigo = true) => new()
    {
        MemberId = dueño ?? Yo, OriginalOwnerMemberId = Yo, TokenTypeId = 13,
        TransactionType = tipo, Quantity = 1, ReferenceId = conCodigo ? code : null,
        Status = TokenInstanceStatus.Issued, CreatedBy = "seed", CreationDate = at
    };

    private static TokenPurchase Compra(TokenPurchaseStatus status, int cantidad, int periodo = 202610) => new()
    {
        MemberId = Yo, TokenTypeId = 13, GrantedProductIds = "prod-vip", Quantity = cantidad,
        UnitPrice = 1, TotalAmount = cantidad, PaymentMethod = TokenPaymentMethod.Crypto,
        Status = status, OrderId = Guid.NewGuid().ToString(), IdempotencyKey = Guid.NewGuid().ToString("N"),
        PeriodKey = periodo, CreatedBy = "seed", CreationDate = Now
    };

    private static Task<int> Usado(AppDbContext db) => new TokenMonthlyQuota(db).UsedAsync(Yo, Now);

    [Fact]
    public async Task Cuenta_los_comprados_del_mes_por_dueno_original_aunque_los_haya_pasado()
    {
        var db = TestDbContextFactory.Create();
        db.TokenTransactions.AddRange(
            Instancia("AAA1111", Now.AddDays(-1)),
            Instancia("AAA2222", Now.AddDays(-14)),
            Instancia("AAA3333", Now.AddDays(-2), dueño: Otro));
        await db.SaveChangesAsync();

        (await Usado(db)).Should().Be(3);
    }

    [Fact]
    public async Task No_cuenta_otros_movimientos_ni_meses_anteriores_ni_filas_sin_codigo()
    {
        var db = TestDbContextFactory.Create();
        db.TokenTransactions.AddRange(
            Instancia("BBB1111", Now.AddDays(-1), TokenTransactionType.AdminGranted),
            Instancia("BBB2222", Now.AddMonths(-1)),
            Instancia("—", Now.AddDays(-1), conCodigo: false));
        await db.SaveChangesAsync();

        (await Usado(db)).Should().Be(0);
    }

    [Fact]
    public async Task Suma_las_compras_en_vuelo_y_no_las_cerradas()
    {
        var db = TestDbContextFactory.Create();
        db.TokenPurchases.AddRange(
            Compra(TokenPurchaseStatus.Pending, 4),
            Compra(TokenPurchaseStatus.AwaitingCryptoConfirmation, 2),
            Compra(TokenPurchaseStatus.NeedsReconciliation, 3),
            Compra(TokenPurchaseStatus.Failed, 10),
            Compra(TokenPurchaseStatus.Completed, 7));
        await db.SaveChangesAsync();

        (await Usado(db)).Should().Be(9);
    }

    [Fact]
    public async Task Una_reserva_de_otro_mes_no_cuenta()
    {
        var db = TestDbContextFactory.Create();
        db.TokenPurchases.Add(Compra(TokenPurchaseStatus.Pending, 5, periodo: 202609));
        await db.SaveChangesAsync();

        (await Usado(db)).Should().Be(0);
    }
}
```

- [ ] **Step 6: Crear `Billing/Services/Tokens/TokenMonthlyQuota.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Services.Tokens;

public interface ITokenMonthlyQuota
{
    /// <summary>Tokens que el miembro ya consumió del tope del mes de <paramref name="now"/> (emitidos + en vuelo).</summary>
    Task<int> UsedAsync(string memberId, DateTime now, CancellationToken ct = default);
}

/// <inheritdoc cref="ITokenMonthlyQuota"/>
/// <remarks>
/// Lo emitido cuenta por OriginalOwnerMemberId: TransferTokenInstance cambia MemberId, y un tope que
/// se libera regalando tokens no es un tope. Una compra en cripto pedida en septiembre y confirmada en
/// octubre cuenta en octubre (sus tokens nacen entonces); como reserva solo ocupó septiembre.
/// </remarks>
public sealed class TokenMonthlyQuota : ITokenMonthlyQuota
{
    private readonly AppDbContext _db;

    public TokenMonthlyQuota(AppDbContext db) => _db = db;

    public async Task<int> UsedAsync(string memberId, DateTime now, CancellationToken ct = default)
    {
        var start  = TokenPurchaseRules.MonthStart(now);
        var end    = start.AddMonths(1);
        var period = TokenPurchaseRules.PeriodKey(now);

        var issued = await _db.TokenTransactions.AsNoTracking()
            .CountAsync(t => t.TransactionType == TokenTransactionType.Purchased
                          && t.ReferenceId != null
                          && t.OriginalOwnerMemberId == memberId
                          && t.CreationDate >= start
                          && t.CreationDate < end, ct);

        var reserved = await _db.TokenPurchases.AsNoTracking()
            .Where(p => p.MemberId == memberId
                     && p.PeriodKey == period
                     && (p.Status == TokenPurchaseStatus.Pending
                         || p.Status == TokenPurchaseStatus.AwaitingCryptoConfirmation
                         || p.Status == TokenPurchaseStatus.NeedsReconciliation))
            .SumAsync(p => (int?)p.Quantity, ct) ?? 0;

        return issued + reserved;
    }
}
```

- [ ] **Step 7: Correr las pruebas de la tarea**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "CurrentUserServiceTests|TokenPricingServiceTests|TokenMonthlyQuotaTests" -nologo` → PASS.

- [ ] **Step 8: Commit**

```bash
git add MLMConquerorGlobalEdition.Billing MLMConquerorGlobalEdition.Billing.Tests
git commit -m "Billing lee el memberId con el nombre que emite el JWT, calcula el precio del token como la suma de los productos que otorga y cuenta el cupo del mes con los comprados y las compras en vuelo"
```

---

### Task 5: Billing — saldo de comisiones gastable

**Files:**
- Modify: `MLMConquerorGlobalEdition.Billing/Services/Recurring/ICommissionBalanceService.cs`
- Modify: `MLMConquerorGlobalEdition.Billing/Services/Recurring/CommissionBalanceService.cs`
- Test: `MLMConquerorGlobalEdition.Billing.Tests/Services/Recurring/SaldoParaComprarTokensTests.cs`

- [ ] **Step 1: Escribir las pruebas (fallan: los métodos no existen)**

```csharp
using FluentAssertions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Recurring;

/// <summary>
/// Con qué saldo de comisiones se pueden pagar tokens: solo lo ya pagable (PaymentDate &lt;= Now)
/// y no metido en un pago en curso.
/// </summary>
public class SaldoParaComprarTokensTests
{
    private const string Yo = "AMB-1";
    private static readonly DateTime Now = new(2026, 10, 15, 10, 0, 0);

    private static Mock<IDateTimeProvider> Reloj()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(Now);
        m.Setup(d => d.UtcNow).Returns(Now);
        return m;
    }

    private static CommissionEarning Comision(decimal monto, DateTime pago) => new()
    {
        BeneficiaryMemberId = Yo, CommissionTypeId = 1, Amount = monto,
        Status = CommissionEarningStatus.Pending, EarnedDate = pago.AddDays(-7), PaymentDate = pago,
        CreatedBy = "seed", CreationDate = pago.AddDays(-7), LastUpdateDate = pago.AddDays(-7)
    };

    [Fact]
    public async Task Gastable_excluye_lo_que_vence_despues_y_lo_reservado_por_un_pago()
    {
        var db = TestDbContextFactory.Create();
        var reservada = Comision(200m, Now.AddDays(-2));
        db.CommissionEarnings.AddRange(Comision(500m, Now.AddDays(-1)), Comision(1000m, Now.AddDays(20)), reservada);
        var intento = new PayoutAttempt
        {
            MemberId = Yo, WalletTypeSnapshot = WalletType.eWallet, PayoutAccountSnapshot = "x",
            AmountUsd = 200m, ProcessDateUtc = Now, Outcome = PayoutOutcome.Pending,
            AttemptedAtUtc = Now, EarningsCount = 1, CreatedBy = "seed", CreationDate = Now
        };
        db.PayoutAttempts.Add(intento);
        await db.SaveChangesAsync();
        db.PayoutAttemptEarnings.Add(new PayoutAttemptEarning
        {
            PayoutAttemptId = intento.Id, CommissionEarningId = reservada.Id, Amount = 200m,
            CreatedBy = "seed", CreationDate = Now
        });
        await db.SaveChangesAsync();

        (await new CommissionBalanceService(db, Reloj().Object).GetSpendableAsync(Yo)).Should().Be(500m);
    }

    [Fact]
    public async Task Pagar_una_compra_deja_un_debito_negativo_y_el_pago()
    {
        var db = TestDbContextFactory.Create();
        db.CommissionEarnings.Add(Comision(500m, Now.AddDays(-1)));
        await db.SaveChangesAsync();
        var sut = new CommissionBalanceService(db, Reloj().Object);

        var r = await sut.FundTokenPurchaseAsync(Yo, 199.98m, "ord-1", "user-1");

        r.IsSuccess.Should().BeTrue();
        var debito = db.CommissionEarnings.Single(e => e.Amount < 0);
        debito.Amount.Should().Be(-199.98m);
        debito.SourceOrderId.Should().Be("ord-1");
        debito.PaymentDate.Should().Be(Now);
        var pago = db.PaymentHistories.Single();
        pago.Id.Should().Be(r.Value);
        pago.GatewayName.Should().Be("CommissionBalance");
        pago.GatewayTransactionId.Should().Be(debito.Id);
        (await sut.GetSpendableAsync(Yo)).Should().Be(300.02m);
    }

    [Fact]
    public async Task Sin_saldo_no_escribe_nada()
    {
        var db = TestDbContextFactory.Create();
        db.CommissionEarnings.Add(Comision(50m, Now.AddDays(-1)));
        await db.SaveChangesAsync();

        var r = await new CommissionBalanceService(db, Reloj().Object).FundTokenPurchaseAsync(Yo, 99.99m, "ord-1", "user-1");

        r.ErrorCode.Should().Be(TokenPurchaseErrors.InsufficientCommissionBalance);
        db.CommissionEarnings.Should().HaveCount(1);
        db.PaymentHistories.Should().BeEmpty();
    }
}
```

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter SaldoParaComprarTokensTests -nologo` → FAIL.

- [ ] **Step 2: Ampliar `ICommissionBalanceService.cs`** — al final de la interfaz:

```csharp
    /// <summary>
    /// Saldo con el que el miembro puede pagar AHORA: comisiones pendientes ya pagables
    /// (PaymentDate &lt;= Now) no reservadas por un pago en curso, más el residual diario si alcanza
    /// el mínimo de consolidación. Más estricto que <see cref="GetAvailableAsync"/>.
    /// </summary>
    Task<decimal> GetSpendableAsync(string memberId, CancellationToken ct = default);

    /// <summary>
    /// Paga una compra de tokens con comisiones: consolida el residual si toca, escribe el débito
    /// negativo ligado al pedido y el PaymentHistory. No emite ni toca el pedido: eso lo hace quien
    /// compra en la misma transacción. Devuelve el id del PaymentHistory.
    /// </summary>
    Task<Result<string>> FundTokenPurchaseAsync(
        string memberId, decimal amount, string orderId, string actor, CancellationToken ct = default);
```

- [ ] **Step 3: Implementar en `CommissionBalanceService.cs`** — añadir `using MLMConquerorGlobalEdition.Domain.Constants;` y `using MLMConquerorGlobalEdition.SharedKernel.Billing;`, y antes de `// ── Helpers`:

```csharp
    // ── Compra de tokens (Plan F) ─────────────────────────────────────────────

    public async Task<decimal> GetSpendableAsync(string memberId, CancellationToken ct = default)
    {
        var now     = _dateTime.Now;
        var minimum = await GetConsolidationMinimumAsync(ct);

        // Las comisiones que ya están dentro de un pago no fallido son de ese pago, no de esta compra.
        var reservedIds = _db.PayoutAttemptEarnings
            .Where(pae => _db.PayoutAttempts.Any(a => a.Id == pae.PayoutAttemptId && a.Outcome != PayoutOutcome.Failed))
            .Select(pae => pae.CommissionEarningId);

        var payable = await _db.CommissionEarnings.AsNoTracking()
            .Where(e => e.BeneficiaryMemberId == memberId
                     && e.Status == CommissionEarningStatus.Pending
                     && !e.IsDeleted
                     && e.PaymentDate <= now
                     && !reservedIds.Contains(e.Id))
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;

        var dailyResidual = await _db.DailyResidualEarnings.AsNoTracking()
            .Where(e => e.BeneficiaryMemberId == memberId && e.Status == CommissionEarningStatus.Pending)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;

        var eligibleResidual = dailyResidual >= minimum ? dailyResidual : 0m;
        return Math.Max(0m, payable + eligibleResidual);
    }

    public async Task<Result<string>> FundTokenPurchaseAsync(
        string memberId, decimal amount, string orderId, string actor, CancellationToken ct = default)
    {
        if (amount <= 0)
            return Result<string>.Failure("INVALID_AMOUNT", "Amount must be positive.");

        var spendable = await GetSpendableAsync(memberId, ct);
        if (spendable < amount)
            return Result<string>.Failure(TokenPurchaseErrors.InsufficientCommissionBalance,
                $"Commission balance {spendable:F2} is below {amount:F2}.");

        var now = _dateTime.Now;

        await ConsolidateCoreAsync(memberId, actor,
            paymentComment: $"Consolidated to fund token purchase order #{orderId}", ct);

        var debit = new CommissionEarning
        {
            BeneficiaryMemberId = memberId,
            CommissionTypeId    = await GetDailyResidualCommissionTypeIdAsync(ct),
            Amount              = -amount,
            Status              = CommissionEarningStatus.Pending,
            EarnedDate          = now,
            PaymentDate         = now,
            SourceOrderId       = orderId,
            Notes               = $"Token purchase debit — {amount:F2} — Order {orderId}",
            IsManualEntry       = false,
            CreatedBy           = actor,
            CreationDate        = now,
            LastUpdateDate      = now
        };
        _db.CommissionEarnings.Add(debit);

        var payment = new PaymentHistory
        {
            OrderId              = orderId,
            MemberId             = memberId,
            Amount               = amount,
            GatewayName          = "CommissionBalance",
            GatewayTransactionId = debit.Id,
            TransactionStatus    = PaymentHistoryTransactionStatus.Captured,
            ProcessedAt          = now,
            CreationDate         = now,
            CreatedBy            = actor,
            LastUpdateDate       = now,
            LastUpdateBy         = actor
        };
        _db.PaymentHistories.Add(payment);

        await _db.SaveChangesAsync(ct);
        return Result<string>.Success(payment.Id);
    }
```

- [ ] **Step 4: Correr las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "SaldoParaComprarTokensTests|CommissionBalanceServiceTests" -nologo` → PASS.

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.Billing MLMConquerorGlobalEdition.Billing.Tests
git commit -m "Saldo de comisiones para comprar tokens: solo lo ya pagable y no reservado por un pago en curso; pagar una compra deja el debito ligado al pedido y su PaymentHistory"
```

---

### Task 6: eWallet real — débito en i-Payout y su verificación

**Files:**
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Payout/EWallet/EWalletContracts.cs`
- Modify: `MLMConquerorGlobalEdition.Repository/Services/Payout/EWallet/EWalletClient.cs` (interfaz y clase)
- Create: `MLMConquerorGlobalEdition.Billing/Services/Tokens/EWalletFunds.cs`
- Test: `MLMConquerorGlobalEdition.Billing.Tests/Services/Payout/EWalletDeductTests.cs`
- Test: `MLMConquerorGlobalEdition.Billing.Tests/Services/Tokens/EWalletFundsTests.cs`

> **Qué hace el legado** (`WarrantyRewards.Service/Code/eWallet.cs:564`, `EWallet_AddDeductionFromBalance`): `fn = "eWallet_AddCheckoutItems"`, `MerchantGUID`, `MerchantPassword`, `UserName`, `AutoChargeAccount = "true"` y `arrItems` con un ítem `{ Amount, CurrencyCode "USD", ItemDescription, MerchantReferenceID, IsSubscription "false", SubscriptionAmount 0, UserReturnURL "", MustComplete "false" }`. Devuelve el `m_Text` del ítem. **Sus llamadores no confían en ese texto**: `BuyItemsWithEWalletBalance` (`DailyRealTimeCommissionEntity.cs:17160`) y la deducción de tokens (`:16880-16960`) leen el saldo antes y después y solo dan la deducción por hecha si `_balanceAfterDeduction < _balance`; PROCESSING/PENDING devuelven `m_Code` 0. Este plan hace lo mismo y, cuando la respuesta es dudosa, en lugar de reintentar (no hay consulta por `MerchantReferenceID`) deja la compra a conciliar.
>
> El usuario de i-Payout es el `AccountIdentifier` de la wallet eWallet del miembro (D10).

- [ ] **Step 1: Pruebas del cliente (fallan: `DeductAsync` no existe)**

`Billing.Tests/Services/Payout/EWalletDeductTests.cs`:

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Payout.EWallet;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Payout;

/// <summary>
/// El débito de eWallet como lo hace el legado (eWallet_AddCheckoutItems con AutoChargeAccount) y
/// cómo se lee la respuesta: código negativo es rechazo; todo lo que no sea una respuesta clara es
/// "no se sabe", nunca "falló" (falló permitiría reintentar y cobrar dos veces).
/// </summary>
public class EWalletDeductTests
{
    private sealed class Manejador(Func<HttpRequestMessage, string, HttpResponseMessage> f) : HttpMessageHandler
    {
        public string? Cuerpo;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Cuerpo = await r.Content!.ReadAsStringAsync(ct);
            return f(r, Cuerpo);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode s, string json) =>
        new(s) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static EWalletClient Cliente(Manejador h)
    {
        AppDbContext db = TestDbContextFactory.Create();
        db.ApiCredentials.Add(new ApiCredential
        {
            ServiceKey = "EWallet", Environment = "Sandbox", BaseUrl = "https://ewallet.pruebas/api",
            ApiKeyEncrypted = "ENC:merchant-guid", SecretKeyEncrypted = "ENC:merchant-pass", IsActive = true,
            CreatedBy = "seed", CreationDate = DateTime.Now
        });
        db.SaveChanges();
        var crypto = new Mock<IEncryptionService>();
        crypto.Setup(c => c.Decrypt(It.IsAny<string>())).Returns<string>(s => s[4..]);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(EWalletClient.HttpClientName)).Returns(new HttpClient(h));
        return new EWalletClient(factory.Object, db, crypto.Object, NullLogger<EWalletClient>.Instance);
    }

    [Fact]
    public async Task Manda_el_cobro_como_el_legado()
    {
        var h = new Manejador((_, _) => Json(HttpStatusCode.OK,
            """{"response":{"m_Code":0,"m_Text":""},"arrItemsResponse":[{"m_Code":0,"m_Text":""}]}"""));

        var d = await Cliente(h).DeductAsync("AMB-1", 99.99m, "purchase-1", "Token purchase TP1");

        d.Status.Should().Be(EWalletDeductionStatus.Accepted);
        using var json = JsonDocument.Parse(h.Cuerpo!);
        var root = json.RootElement;
        root.GetProperty("fn").GetString().Should().Be("eWallet_AddCheckoutItems");
        root.GetProperty("MerchantGUID").GetString().Should().Be("merchant-guid");
        root.GetProperty("UserName").GetString().Should().Be("AMB-1");
        root.GetProperty("AutoChargeAccount").GetString().Should().Be("true");
        var item = root.GetProperty("arrItems")[0];
        item.GetProperty("Amount").GetDecimal().Should().Be(99.99m);
        item.GetProperty("CurrencyCode").GetString().Should().Be("USD");
        item.GetProperty("MerchantReferenceID").GetString().Should().Be("purchase-1");
        item.GetProperty("IsSubscription").GetString().Should().Be("false");
        item.GetProperty("MustComplete").GetString().Should().Be("false");
    }

    [Fact]
    public async Task Un_item_con_codigo_negativo_es_rechazo()
    {
        var h = new Manejador((_, _) => Json(HttpStatusCode.OK,
            """{"response":{"m_Code":0,"m_Text":""},"arrItemsResponse":[{"m_Code":-5,"m_Text":"INSUFFICIENT_FUNDS"}]}"""));

        var d = await Cliente(h).DeductAsync("AMB-1", 10m, "p", "x");

        d.Status.Should().Be(EWalletDeductionStatus.Rejected);
        d.Code.Should().Be("EWALLET_ITEM_-5");
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    [InlineData(HttpStatusCode.OK, "esto no es json")]
    public async Task Una_respuesta_que_no_se_entiende_es_desconocida(HttpStatusCode status, string body)
    {
        var d = await Cliente(new Manejador((_, _) => Json(status, body))).DeductAsync("AMB-1", 10m, "p", "x");

        d.Status.Should().Be(EWalletDeductionStatus.Unknown);
    }

    [Fact]
    public async Task Una_caida_de_red_es_desconocida()
    {
        var d = await Cliente(new Manejador((_, _) => throw new HttpRequestException("caida")))
            .DeductAsync("AMB-1", 10m, "p", "x");

        d.Status.Should().Be(EWalletDeductionStatus.Unknown);
    }
}
```

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter EWalletDeductTests -nologo` → FAIL.

- [ ] **Step 2: Contratos en `EWalletContracts.cs`**

En `EWalletFunctions`, después de `Load`:

```csharp
    /// <summary>
    /// Cobra de la cuenta del usuario con AutoChargeAccount = "true". Es el débito del legado
    /// (EWallet_AddDeductionFromBalance). PROCESSING y PENDING devuelven también m_Code 0.
    /// </summary>
    public const string AddCheckoutItems = "eWallet_AddCheckoutItems";
```

Al final del archivo:

```csharp
/// <summary>Un ítem de arrItems de eWallet_AddCheckoutItems, con los valores del legado.</summary>
public sealed class EWalletCheckoutItem
{
    [JsonPropertyName("Amount")]              public decimal Amount              { get; set; }
    [JsonPropertyName("CurrencyCode")]        public string  CurrencyCode        { get; set; } = "USD";
    [JsonPropertyName("ItemDescription")]     public string  ItemDescription     { get; set; } = string.Empty;
    [JsonPropertyName("MerchantReferenceID")] public string  MerchantReferenceId { get; set; } = string.Empty;
    [JsonPropertyName("IsSubscription")]      public string  IsSubscription      { get; set; } = "false";
    [JsonPropertyName("SubscriptionAmount")]  public decimal SubscriptionAmount  { get; set; }
    [JsonPropertyName("UserReturnURL")]       public string  UserReturnUrl       { get; set; } = string.Empty;
    [JsonPropertyName("MustComplete")]        public string  MustComplete        { get; set; } = "false";
}

public sealed class EWalletCheckoutResponse : EWalletResponseEnvelope
{
    [JsonPropertyName("arrItemsResponse")] public List<EWalletStatus>? Items { get; set; }
}

public enum EWalletDeductionStatus
{
    /// <summary>Envoltorio e ítems con m_Code &gt;= 0. Puede estar en PROCESSING/PENDING: hay que verificar el saldo.</summary>
    Accepted,

    /// <summary>i-Payout dijo que no (código negativo) o no se llegó a enviar (sin credencial).</summary>
    Rejected,

    /// <summary>No hubo respuesta clara: el dinero pudo salir o no. No se reintenta.</summary>
    Unknown
}

public sealed record EWalletDeduction(EWalletDeductionStatus Status, string? Code, string? Text);
```

- [ ] **Step 3: `DeductAsync` en `EWalletClient.cs`**

En `IEWalletClient`, debajo de `LoadAsync`:

```csharp
    /// <summary>
    /// Cobra <paramref name="amount"/> USD de la cuenta. No lanza: un fallo de red, un 5xx o un JSON
    /// ilegible vuelven como <see cref="EWalletDeductionStatus.Unknown"/>, porque no prueban que el
    /// cobro no ocurriera.
    /// </summary>
    Task<EWalletDeduction> DeductAsync(
        string userName, decimal amount, string merchantReferenceId, string description, CancellationToken ct = default);
```

En la clase, después de `LoadAsync`:

```csharp
    public async Task<EWalletDeduction> DeductAsync(
        string userName, decimal amount, string merchantReferenceId, string description, CancellationToken ct = default)
    {
        var cfg = await ResolveAsync(ct);
        if (!cfg.IsSuccess)
            return new EWalletDeduction(EWalletDeductionStatus.Rejected, cfg.ErrorCode, cfg.Error);

        var payload = new Dictionary<string, object?>
        {
            ["fn"]                = EWalletFunctions.AddCheckoutItems,
            ["MerchantGUID"]      = cfg.Value!.MerchantGuid,
            ["MerchantPassword"]  = cfg.Value!.MerchantPassword,
            ["UserName"]          = userName,
            ["AutoChargeAccount"] = "true",
            ["arrItems"]          = new[]
            {
                new EWalletCheckoutItem
                {
                    Amount              = amount,
                    CurrencyCode        = "USD",
                    ItemDescription     = description,
                    MerchantReferenceId = merchantReferenceId
                }
            }
        };

        Result<EWalletCheckoutResponse> result;
        try
        {
            result = await PostAsync<EWalletCheckoutResponse>(cfg.Value!, payload, "add checkout items", ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "i-Payout: sin respuesta al cobrar {Reference} de {UserName}.", merchantReferenceId, userName);
            return new EWalletDeduction(EWalletDeductionStatus.Unknown, "EWALLET_NO_RESPONSE", ex.Message);
        }

        // HTTP no 2xx, cuerpo vacío o JSON ilegible: i-Payout pudo haber procesado el cobro igual.
        if (!result.IsSuccess)
            return new EWalletDeduction(EWalletDeductionStatus.Unknown, result.ErrorCode, result.Error);

        var status = result.Value!.Response;
        if (status is null)
            return new EWalletDeduction(EWalletDeductionStatus.Unknown, "EWALLET_NO_ENVELOPE", null);
        if (status.Code < 0)
            return new EWalletDeduction(EWalletDeductionStatus.Rejected, $"EWALLET_{status.Code}", status.Text);

        var items = result.Value!.Items;
        if (items is null || items.Count == 0)
            return new EWalletDeduction(EWalletDeductionStatus.Unknown, "EWALLET_NO_ITEMS", status.Text);

        var rejected = items.FirstOrDefault(i => i.Code < 0);
        if (rejected is not null)
            return new EWalletDeduction(EWalletDeductionStatus.Rejected, $"EWALLET_ITEM_{rejected.Code}", rejected.Text);

        return new EWalletDeduction(EWalletDeductionStatus.Accepted, null,
            string.Join(" | ", items.Select(i => i.Text).Where(t => !string.IsNullOrWhiteSpace(t))));
    }
```

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter EWalletDeductTests -nologo` → PASS.

- [ ] **Step 4: Pruebas de la verificación (fallan: `EWalletFunds` no existe)**

`Billing.Tests/Services/Tokens/EWalletFundsTests.cs`:

```csharp
using FluentAssertions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.Payout.EWallet;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Tokens;

/// <summary>
/// Como el legado: el débito se da por hecho solo si el saldo bajó. Si i-Payout dijo que sí pero el
/// saldo no se movió (PROCESSING/PENDING), o no se sabe qué pasó, la compra va a conciliación.
/// </summary>
public class EWalletFundsTests
{
    private readonly Mock<IEWalletClient> _client = new();

    private EWalletFunds Sut() => new(TestDbContextFactory.Create(), _client.Object);

    private void Deduccion(EWalletDeductionStatus status, string? code = null) =>
        _client.Setup(c => c.DeductAsync("AMB-1", 100m, "p1", "d", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new EWalletDeduction(status, code, null));

    private void SaldoDespues(decimal saldo) =>
        _client.Setup(c => c.GetBalanceAsync("AMB-1", "USD", It.IsAny<CancellationToken>()))
               .ReturnsAsync(Result<decimal>.Success(saldo));

    [Fact]
    public async Task Aceptado_y_con_el_saldo_bajado_es_cobrado()
    {
        Deduccion(EWalletDeductionStatus.Accepted);
        SaldoDespues(400m);

        (await Sut().DebitAsync("AMB-1", 100m, 500m, "p1", "d")).Status.Should().Be(EWalletDebitStatus.Completed);
    }

    [Fact]
    public async Task Aceptado_pero_el_saldo_no_bajo_queda_a_conciliar()
    {
        Deduccion(EWalletDeductionStatus.Accepted);
        SaldoDespues(500m);

        var o = await Sut().DebitAsync("AMB-1", 100m, 500m, "p1", "d");

        o.Status.Should().Be(EWalletDebitStatus.NeedsReconciliation);
        o.Code.Should().Be("EWALLET_DEBIT_PENDING");
    }

    [Fact]
    public async Task Desconocido_queda_a_conciliar_sin_mirar_el_saldo()
    {
        Deduccion(EWalletDeductionStatus.Unknown, "EWALLET_NO_RESPONSE");

        (await Sut().DebitAsync("AMB-1", 100m, 500m, "p1", "d")).Status.Should().Be(EWalletDebitStatus.NeedsReconciliation);
        _client.Verify(c => c.GetBalanceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Rechazado_es_rechazado()
    {
        Deduccion(EWalletDeductionStatus.Rejected, "EWALLET_ITEM_-5");

        var o = await Sut().DebitAsync("AMB-1", 100m, 500m, "p1", "d");

        o.Status.Should().Be(EWalletDebitStatus.Rejected);
        o.Code.Should().Be("EWALLET_ITEM_-5");
    }

    [Fact]
    public async Task El_usuario_es_el_AccountIdentifier_de_la_eWallet_preferida_primero()
    {
        var db = TestDbContextFactory.Create();
        MemberProfilesWallet W(string id, bool pref, string? acc, WalletType t = WalletType.eWallet) => new()
        {
            Id = id, MemberId = "AMB-1", WalletType = t, IsPreferred = pref, AccountIdentifier = acc,
            Status = WalletStatus.Approved, CreatedBy = "seed", CreationDate = DateTime.Now
        };
        db.Wallets.AddRange(W("a", false, "ipayout-old"), W("b", true, "ipayout-main"), W("c", true, "x@y.com", WalletType.Volet));
        await db.SaveChangesAsync();

        (await new EWalletFunds(db, _client.Object).UserNameOfAsync("AMB-1")).Should().Be("ipayout-main");
        (await new EWalletFunds(db, _client.Object).UserNameOfAsync("AMB-404")).Should().BeNull();
    }
}
```

- [ ] **Step 5: Crear `Billing/Services/Tokens/EWalletFunds.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Payout.EWallet;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Tokens;

public enum EWalletDebitStatus { Completed, Rejected, NeedsReconciliation }

public sealed record EWalletDebitOutcome(EWalletDebitStatus Status, string? Code, string? Detail);

/// <summary>
/// El saldo REAL de i-Payout y el débito verificado. Sustituye, para las compras, al libro interno
/// de WalletHistory de EWalletGatewayService, que nadie acredita.
/// </summary>
public interface IEWalletFunds
{
    /// <summary>El UserName de i-Payout del miembro (AccountIdentifier de su eWallet), o null si no tiene.</summary>
    Task<string?> UserNameOfAsync(string memberId, CancellationToken ct = default);

    Task<Result<decimal>> BalanceAsync(string userName, CancellationToken ct = default);

    /// <summary>Cobra y verifica por saldo, como el legado. Nunca reintenta.</summary>
    Task<EWalletDebitOutcome> DebitAsync(
        string userName, decimal amount, decimal balanceBefore, string reference, string description,
        CancellationToken ct = default);
}

public sealed class EWalletFunds : IEWalletFunds
{
    /// <summary>Holgura por redondeo de i-Payout.</summary>
    private const decimal Tolerance = 0.01m;

    private readonly AppDbContext   _db;
    private readonly IEWalletClient _client;

    public EWalletFunds(AppDbContext db, IEWalletClient client)
    {
        _db     = db;
        _client = client;
    }

    /// <remarks>
    /// EWalletPayoutGatewayService propone el MemberId como UserName al registrar la cuenta y guarda
    /// en AccountIdentifier lo que el gateway confirma o reasigna: el dato guardado es el bueno.
    /// </remarks>
    public Task<string?> UserNameOfAsync(string memberId, CancellationToken ct = default) =>
        _db.Wallets.AsNoTracking()
            .Where(w => w.MemberId == memberId
                     && w.WalletType == WalletType.eWallet
                     && !w.IsDeleted
                     && w.AccountIdentifier != null
                     && w.AccountIdentifier != "")
            .OrderByDescending(w => w.IsPreferred)
            .Select(w => w.AccountIdentifier)
            .FirstOrDefaultAsync(ct);

    public Task<Result<decimal>> BalanceAsync(string userName, CancellationToken ct = default) =>
        _client.GetBalanceAsync(userName, "USD", ct);

    public async Task<EWalletDebitOutcome> DebitAsync(
        string userName, decimal amount, decimal balanceBefore, string reference, string description,
        CancellationToken ct = default)
    {
        var deduction = await _client.DeductAsync(userName, amount, reference, description, ct);

        if (deduction.Status == EWalletDeductionStatus.Rejected)
            return new EWalletDebitOutcome(EWalletDebitStatus.Rejected, deduction.Code, deduction.Text);

        if (deduction.Status == EWalletDeductionStatus.Unknown)
            return new EWalletDebitOutcome(EWalletDebitStatus.NeedsReconciliation,
                "EWALLET_OUTCOME_UNKNOWN", $"{deduction.Code}: {deduction.Text}");

        // Desde aquí el dinero pudo haber salido: nada cancela la verificación.
        var after = await _client.GetBalanceAsync(userName, "USD", CancellationToken.None);
        if (!after.IsSuccess)
            return new EWalletDebitOutcome(EWalletDebitStatus.NeedsReconciliation,
                "EWALLET_BALANCE_UNVERIFIED", after.Error);

        if (after.Value <= balanceBefore - amount + Tolerance)
            return new EWalletDebitOutcome(EWalletDebitStatus.Completed, null, deduction.Text);

        return new EWalletDebitOutcome(EWalletDebitStatus.NeedsReconciliation, "EWALLET_DEBIT_PENDING",
            $"Accepted ({deduction.Text}) but balance went from {balanceBefore:F2} to {after.Value:F2}.");
    }
}
```

- [ ] **Step 6: Correr las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter "EWalletDeductTests|EWalletFundsTests" -nologo` → PASS.

- [ ] **Step 7: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository MLMConquerorGlobalEdition.Billing MLMConquerorGlobalEdition.Billing.Tests
git commit -m "Debito real de eWallet en i-Payout como el legado (eWallet_AddCheckoutItems con AutoChargeAccount) y verificado por saldo: si i-Payout acepta pero el saldo no baja, o no hay respuesta clara, la compra queda a conciliar en vez de reintentarse"
```

---
### Task 7: Billing — emisión y candado por miembro

**Files:**
- Create: `MLMConquerorGlobalEdition.Billing/Services/Tokens/TokenIssuer.cs`
- Create: `MLMConquerorGlobalEdition.Billing/Services/Tokens/TokenPurchaseGate.cs`
- Create: `MLMConquerorGlobalEdition.Billing.Tests/Helpers/TransactionOnlyGate.cs`
- Test: `MLMConquerorGlobalEdition.Billing.Tests/Services/Tokens/TokenIssuerTests.cs`

- [ ] **Step 1: Escribir las pruebas (fallan: el emisor no existe)**

```csharp
using FluentAssertions;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Tokens;

/// <summary>Una instancia canjeable por token, con código único, dueño original y vínculo a la compra.</summary>
public class TokenIssuerTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 10, 0, 0);

    [Fact]
    public async Task Emite_una_instancia_canjeable_por_token_y_crea_el_saldo()
    {
        var db = TestDbContextFactory.Create();

        var codes = await new TokenIssuer(db).IssueAsync(new TokenIssue("AMB-1", 13, 3, "purchase-1", "TP1015-1", "user-1", Now));
        await db.SaveChangesAsync();

        codes.Should().HaveCount(3).And.OnlyHaveUniqueItems();
        var rows = db.TokenTransactions.ToList();
        rows.Should().HaveCount(3).And.OnlyContain(t =>
            t.MemberId == "AMB-1" && t.OriginalOwnerMemberId == "AMB-1" && t.TokenTypeId == 13 &&
            t.TransactionType == TokenTransactionType.Purchased && t.Quantity == 1 &&
            t.Status == TokenInstanceStatus.Issued && t.TokenPurchaseId == "purchase-1" && t.CreationDate == Now);
        rows.Select(t => t.ReferenceId).Should().BeEquivalentTo(codes);
        db.TokenBalances.Single().Balance.Should().Be(3);
    }

    [Fact]
    public async Task Suma_al_saldo_que_ya_existe()
    {
        var db = TestDbContextFactory.Create();
        db.TokenBalances.Add(new TokenBalance { MemberId = "AMB-1", TokenTypeId = 13, Balance = 4, CreatedBy = "seed", CreationDate = Now });
        await db.SaveChangesAsync();

        await new TokenIssuer(db).IssueAsync(new TokenIssue("AMB-1", 13, 2, "p", null, "user-1", Now));
        await db.SaveChangesAsync();

        db.TokenBalances.Single().Balance.Should().Be(6);
    }

    [Fact]
    public async Task No_emite_cero()
    {
        var act = () => new TokenIssuer(TestDbContextFactory.Create())
            .IssueAsync(new TokenIssue("AMB-1", 13, 0, "p", null, "user-1", Now));

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
```

- [ ] **Step 2: Crear `Billing/Services/Tokens/TokenIssuer.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Tokens;

public sealed record TokenIssue(
    string MemberId, int TokenTypeId, int Quantity, string PurchaseId, string? OrderNo, string Actor, DateTime Now);

public interface ITokenIssuer
{
    /// <summary>
    /// Añade una TokenTransaction por token y sube el TokenBalance. NO guarda: quien llama lo
    /// persiste en el mismo SaveChanges que el cobro.
    /// </summary>
    Task<IReadOnlyList<string>> IssueAsync(TokenIssue issue, CancellationToken ct = default);
}

/// <inheritdoc cref="ITokenIssuer"/>
/// <remarks>
/// Como GrantTokensHandler y DistributeTokenHandler, pero rellena OriginalOwnerMemberId (el cupo
/// cuenta por él) y descarta códigos que ya existen: TokenCodeGenerator puede repetir y el índice
/// único de ReferenceId haría fallar el guardado DESPUÉS de cobrar.
/// </remarks>
public sealed class TokenIssuer : ITokenIssuer
{
    private const int MaxRounds = 10;

    private readonly AppDbContext _db;

    public TokenIssuer(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<string>> IssueAsync(TokenIssue issue, CancellationToken ct = default)
    {
        if (issue.Quantity < 1)
            throw new ArgumentOutOfRangeException(nameof(issue), "Quantity must be positive.");

        var codes = await NewCodesAsync(issue.Quantity, ct);

        foreach (var code in codes)
        {
            _db.TokenTransactions.Add(new TokenTransaction
            {
                MemberId              = issue.MemberId,
                OriginalOwnerMemberId = issue.MemberId,
                TokenTypeId           = issue.TokenTypeId,
                TransactionType       = TokenTransactionType.Purchased,
                Quantity              = 1,
                ReferenceId           = code,
                Status                = TokenInstanceStatus.Issued,
                TokenPurchaseId       = issue.PurchaseId,
                Notes                 = $"Purchased — order {issue.OrderNo}",
                CreatedBy             = issue.Actor,
                CreationDate          = issue.Now,
                LastUpdateDate        = issue.Now
            });
        }

        var balance = _db.TokenBalances.Local
                          .FirstOrDefault(b => b.MemberId == issue.MemberId && b.TokenTypeId == issue.TokenTypeId)
                      ?? await _db.TokenBalances
                          .FirstOrDefaultAsync(b => b.MemberId == issue.MemberId && b.TokenTypeId == issue.TokenTypeId, ct);

        if (balance is null)
        {
            balance = new TokenBalance
            {
                MemberId = issue.MemberId, TokenTypeId = issue.TokenTypeId, Balance = 0,
                CreatedBy = issue.Actor, CreationDate = issue.Now, LastUpdateDate = issue.Now
            };
            _db.TokenBalances.Add(balance);
        }

        balance.Add(issue.Quantity);
        balance.LastUpdateDate = issue.Now;
        balance.LastUpdateBy   = issue.Actor;

        return codes;
    }

    private async Task<List<string>> NewCodesAsync(int quantity, CancellationToken ct)
    {
        var chosen = new List<string>(quantity);

        for (var round = 0; round < MaxRounds && chosen.Count < quantity; round++)
        {
            var candidates = new HashSet<string>(StringComparer.Ordinal);
            while (candidates.Count < quantity - chosen.Count)
            {
                var code = TokenCodeGenerator.Generate();
                if (!chosen.Contains(code)) candidates.Add(code);
            }

            var batch = candidates.ToList();
            var taken = await _db.TokenTransactions.AsNoTracking()
                .Where(t => t.ReferenceId != null && batch.Contains(t.ReferenceId))
                .Select(t => t.ReferenceId!)
                .ToListAsync(ct);

            chosen.AddRange(batch.Except(taken, StringComparer.Ordinal));
        }

        if (chosen.Count < quantity)
            throw new InvalidOperationException("Could not generate unique token codes.");

        return chosen;
    }
}
```

- [ ] **Step 3: Crear `Billing/Services/Tokens/TokenPurchaseGate.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Services.Tokens;

/// <summary>Serializa las compras de UN miembro; las de miembros distintos no se esperan.</summary>
public interface IMemberFundsGate
{
    /// <summary>Abre una transacción y toma dentro el candado exclusivo del miembro. Se suelta al confirmar o deshacer.</summary>
    /// <exception cref="MemberFundsBusyException">Si no se obtuvo el candado a tiempo.</exception>
    Task<IDbContextTransaction> EnterAsync(string memberId, CancellationToken ct = default);
}

public sealed class MemberFundsBusyException(string memberId)
    : Exception($"Another token purchase for member {memberId} is in progress.");

/// <summary>
/// <c>sp_getapplock</c> con dueño Transaction: vive en SQL Server, así que vale con varias
/// instancias de Billing. Preferido a SERIALIZABLE (resolvería la carrera con un interbloqueo) y a
/// un contador con RowVersion (TokenBalance no tiene ni índice único ni RowVersion real).
/// </summary>
public sealed class SqlServerMemberFundsGate : IMemberFundsGate
{
    private const int LockTimeoutMs = 15000;

    private readonly AppDbContext _db;

    public SqlServerMemberFundsGate(AppDbContext db) => _db = db;

    public async Task<IDbContextTransaction> EnterAsync(string memberId, CancellationToken ct = default)
    {
        var tx       = await _db.Database.BeginTransactionAsync(ct);
        var resource = $"member-funds:{memberId}";

        // ToListAsync sin componer: EF manda el SQL tal cual. Un FirstAsync lo envolvería en una
        // subconsulta y SQL Server rechazaría el DECLARE/EXEC.
        var result = await _db.Database
            .SqlQuery<int>($"DECLARE @r int; EXEC @r = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = {LockTimeoutMs}; SELECT @r AS [Value]")
            .ToListAsync(ct);

        if (result.Count == 1 && result[0] >= 0)
            return tx;

        await tx.RollbackAsync(ct);
        await tx.DisposeAsync();
        throw new MemberFundsBusyException(memberId);
    }
}
```

- [ ] **Step 4: Crear `Billing.Tests/Helpers/TransactionOnlyGate.cs`**

```csharp
using Microsoft.EntityFrameworkCore.Storage;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Tests.Helpers;

/// <summary>
/// La puerta en pruebas: la transacción (no-op en InMemory) sin el candado de SQL Server. La
/// exclusión mutua real se comprueba contra SQL Server en la Tarea 16, paso 6.
/// </summary>
public sealed class TransactionOnlyGate(AppDbContext db) : IMemberFundsGate
{
    public Task<IDbContextTransaction> EnterAsync(string memberId, CancellationToken ct = default) =>
        db.Database.BeginTransactionAsync(ct);
}
```

- [ ] **Step 5: Correr las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter TokenIssuerTests -nologo` → PASS.

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.Billing MLMConquerorGlobalEdition.Billing.Tests
git commit -m "Emisor de tokens comprados con codigo unico y dueno original, y candado por miembro con sp_getapplock para que dos compras simultaneas no rompan el tope del mes"
```

---

### Task 8: Recibo por correo de la compra

**Files:**
- Create: `MLMConquerorGlobalEdition.Repository/Migrations/*_SiembraPlantillaReciboCompraTokens.cs` (vía `dotnet ef migrations add`, cuerpo a mano)
- Create: `MLMConquerorGlobalEdition.Billing/Services/Tokens/TokenPurchaseReceipts.cs`
- Test: `MLMConquerorGlobalEdition.Billing.Tests/Services/Tokens/TokenPurchaseReceiptsTests.cs`

> Billing ya llama a `AddNotifications(builder.Configuration)` (`Program.cs`, lo usa `PayoutReceiptService`), así que `IEmailService` está registrado. Ids de plantilla: los últimos sembrados son `9012` (plantilla) y `9012/9013` (localizaciones) en `20261015022139_SiembraPlantillaBloqueoDelSegundoFactor`; aquí `9014` y `9014/9015`. **Comprobar antes de generar** con `grep -h "private const int Email" MLMConquerorGlobalEdition.Repository/Migrations/*.cs | sort | tail -3` y usar el siguiente bloque libre si alguien sembró otro.

- [ ] **Step 1: Escribir la prueba (falla: el servicio no existe)**

```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;
using IEmailService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IEmailService;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Tokens;

/// <summary>
/// El recibo lleva lo que el miembro necesita para reconocer la compra, en su idioma, y nunca los
/// códigos (son al portador). Si el correo falla, la compra sigue hecha.
/// </summary>
public class TokenPurchaseReceiptsTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 10, 30, 0);

    private static (TokenPurchaseReceipts Sut, Mock<IEmailService> Email) Build(string lang = "es")
    {
        var db = TestDbContextFactory.Create();
        db.MemberProfiles.Add(new MemberProfile
        {
            MemberId = "AMB-1", Email = "ana@example.com", FirstName = "Ana", LastName = "Ruiz",
            DefaultLanguage = lang, MemberType = MemberType.Ambassador, EnrollDate = Now, Country = "US",
            CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now
        });
        db.SaveChanges();
        var email = new Mock<IEmailService>();
        return (new TokenPurchaseReceipts(db, email.Object, NullLogger<TokenPurchaseReceipts>.Instance), email);
    }

    private static TokenPurchase Compra() => new()
    {
        Id = "purchase-1", MemberId = "AMB-1", TokenTypeId = 13, Quantity = 2, UnitPrice = 99.99m,
        TotalAmount = 199.98m, PaymentMethod = TokenPaymentMethod.CommissionBalance,
        Status = TokenPurchaseStatus.Completed, CompletedAt = Now
    };

    [Fact]
    public async Task Manda_el_recibo_con_los_datos_de_la_compra_y_sin_codigos()
    {
        var (sut, email) = Build();
        Dictionary<string, string>? vars = null;
        email.Setup(e => e.SendAsync("ana@example.com", "Ana Ruiz", "es", NotificationEvents.TokenPurchaseReceipt,
                                     It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
             .Callback<string, string, string, string, Dictionary<string, string>, CancellationToken>((_, _, _, _, v, _) => vars = v)
             .Returns(Task.CompletedTask);

        await sut.SendAsync(Compra(), "TP1015-1", "Enrollment: VIP Member");

        vars!["TokenType"].Should().Be("Enrollment: VIP Member");
        vars["Quantity"].Should().Be("2");
        vars["Amount"].Should().Be("199.98 USD");
        vars["PaymentMethod"].Should().Be("saldo de comisiones");
        vars["PurchaseDate"].Should().Be("2026-10-15 10:30");
        vars["PurchaseId"].Should().Be("purchase-1");
        vars["OrderNo"].Should().Be("TP1015-1");
        vars.Keys.Should().NotContain(k => k.Contains("Code", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Si_el_correo_falla_no_lanza()
    {
        var (sut, email) = Build("en");
        email.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                                     It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("SES caido"));

        var act = () => sut.SendAsync(Compra(), "TP1", "VIP");

        await act.Should().NotThrowAsync();
    }
}
```

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter TokenPurchaseReceiptsTests -nologo` → FAIL.

- [ ] **Step 2: Crear `Billing/Services/Tokens/TokenPurchaseReceipts.cs`**

```csharp
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using IEmailService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IEmailService;

namespace MLMConquerorGlobalEdition.Billing.Services.Tokens;

public interface ITokenPurchaseReceipts
{
    /// <summary>Manda el recibo. No lanza: un fallo de correo se registra y la compra sigue hecha.</summary>
    Task SendAsync(TokenPurchase purchase, string? orderNo, string tokenTypeName, CancellationToken ct = default);
}

/// <summary>
/// Recibo de una compra completada o de una compra en cripto confirmada. Sin códigos de token: son
/// al portador y un buzón no es un lugar seguro para ellos; la pantalla los muestra al comprar.
/// </summary>
public sealed class TokenPurchaseReceipts : ITokenPurchaseReceipts
{
    private readonly AppDbContext _db;
    private readonly IEmailService _email;
    private readonly ILogger<TokenPurchaseReceipts> _logger;

    public TokenPurchaseReceipts(AppDbContext db, IEmailService email, ILogger<TokenPurchaseReceipts> logger)
    {
        _db     = db;
        _email  = email;
        _logger = logger;
    }

    public async Task SendAsync(TokenPurchase purchase, string? orderNo, string tokenTypeName, CancellationToken ct = default)
    {
        try
        {
            var member = await _db.MemberProfiles.AsNoTracking()
                .Where(m => m.MemberId == purchase.MemberId)
                .Select(m => new { m.Email, m.FirstName, m.LastName, m.DefaultLanguage })
                .FirstOrDefaultAsync(ct);

            if (member is null || string.IsNullOrWhiteSpace(member.Email))
            {
                _logger.LogWarning("Recibo de compra {PurchaseId} sin enviar: el miembro no tiene correo.", purchase.Id);
                return;
            }

            var fullName = $"{member.FirstName} {member.LastName}".Trim();
            var language = string.IsNullOrWhiteSpace(member.DefaultLanguage) ? "en" : member.DefaultLanguage;

            await _email.SendAsync(member.Email, fullName, language, NotificationEvents.TokenPurchaseReceipt,
                new Dictionary<string, string>
                {
                    ["FullName"]      = fullName,
                    ["TokenType"]     = tokenTypeName,
                    ["Quantity"]      = purchase.Quantity.ToString(CultureInfo.InvariantCulture),
                    ["Amount"]        = purchase.TotalAmount.ToString("F2", CultureInfo.InvariantCulture) + " USD",
                    ["PaymentMethod"] = MethodText(purchase.PaymentMethod, language),
                    ["PurchaseDate"]  = (purchase.CompletedAt ?? purchase.CreationDate).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    ["PurchaseId"]    = purchase.Id,
                    ["OrderNo"]       = orderNo ?? purchase.OrderId
                }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo enviar el recibo de la compra de tokens {PurchaseId}.", purchase.Id);
        }
    }

    /// <summary>La plantilla solo existe en en/es; el resto de idiomas cae a inglés en el transporte.</summary>
    private static string MethodText(TokenPaymentMethod method, string language)
    {
        var es = language.StartsWith("es", StringComparison.OrdinalIgnoreCase);
        return method switch
        {
            TokenPaymentMethod.CommissionBalance => es ? "saldo de comisiones" : "commission balance",
            TokenPaymentMethod.EWallet           => "eWallet",
            TokenPaymentMethod.CreditCard        => es ? "tarjeta" : "card",
            TokenPaymentMethod.Crypto            => es ? "cripto" : "crypto",
            _                                    => method.ToString()
        };
    }
}
```

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter TokenPurchaseReceiptsTests -nologo` → PASS.

- [ ] **Step 3: Sembrar la plantilla**

```bash
dotnet ef migrations add SiembraPlantillaReciboCompraTokens \
  --project MLMConquerorGlobalEdition.Repository \
  --startup-project MLMConquerorGlobalEdition.Billing
```

Reemplazar el contenido de la clase generada (el `.Designer.cs` no se toca) por:

```csharp
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <summary>
    /// Siembra la plantilla TOKEN_PURCHASE_RECEIPT (NotificationEvents.TokenPurchaseReceipt): el recibo
    /// de una compra de tokens desde el BizCenter, y de una compra en cripto cuando se confirma. Sin la
    /// fila, el transporte lanza al buscarla; TokenPurchaseReceipts lo registra y la compra sigue.
    ///
    /// Solo en/es, como las demás plantillas recientes: el transporte respalda a "en".
    /// Variables: {{FullName}}, {{TokenType}}, {{Quantity}}, {{Amount}}, {{PaymentMethod}},
    /// {{PurchaseDate}} (hora de servidor), {{PurchaseId}}, {{OrderNo}}. Sin códigos de token: son al
    /// portador.
    /// </summary>
    public partial class SiembraPlantillaReciboCompraTokens : Migration
    {
        // Siguiente bloque libre tras SiembraPlantillaBloqueoDelSegundoFactor (9012 + 9012/9013).
        private const int EmailTemplateId       = 9014;
        private const int EmailLocalizationEnId = 9014;
        private const int EmailLocalizationEsId = 9015;

        private const string Actor = "system:seed";

        private static readonly DateTime SeedDate = new(2026, 10, 15, 0, 0, 0, 0, DateTimeKind.Unspecified);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "EmailTemplates",
                columns: new[] { "Id", "Name", "EventType", "Category", "Description", "IsActive", "CreationDate", "CreatedBy", "LastUpdateDate", "LastUpdateBy" },
                values: new object[] { EmailTemplateId, "Token Purchase Receipt", "TOKEN_PURCHASE_RECEIPT", "Billing", "Sent to the member when a token purchase from the BizCenter is completed, or when a crypto token purchase is confirmed. Never includes token codes.", true, SeedDate, Actor, null, null });

            migrationBuilder.InsertData(
                table: "EmailTemplateLocalizations",
                columns: new[] { "Id", "EmailTemplateId", "LanguageCode", "Subject", "HtmlBody", "TextBody", "CreationDate", "CreatedBy", "LastUpdateDate", "LastUpdateBy" },
                values: new object[,]
                {
                    { EmailLocalizationEnId, EmailTemplateId, "en", "Your token purchase receipt — order {{OrderNo}}", EmailHtmlBodyEn, EmailTextBodyEn, SeedDate, Actor, null, null },
                    { EmailLocalizationEsId, EmailTemplateId, "es", "Recibo de tu compra de tokens — pedido {{OrderNo}}", EmailHtmlBodyEs, EmailTextBodyEs, SeedDate, Actor, null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "EmailTemplateLocalizations", keyColumn: "Id", keyValue: EmailLocalizationEnId);
            migrationBuilder.DeleteData(table: "EmailTemplateLocalizations", keyColumn: "Id", keyValue: EmailLocalizationEsId);
            migrationBuilder.DeleteData(table: "EmailTemplates", keyColumn: "Id", keyValue: EmailTemplateId);
        }

        private const string EmailHtmlBodyEn = @"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;"">
  <tr>
    <td align=""center"">
      <table role=""presentation"" width=""480"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 480px;"">
        <tr><td style=""font-size: 16px; color: #222222; padding-bottom: 16px; line-height: 1.5;"">Hi {{FullName}}, thanks for your purchase.</td></tr>
        <tr>
          <td style=""font-size: 14px; color: #222222; padding-bottom: 16px; line-height: 1.7;"">
            <strong>Tokens:</strong> {{Quantity}} × {{TokenType}}<br />
            <strong>Total:</strong> {{Amount}}<br />
            <strong>Paid with:</strong> {{PaymentMethod}}<br />
            <strong>Date:</strong> {{PurchaseDate}}<br />
            <strong>Order:</strong> {{OrderNo}}<br />
            <strong>Purchase id:</strong> {{PurchaseId}}
          </td>
        </tr>
        <tr><td style=""font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;"">Your tokens are in the Tokens section of your Biz Center. Token purchases are non-refundable.</td></tr>
        <tr><td style=""font-size: 13px; color: #b00020; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;"">If you did not make this purchase, sign in the usual way, change your password and contact support.</td></tr>
      </table>
    </td>
  </tr>
</table>";

        private const string EmailTextBodyEn =
            "Hi {{FullName}}, thanks for your purchase.\n\n" +
            "Tokens: {{Quantity}} x {{TokenType}}\nTotal: {{Amount}}\nPaid with: {{PaymentMethod}}\nDate: {{PurchaseDate}}\nOrder: {{OrderNo}}\nPurchase id: {{PurchaseId}}\n\n" +
            "Your tokens are in the Tokens section of your Biz Center. Token purchases are non-refundable.\n\n" +
            "If you did not make this purchase, sign in the usual way, change your password and contact support.";

        private const string EmailHtmlBodyEs = @"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;"">
  <tr>
    <td align=""center"">
      <table role=""presentation"" width=""480"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 480px;"">
        <tr><td style=""font-size: 16px; color: #222222; padding-bottom: 16px; line-height: 1.5;"">Hola {{FullName}}, gracias por tu compra.</td></tr>
        <tr>
          <td style=""font-size: 14px; color: #222222; padding-bottom: 16px; line-height: 1.7;"">
            <strong>Tokens:</strong> {{Quantity}} × {{TokenType}}<br />
            <strong>Total:</strong> {{Amount}}<br />
            <strong>Pagado con:</strong> {{PaymentMethod}}<br />
            <strong>Fecha:</strong> {{PurchaseDate}}<br />
            <strong>Pedido:</strong> {{OrderNo}}<br />
            <strong>Id de compra:</strong> {{PurchaseId}}
          </td>
        </tr>
        <tr><td style=""font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;"">Tus tokens están en la sección Tokens de tu Biz Center. Las compras de tokens no son reembolsables.</td></tr>
        <tr><td style=""font-size: 13px; color: #b00020; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;"">Si no hiciste esta compra, entra por el camino de siempre, cambia tu contraseña y contacta con soporte.</td></tr>
      </table>
    </td>
  </tr>
</table>";

        private const string EmailTextBodyEs =
            "Hola {{FullName}}, gracias por tu compra.\n\n" +
            "Tokens: {{Quantity}} x {{TokenType}}\nTotal: {{Amount}}\nPagado con: {{PaymentMethod}}\nFecha: {{PurchaseDate}}\nPedido: {{OrderNo}}\nId de compra: {{PurchaseId}}\n\n" +
            "Tus tokens están en la sección Tokens de tu Biz Center. Las compras de tokens no son reembolsables.\n\n" +
            "Si no hiciste esta compra, entra por el camino de siempre, cambia tu contraseña y contacta con soporte.";
    }
}
```

Verificar que el Designer quedó con el mismo nombre de clase y que el snapshot no cambió:

Run: `git status --short MLMConquerorGlobalEdition.Repository/Migrations`
Expected: solo los dos archivos nuevos `*_SiembraPlantillaReciboCompraTokens*.cs` (el snapshot no cambia: `InsertData` no toca el modelo).

- [ ] **Step 4: Compilar**

Run: `dotnet build MLMConquerorGlobalEdition.Billing -v q -nologo` → `0 Error(s)`.

- [ ] **Step 5: Commit**

```bash
git add MLMConquerorGlobalEdition.Repository MLMConquerorGlobalEdition.Billing MLMConquerorGlobalEdition.Billing.Tests
git commit -m "Recibo por correo de la compra de tokens: plantilla TOKEN_PURCHASE_RECEIPT en/es sembrada y servicio que la envia sin los codigos y sin deshacer la compra si el correo falla"
```

---

### Task 9: Billing — el servicio de compra

**Files:**
- Create: `MLMConquerorGlobalEdition.Billing/Services/Tokens/TokenPurchaseService.cs`
- Test: `MLMConquerorGlobalEdition.Billing.Tests/Services/Tokens/TokenPurchaseServiceTests.cs`

- [ ] **Step 1: Escribir las pruebas (fallan: el servicio no existe)**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Crypto;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using ICacheService            = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Tokens;

/// <summary>
/// La compra de punta a punta contra InMemory: cada método cobra y emite o no deja nada; el tope y la
/// clave aguantan; la tarjeta usa solo pasos inmediatos; la eWallet verifica y lo dudoso va a
/// conciliar; la cripto ofrece solo monedas con dirección; confirmar y conciliar emiten una vez.
/// </summary>
public class TokenPurchaseServiceTests
{
    private const string  MemberId  = "AMB-100001";
    private const string  Actor     = "user-001";
    private const int     VipType   = 13;
    private const int     FreeType  = 92;
    private const decimal UnitPrice = 99.99m; // SetupFee 50 + MonthlyFee 49.99
    private static readonly DateTime FixedNow = new(2026, 10, 15, 10, 0, 0);

    private readonly AppDbContext _db = TestDbContextFactory.Create();
    private readonly Mock<IGatewayRouter> _router = new();
    private readonly Mock<IGatewayChargeOrchestrator> _orchestrator = new();
    private readonly Mock<IEWalletFunds> _eWallet = new();
    private readonly Mock<ITokenPurchaseReceipts> _receipts = new();

    public TokenPurchaseServiceTests()
    {
        _db.MemberProfiles.Add(new MemberProfile
        {
            MemberId = MemberId, Email = "amb@example.com", FirstName = "Ana", LastName = "Ruiz",
            MemberType = MemberType.Ambassador, Status = MemberAccountStatus.Active,
            EnrollDate = FixedNow.AddYears(-1), Country = "US",
            CreatedBy = "seed", CreationDate = FixedNow, LastUpdateDate = FixedNow
        });
        _db.Products.Add(new Product { Id = "prod-vip", Name = "VIP", Description = "VIP", ImageUrl = "", SetupFee = 50m, MonthlyFee = 49.99m, IsActive = true, CreatedBy = "seed", CreationDate = FixedNow });
        _db.TokenTypes.AddRange(
            new TokenType { Id = VipType, Name = "Enrollment: VIP Member", IsActive = true, IsPurchasable = true, Category = TokenCategory.Enrollment, CreatedBy = "seed", CreationDate = FixedNow },
            new TokenType { Id = FreeType, Name = "Enrollment: VIP Member FREE", IsActive = true, IsPurchasable = false, Category = TokenCategory.Enrollment, CreatedBy = "seed", CreationDate = FixedNow });
        _db.TokenTypeProducts.AddRange(
            new TokenTypeProduct { TokenTypeId = VipType, ProductId = "prod-vip", Role = TokenProductRole.Granted, CreatedBy = "seed", CreationDate = FixedNow },
            new TokenTypeProduct { TokenTypeId = FreeType, ProductId = "prod-vip", Role = TokenProductRole.Granted, CreatedBy = "seed", CreationDate = FixedNow });
        GlobalParameter P(string k, string v) => new() { Key = k, Value = v, CreatedBy = "seed", CreationDate = FixedNow };
        _db.GlobalParameters.AddRange(
            P(CryptoDepositKeys.AddressKey("BTC"), "bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh"),
            P(CryptoDepositKeys.AddressKey("USDT"), "TQ5NkEQwK6x9C2iGBDbTj9JxF8tqmL7Ak5"),
            P(CryptoDepositKeys.NetworkKey("USDT"), "TRC-20"));
        _db.SaveChanges();
    }

    private TokenPurchaseService Service()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.Now).Returns(FixedNow);
        clock.Setup(c => c.UtcNow).Returns(FixedNow);
        var brands = new Mock<ICardBrandDetector>();
        brands.Setup(b => b.Detect(It.IsAny<string>())).Returns(CardBrand.Visa);
        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var push = new Mock<IPushNotificationService>();
        push.Setup(p => p.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _receipts.Setup(r => r.SendAsync(It.IsAny<TokenPurchase>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        return new TokenPurchaseService(
            _db, clock.Object,
            new TokenPricingService(_db), new TokenMonthlyQuota(_db), new TransactionOnlyGate(_db),
            new TokenIssuer(_db), new CommissionBalanceService(_db, clock.Object), _eWallet.Object,
            _router.Object, _orchestrator.Object, brands.Object, new CryptoDepositAddressReader(_db),
            _receipts.Object, cache.Object, push.Object, NullLogger<TokenPurchaseService>.Instance);
    }

    private static PurchaseTokensRequest Request(
        string method, int qty = 1, string? key = null, string? card = null, string? crypto = null, int type = VipType) => new()
    {
        TokenTypeId = type, Quantity = qty, PaymentMethod = method,
        CreditCardId = card, CryptoCurrency = crypto, IdempotencyKey = key ?? Guid.NewGuid().ToString("N")
    };

    private Task<Result<TokenPurchaseResultDto>> Buy(PurchaseTokensRequest r) =>
        Service().PurchaseAsync(new TokenPurchaseInput(MemberId, Actor, r));

    private void SeedCommissions(decimal amount)
    {
        _db.CommissionEarnings.Add(new CommissionEarning
        {
            BeneficiaryMemberId = MemberId, CommissionTypeId = 1, Amount = amount,
            Status = CommissionEarningStatus.Pending, EarnedDate = FixedNow.AddDays(-10),
            PaymentDate = FixedNow.AddDays(-1), CreatedBy = "seed",
            CreationDate = FixedNow.AddDays(-10), LastUpdateDate = FixedNow.AddDays(-10)
        });
        _db.SaveChanges();
    }

    private void SeedPurchased(int count, DateTime at)
    {
        for (var i = 0; i < count; i++)
            _db.TokenTransactions.Add(new TokenTransaction
            {
                MemberId = MemberId, OriginalOwnerMemberId = MemberId, TokenTypeId = VipType,
                TransactionType = TokenTransactionType.Purchased, Quantity = 1, ReferenceId = $"OLD{i:0000}",
                Status = TokenInstanceStatus.Issued, CreatedBy = "seed", CreationDate = at
            });
        _db.SaveChanges();
    }

    private void SeedCard(string id, string owner)
    {
        _db.CreditCards.Add(new MemberCreditCard
        {
            Id = id, MemberId = owner, First6 = "411111", Last4 = "1111", CardBrand = "Visa",
            CardToken = "tok_1", SpreedlyPaymentMethodToken = "spr_1", CreatedBy = "seed", CreationDate = FixedNow
        });
        _db.SaveChanges();
    }

    private void RouterWithImmediateAndDelayedSteps() =>
        _router.Setup(r => r.ResolveAsync(It.IsAny<GatewayRoutingContext>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(Result<GatewayRoutingPlan>.Success(new GatewayRoutingPlan
               {
                   RouteBucketKey = "US-VISA",
                   Steps = new[]
                   {
                       new GatewayAttemptPlan { CardProcessor = CardProcessor.NmiSpreedly, Amount = UnitPrice, FallbackStepIndex = 0 },
                       new GatewayAttemptPlan { CardProcessor = CardProcessor.NmiDirect, Amount = UnitPrice, FallbackStepIndex = 1, DelayMinutes = 60 }
                   }
               }));

    private void EWalletWith(decimal balance, EWalletDebitStatus outcome, string? code = null)
    {
        _eWallet.Setup(e => e.UserNameOfAsync(MemberId, It.IsAny<CancellationToken>())).ReturnsAsync("ipayout-1");
        _eWallet.Setup(e => e.BalanceAsync("ipayout-1", It.IsAny<CancellationToken>())).ReturnsAsync(Result<decimal>.Success(balance));
        _eWallet.Setup(e => e.DebitAsync("ipayout-1", It.IsAny<decimal>(), balance, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EWalletDebitOutcome(outcome, code, "detalle"));
    }

    // ── Comisiones ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Comisiones_con_saldo_cobra_emite_manda_recibo_y_deja_el_pedido_pagado_sin_detalle()
    {
        SeedCommissions(500m);

        var r = await Buy(Request(TokenPaymentMethods.CommissionBalance, qty: 2));

        r.IsSuccess.Should().BeTrue();
        r.Value!.Status.Should().Be(TokenPurchaseStatuses.Completed);
        r.Value.TotalAmount.Should().Be(UnitPrice * 2);
        r.Value.MonthlyRemaining.Should().Be(48);
        var tokens = await _db.TokenTransactions.Where(t => t.ReferenceId != null).ToListAsync();
        tokens.Should().HaveCount(2).And.OnlyContain(t =>
            t.TransactionType == TokenTransactionType.Purchased && t.OriginalOwnerMemberId == MemberId &&
            t.Status == TokenInstanceStatus.Issued && t.TokenPurchaseId == r.Value.PurchaseId);
        tokens.Select(t => t.ReferenceId).Should().BeEquivalentTo(r.Value.TokenCodes);
        (await _db.CommissionEarnings.SingleAsync(e => e.Amount < 0)).Amount.Should().Be(-(UnitPrice * 2));
        (await _db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Paid);
        _db.OrderDetails.Should().BeEmpty("comprar no genera puntos: llegan al canjear");
        _receipts.Verify(x => x.SendAsync(It.Is<TokenPurchase>(p => p.Id == r.Value.PurchaseId), It.IsAny<string?>(),
                                          "Enrollment: VIP Member", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Comisiones_sin_saldo_no_escribe_nada()
    {
        SeedCommissions(50m);

        (await Buy(Request(TokenPaymentMethods.CommissionBalance))).ErrorCode
            .Should().Be(TokenPurchaseErrors.InsufficientCommissionBalance);
        _db.Orders.Should().BeEmpty();
        _db.TokenPurchases.Should().BeEmpty();
    }

    // ── Tope, clave y reglas ──────────────────────────────────────────────────

    [Fact]
    public async Task Con_49_comprados_este_mes_no_deja_comprar_2_pero_si_1()
    {
        SeedCommissions(500m);
        SeedPurchased(49, FixedNow.AddDays(-3));

        (await Buy(Request(TokenPaymentMethods.CommissionBalance, qty: 2))).ErrorCode.Should().Be(TokenPurchaseErrors.MonthlyLimitReached);
        (await Buy(Request(TokenPaymentMethods.CommissionBalance, qty: 1))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Los_comprados_el_mes_pasado_no_cuentan()
    {
        SeedCommissions(500m);
        SeedPurchased(50, FixedNow.AddMonths(-1));

        (await Buy(Request(TokenPaymentMethods.CommissionBalance))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task La_misma_clave_devuelve_la_misma_compra_y_no_cobra_dos_veces()
    {
        SeedCommissions(500m);
        var req = Request(TokenPaymentMethods.CommissionBalance, key: "clave-unica-1");

        var first  = await Buy(req);
        var second = await Buy(req);

        second.Value!.PurchaseId.Should().Be(first.Value!.PurchaseId);
        second.Value.TokenCodes.Should().BeEquivalentTo(first.Value.TokenCodes);
        _db.TokenPurchases.Should().HaveCount(1);
        _db.CommissionEarnings.Count(e => e.Amount < 0).Should().Be(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("tiene espacios")]
    public async Task Sin_clave_valida_no_compra(string key)
        => (await Buy(Request(TokenPaymentMethods.CommissionBalance, key: key))).ErrorCode
            .Should().Be(TokenPurchaseErrors.IdempotencyKeyRequired);

    [Fact]
    public async Task Una_variante_que_no_se_vende_lo_dice()
        => (await Buy(Request(TokenPaymentMethods.CommissionBalance, type: FreeType))).ErrorCode
            .Should().Be(TokenPurchaseErrors.NotForSale);

    [Fact]
    public async Task SafetyPay_no_es_un_metodo()
        => (await Buy(Request("SafetyPay"))).ErrorCode.Should().Be(TokenPurchaseErrors.PaymentMethodNotSupported);

    [Fact]
    public async Task Un_miembro_inactivo_no_compra()
    {
        (await _db.MemberProfiles.SingleAsync()).Status = MemberAccountStatus.Inactive;
        await _db.SaveChangesAsync();

        (await Buy(Request(TokenPaymentMethods.CommissionBalance))).ErrorCode.Should().Be(TokenPurchaseErrors.MemberInactive);
    }

    // ── eWallet ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task eWallet_cobrada_emite_con_la_compra_como_referencia()
    {
        EWalletWith(500m, EWalletDebitStatus.Completed);

        var r = await Buy(Request(TokenPaymentMethods.EWallet));

        r.IsSuccess.Should().BeTrue();
        r.Value!.TokenCodes.Should().HaveCount(1);
        var pago = await _db.PaymentHistories.SingleAsync();
        pago.GatewayName.Should().Be("eWallet");
        pago.GatewayTransactionId.Should().Be(r.Value.PurchaseId);
        _eWallet.Verify(e => e.DebitAsync("ipayout-1", UnitPrice, 500m, r.Value.PurchaseId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task eWallet_dudosa_queda_a_conciliar_sin_tokens_y_la_clave_no_reintenta()
    {
        EWalletWith(500m, EWalletDebitStatus.NeedsReconciliation, "EWALLET_DEBIT_PENDING");
        var req = Request(TokenPaymentMethods.EWallet, key: "clave-ew-1");

        var r = await Buy(req);

        r.ErrorCode.Should().Be(TokenPurchaseErrors.NeedsReview);
        var compra = await _db.TokenPurchases.SingleAsync();
        compra.Status.Should().Be(TokenPurchaseStatus.NeedsReconciliation);
        compra.FailureCode.Should().Be("EWALLET_DEBIT_PENDING");
        _db.TokenTransactions.Should().BeEmpty();
        (await new TokenMonthlyQuota(_db).UsedAsync(MemberId, FixedNow)).Should().Be(1);

        (await Buy(req)).ErrorCode.Should().Be(TokenPurchaseErrors.NeedsReview);
        _eWallet.Verify(e => e.DebitAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task eWallet_rechazada_falla_y_cancela_el_pedido()
    {
        EWalletWith(500m, EWalletDebitStatus.Rejected, "EWALLET_ITEM_-5");

        (await Buy(Request(TokenPaymentMethods.EWallet))).ErrorCode.Should().Be(TokenPurchaseErrors.EWalletRejected);
        (await _db.TokenPurchases.SingleAsync()).Status.Should().Be(TokenPurchaseStatus.Failed);
        (await _db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task eWallet_sin_saldo_o_sin_cuenta_no_escribe()
    {
        EWalletWith(10m, EWalletDebitStatus.Completed);
        (await Buy(Request(TokenPaymentMethods.EWallet))).ErrorCode.Should().Be(TokenPurchaseErrors.InsufficientEWalletBalance);

        _eWallet.Setup(e => e.UserNameOfAsync(MemberId, It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        (await Buy(Request(TokenPaymentMethods.EWallet))).ErrorCode.Should().Be(TokenPurchaseErrors.EWalletNotFound);

        _db.Orders.Should().BeEmpty();
    }

    // ── Tarjeta ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Tarjeta_cobra_solo_los_pasos_inmediatos_con_el_token_de_Spreedly_y_emite()
    {
        SeedCard("card-1", MemberId);
        RouterWithImmediateAndDelayedSteps();
        GatewayRoutingPlan? usado = null;
        OrchestratorChargeRequest? pedido = null;
        _orchestrator
            .Setup(o => o.ExecuteAsync(It.IsAny<GatewayRoutingPlan>(), It.IsAny<GatewayRoutingContext>(), It.IsAny<OrchestratorChargeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GatewayRoutingPlan, GatewayRoutingContext, OrchestratorChargeRequest, CancellationToken>((p, _, r, _) => { usado = p; pedido = r; })
            .ReturnsAsync(Result<OrchestratorChargeResult>.Success(new OrchestratorChargeResult
            {
                Status = "Success", PaymentHistoryId = "ph-card", GatewayTransactionId = "tx-1", ProcessorUsed = "NmiSpreedly", AmountCharged = UnitPrice
            }));

        var r = await Buy(Request(TokenPaymentMethods.CreditCard, card: "card-1"));

        r.IsSuccess.Should().BeTrue();
        usado!.Steps.Should().OnlyContain(s => s.DelayMinutes == 0);
        pedido!.TokenizedCardRef.Should().Be("spr_1");
        pedido.IsRecurring.Should().BeFalse();
        (await _db.TokenPurchases.SingleAsync()).PaymentHistoryId.Should().Be("ph-card");
    }

    [Fact]
    public async Task Tarjeta_rechazada_marca_la_compra_fallida_y_libera_el_cupo()
    {
        SeedCard("card-1", MemberId);
        RouterWithImmediateAndDelayedSteps();
        _orchestrator
            .Setup(o => o.ExecuteAsync(It.IsAny<GatewayRoutingPlan>(), It.IsAny<GatewayRoutingContext>(), It.IsAny<OrchestratorChargeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrchestratorChargeResult>.Failure("DECLINED", "Do not honor"));

        (await Buy(Request(TokenPaymentMethods.CreditCard, card: "card-1"))).ErrorCode.Should().Be(TokenPurchaseErrors.CardChargeFailed);
        (await _db.TokenPurchases.SingleAsync()).Status.Should().Be(TokenPurchaseStatus.Failed);
        (await _db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Cancelled);
        (await new TokenMonthlyQuota(_db).UsedAsync(MemberId, FixedNow)).Should().Be(0);
    }

    [Fact]
    public async Task La_tarjeta_de_otro_no_se_cobra()
    {
        SeedCard("card-otro", "AMB-999999");

        (await Buy(Request(TokenPaymentMethods.CreditCard, card: "card-otro"))).ErrorCode.Should().Be(TokenPurchaseErrors.CreditCardNotFound);
        _orchestrator.VerifyNoOtherCalls();
    }

    // ── Cripto ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cripto_devuelve_la_direccion_reserva_cupo_y_no_emite()
    {
        var r = await Buy(Request(TokenPaymentMethods.Crypto, qty: 3, crypto: "usdt"));

        r.Value!.Status.Should().Be(TokenPurchaseStatuses.AwaitingCryptoConfirmation);
        r.Value.CryptoCurrency.Should().Be("USDT");
        r.Value.CryptoDepositAddress.Should().Be("TQ5NkEQwK6x9C2iGBDbTj9JxF8tqmL7Ak5");
        r.Value.CryptoNetwork.Should().Be("TRC-20");
        r.Value.TotalAmount.Should().Be(UnitPrice * 3);
        var conf = await _db.CryptoPaymentConfirmations.SingleAsync();
        conf.Purpose.Should().Be(CryptoPaymentPurpose.TokenPurchase);
        conf.AmountDue.Should().Be(UnitPrice * 3);
        (await new TokenMonthlyQuota(_db).UsedAsync(MemberId, FixedNow)).Should().Be(3);
        _db.TokenTransactions.Should().BeEmpty();
    }

    [Theory]
    [InlineData("ETH", TokenPurchaseErrors.CryptoAddressNotConfigured)]
    [InlineData("DOGE", TokenPurchaseErrors.CryptoCurrencyNotSupported)]
    public async Task Cripto_sin_direccion_o_no_admitida_no_crea_pedido(string moneda, string codigo)
    {
        (await Buy(Request(TokenPaymentMethods.Crypto, crypto: moneda))).ErrorCode.Should().Be(codigo);
        _db.Orders.Should().BeEmpty();
    }

    [Fact]
    public async Task El_catalogo_trae_las_monedas_con_direccion()
        => (await Service().GetCatalogAsync(MemberId)).CryptoDeposits.Select(d => d.Currency).Should().Equal("BTC", "USDT");

    [Fact]
    public async Task Confirmar_cripto_emite_manda_recibo_y_no_se_confirma_dos_veces()
    {
        var orderId = (await Buy(Request(TokenPaymentMethods.Crypto, qty: 2, crypto: "BTC"))).Value!.OrderId;

        var ok = await Service().ConfirmCryptoAsync(orderId,
            new ConfirmCryptoTokenPurchaseRequest { CryptoTransactionId = " 0xabc ", Notes = "red BTC" }, "admin-1", "admin@x.com");

        ok.Value!.TokenCodes.Should().HaveCount(2);
        var conf = await _db.CryptoPaymentConfirmations.SingleAsync();
        conf.Status.Should().Be(CryptoPaymentConfirmationStatus.Confirmed);
        conf.CryptoTransactionId.Should().Be("0xabc");
        conf.ConfirmedByEmail.Should().Be("admin@x.com");
        (await _db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Paid);
        _receipts.Verify(x => x.SendAsync(It.IsAny<TokenPurchase>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        (await Service().ConfirmCryptoAsync(orderId, new ConfirmCryptoTokenPurchaseRequest { CryptoTransactionId = "0xabc" }, "admin-1", "admin@x.com"))
            .ErrorCode.Should().Be(TokenPurchaseErrors.CryptoAlreadyConfirmed);
        _db.TokenTransactions.Count(t => t.ReferenceId != null).Should().Be(2);
    }

    [Fact]
    public async Task Billing_no_confirma_el_cobro_en_cripto_de_un_alta()
    {
        _db.Orders.Add(new Orders { Id = "ord-signup", MemberId = MemberId, Status = OrderStatus.Processing, OrderDate = FixedNow, CreatedBy = "seed", CreationDate = FixedNow });
        _db.CryptoPaymentConfirmations.Add(new CryptoPaymentConfirmation
        {
            OrderId = "ord-signup", MemberId = MemberId, MemberEmail = "amb@example.com", CryptoCurrency = "BTC",
            AmountDue = 80m, Purpose = CryptoPaymentPurpose.Signup, CreatedBy = "seed", CreationDate = FixedNow
        });
        await _db.SaveChangesAsync();

        (await Service().ConfirmCryptoAsync("ord-signup", new ConfirmCryptoTokenPurchaseRequest { CryptoTransactionId = "0x1" }, "admin-1", "admin@x.com"))
            .ErrorCode.Should().Be(TokenPurchaseErrors.CryptoPaymentNotFound);
    }

    // ── Conciliación ──────────────────────────────────────────────────────────

    private async Task<string> CompraAConciliar()
    {
        EWalletWith(500m, EWalletDebitStatus.NeedsReconciliation, "EWALLET_OUTCOME_UNKNOWN");
        await Buy(Request(TokenPaymentMethods.EWallet, qty: 2));
        return (await _db.TokenPurchases.SingleAsync()).Id;
    }

    [Fact]
    public async Task Conciliar_como_cobrada_emite_y_manda_recibo()
    {
        var id = await CompraAConciliar();

        var r = await Service().ReconcileAsync(id,
            new ReconcileTokenPurchaseRequest { Charged = true, GatewayReference = "ipayout-tx-9", Notes = "visto en i-Payout" }, "tesoreria@x.com");

        r.Value!.TokenCodes.Should().HaveCount(2);
        (await _db.TokenPurchases.SingleAsync()).Status.Should().Be(TokenPurchaseStatus.Completed);
        (await _db.PaymentHistories.SingleAsync()).GatewayTransactionId.Should().Be("ipayout-tx-9");
        _receipts.Verify(x => x.SendAsync(It.IsAny<TokenPurchase>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Conciliar_como_no_cobrada_libera_el_cupo_y_no_se_concilia_dos_veces()
    {
        var id = await CompraAConciliar();

        (await Service().ReconcileAsync(id, new ReconcileTokenPurchaseRequest { Charged = false }, "tesoreria@x.com")).IsSuccess.Should().BeTrue();
        (await _db.TokenPurchases.SingleAsync()).Status.Should().Be(TokenPurchaseStatus.Failed);
        (await _db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Cancelled);
        (await new TokenMonthlyQuota(_db).UsedAsync(MemberId, FixedNow)).Should().Be(0);

        (await Service().ReconcileAsync(id, new ReconcileTokenPurchaseRequest { Charged = true }, "tesoreria@x.com"))
            .ErrorCode.Should().Be(TokenPurchaseErrors.NotUnderReview);
    }

    // ── Cripto sin pagar: cancelar, rechazar, caducar (D18) ────────────────────

    private async Task<TokenPurchaseResultDto> CriptoPendiente(int qty = 3, string? key = null) =>
        (await Buy(Request(TokenPaymentMethods.Crypto, qty: qty, crypto: "USDT", key: key))).Value!;

    [Fact]
    public async Task El_miembro_cancela_su_cripto_sin_pagar_libera_el_cupo_y_ya_no_se_confirma()
    {
        var p = await CriptoPendiente();

        var r = await Service().CancelCryptoAsync(MemberId, p.PurchaseId, Actor);

        r.Value!.Status.Should().Be(TokenPurchaseStatuses.Cancelled);
        (await _db.TokenPurchases.SingleAsync()).Status.Should().Be(TokenPurchaseStatus.Cancelled);
        (await _db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Cancelled);
        (await _db.CryptoPaymentConfirmations.SingleAsync()).Status.Should().Be(CryptoPaymentConfirmationStatus.Cancelled);
        (await new TokenMonthlyQuota(_db).UsedAsync(MemberId, FixedNow)).Should().Be(0);

        (await Service().ConfirmCryptoAsync(p.OrderId, new ConfirmCryptoTokenPurchaseRequest { CryptoTransactionId = "0x1" }, "admin-1", "admin@x.com"))
            .ErrorCode.Should().Be(TokenPurchaseErrors.NotAwaitingCrypto);
        _db.TokenTransactions.Should().BeEmpty();
    }

    [Fact]
    public async Task Nadie_cancela_la_compra_de_otro_ni_una_que_no_es_cripto_pendiente()
    {
        var p = await CriptoPendiente();
        (await Service().CancelCryptoAsync("AMB-999999", p.PurchaseId, "otro")).ErrorCode.Should().Be(TokenPurchaseErrors.PurchaseNotFound);

        SeedCommissions(500m);
        var pagada = (await Buy(Request(TokenPaymentMethods.CommissionBalance))).Value!;
        (await Service().CancelCryptoAsync(MemberId, pagada.PurchaseId, Actor)).ErrorCode.Should().Be(TokenPurchaseErrors.NotAwaitingCrypto);
    }

    [Fact]
    public async Task El_admin_rechaza_con_motivo_y_la_misma_clave_lo_devuelve()
    {
        var p = await CriptoPendiente(2, key: "clave-cripto-1");

        var r = await Service().RejectCryptoAsync(p.OrderId, new RejectCryptoTokenPurchaseRequest { Reason = "No llegó" }, "admin-1", "admin@x.com");

        r.Value!.Status.Should().Be(TokenPurchaseStatuses.Rejected);
        (await _db.TokenPurchases.SingleAsync()).Notes.Should().Be("No llegó");
        (await _db.CryptoPaymentConfirmations.SingleAsync()).Notes.Should().Contain("No llegó");
        (await new TokenMonthlyQuota(_db).UsedAsync(MemberId, FixedNow)).Should().Be(0);
        (await Buy(Request(TokenPaymentMethods.Crypto, qty: 2, crypto: "USDT", key: "clave-cripto-1")))
            .ErrorCode.Should().Be(TokenPurchase.RejectedByAdminCode);
    }

    [Fact]
    public async Task Caducan_las_cripto_sin_confirmar_pasado_el_plazo_y_no_las_recientes()
    {
        var vieja    = await CriptoPendiente(2);
        var reciente = await CriptoPendiente(1);
        // AuditInterceptor sella CreationDate al insertar; se reescribe para fijar la antigüedad.
        foreach (var c in await _db.TokenPurchases.ToListAsync())
            c.CreationDate = c.Id == vieja.PurchaseId ? FixedNow.AddDays(-4) : FixedNow.AddHours(-1);
        await _db.SaveChangesAsync();

        var caducadas = await Service().ExpireStaleCryptoAsync(expiryDays: 3, actor: "token-purchase-crypto-expiry");

        caducadas.Should().Be(1);
        (await _db.TokenPurchases.SingleAsync(x => x.Id == vieja.PurchaseId)).Status.Should().Be(TokenPurchaseStatus.Expired);
        (await _db.TokenPurchases.SingleAsync(x => x.Id == reciente.PurchaseId)).Status.Should().Be(TokenPurchaseStatus.AwaitingCryptoConfirmation);
        (await new TokenMonthlyQuota(_db).UsedAsync(MemberId, FixedNow)).Should().Be(1);
    }

    [Fact]
    public async Task Confirmar_en_octubre_una_cripto_de_septiembre_no_vuelve_a_mirar_el_tope()
    {
        // Septiembre: 50 en cripto sin pagar. Octubre: el miembro agota su cupo con otros 50.
        var septiembre = await CriptoPendiente(50);
        (await _db.TokenPurchases.SingleAsync()).PeriodKey = 202609;
        await _db.SaveChangesAsync();
        SeedCommissions(10000m);
        (await Buy(Request(TokenPaymentMethods.CommissionBalance, qty: 50))).IsSuccess.Should().BeTrue();

        var ok = await Service().ConfirmCryptoAsync(septiembre.OrderId,
            new ConfirmCryptoTokenPurchaseRequest { CryptoTransactionId = "0xsep" }, "admin-1", "admin@x.com");

        ok.IsSuccess.Should().BeTrue("el cupo se comprobó al reservar, en septiembre");
        (await new TokenMonthlyQuota(_db).UsedAsync(MemberId, FixedNow)).Should().Be(50, "octubre solo cuenta lo suyo");
    }
}
```

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter TokenPurchaseServiceTests -nologo`
Expected: FAIL — `TokenPurchaseService` no existe.

- [ ] **Step 2: Crear `Billing/Services/Tokens/TokenPurchaseService.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Crypto;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using ICacheService            = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;

namespace MLMConquerorGlobalEdition.Billing.Services.Tokens;

/// <summary>Quién compra (siempre del token), con qué identidad queda el rastro y qué pide.</summary>
public sealed record TokenPurchaseInput(string MemberId, string Actor, PurchaseTokensRequest Request);

public interface ITokenPurchaseService
{
    Task<TokenCatalogDto> GetCatalogAsync(string memberId, CancellationToken ct = default);
    Task<TokenPurchaseFundsDto> GetFundsAsync(string memberId, CancellationToken ct = default);
    Task<Result<TokenPurchaseResultDto>> PurchaseAsync(TokenPurchaseInput input, CancellationToken ct = default);
    Task<Result<TokenPurchaseResultDto>> ConfirmCryptoAsync(
        string orderId, ConfirmCryptoTokenPurchaseRequest request, string confirmedByUserId, string confirmedByEmail,
        CancellationToken ct = default);
    Task<Result<TokenPurchaseResultDto>> ReconcileAsync(
        string purchaseId, ReconcileTokenPurchaseRequest request, string actor, CancellationToken ct = default);

    /// <summary>El miembro retira su compra en cripto sin pagar (D18). Solo la suya.</summary>
    Task<Result<TokenPurchaseResultDto>> CancelCryptoAsync(
        string memberId, string purchaseId, string actor, CancellationToken ct = default);

    /// <summary>Un aprobador de cripto rechaza la compra: la transferencia no llegó o no vale (D18).</summary>
    Task<Result<TokenPurchaseResultDto>> RejectCryptoAsync(
        string orderId, RejectCryptoTokenPurchaseRequest request, string rejectedByUserId, string rejectedByEmail,
        CancellationToken ct = default);

    /// <summary>Caduca las cripto sin confirmar de <paramref name="expiryDays"/> días o más. Devuelve cuántas.</summary>
    Task<int> ExpireStaleCryptoAsync(int expiryDays, string actor, CancellationToken ct = default);
}

/// <summary>
/// La compra de tokens. Cuatro caminos según dónde está el dinero:
///
///   • COMISIONES: todo en UNA transacción bajo el candado del miembro. El saldo se comprueba antes de
///     escribir; si algo falla a mitad, se deshace entero.
///   • EWALLET (i-Payout): (1) reservar cupo y confirmar; (2) con el candado otra vez tomado, releer el
///     saldo, debitar, verificar por saldo (como el legado) y emitir o dejar a conciliar. El candado en
///     la fase 2 es lo que hace fiable la verificación por saldo.
///   • TARJETA: (1) reservar; (2) cobrar FUERA de la transacción; (3) emitir en un único SaveChanges con
///     CancellationToken.None, porque el dinero ya se movió.
///   • CRIPTO: reservar cupo, dejar el pedido en Processing con su CryptoPaymentConfirmation y devolver
///     dirección, moneda e importe. Emite <see cref="ConfirmCryptoAsync"/>.
///
/// Lo dudoso nunca se reintenta: queda NeedsReconciliation y lo cierra <see cref="ReconcileAsync"/>.
/// Una cripto sin pagar sale por <see cref="CancelCryptoAsync"/>, <see cref="RejectCryptoAsync"/> o
/// <see cref="ExpireStaleCryptoAsync"/>, y las tres liberan el cupo.
/// No reembolsable.
/// </summary>
public sealed class TokenPurchaseService : ITokenPurchaseService
{
    private readonly AppDbContext                _db;
    private readonly IDateTimeProvider           _clock;
    private readonly ITokenPricingService        _pricing;
    private readonly ITokenMonthlyQuota          _quota;
    private readonly IMemberFundsGate            _gate;
    private readonly ITokenIssuer                _issuer;
    private readonly ICommissionBalanceService   _commissions;
    private readonly IEWalletFunds               _eWallet;
    private readonly IGatewayRouter              _router;
    private readonly IGatewayChargeOrchestrator  _orchestrator;
    private readonly ICardBrandDetector          _brands;
    private readonly ICryptoDepositAddressReader _cryptoAddresses;
    private readonly ITokenPurchaseReceipts      _receipts;
    private readonly ICacheService               _cache;
    private readonly IPushNotificationService    _push;
    private readonly ILogger<TokenPurchaseService> _logger;

    public TokenPurchaseService(
        AppDbContext db, IDateTimeProvider clock, ITokenPricingService pricing, ITokenMonthlyQuota quota,
        IMemberFundsGate gate, ITokenIssuer issuer, ICommissionBalanceService commissions, IEWalletFunds eWallet,
        IGatewayRouter router, IGatewayChargeOrchestrator orchestrator, ICardBrandDetector brands,
        ICryptoDepositAddressReader cryptoAddresses, ITokenPurchaseReceipts receipts,
        ICacheService cache, IPushNotificationService push, ILogger<TokenPurchaseService> logger)
    {
        _db              = db;
        _clock           = clock;
        _pricing         = pricing;
        _quota           = quota;
        _gate            = gate;
        _issuer          = issuer;
        _commissions     = commissions;
        _eWallet         = eWallet;
        _router          = router;
        _orchestrator    = orchestrator;
        _brands          = brands;
        _cryptoAddresses = cryptoAddresses;
        _receipts        = receipts;
        _cache           = cache;
        _push            = push;
        _logger          = logger;
    }

    // ── Lecturas ──────────────────────────────────────────────────────────────

    public async Task<TokenCatalogDto> GetCatalogAsync(string memberId, CancellationToken ct = default)
    {
        var items = await _pricing.GetCatalogAsync(ct);
        var used  = await _quota.UsedAsync(memberId, _clock.Now, ct);

        return new TokenCatalogDto
        {
            MonthlyLimit       = TokenPurchaseRules.MonthlyLimit,
            PurchasedThisMonth = used,
            MonthlyRemaining   = Math.Max(0, TokenPurchaseRules.MonthlyLimit - used),
            CryptoDeposits     = (await _cryptoAddresses.GetConfiguredAsync(ct)).ToList(),
            Items = items.Select(q => new TokenCatalogItemDto
            {
                TokenTypeId = q.TokenTypeId, Name = q.Name, Description = q.Description, IsGuestPass = q.IsGuestPass,
                UnitPrice = q.UnitPrice, Purchasable = q.Purchasable, NotPurchasableReason = q.NotPurchasableReason
            }).ToList()
        };
    }

    public async Task<TokenPurchaseFundsDto> GetFundsAsync(string memberId, CancellationToken ct = default)
    {
        var funds = new TokenPurchaseFundsDto { CommissionBalance = await _commissions.GetSpendableAsync(memberId, ct) };

        var userName = await _eWallet.UserNameOfAsync(memberId, ct);
        if (userName is null) return funds;

        funds.HasEWallet = true;
        var balance = await _eWallet.BalanceAsync(userName, ct);
        funds.EWalletBalance = balance.IsSuccess ? balance.Value : null;
        return funds;
    }

    // ── Compra ────────────────────────────────────────────────────────────────

    private sealed record Draft(
        string MemberId, string MemberEmail, string? Country, string Actor,
        TokenQuote Quote, int Quantity, TokenPaymentMethod Method, string IdempotencyKey)
    {
        public decimal Total => Quote.UnitPrice!.Value * Quantity;
    }

    public async Task<Result<TokenPurchaseResultDto>> PurchaseAsync(TokenPurchaseInput input, CancellationToken ct = default)
    {
        var req = input.Request;

        if (!Enum.TryParse<TokenPaymentMethod>(req.PaymentMethod, ignoreCase: true, out var method) || !Enum.IsDefined(method))
            return Fail(TokenPurchaseErrors.PaymentMethodNotSupported, "Payment method not supported for tokens.");
        if (req.Quantity < 1 || req.Quantity > TokenPurchaseRules.MonthlyLimit)
            return Fail(TokenPurchaseErrors.InvalidQuantity, $"Quantity must be between 1 and {TokenPurchaseRules.MonthlyLimit}.");
        if (!TokenPurchaseRules.IsValidIdempotencyKey(req.IdempotencyKey))
            return Fail(TokenPurchaseErrors.IdempotencyKeyRequired, "A valid idempotency key is required.");

        var replay = await ReplayAsync(input.MemberId, req.IdempotencyKey, ct);
        if (replay is not null) return replay;

        var member = await _db.MemberProfiles.AsNoTracking()
            .FirstOrDefaultAsync(m => m.MemberId == input.MemberId && !m.IsDeleted, ct);
        if (member is null) return Fail(TokenPurchaseErrors.MemberNotFound, "Member not found.");
        if (member.Status != MemberAccountStatus.Active)
            return Fail(TokenPurchaseErrors.MemberInactive, "Only active members can buy tokens.");

        var quote = await _pricing.QuoteAsync(req.TokenTypeId, ct);
        if (!quote.IsSuccess) return Fail(quote.ErrorCode!, quote.Error!);

        var d = new Draft(input.MemberId, member.Email, member.Country, input.Actor,
                          quote.Value!, req.Quantity, method, req.IdempotencyKey);
        try
        {
            return method switch
            {
                TokenPaymentMethod.CommissionBalance => await PayWithCommissionsAsync(d, ct),
                TokenPaymentMethod.EWallet           => await PayWithEWalletAsync(d, ct),
                TokenPaymentMethod.CreditCard        => await PayWithCardAsync(d, req.CreditCardId, ct),
                TokenPaymentMethod.Crypto            => await PayWithCryptoAsync(d, req.CryptoCurrency, ct),
                _ => Fail(TokenPurchaseErrors.PaymentMethodNotSupported, "Payment method not supported for tokens.")
            };
        }
        catch (MemberFundsBusyException)
        {
            return Fail(TokenPurchaseErrors.Busy, "Another purchase for this account is in progress. Try again in a moment.");
        }
    }

    private async Task<Result<TokenPurchaseResultDto>> PayWithCommissionsAsync(Draft d, CancellationToken ct)
    {
        var now = _clock.Now;
        await using var tx = await _gate.EnterAsync(d.MemberId, ct);

        var replay = await ReplayAsync(d.MemberId, d.IdempotencyKey, ct);
        if (replay is not null) return replay;

        var used = await _quota.UsedAsync(d.MemberId, now, ct);
        if (used + d.Quantity > TokenPurchaseRules.MonthlyLimit) return LimitReached(used);

        var spendable = await _commissions.GetSpendableAsync(d.MemberId, ct);
        if (spendable < d.Total)
            return Fail(TokenPurchaseErrors.InsufficientCommissionBalance, $"Commission balance {spendable:F2} is below {d.Total:F2}.");

        var (order, purchase) = Start(d, TokenPurchaseStatus.Pending, creditCardId: null, now);
        await _db.SaveChangesAsync(ct);

        var fund = await _commissions.FundTokenPurchaseAsync(d.MemberId, d.Total, order.Id, d.Actor, ct);
        if (!fund.IsSuccess) return await RollbackAsync(tx, fund.ErrorCode!, fund.Error!);

        var codes = await _issuer.IssueAsync(
            new TokenIssue(d.MemberId, d.Quote.TokenTypeId, d.Quantity, purchase.Id, order.OrderNo, d.Actor, now), ct);
        purchase.Complete(fund.Value!, now, d.Actor);
        MarkPaid(order, d.Actor, now);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await AfterIssueAsync(purchase, order, d.Quote.Name);
        return Done(purchase, order, codes, used + d.Quantity);
    }

    private async Task<Result<TokenPurchaseResultDto>> PayWithEWalletAsync(Draft d, CancellationToken ct)
    {
        var userName = await _eWallet.UserNameOfAsync(d.MemberId, ct);
        if (userName is null)
            return Fail(TokenPurchaseErrors.EWalletNotFound, "The member has no registered eWallet account.");

        // Comprobación temprana para responder rápido; la que cuenta se repite bajo el candado.
        var early = await _eWallet.BalanceAsync(userName, ct);
        if (!early.IsSuccess) return Fail(TokenPurchaseErrors.EWalletUnavailable, early.Error ?? "eWallet unavailable.");
        if (early.Value < EWalletFunds.ChargeAmount(d.Total))
            return Fail(TokenPurchaseErrors.InsufficientEWalletBalance, $"eWallet balance {early.Value:F2} is below {d.Total:F2}.");

        var now = _clock.Now;
        Orders order;
        TokenPurchase purchase;
        int usedAfter;

        // Fase 1 — reservar cupo bajo el candado y dejarlo escrito antes de tocar i-Payout.
        await using (var tx = await _gate.EnterAsync(d.MemberId, ct))
        {
            var replay = await ReplayAsync(d.MemberId, d.IdempotencyKey, ct);
            if (replay is not null) return replay;

            var used = await _quota.UsedAsync(d.MemberId, now, ct);
            if (used + d.Quantity > TokenPurchaseRules.MonthlyLimit) return LimitReached(used);

            (order, purchase) = Start(d, TokenPurchaseStatus.Pending, creditCardId: null, now);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            usedAfter = used + d.Quantity;
        }

        // Fase 2 — con el candado: ningún otro débito nuestro del mismo miembro puede cruzarse con la
        // verificación por saldo. Sin cancelación: desde aquí puede moverse dinero.
        IDbContextTransaction tx2;
        try
        {
            tx2 = await _gate.EnterAsync(d.MemberId, CancellationToken.None);
        }
        catch (MemberFundsBusyException)
        {
            // Todavía no se tocó i-Payout: se puede soltar la reserva.
            return await AbandonAsync(order, purchase, TokenPurchaseErrors.Busy, d.Actor,
                "Another purchase for this account is in progress. Try again in a moment.");
        }

        await using (tx2)
        {
            var before = await _eWallet.BalanceAsync(userName, CancellationToken.None);
            if (!before.IsSuccess || before.Value < EWalletFunds.ChargeAmount(d.Total))
            {
                await tx2.RollbackAsync(CancellationToken.None);
                return await AbandonAsync(order, purchase,
                    before.IsSuccess ? TokenPurchaseErrors.InsufficientEWalletBalance : TokenPurchaseErrors.EWalletUnavailable,
                    d.Actor, before.IsSuccess ? "Insufficient eWallet balance." : (before.Error ?? "eWallet unavailable."));
            }

            var outcome = await _eWallet.DebitAsync(userName, d.Total, before.Value, purchase.Id,
                $"Token purchase {order.OrderNo}", CancellationToken.None);
            var at = _clock.Now;

            switch (outcome.Status)
            {
                case EWalletDebitStatus.Rejected:
                    purchase.Fail(TokenPurchaseErrors.EWalletRejected, at, d.Actor);
                    purchase.Notes = Truncate($"{outcome.Code}: {outcome.Detail}");
                    Cancel(order, d.Actor, at);
                    await _db.SaveChangesAsync(CancellationToken.None);
                    await tx2.CommitAsync(CancellationToken.None);
                    return Fail(TokenPurchaseErrors.EWalletRejected, "i-Payout rejected the charge.");

                case EWalletDebitStatus.NeedsReconciliation:
                    purchase.MarkForReconciliation(outcome.Code ?? "EWALLET_OUTCOME_UNKNOWN", Truncate(outcome.Detail), at, d.Actor);
                    await _db.SaveChangesAsync(CancellationToken.None);
                    await tx2.CommitAsync(CancellationToken.None);
                    _logger.LogWarning("Compra de tokens {PurchaseId} a conciliar: {Code} {Detail}", purchase.Id, outcome.Code, outcome.Detail);
                    return Fail(TokenPurchaseErrors.NeedsReview, "We couldn't confirm the eWallet charge. It will be reviewed; do not retry.");
            }

            // Lo cobrado es el importe a centavos que mandó EWalletFunds, no los 4 decimales del precio.
            var payment = AddPaymentHistory(order, EWalletFunds.ChargeAmount(d.Total), "eWallet", purchase.Id, d.Actor, at);
            var codes = await _issuer.IssueAsync(
                new TokenIssue(d.MemberId, d.Quote.TokenTypeId, d.Quantity, purchase.Id, order.OrderNo, d.Actor, at),
                CancellationToken.None);
            purchase.Complete(payment.Id, at, d.Actor);
            MarkPaid(order, d.Actor, at);
            await _db.SaveChangesAsync(CancellationToken.None);
            await tx2.CommitAsync(CancellationToken.None);

            await AfterIssueAsync(purchase, order, d.Quote.Name);
            return Done(purchase, order, codes, usedAfter);
        }
    }

    private async Task<Result<TokenPurchaseResultDto>> PayWithCardAsync(Draft d, string? creditCardId, CancellationToken ct)
    {
        var card = string.IsNullOrWhiteSpace(creditCardId)
            ? null
            : await _db.CreditCards.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == creditCardId && c.MemberId == d.MemberId && !c.IsDeleted, ct);

        if (card is null) return Fail(TokenPurchaseErrors.CreditCardNotFound, "Card not found.");
        if (card.IsExpired) return Fail(TokenPurchaseErrors.CreditCardExpired, "The card is expired.");

        var cardRef = !string.IsNullOrWhiteSpace(card.SpreedlyPaymentMethodToken) ? card.SpreedlyPaymentMethodToken : card.CardToken;
        if (string.IsNullOrWhiteSpace(cardRef))
            return Fail(TokenPurchaseErrors.CreditCardNotChargeable, "The card has no chargeable token.");

        var now = _clock.Now;
        Orders order;
        TokenPurchase purchase;
        int usedAfter;

        // Fase 1 — reservar cupo bajo el candado.
        await using (var tx = await _gate.EnterAsync(d.MemberId, ct))
        {
            var replay = await ReplayAsync(d.MemberId, d.IdempotencyKey, ct);
            if (replay is not null) return replay;

            var used = await _quota.UsedAsync(d.MemberId, now, ct);
            if (used + d.Quantity > TokenPurchaseRules.MonthlyLimit) return LimitReached(used);

            (order, purchase) = Start(d, TokenPurchaseStatus.Pending, card.Id, now);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            usedAfter = used + d.Quantity;
        }

        // Fase 2 — cobrar fuera de la transacción.
        var routingCtx = new GatewayRoutingContext
        {
            OperationType        = BillingOperationType.Payment,
            CardBrand            = _brands.Detect(card.First6),
            CardholderCountryIso = string.IsNullOrWhiteSpace(d.Country) ? "US" : d.Country!,
            AmountUsd            = d.Total,
            MemberId             = d.MemberId
        };

        var plan = await _router.ResolveAsync(routingCtx, ct);
        if (!plan.IsSuccess)
            return await AbandonAsync(order, purchase, TokenPurchaseErrors.NoImmediateGateway, d.Actor, "No card gateway is available right now.");

        // Solo pasos inmediatos: uno con espera lo cobraría DelayedFallbackChargeJob, que no emite tokens.
        var immediate = new GatewayRoutingPlan
        {
            RouteBucketKey = plan.Value!.RouteBucketKey,
            Steps          = plan.Value.Steps.Where(s => s.DelayMinutes == 0).ToList()
        };
        if (immediate.Steps.Count == 0)
            return await AbandonAsync(order, purchase, TokenPurchaseErrors.NoImmediateGateway, d.Actor, "No card gateway is available right now.");

        var charge = await _orchestrator.ExecuteAsync(immediate, routingCtx, new OrchestratorChargeRequest
        {
            MemberId = d.MemberId, TokenizedCardRef = cardRef, Description = $"Token purchase {order.OrderNo}",
            OrderId = order.Id, IsRecurring = false
        }, ct);

        if (!charge.IsSuccess || charge.Value!.Status != "Success")
        {
            _logger.LogWarning("Compra de tokens {PurchaseId}: la tarjeta no se cobró ({Code}) {Error}", purchase.Id, charge.ErrorCode, charge.Error);
            return await AbandonAsync(order, purchase, TokenPurchaseErrors.CardChargeFailed, d.Actor, "The card payment was declined.");
        }

        // Fase 3 — emitir en un único SaveChanges, sin cancelación ni candado.
        var issuedAt = _clock.Now;
        var codes = await _issuer.IssueAsync(
            new TokenIssue(d.MemberId, d.Quote.TokenTypeId, d.Quantity, purchase.Id, order.OrderNo, d.Actor, issuedAt),
            CancellationToken.None);
        purchase.Complete(charge.Value.PaymentHistoryId, issuedAt, d.Actor);
        MarkPaid(order, d.Actor, issuedAt);
        await _db.SaveChangesAsync(CancellationToken.None);

        await AfterIssueAsync(purchase, order, d.Quote.Name);
        return Done(purchase, order, codes, usedAfter);
    }

    private async Task<Result<TokenPurchaseResultDto>> PayWithCryptoAsync(Draft d, string? cryptoCurrency, CancellationToken ct)
    {
        var currency = cryptoCurrency?.Trim().ToUpperInvariant();
        if (currency is null || !TokenPurchaseRules.CryptoCurrencies.Contains(currency))
            return Fail(TokenPurchaseErrors.CryptoCurrencyNotSupported,
                $"Supported currencies: {string.Join(", ", TokenPurchaseRules.CryptoCurrencies)}.");

        var deposit = (await _cryptoAddresses.GetConfiguredAsync(ct)).FirstOrDefault(c => c.Currency == currency);
        if (deposit is null)
            return Fail(TokenPurchaseErrors.CryptoAddressNotConfigured, $"Payments in {currency} are not available right now.");

        var now = _clock.Now;
        await using var tx = await _gate.EnterAsync(d.MemberId, ct);

        var replay = await ReplayAsync(d.MemberId, d.IdempotencyKey, ct);
        if (replay is not null) return replay;

        var used = await _quota.UsedAsync(d.MemberId, now, ct);
        if (used + d.Quantity > TokenPurchaseRules.MonthlyLimit) return LimitReached(used);

        var (order, purchase) = Start(d, TokenPurchaseStatus.AwaitingCryptoConfirmation, creditCardId: null, now);
        _db.CryptoPaymentConfirmations.Add(new CryptoPaymentConfirmation
        {
            OrderId = order.Id, MemberId = d.MemberId, MemberEmail = d.MemberEmail, CryptoCurrency = currency,
            AmountDue = d.Total, Status = CryptoPaymentConfirmationStatus.AwaitingPayment,
            Purpose = CryptoPaymentPurpose.TokenPurchase, CreatedBy = d.Actor, CreationDate = now, LastUpdateDate = now
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Done(purchase, order, Array.Empty<string>(), used + d.Quantity, deposit);
    }

    // ── Confirmación de cripto ────────────────────────────────────────────────

    /// <remarks>
    /// Mismas capas que ConfirmCryptoPaymentHandler (altas): máquina de estados, RowVersion de la
    /// confirmación en el mismo SaveChanges que los tokens y unicidad de OrderId. Solo filas de compra.
    /// NO vuelve a comprobar el tope (D3): el cupo se comprobó al reservar, en el mes de su PeriodKey, y
    /// los tokens que emite cuentan en ese mes aunque se confirme en el siguiente.
    /// </remarks>
    public async Task<Result<TokenPurchaseResultDto>> ConfirmCryptoAsync(
        string orderId, ConfirmCryptoTokenPurchaseRequest request, string confirmedByUserId, string confirmedByEmail,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CryptoTransactionId))
            return Fail(TokenPurchaseErrors.CryptoTransactionIdRequired, "The transaction id is required.");

        var confirmation = await _db.CryptoPaymentConfirmations
            .FirstOrDefaultAsync(c => c.OrderId == orderId && c.Purpose == CryptoPaymentPurpose.TokenPurchase, ct);
        if (confirmation is null)
            return Fail(TokenPurchaseErrors.CryptoPaymentNotFound, "No crypto token purchase awaiting confirmation was found for that order.");
        if (confirmation.Status == CryptoPaymentConfirmationStatus.Cancelled)
            return Fail(TokenPurchaseErrors.NotAwaitingCrypto, "This crypto purchase was cancelled, rejected or expired.");
        if (confirmation.Status != CryptoPaymentConfirmationStatus.AwaitingPayment)
            return Fail(TokenPurchaseErrors.CryptoAlreadyConfirmed, "This crypto payment has already been confirmed.");

        var purchase = await _db.TokenPurchases.FirstOrDefaultAsync(p => p.OrderId == orderId, ct);
        var order    = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (purchase is null || order is null)
            return Fail(TokenPurchaseErrors.CryptoPaymentNotFound, "The token purchase for this payment no longer exists.");
        if (purchase.Status != TokenPurchaseStatus.AwaitingCryptoConfirmation)
            return Fail(TokenPurchaseErrors.CryptoAlreadyConfirmed, "This crypto payment has already been confirmed.");

        var now = _clock.Now;
        confirmation.Status              = CryptoPaymentConfirmationStatus.Confirmed;
        confirmation.CryptoTransactionId = request.CryptoTransactionId.Trim();
        confirmation.Notes               = request.Notes?.Trim();
        confirmation.ConfirmedByUserId   = confirmedByUserId;
        confirmation.ConfirmedByEmail    = confirmedByEmail;
        confirmation.ConfirmedAt         = now;
        confirmation.LastUpdateDate      = now;
        confirmation.LastUpdateBy        = confirmedByEmail;

        var payment = AddPaymentHistory(order, confirmation.AmountDue, $"Crypto:{confirmation.CryptoCurrency}",
                                        confirmation.CryptoTransactionId, confirmedByEmail, now);
        var codes = await _issuer.IssueAsync(
            new TokenIssue(purchase.MemberId, purchase.TokenTypeId, purchase.Quantity, purchase.Id, order.OrderNo, confirmedByEmail, now), ct);
        purchase.Complete(payment.Id, now, confirmedByEmail);
        MarkPaid(order, confirmedByEmail, now);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return Fail(TokenPurchaseErrors.CryptoAlreadyConfirmed, "This crypto payment has already been confirmed.");
        }

        await AfterIssueAsync(purchase, order, await TokenTypeNameAsync(purchase.TokenTypeId, ct));
        return Done(purchase, order, codes, await _quota.UsedAsync(purchase.MemberId, now, ct));
    }

    // ── Conciliación ──────────────────────────────────────────────────────────

    /// <summary>
    /// Tesorería mira i-Payout y dice si el dinero salió. Sí → se emiten los tokens; no → falla y libera
    /// el cupo. RowVersion de TokenPurchase impide cerrar la misma compra dos veces.
    /// </summary>
    public async Task<Result<TokenPurchaseResultDto>> ReconcileAsync(
        string purchaseId, ReconcileTokenPurchaseRequest request, string actor, CancellationToken ct = default)
    {
        var purchase = await _db.TokenPurchases.FirstOrDefaultAsync(p => p.Id == purchaseId, ct);
        if (purchase is null) return Fail(TokenPurchaseErrors.PurchaseNotFound, "Token purchase not found.");
        if (purchase.Status != TokenPurchaseStatus.NeedsReconciliation)
            return Fail(TokenPurchaseErrors.NotUnderReview, "This purchase is not waiting for reconciliation.");

        var order = await _db.Orders.FirstAsync(o => o.Id == purchase.OrderId, ct);
        var now   = _clock.Now;
        var note  = Truncate($"{purchase.Notes} | Reconciled by {actor}: {(request.Charged ? "charged" : "not charged")}. {request.Notes}".Trim());
        IReadOnlyList<string> codes = Array.Empty<string>();

        if (request.Charged)
        {
            var payment = AddPaymentHistory(order, purchase.TotalAmount, purchase.PaymentMethod.ToString(),
                                            string.IsNullOrWhiteSpace(request.GatewayReference) ? purchase.Id : request.GatewayReference.Trim(),
                                            actor, now);
            codes = await _issuer.IssueAsync(
                new TokenIssue(purchase.MemberId, purchase.TokenTypeId, purchase.Quantity, purchase.Id, order.OrderNo, actor, now), ct);
            purchase.Complete(payment.Id, now, actor);
            MarkPaid(order, actor, now);
        }
        else
        {
            purchase.Fail("NOT_CHARGED", now, actor);
            Cancel(order, actor, now);
        }
        purchase.Notes = note;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return Fail(TokenPurchaseErrors.NotUnderReview, "This purchase was already reconciled.");
        }

        if (request.Charged)
            await AfterIssueAsync(purchase, order, await TokenTypeNameAsync(purchase.TokenTypeId, ct));

        return Done(purchase, order, codes, await _quota.UsedAsync(purchase.MemberId, now, ct));
    }

    // ── Cripto sin pagar (D18) ────────────────────────────────────────────────
    // Sin candado de fondos: cerrar una reserva solo puede BAJAR lo usado del mes, así que no rompe el
    // tope aunque se cruce con una compra. Contra una confirmación simultánea protege el RowVersion.

    public async Task<Result<TokenPurchaseResultDto>> CancelCryptoAsync(
        string memberId, string purchaseId, string actor, CancellationToken ct = default)
    {
        // Por MemberId además de Id: una compra de otro es "no existe", no "no es tuya".
        var purchase = await _db.TokenPurchases.FirstOrDefaultAsync(p => p.Id == purchaseId && p.MemberId == memberId, ct);
        if (purchase is null) return Fail(TokenPurchaseErrors.PurchaseNotFound, "Token purchase not found.");

        return await CloseUnpaidCryptoAsync(purchase, p => p.CancelByMember(_clock.Now, actor),
            "Cancelled by the member before paying.", actor, ct);
    }

    public async Task<Result<TokenPurchaseResultDto>> RejectCryptoAsync(
        string orderId, RejectCryptoTokenPurchaseRequest request, string rejectedByUserId, string rejectedByEmail,
        CancellationToken ct = default)
    {
        var purchase = await _db.TokenPurchases
            .FirstOrDefaultAsync(p => p.OrderId == orderId && p.PaymentMethod == TokenPaymentMethod.Crypto, ct);
        if (purchase is null)
            return Fail(TokenPurchaseErrors.CryptoPaymentNotFound, "No crypto token purchase was found for that order.");

        var actor  = string.IsNullOrWhiteSpace(rejectedByEmail) ? rejectedByUserId : rejectedByEmail;
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Rejected by an administrator." : request.Reason.Trim();
        return await CloseUnpaidCryptoAsync(purchase, p => p.Reject(reason, _clock.Now, actor),
            $"Rejected by {actor}: {reason}", actor, ct);
    }

    /// <remarks>
    /// Lo llama TokenPurchaseCryptoExpiryJob cada hora con Billing:TokenPurchase:CryptoExpiryDays. Cada
    /// compra se guarda por separado: si un administrador confirma una justo entonces, el conflicto de
    /// RowVersion la deja confirmada y las demás siguen.
    /// </remarks>
    public async Task<int> ExpireStaleCryptoAsync(int expiryDays, string actor, CancellationToken ct = default)
    {
        if (expiryDays <= 0) return 0;

        var cutoff = _clock.Now.AddDays(-expiryDays);
        var ids = await _db.TokenPurchases.AsNoTracking()
            .Where(p => p.Status == TokenPurchaseStatus.AwaitingCryptoConfirmation && p.CreationDate <= cutoff)
            .Select(p => p.Id)
            .ToListAsync(ct);

        var expired = 0;
        foreach (var id in ids)
        {
            var purchase = await _db.TokenPurchases.FirstAsync(p => p.Id == id, ct);
            var result = await CloseUnpaidCryptoAsync(purchase, p => p.Expire(_clock.Now, actor),
                $"Expired after {expiryDays} days without confirmation.", actor, ct);
            if (result.IsSuccess) expired++;
        }

        return expired;
    }

    /// <summary>
    /// Cierra sin cobro una compra en cripto: la transición de la compra, el pedido cancelado y la
    /// confirmación en Cancelled (sale de la lista del panel y ya no se puede confirmar), en un único
    /// SaveChanges.
    /// </summary>
    private async Task<Result<TokenPurchaseResultDto>> CloseUnpaidCryptoAsync(
        TokenPurchase purchase, Action<TokenPurchase> transition, string note, string actor, CancellationToken ct)
    {
        if (purchase.Status != TokenPurchaseStatus.AwaitingCryptoConfirmation)
            return Fail(TokenPurchaseErrors.NotAwaitingCrypto, "This purchase is not a crypto payment awaiting confirmation.");

        var order        = await _db.Orders.FirstAsync(o => o.Id == purchase.OrderId, ct);
        var confirmation = await _db.CryptoPaymentConfirmations.FirstOrDefaultAsync(c => c.OrderId == purchase.OrderId, ct);
        var now          = _clock.Now;

        transition(purchase);
        Cancel(order, actor, now);
        if (confirmation is not null)
        {
            confirmation.Status         = CryptoPaymentConfirmationStatus.Cancelled;
            confirmation.Notes          = Truncate(note);
            confirmation.LastUpdateDate = now;
            confirmation.LastUpdateBy   = actor;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return Fail(TokenPurchaseErrors.NotAwaitingCrypto, "This crypto payment was closed by someone else.");
        }

        return Done(purchase, order, Array.Empty<string>(), await _quota.UsedAsync(purchase.MemberId, now, ct));
    }

    // ── Piezas ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pedido en Processing y compra con su reserva. SIN OrderDetails: los puntos de rango se suman de
    /// los OrderDetails de pedidos Completed y comprar no da puntos (llegan al canjear el token).
    /// </summary>
    private (Orders Order, TokenPurchase Purchase) Start(Draft d, TokenPurchaseStatus status, string? creditCardId, DateTime now)
    {
        var order = new Orders
        {
            MemberId = d.MemberId, OrderNo = OrderNumberHelper.Generate("Token Purchase", now),
            TotalAmount = d.Total, Status = OrderStatus.Processing, OrderDate = now,
            Notes = $"Token purchase: {d.Quantity} x {d.Quote.Name}",
            CreationDate = now, CreatedBy = d.Actor, LastUpdateDate = now, LastUpdateBy = d.Actor
        };

        var purchase = new TokenPurchase
        {
            MemberId = d.MemberId, TokenTypeId = d.Quote.TokenTypeId,
            GrantedProductIds = string.Join(",", d.Quote.ProductIds),
            Quantity = d.Quantity, UnitPrice = d.Quote.UnitPrice!.Value, TotalAmount = d.Total,
            PaymentMethod = d.Method, Status = status, OrderId = order.Id, IdempotencyKey = d.IdempotencyKey,
            PeriodKey = TokenPurchaseRules.PeriodKey(now), CreditCardId = creditCardId,
            CreatedBy = d.Actor, CreationDate = now, LastUpdateDate = now
        };

        _db.Orders.Add(order);
        _db.TokenPurchases.Add(purchase);
        return (order, purchase);
    }

    private PaymentHistory AddPaymentHistory(Orders order, decimal amount, string gateway, string? gatewayTxId, string actor, DateTime now)
    {
        var payment = new PaymentHistory
        {
            OrderId = order.Id, MemberId = order.MemberId, Amount = amount, GatewayName = gateway,
            GatewayTransactionId = gatewayTxId, TransactionStatus = PaymentHistoryTransactionStatus.Captured,
            ProcessedAt = now, CreationDate = now, CreatedBy = actor, LastUpdateDate = now, LastUpdateBy = actor
        };
        _db.PaymentHistories.Add(payment);
        return payment;
    }

    private static void MarkPaid(Orders order, string actor, DateTime now)
    {
        order.Status = OrderStatus.Paid;
        order.LastUpdateDate = now;
        order.LastUpdateBy = actor;
    }

    private static void Cancel(Orders order, string actor, DateTime now)
    {
        order.Status = OrderStatus.Cancelled;
        order.LastUpdateDate = now;
        order.LastUpdateBy = actor;
    }

    private async Task<Result<TokenPurchaseResultDto>> RollbackAsync(IDbContextTransaction tx, string code, string message)
    {
        await tx.RollbackAsync(CancellationToken.None);
        _db.ChangeTracker.Clear();
        return Fail(code, message);
    }

    /// <summary>Suelta una reserva cuando consta que NO se movió dinero.</summary>
    private async Task<Result<TokenPurchaseResultDto>> AbandonAsync(
        Orders order, TokenPurchase purchase, string code, string actor, string message)
    {
        var now = _clock.Now;
        purchase.Fail(code, now, actor);
        Cancel(order, actor, now);
        await _db.SaveChangesAsync(CancellationToken.None);
        return Fail(code, message);
    }

    /// <summary>
    /// La misma clave del mismo miembro devuelve lo que ya pasó: Completed o cripto → la compra (con
    /// códigos o con la dirección); Pending → en curso; NeedsReconciliation → en revisión; Failed → el
    /// mismo fallo (la pantalla genera otra clave).
    /// </summary>
    private async Task<Result<TokenPurchaseResultDto>?> ReplayAsync(string memberId, string key, CancellationToken ct)
    {
        var existing = await _db.TokenPurchases.AsNoTracking()
            .FirstOrDefaultAsync(p => p.MemberId == memberId && p.IdempotencyKey == key, ct);
        if (existing is null) return null;

        switch (existing.Status)
        {
            case TokenPurchaseStatus.Pending:
                return Fail(TokenPurchaseErrors.InProgress, "This purchase is still being processed.");
            case TokenPurchaseStatus.NeedsReconciliation:
                return Fail(TokenPurchaseErrors.NeedsReview, "This purchase is being reviewed; do not retry.");
            case TokenPurchaseStatus.Failed:
                return Fail(existing.FailureCode ?? TokenPurchaseErrors.CardChargeFailed, "This purchase already failed. Start a new one.");
            case TokenPurchaseStatus.Cancelled:
            case TokenPurchaseStatus.Rejected:
            case TokenPurchaseStatus.Expired:
                return Fail(existing.FailureCode ?? TokenPurchaseErrors.NotAwaitingCrypto,
                    "This crypto purchase was closed without payment. Start a new one.");
        }

        var order = await _db.Orders.AsNoTracking().FirstAsync(o => o.Id == existing.OrderId, ct);
        var codes = await _db.TokenTransactions.AsNoTracking()
            .Where(t => t.TokenPurchaseId == existing.Id && t.ReferenceId != null)
            .OrderBy(t => t.Id).Select(t => t.ReferenceId!).ToListAsync(ct);
        var used = await _quota.UsedAsync(memberId, _clock.Now, ct);

        CryptoDepositAddressDto? deposit = null;
        if (existing.Status == TokenPurchaseStatus.AwaitingCryptoConfirmation)
        {
            var currency = await _db.CryptoPaymentConfirmations.AsNoTracking()
                .Where(c => c.OrderId == existing.OrderId).Select(c => c.CryptoCurrency).FirstOrDefaultAsync(ct);
            deposit = (await _cryptoAddresses.GetConfiguredAsync(ct)).FirstOrDefault(c => c.Currency == currency)
                      ?? (currency is null ? null : new CryptoDepositAddressDto { Currency = currency });
        }

        return Done(existing, order, codes, used, deposit);
    }

    private async Task AfterIssueAsync(TokenPurchase purchase, Orders order, string tokenTypeName)
    {
        try
        {
            await _cache.RemoveAsync(CacheKeys.MemberTokenBalances(purchase.MemberId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo invalidar la caché de saldos de tokens de {MemberId}.", purchase.MemberId);
        }

        _ = _push.SendAsync(purchase.MemberId, NotificationEvents.TokenReceived,
            "Tokens purchased", $"{purchase.Quantity} token(s) were added to your account.");

        await _receipts.SendAsync(purchase, order.OrderNo, tokenTypeName, CancellationToken.None);
    }

    private async Task<string> TokenTypeNameAsync(int tokenTypeId, CancellationToken ct) =>
        await _db.TokenTypes.AsNoTracking().Where(t => t.Id == tokenTypeId).Select(t => t.Name).FirstOrDefaultAsync(ct)
        ?? $"Token type {tokenTypeId}";

    private static string? Truncate(string? s) => s is null || s.Length <= 500 ? s : s[..500];

    private static Result<TokenPurchaseResultDto> LimitReached(int used) =>
        Fail(TokenPurchaseErrors.MonthlyLimitReached,
            $"The monthly limit is {TokenPurchaseRules.MonthlyLimit} tokens; {Math.Max(0, TokenPurchaseRules.MonthlyLimit - used)} left this month.");

    private static Result<TokenPurchaseResultDto> Fail(string code, string message) =>
        Result<TokenPurchaseResultDto>.Failure(code, message);

    private static Result<TokenPurchaseResultDto> Done(
        TokenPurchase p, Orders o, IReadOnlyList<string> codes, int usedAfter, CryptoDepositAddressDto? deposit = null) =>
        Result<TokenPurchaseResultDto>.Success(new TokenPurchaseResultDto
        {
            PurchaseId = p.Id, OrderId = o.Id, OrderNo = o.OrderNo,
            Status = p.Status switch
            {
                TokenPurchaseStatus.Completed                  => TokenPurchaseStatuses.Completed,
                TokenPurchaseStatus.AwaitingCryptoConfirmation => TokenPurchaseStatuses.AwaitingCryptoConfirmation,
                TokenPurchaseStatus.NeedsReconciliation        => TokenPurchaseStatuses.NeedsReconciliation,
                TokenPurchaseStatus.Failed                     => TokenPurchaseStatuses.Failed,
                TokenPurchaseStatus.Cancelled                  => TokenPurchaseStatuses.Cancelled,
                TokenPurchaseStatus.Rejected                   => TokenPurchaseStatuses.Rejected,
                TokenPurchaseStatus.Expired                    => TokenPurchaseStatuses.Expired,
                _                                              => TokenPurchaseStatuses.Pending
            },
            TokenTypeId = p.TokenTypeId, Quantity = p.Quantity, UnitPrice = p.UnitPrice, TotalAmount = p.TotalAmount,
            PaymentMethod = p.PaymentMethod.ToString(), TokenCodes = codes.ToList(),
            MonthlyRemaining = Math.Max(0, TokenPurchaseRules.MonthlyLimit - usedAfter),
            CryptoCurrency = deposit?.Currency, CryptoDepositAddress = deposit?.Address, CryptoNetwork = deposit?.Network
        });
}
```

> `OrderNumberHelper` y `CacheKeys` están en `MLMConquerorGlobalEdition.SharedKernel`. `MemberProfile.Country` es `string`. El débito eWallet de `EWalletGatewayService` (libro interno) deja de usarse para comprar; sigue registrado para la ruta heredada `charge` con `Gateway = eWallet`.

- [ ] **Step 3: Correr las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests --filter TokenPurchaseServiceTests -nologo`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add MLMConquerorGlobalEdition.Billing MLMConquerorGlobalEdition.Billing.Tests
git commit -m "Servicio de compra de tokens en Billing: comisiones en una transaccion bajo candado, eWallet real con debito verificado bajo candado y conciliacion manual de lo dudoso, tarjeta en tres fases con solo pasos inmediatos, cripto con la direccion de deposito configurada, recibo por correo y la clave de idempotencia devuelve la misma compra"
```

---
### Task 10: Billing — endpoints, step-up y registro

**Files:**
- Create: `MLMConquerorGlobalEdition.Billing/Services/SharedClockAdapter.cs`
- Create: `MLMConquerorGlobalEdition.Billing/Services/Tokens/StepUpGrantCheck.cs`
- Create: `MLMConquerorGlobalEdition.Billing/Controllers/TokenPurchasesController.cs`
- Create: `MLMConquerorGlobalEdition.Billing/Jobs/TokenPurchaseCryptoExpiryJob.cs`
- Modify: `MLMConquerorGlobalEdition.Billing/Program.cs`
- Test: `MLMConquerorGlobalEdition.Billing.Tests/CompraDeTokensControllerTests.cs`

> **Por qué Billing valida el permiso de step-up sin quemarlo (D13).** El BizCenter lo valida y lo marca como usado en la caché compartida (`StepUpAuthorizationFilter`). Billing solo comprueba firma, usuario, operación y vigencia con `StepUpGrantValidator` (sin estado): así nadie paga con saldo llamando a Billing directamente con su JWT, y el permiso que ya quemó el BizCenter sigue sirviendo para esta llamada. `StepUpGrantValidator` pide el `IDateTimeProvider` de SharedKernel, que Billing no registra (tiene el suyo): de ahí el adaptador.

- [ ] **Step 1: Escribir las pruebas (fallan: el controlador no existe)**

```csharp
using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MLMConquerorGlobalEdition.Billing.Controllers;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.Billing.Tests;

/// <summary>
/// EL COMPRADOR SALE DEL TOKEN. El personal no compra por nadie, una suplantación tampoco, y pagar
/// con saldo exige el permiso de step-up también aquí, para que no se salte llamando a Billing.
/// </summary>
public class CompraDeTokensControllerTests
{
    private readonly Mock<ITokenPurchaseService> _svc = new();
    private readonly Mock<IStepUpGrantCheck> _stepUp = new();

    private static ClaimsPrincipal Usuario(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "prueba", ClaimTypes.NameIdentifier, ClaimTypes.Role));

    private static ClaimsPrincipal Miembro(params Claim[] extra) =>
        Usuario(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-001"),
            new Claim(CallerIdentity.MemberIdClaim, "AMB-100001"),
            new Claim(ClaimTypes.Role, AppRoles.Ambassador)
        }.Concat(extra).ToArray());

    private TokenPurchasesController Controlador(ClaimsPrincipal user, string? grant = null)
    {
        _svc.Setup(s => s.PurchaseAsync(It.IsAny<TokenPurchaseInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TokenPurchaseResultDto>.Success(new TokenPurchaseResultDto { PurchaseId = "p1" }));
        var ctx = new DefaultHttpContext { User = user };
        if (grant is not null) ctx.Request.Headers[StepUpGrant.HeaderName] = grant;
        return new TokenPurchasesController(_svc.Object, _stepUp.Object) { ControllerContext = new ControllerContext { HttpContext = ctx } };
    }

    private static PurchaseTokensRequest Pedido(string method) => new() { PaymentMethod = method };

    [Fact]
    public async Task Con_tarjeta_compra_con_el_miembro_y_el_usuario_del_token_sin_pedir_step_up()
    {
        var r = await Controlador(Miembro()).Purchase(Pedido(TokenPaymentMethods.CreditCard), default);

        r.Should().BeOfType<OkObjectResult>();
        _svc.Verify(s => s.PurchaseAsync(It.Is<TokenPurchaseInput>(i => i.MemberId == "AMB-100001" && i.Actor == "user-001"), It.IsAny<CancellationToken>()));
        _stepUp.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(TokenPaymentMethods.CommissionBalance)]
    [InlineData(TokenPaymentMethods.EWallet)]
    public async Task Con_saldo_y_sin_permiso_es_403_STEP_UP_REQUIRED(string metodo)
    {
        var r = await Controlador(Miembro()).Purchase(Pedido(metodo), default);

        var obj = r.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        obj.Value.Should().BeOfType<ApiResponse<bool>>().Which.ErrorCode.Should().Be(StepUpGrant.RequiredErrorCode);
        _svc.Verify(s => s.PurchaseAsync(It.IsAny<TokenPurchaseInput>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Con_saldo_y_permiso_valido_compra()
    {
        _stepUp.Setup(s => s.IsValid("permiso", "user-001", StepUpOperations.TokenPurchaseFromBalance)).Returns(true);

        var r = await Controlador(Miembro(), grant: "permiso").Purchase(Pedido(TokenPaymentMethods.EWallet), default);

        r.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task El_personal_sin_memberId_no_compra()
    {
        var staff = Usuario(new Claim(ClaimTypes.NameIdentifier, "staff-1"), new Claim(ClaimTypes.Role, AppRoles.Admin));

        var r = await Controlador(staff).Purchase(Pedido(TokenPaymentMethods.CreditCard), default);

        r.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _svc.Verify(s => s.PurchaseAsync(It.IsAny<TokenPurchaseInput>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Una_suplantacion_no_compra_aunque_no_sea_de_solo_lectura()
    {
        var suplantado = Miembro(new Claim(ImpersonationScope.ImpersonatedByClaim, "admin-9"));

        var r = await Controlador(suplantado).Purchase(Pedido(TokenPaymentMethods.CreditCard), default);

        r.Should().BeOfType<ObjectResult>().Which.Value.Should().BeOfType<ApiResponse<bool>>()
         .Which.ErrorCode.Should().Be(TokenPurchaseErrors.Impersonation);
    }

    [Fact]
    public void Confirmar_cripto_y_conciliar_llevan_sus_roles()
    {
        typeof(TokenPurchasesController).GetMethod(nameof(TokenPurchasesController.ConfirmCrypto))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be(AppRoles.CryptoPaymentApprovers);
        typeof(TokenPurchasesController).GetMethod(nameof(TokenPurchasesController.Reconcile))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("SuperAdmin,Admin,BillingManager");
    }

    [Fact]
    public async Task Confirmar_dos_veces_es_409()
    {
        _svc.Setup(s => s.ConfirmCryptoAsync("ord-1", It.IsAny<ConfirmCryptoTokenPurchaseRequest>(), "admin-1", "admin@x.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TokenPurchaseResultDto>.Failure(TokenPurchaseErrors.CryptoAlreadyConfirmed, "ya"));
        var admin = Usuario(new Claim(ClaimTypes.NameIdentifier, "admin-1"), new Claim(ClaimTypes.Email, "admin@x.com"),
                            new Claim(ClaimTypes.Role, AppRoles.BillingManager));

        var r = await Controlador(admin).ConfirmCrypto("ord-1", new ConfirmCryptoTokenPurchaseRequest(), default);

        r.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Cancelar_una_cripto_usa_el_miembro_del_token()
    {
        _svc.Setup(s => s.CancelCryptoAsync("AMB-100001", "p1", "user-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TokenPurchaseResultDto>.Success(new TokenPurchaseResultDto { PurchaseId = "p1", Status = TokenPurchaseStatuses.Cancelled }));

        (await Controlador(Miembro()).Cancel("p1", default)).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Ni_el_personal_ni_una_suplantacion_cancelan()
    {
        var staff = Usuario(new Claim(ClaimTypes.NameIdentifier, "staff-1"), new Claim(ClaimTypes.Role, AppRoles.Admin));
        var suplantado = Miembro(new Claim(ImpersonationScope.ImpersonatedByClaim, "admin-9"));

        (await Controlador(staff).Cancel("p1", default)).Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await Controlador(suplantado).Cancel("p1", default)).Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _svc.Verify(s => s.CancelCryptoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Rechazar_cripto_lleva_el_rol_de_quien_confirma()
        => typeof(TokenPurchasesController).GetMethod(nameof(TokenPurchasesController.RejectCrypto))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be(AppRoles.CryptoPaymentApprovers);
}
```

- [ ] **Step 2: Adaptador de reloj y comprobación de step-up**

`Billing/Services/SharedClockAdapter.cs`:

```csharp
namespace MLMConquerorGlobalEdition.Billing.Services;

/// <summary>
/// El reloj de Billing visto como el de SharedKernel, para las piezas compartidas que lo piden
/// (StepUpGrantValidator). Billing tiene su propia interfaz con los mismos dos miembros.
/// </summary>
public sealed class SharedClockAdapter : MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider
{
    private readonly IDateTimeProvider _inner;

    public SharedClockAdapter(IDateTimeProvider inner) => _inner = inner;

    public DateTime Now    => _inner.Now;
    public DateTime UtcNow => _inner.UtcNow;
}
```

`Billing/Services/Tokens/StepUpGrantCheck.cs`:

```csharp
using MLMConquerorGlobalEdition.SharedKernel.Server.Security;

namespace MLMConquerorGlobalEdition.Billing.Services.Tokens;

/// <summary>¿Este permiso de step-up vale para este usuario y esta operación? Sin estado: no lo quema.</summary>
public interface IStepUpGrantCheck
{
    bool IsValid(string? grant, string userId, string operationKey);
}

public sealed class StepUpGrantCheck : IStepUpGrantCheck
{
    private readonly StepUpGrantValidator _validator;

    public StepUpGrantCheck(StepUpGrantValidator validator) => _validator = validator;

    public bool IsValid(string? grant, string userId, string operationKey) =>
        !string.IsNullOrWhiteSpace(grant)
        && !string.IsNullOrWhiteSpace(userId)
        && _validator.Validate(grant, userId, operationKey).IsSuccess;
}
```

- [ ] **Step 3: Crear `Billing/Controllers/TokenPurchasesController.cs`**

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.Billing.Controllers;

/// <summary>Compra de tokens. Lo llama el BizCenter reenviando el JWT del miembro.</summary>
/// <remarks>
/// EL COMPRADOR ES EL DEL TOKEN. El contrato no lleva MemberId. El personal (sin memberId) no compra
/// por nadie; una suplantación tampoco, sea o no de solo lectura. Pagar con saldo exige el permiso de
/// step-up (el BizCenter ya lo quemó; aquí solo se valida) para que llamar directamente a Billing no
/// se salte el código.
/// </remarks>
[ApiController]
[Route("api/v1/billing/tokens")]
[Authorize]
public class TokenPurchasesController : ControllerBase
{
    private const string Treasury = AppRoles.SuperAdmin + "," + AppRoles.Admin + "," + AppRoles.BillingManager;

    private readonly ITokenPurchaseService _service;
    private readonly IStepUpGrantCheck _stepUp;

    public TokenPurchasesController(ITokenPurchaseService service, IStepUpGrantCheck stepUp)
    {
        _service = service;
        _stepUp  = stepUp;
    }

    /// <summary>GET api/v1/billing/tokens/catalog — tipos con precio, cupo del mes y monedas cripto disponibles.</summary>
    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog(CancellationToken ct)
    {
        var memberId = CallerIdentity.MemberIdOf(User);
        if (memberId is null) return Forbidden(TokenPurchaseErrors.NotAMember, "Only members can buy tokens.");
        return Ok(ApiResponse<TokenCatalogDto>.Ok(await _service.GetCatalogAsync(memberId, ct)));
    }

    /// <summary>GET api/v1/billing/tokens/funds — comisiones gastables y saldo real de eWallet.</summary>
    [HttpGet("funds")]
    public async Task<IActionResult> Funds(CancellationToken ct)
    {
        var memberId = CallerIdentity.MemberIdOf(User);
        if (memberId is null) return Forbidden(TokenPurchaseErrors.NotAMember, "Only members can buy tokens.");
        return Ok(ApiResponse<TokenPurchaseFundsDto>.Ok(await _service.GetFundsAsync(memberId, ct)));
    }

    /// <summary>POST api/v1/billing/tokens/purchase — compra. No reembolsable.</summary>
    [HttpPost("purchase")]
    public async Task<IActionResult> Purchase([FromBody] PurchaseTokensRequest request, CancellationToken ct)
    {
        var memberId = CallerIdentity.MemberIdOf(User);
        if (memberId is null)
            return Forbidden(TokenPurchaseErrors.NotAMember, "Only members can buy tokens.");
        if (ImpersonationScope.IsImpersonating(User))
            return Forbidden(TokenPurchaseErrors.Impersonation, "Purchases are not allowed during impersonation.");

        var userId = CallerIdentity.UserIdOf(User) ?? string.Empty;
        if (TokenPaymentMethods.IsBalance(request.PaymentMethod)
            && !_stepUp.IsValid(Request.Headers[StepUpGrant.HeaderName].ToString(), userId, StepUpOperations.TokenPurchaseFromBalance))
            return Forbidden(StepUpGrant.RequiredErrorCode, "Paying with your balance requires a verification code.");

        var actor  = string.IsNullOrEmpty(userId) ? memberId : userId;
        var result = await _service.PurchaseAsync(new TokenPurchaseInput(memberId, actor, request), ct);

        return result.IsSuccess
            ? Ok(ApiResponse<TokenPurchaseResultDto>.Ok(result.Value!))
            : BadRequest(ApiResponse<TokenPurchaseResultDto>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
    }

    /// <summary>POST api/v1/billing/tokens/purchases/crypto/{orderId}/confirm — un administrador confirma la transferencia.</summary>
    [HttpPost("purchases/crypto/{orderId}/confirm")]
    [Authorize(Roles = AppRoles.CryptoPaymentApprovers)]
    public async Task<IActionResult> ConfirmCrypto(
        string orderId, [FromBody] ConfirmCryptoTokenPurchaseRequest request, CancellationToken ct)
    {
        var result = await _service.ConfirmCryptoAsync(orderId, request,
            CallerIdentity.UserIdOf(User) ?? string.Empty, EmailOf(User), ct);
        return FromAdminResult(result);
    }

    /// <summary>
    /// POST api/v1/billing/tokens/purchases/{purchaseId}/reconcile — tesorería cierra una compra a
    /// conciliar tras mirar i-Payout: cobrada (emite) o no cobrada (libera el cupo).
    /// </summary>
    [HttpPost("purchases/{purchaseId}/reconcile")]
    [Authorize(Roles = Treasury)]
    public async Task<IActionResult> Reconcile(
        string purchaseId, [FromBody] ReconcileTokenPurchaseRequest request, CancellationToken ct)
    {
        var actor = EmailOf(User);
        var result = await _service.ReconcileAsync(purchaseId, request,
            string.IsNullOrEmpty(actor) ? CallerIdentity.UserIdOf(User) ?? "admin" : actor, ct);
        return FromAdminResult(result);
    }

    /// <summary>
    /// POST api/v1/billing/tokens/purchases/{purchaseId}/cancel — el miembro retira su compra en cripto
    /// antes de pagarla y libera el cupo (D18). Solo la suya; nunca suplantando.
    /// </summary>
    [HttpPost("purchases/{purchaseId}/cancel")]
    public async Task<IActionResult> Cancel(string purchaseId, CancellationToken ct)
    {
        var memberId = CallerIdentity.MemberIdOf(User);
        if (memberId is null)
            return Forbidden(TokenPurchaseErrors.NotAMember, "Only members can cancel their purchases.");
        if (ImpersonationScope.IsImpersonating(User))
            return Forbidden(TokenPurchaseErrors.Impersonation, "Purchases cannot be changed during impersonation.");

        var userId = CallerIdentity.UserIdOf(User);
        var result = await _service.CancelCryptoAsync(memberId, purchaseId,
            string.IsNullOrEmpty(userId) ? memberId : userId, ct);
        return FromAdminResult(result);
    }

    /// <summary>
    /// POST api/v1/billing/tokens/purchases/crypto/{orderId}/reject — un aprobador de cripto rechaza la
    /// compra (la transferencia no llegó o no vale). Libera el cupo; no emite (D18).
    /// </summary>
    [HttpPost("purchases/crypto/{orderId}/reject")]
    [Authorize(Roles = AppRoles.CryptoPaymentApprovers)]
    public async Task<IActionResult> RejectCrypto(
        string orderId, [FromBody] RejectCryptoTokenPurchaseRequest request, CancellationToken ct)
    {
        var result = await _service.RejectCryptoAsync(orderId, request,
            CallerIdentity.UserIdOf(User) ?? string.Empty, EmailOf(User), ct);
        return FromAdminResult(result);
    }

    private IActionResult FromAdminResult(Result<TokenPurchaseResultDto> result)
    {
        if (result.IsSuccess) return Ok(ApiResponse<TokenPurchaseResultDto>.Ok(result.Value!));

        var status = result.ErrorCode switch
        {
            TokenPurchaseErrors.CryptoPaymentNotFound or TokenPurchaseErrors.PurchaseNotFound => StatusCodes.Status404NotFound,
            TokenPurchaseErrors.CryptoAlreadyConfirmed or TokenPurchaseErrors.NotUnderReview
                or TokenPurchaseErrors.NotAwaitingCrypto                                    => StatusCodes.Status409Conflict,
            _                                                                                 => StatusCodes.Status400BadRequest
        };
        return StatusCode(status, ApiResponse<TokenPurchaseResultDto>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
    }

    private static string EmailOf(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("email") ?? string.Empty;

    private IActionResult Forbidden(string code, string message) =>
        StatusCode(StatusCodes.Status403Forbidden, ApiResponse<bool>.Fail(code, message, HttpContext.TraceIdentifier));
}
```

- [ ] **Step 4: Registrar en `Billing/Program.cs`** — añadir `using MLMConquerorGlobalEdition.Billing.Services.Tokens;`, `using MLMConquerorGlobalEdition.Repository.Services.Crypto;` y `using MLMConquerorGlobalEdition.SharedKernel.Server.Security;`, y después del bloque `// ── Recurring Billing Engine services`:

```csharp
// ── Compra de tokens (Plan F) ─────────────────────────────────────────────
// El candado por miembro es de SQL Server (sp_getapplock): con varias instancias de Billing sigue
// serializando las compras del mismo miembro. IEWalletClient ya lo registra AddPayoutServices.
builder.Services.AddSingleton<MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider>(
    sp => new SharedClockAdapter(sp.GetRequiredService<IDateTimeProvider>()));
builder.Services.AddSingleton<StepUpGrantValidator>();
builder.Services.AddSingleton<IStepUpGrantCheck, StepUpGrantCheck>();
builder.Services.AddScoped<ICryptoDepositAddressReader, CryptoDepositAddressReader>();
builder.Services.AddScoped<ITokenPricingService, TokenPricingService>();
builder.Services.AddScoped<ITokenMonthlyQuota, TokenMonthlyQuota>();
builder.Services.AddScoped<ITokenIssuer, TokenIssuer>();
// IMemberFundsGate (el candado de fondos) ya lo registra AddPayoutServices: lo comparten los pagos.
builder.Services.AddScoped<IEWalletFunds, EWalletFunds>();
builder.Services.AddScoped<ITokenPurchaseReceipts, TokenPurchaseReceipts>();
builder.Services.AddScoped<ITokenPurchaseService, TokenPurchaseService>();
```

y, junto a los demás trabajos (`builder.Services.AddScoped<PayoutReconciliationJob>();`):

```csharp
builder.Services.AddScoped<TokenPurchaseCryptoExpiryJob>();
```

y después de `RecurringJob.AddOrUpdate<DeferredPayoutAccountJob>(…)`:

```csharp
// Caduca las compras de tokens en cripto que nadie confirmó (Plan F, D18) y libera su cupo.
RecurringJob.AddOrUpdate<TokenPurchaseCryptoExpiryJob>(
    "token-purchase-crypto-expiry",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 * * * *",                    // Cada hora
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
```

`Billing/Jobs/TokenPurchaseCryptoExpiryJob.cs`:

```csharp
using Hangfire;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Jobs;

/// <summary>
/// Trabajo recurrente de Hangfire — cada hora, cola "billing". Caduca las compras de tokens en cripto
/// que nadie confirmó en Billing:TokenPurchase:CryptoExpiryDays días (3 por defecto; 0 lo apaga) y
/// libera su cupo (D18). Idempotente: solo toca las que siguen esperando.
/// </summary>
[Queue("billing")]
public class TokenPurchaseCryptoExpiryJob
{
    private const string Actor = "token-purchase-crypto-expiry";

    private readonly ITokenPurchaseService _purchases;
    private readonly IConfiguration _config;
    private readonly ILogger<TokenPurchaseCryptoExpiryJob> _logger;

    public TokenPurchaseCryptoExpiryJob(
        ITokenPurchaseService purchases, IConfiguration config, ILogger<TokenPurchaseCryptoExpiryJob> logger)
    {
        _purchases = purchases;
        _config    = config;
        _logger    = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var days = _config.GetValue(TokenPurchaseRules.CryptoExpiryDaysConfigKey, TokenPurchaseRules.DefaultCryptoExpiryDays);
        var expired = await _purchases.ExpireStaleCryptoAsync(days, Actor, ct);

        if (expired > 0)
            _logger.LogInformation("TokenPurchaseCryptoExpiryJob: {Count} compras en cripto caducadas tras {Days} días.", expired, days);
    }
}
```

`appsettings.json` de Billing no necesita la clave (el valor por defecto es 3); si se quiere otro plazo: `"Billing": { "TokenPurchase": { "CryptoExpiryDays": 3 } }`.

Comprobar que `IEWalletClient` está en el contenedor de Billing:

Run: `grep -n "IEWalletClient" MLMConquerorGlobalEdition.Repository/Services/Payout/PayoutGatewayServiceCollectionExtensions.cs && grep -n "AddPayoutGatewayClients" MLMConquerorGlobalEdition.Billing/Extensions/PayoutServiceCollectionExtensions.cs`
Expected: las dos líneas aparecen.

- [ ] **Step 5: Correr todas las pruebas de Billing y compilar**

Run: `dotnet test MLMConquerorGlobalEdition.Billing.Tests -nologo` → PASS.
Run: `dotnet build MLMConquerorGlobalEdition.Billing -v q -nologo` → `0 Error(s)`.

- [ ] **Step 6: Commit**

```bash
git add MLMConquerorGlobalEdition.Billing MLMConquerorGlobalEdition.Billing.Tests
git commit -m "Endpoints de compra de tokens en Billing: catalogo, fondos, compra, confirmacion, rechazo y cancelacion de cripto, caducidad de la cripto sin pagar y conciliacion; el comprador sale del token, nadie compra suplantando y pagar con saldo exige el permiso de step-up tambien aqui"
```

---

### Task 11: Altas en cripto — separadas de las compras y con la dirección configurada

**Files:**
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Features/Signups/Commands/ConfirmCryptoPayment/ConfirmCryptoPaymentHandler.cs`
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminCryptoSignupsController.cs`
- Modify: `MLMConquerorGlobalEdition.SignupAPI/Controllers/SignupsController.cs`, `MLMConquerorGlobalEdition.SignupAPI/Program.cs`
- Modify: `MLMConquerorGlobalEdition.Signups.Client/Pages/Signup.razor`
- Test: `MLMConquerorGlobalEdition.Signups.Tests/Features/Signups/CriptoDeTokensNoEsUnAltaTests.cs`
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Features/CryptoSignups/AdminCryptoSignupsControllerTests.cs` (prueba nueva)

- [ ] **Step 1: Prueba en SignupAPI (falla: hoy confirma cualquier fila)**

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MediatR;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Crypto;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Controllers;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.ConfirmCryptoPayment;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Signups;

/// <summary>
/// Una compra de tokens en cripto también tiene CryptoPaymentConfirmation. SignupAPI no la trata
/// como un alta: no hay suscripción que activar. La confirma Billing. Y el alta muestra la dirección
/// de depósito configurada en vez de prometer un correo.
/// </summary>
public class CriptoDeTokensNoEsUnAltaTests
{
    [Fact]
    public async Task La_confirmacion_de_altas_no_encuentra_una_compra_de_tokens()
    {
        await using var db = InMemoryDbHelper.Create();
        db.Orders.Add(new Orders { Id = "ord-t", MemberId = "AMB-1", Status = OrderStatus.Processing, OrderDate = DateTime.Now, CreatedBy = "seed" });
        db.CryptoPaymentConfirmations.Add(new CryptoPaymentConfirmation
        {
            OrderId = "ord-t", MemberId = "AMB-1", MemberEmail = "a@x.com", CryptoCurrency = "BTC",
            AmountDue = 99.99m, Purpose = CryptoPaymentPurpose.TokenPurchase, CreatedBy = "seed", CreationDate = DateTime.Now
        });
        await db.SaveChangesAsync();

        var usuarios = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        var handler = new ConfirmCryptoPaymentHandler(
            db, Mock.Of<IDateTimeProvider>(), usuarios.Object, new Mock<ISignupActivationService>(MockBehavior.Strict).Object,
            Mock.Of<ILogger<ConfirmCryptoPaymentHandler>>());

        var r = await handler.Handle(new ConfirmCryptoPaymentCommand(
            "ord-t", new ConfirmCryptoPaymentRequest { CryptoTransactionId = "0x1" }, "admin-1", "admin@x.com"), default);

        r.ErrorCode.Should().Be("CRYPTO_PAYMENT_NOT_FOUND");
        db.CryptoPaymentConfirmations.Single().Status.Should().Be(CryptoPaymentConfirmationStatus.AwaitingPayment);
    }

    [Fact]
    public async Task El_alta_puede_pedir_las_direcciones_configuradas()
    {
        var lector = new Mock<ICryptoDepositAddressReader>();
        lector.Setup(l => l.GetConfiguredAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(new[] { new CryptoDepositAddressDto { Currency = "BTC", Address = "bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh" } });
        var controller = new SignupsController(Mock.Of<IMediator>(), Mock.Of<IFraudFingerprintService>());

        var r = await controller.GetCryptoDepositAddresses(lector.Object, default);

        ((OkObjectResult)r).Value.Should().BeOfType<ApiResponse<IReadOnlyList<CryptoDepositAddressDto>>>()
            .Which.Data!.Single().Currency.Should().Be("BTC");
    }
}
```

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter CriptoDeTokensNoEsUnAltaTests -nologo` → FAIL.

- [ ] **Step 2: Filtrar en `ConfirmCryptoPaymentHandler.cs`** — reemplazar:

```csharp
        var confirmation = await _db.CryptoPaymentConfirmations
            .FirstOrDefaultAsync(c => c.OrderId == command.OrderId, ct);
```

por:

```csharp
        // Solo altas. Las compras de tokens en cripto las confirma Billing (Plan F).
        var confirmation = await _db.CryptoPaymentConfirmations
            .FirstOrDefaultAsync(c => c.OrderId == command.OrderId && c.Purpose == CryptoPaymentPurpose.Signup, ct);
```

- [ ] **Step 3: Endpoint de direcciones en `SignupsController.cs`** — añadir `using MLMConquerorGlobalEdition.Repository.Services.Crypto;` y `using MLMConquerorGlobalEdition.SharedKernel.Billing;`, y después de `GetCountries`:

```csharp
    /// <summary>
    /// GET api/v1/signups/crypto-deposit-addresses — monedas con dirección de depósito configurada en
    /// Admin → Global Parameters. Anónimo: lo pide el alta antes de que exista la cuenta, y una
    /// dirección de depósito no es un secreto.
    /// </summary>
    [HttpGet("crypto-deposit-addresses")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> GetCryptoDepositAddresses(
        [FromServices] ICryptoDepositAddressReader reader, CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<CryptoDepositAddressDto>>.Ok(await reader.GetConfiguredAsync(ct)));
```

En `SignupAPI/Program.cs`, debajo de `builder.Services.AddScoped<ISignupActivationService, SignupActivationService>();`:

```csharp
builder.Services.AddScoped<MLMConquerorGlobalEdition.Repository.Services.Crypto.ICryptoDepositAddressReader,
                           MLMConquerorGlobalEdition.Repository.Services.Crypto.CryptoDepositAddressReader>();
```

Run: `dotnet test MLMConquerorGlobalEdition.Signups.Tests --filter "CriptoDeTokensNoEsUnAltaTests|AltaPorCriptoTests" -nologo` → PASS.

- [ ] **Step 4: Prueba en AdminAPI** — añadir al final de `AdminCryptoSignupsControllerTests`:

```csharp
    [Fact]
    public async Task ListPending_no_muestra_compras_de_tokens()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "ORD-A", "AMB-A", CryptoPaymentConfirmationStatus.AwaitingPayment);
        await SeedAsync(db, "ORD-T", "AMB-T", CryptoPaymentConfirmationStatus.AwaitingPayment);
        (await db.CryptoPaymentConfirmations.SingleAsync(c => c.OrderId == "ORD-T")).Purpose = CryptoPaymentPurpose.TokenPurchase;
        await db.SaveChangesAsync();

        var result = await Build(db).ListPending();

        ((OkObjectResult)result).Value
            .Should().BeOfType<ApiResponse<IEnumerable<AdminCryptoSignupsController.PendingCryptoSignupDto>>>()
            .Which.Data!.Select(d => d.OrderId).Should().Equal("ORD-A");
    }
```

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter AdminCryptoSignupsControllerTests -nologo` → FAIL.

- [ ] **Step 5: Filtrar en `AdminCryptoSignupsController.cs`** — tres reemplazos exactos:

`where c.Status == CryptoPaymentConfirmationStatus.AwaitingPayment` → `where c.Status == CryptoPaymentConfirmationStatus.AwaitingPayment && c.Purpose == CryptoPaymentPurpose.Signup`

`where c.Status == CryptoPaymentConfirmationStatus.Confirmed` → `where c.Status == CryptoPaymentConfirmationStatus.Confirmed && c.Purpose == CryptoPaymentPurpose.Signup`

`.AnyAsync(c => c.OrderId == orderId, ct);` → `.AnyAsync(c => c.OrderId == orderId && c.Purpose == CryptoPaymentPurpose.Signup, ct);`

Run → PASS.

- [ ] **Step 6: La dirección en el alta (`Signups.Client/Pages/Signup.razor`)** — alcance acotado: mostrar la dirección configurada y mandar la moneda elegida; si no hay ninguna, la pestaña lo dice y no deja continuar por cripto.

a) Campos — después de `private string _paymentTab    = "card";`:

```csharp
    // Direcciones de depósito configuradas en Admin → Global Parameters (Plan F). Vacío = sin cripto.
    private List<MLMConquerorGlobalEdition.SharedKernel.Billing.CryptoDepositAddressDto> _cryptoDeposits = new();
    private string? _cryptoCurrency;
    private MLMConquerorGlobalEdition.SharedKernel.Billing.CryptoDepositAddressDto? _cryptoDeposit =>
        _cryptoDeposits.FirstOrDefault(d => d.Currency == _cryptoCurrency);
```

b) Carga — reemplazar:

```csharp
        var api = HttpFactory.CreateClient("SignupsInternal");

        // Load countries first (needed to populate dropdowns)
```

por:

```csharp
        var api = HttpFactory.CreateClient("SignupsInternal");

        try
        {
            var cd = await api.GetFromJsonAsync<ApiWrapper<List<MLMConquerorGlobalEdition.SharedKernel.Billing.CryptoDepositAddressDto>>>(
                "api/v1/signups/crypto-deposit-addresses");
            _cryptoDeposits = cd?.Data ?? new();
            _cryptoCurrency = _cryptoDeposits.FirstOrDefault()?.Currency;
        }
        catch { _cryptoDeposits = new(); }

        // Load countries first (needed to populate dropdowns)
```

c) El bloque de la pestaña — reemplazar desde `@if (_paymentTab == "crypto")` hasta su `}` de cierre (el `<div class="text-center py-4 text-muted">` con el texto "Send your payment to the wallet address our team will email you…") por:

```razor
                @if (_paymentTab == "crypto")
                {
                    <div class="py-4">
                        @if (_cryptoDeposits.Count == 0)
                        {
                            <p class="text-center text-muted small mb-0">Crypto payments aren't available right now. Please choose another payment method.</p>
                        }
                        else
                        {
                            <label class="form-label small" for="signup-crypto">Currency</label>
                            <select id="signup-crypto" class="form-select mb-3" @bind="_cryptoCurrency">
                                @foreach (var d in _cryptoDeposits)
                                {
                                    <option value="@d.Currency">@d.Currency</option>
                                }
                            </select>
                            @if (_cryptoDeposit is not null)
                            {
                                <p class="small mb-1">Send your payment in <strong>@_cryptoDeposit.Currency</strong> to:</p>
                                <p class="font-monospace small text-break mb-1">@_cryptoDeposit.Address</p>
                                @if (!string.IsNullOrWhiteSpace(_cryptoDeposit.Network))
                                {
                                    <p class="small text-muted mb-1">Network: @_cryptoDeposit.Network</p>
                                }
                            }
                            <p class="small text-muted mb-0">
                                Your spot in the team is reserved right away. Your membership activates once we
                                confirm the transfer — usually within one business day.
                            </p>
                        }
                    </div>
                }
```

d) Moneda elegida — reemplazar `"crypto" => (object)new { PaymentMethod = "Crypto", CryptoCurrency = "BTC" },` por:

```csharp
                    "crypto" => (object)new { PaymentMethod = "Crypto", CryptoCurrency = _cryptoCurrency },
```

e) Botón — reemplazar `disabled="@(!_agreedTerms || _busy || (_paymentTab == "token" && !_tokenValid))"` por:

```razor
                            disabled="@(!_agreedTerms || _busy || (_paymentTab == "token" && !_tokenValid) || (_paymentTab == "crypto" && _cryptoDeposit is null))"
```

> `MemberJoin.razor:597-601` tiene el mismo texto falso ("You'll be redirected to our crypto gateway"); queda fuera de este alcance y anotado en *Fuera de alcance*.

- [ ] **Step 7: Compilar**

Run: `dotnet build MLMConquerorGlobalEdition.Signups.Client -v q -nologo` → `0 Error(s)`.
Run: `dotnet build MLMConquerorGlobalEdition.SignupAPI -v q -nologo` → `0 Error(s)`.

- [ ] **Step 8: Commit**

```bash
git add MLMConquerorGlobalEdition.SignupAPI MLMConquerorGlobalEdition.Signups.Tests MLMConquerorGlobalEdition.Signups.Client MLMConquerorGlobalEdition.AdminAPI MLMConquerorGlobalEdition.AdminAPI.Tests
git commit -m "Las altas en cripto no se mezclan con las compras de tokens, y el alta muestra la direccion de deposito configurada y manda la moneda elegida en lugar de prometer un correo y fijar BTC"
```

---

### Task 12: Admin — compras de tokens (cripto y conciliación) y tipos vendibles

**Files:**
- Create: `MLMConquerorGlobalEdition.AdminAPI/Controllers/AdminTokenPurchasesController.cs`
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Program.cs` (junto a `AddHttpClient("SignupApi", …)`)
- Modify: `MLMConquerorGlobalEdition.AdminAPI/DTOs/Tokens/TokenTypeDto.cs`, `CreateTokenTypeDto.cs`, `UpdateTokenTypeDto.cs`, `MLMConquerorGlobalEdition.AdminAPI/Mappings/AdminMappingExtensions.cs:207-235`
- Create: `MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminTokenPurchases.razor`
- Modify: `MLMConquerorGlobalEdition.AdminWeb/Components/Pages/AdminTokenTypes.razor`
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Components/Layout/AdminSidebarMenu.razor` (~:312)
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Features/TokenAdmin/AdminTokenPurchasesControllerTests.cs`
- Test: `MLMConquerorGlobalEdition.AdminAPI.Tests/Features/TokenAdmin/TokenTypeVendibleTests.cs`

- [ ] **Step 1: Escribir las pruebas (fallan: el controlador y la columna en los DTOs no existen)**

`AdminTokenPurchasesControllerTests.cs`:

```csharp
using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Constants;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.TokenAdmin;

/// <summary>
/// AdminAPI lista las compras de tokens en cripto pendientes y las que hay que conciliar, y reenvía
/// las acciones a Billing con el Bearer del administrador. Billing vuelve a comprobar el rol.
/// </summary>
public class AdminTokenPurchasesControllerTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 10, 0, 0);

    private sealed class Captura : HttpMessageHandler
    {
        public HttpRequestMessage? Peticion;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Peticion = r;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"success\":true}", System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private static async Task SeedAsync(AppDbContext db, string orderId, TokenPurchaseStatus status,
        CryptoPaymentPurpose? crypto = CryptoPaymentPurpose.TokenPurchase)
    {
        if (!db.MemberProfiles.Any())
            db.MemberProfiles.Add(new MemberProfile
            {
                MemberId = "AMB-1", Email = "a@x.com", FirstName = "Ana", LastName = "Ruiz", MemberType = MemberType.Ambassador,
                Status = MemberAccountStatus.Active, EnrollDate = Now, Country = "US", CreatedBy = "seed", CreationDate = Now, LastUpdateDate = Now
            });
        if (!db.TokenTypes.Any())
            db.TokenTypes.Add(new TokenType { Id = 13, Name = "VIP Member", IsActive = true, CreatedBy = "seed", CreationDate = Now });
        db.Orders.Add(new Orders { Id = orderId, MemberId = "AMB-1", OrderNo = $"NO-{orderId}", TotalAmount = 99.99m, Status = OrderStatus.Processing, OrderDate = Now, CreatedBy = "seed", CreationDate = Now });
        if (crypto is not null)
            db.CryptoPaymentConfirmations.Add(new CryptoPaymentConfirmation
            {
                OrderId = orderId, MemberId = "AMB-1", MemberEmail = "a@x.com", CryptoCurrency = "USDT",
                AmountDue = 99.99m, Purpose = crypto.Value, CreatedBy = "seed", CreationDate = Now
            });
        if (crypto != CryptoPaymentPurpose.Signup)
            db.TokenPurchases.Add(new TokenPurchase
            {
                Id = $"p-{orderId}", MemberId = "AMB-1", TokenTypeId = 13, GrantedProductIds = "prod-vip", Quantity = 1,
                UnitPrice = 99.99m, TotalAmount = 99.99m,
                PaymentMethod = crypto is null ? TokenPaymentMethod.EWallet : TokenPaymentMethod.Crypto,
                Status = status, OrderId = orderId, IdempotencyKey = orderId, PeriodKey = 202610,
                FailureCode = status == TokenPurchaseStatus.NeedsReconciliation ? "EWALLET_DEBIT_PENDING" : null,
                CreatedBy = "seed", CreationDate = Now
            });
        await db.SaveChangesAsync();
    }

    private static AdminTokenPurchasesController Build(AppDbContext db, IHttpClientFactory? f = null, string? bearer = null)
    {
        var ctx = new DefaultHttpContext();
        if (bearer is not null) ctx.Request.Headers.Authorization = bearer;
        return new AdminTokenPurchasesController(db, f ?? Mock.Of<IHttpClientFactory>())
        {
            ControllerContext = new ControllerContext { HttpContext = ctx }
        };
    }

    [Fact]
    public void Roles_de_cada_accion()
    {
        typeof(AdminTokenPurchasesController).GetCustomAttribute<AuthorizeAttribute>()!.Roles
            .Should().Be(AppRoles.CryptoPaymentApprovers);
        typeof(AdminTokenPurchasesController).GetMethod(nameof(AdminTokenPurchasesController.Reconcile))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("SuperAdmin,Admin,BillingManager");
    }

    [Fact]
    public async Task Cripto_pendiente_solo_muestra_compras_de_tokens()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "ORD-SIGNUP", TokenPurchaseStatus.AwaitingCryptoConfirmation, CryptoPaymentPurpose.Signup);
        await SeedAsync(db, "ORD-TOKENS", TokenPurchaseStatus.AwaitingCryptoConfirmation);

        var r = await Build(db).ListCryptoPending();

        ((OkObjectResult)r).Value.Should()
            .BeOfType<ApiResponse<IEnumerable<AdminTokenPurchasesController.PendingCryptoTokenPurchaseDto>>>()
            .Which.Data!.Single().OrderId.Should().Be("ORD-TOKENS");
    }

    [Fact]
    public async Task A_revisar_muestra_las_que_esperan_conciliacion()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "ORD-EW", TokenPurchaseStatus.NeedsReconciliation, crypto: null);
        await SeedAsync(db, "ORD-OK", TokenPurchaseStatus.Completed, crypto: null);

        var r = await Build(db).ListToReview();

        var fila = ((OkObjectResult)r).Value.Should()
            .BeOfType<ApiResponse<IEnumerable<AdminTokenPurchasesController.TokenPurchaseToReviewDto>>>()
            .Which.Data!.Single();
        fila.PurchaseId.Should().Be("p-ORD-EW");
        fila.Reason.Should().Be("EWALLET_DEBIT_PENDING");
    }

    [Fact]
    public async Task Confirmar_reenvia_a_Billing_con_el_Bearer_del_admin()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "ORD-TOKENS", TokenPurchaseStatus.AwaitingCryptoConfirmation);
        var captura = new Captura();
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("BillingApi")).Returns(new HttpClient(captura) { BaseAddress = new Uri("https://billing.pruebas/") });

        var r = await Build(db, factory.Object, "Bearer admin.jwt")
            .ConfirmCrypto("ORD-TOKENS", new AdminTokenPurchasesController.ConfirmCryptoRequest("0xabc", null));

        r.Should().BeOfType<ContentResult>().Which.StatusCode.Should().Be(200);
        captura.Peticion!.RequestUri!.AbsolutePath.Should().Be("/api/v1/billing/tokens/purchases/crypto/ORD-TOKENS/confirm");
        captura.Peticion.Headers.Authorization!.ToString().Should().Be("Bearer admin.jwt");
    }

    [Fact]
    public async Task Conciliar_reenvia_a_Billing()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "ORD-EW", TokenPurchaseStatus.NeedsReconciliation, crypto: null);
        var captura = new Captura();
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("BillingApi")).Returns(new HttpClient(captura) { BaseAddress = new Uri("https://billing.pruebas/") });

        await Build(db, factory.Object, "Bearer admin.jwt")
            .Reconcile("p-ORD-EW", new AdminTokenPurchasesController.ReconcileRequest(true, "ipayout-tx-9", null));

        captura.Peticion!.RequestUri!.AbsolutePath.Should().Be("/api/v1/billing/tokens/purchases/p-ORD-EW/reconcile");
    }

    [Fact]
    public async Task Rechazar_reenvia_a_Billing_con_el_motivo()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "ORD-TOKENS", TokenPurchaseStatus.AwaitingCryptoConfirmation);
        var captura = new Captura();
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("BillingApi")).Returns(new HttpClient(captura) { BaseAddress = new Uri("https://billing.pruebas/") });

        var r = await Build(db, factory.Object, "Bearer admin.jwt")
            .RejectCrypto("ORD-TOKENS", new AdminTokenPurchasesController.RejectCryptoRequest("No llegó"));

        r.Should().BeOfType<ContentResult>().Which.StatusCode.Should().Be(200);
        captura.Peticion!.RequestUri!.AbsolutePath.Should().Be("/api/v1/billing/tokens/purchases/crypto/ORD-TOKENS/reject");
        (await captura.Peticion.Content!.ReadAsStringAsync()).Should().Contain("No lleg");
    }

    [Fact]
    public async Task Confirmar_un_alta_no_se_reenvia()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedAsync(db, "ORD-SIGNUP", TokenPurchaseStatus.AwaitingCryptoConfirmation, CryptoPaymentPurpose.Signup);
        var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);

        var r = await Build(db, factory.Object)
            .ConfirmCrypto("ORD-SIGNUP", new AdminTokenPurchasesController.ConfirmCryptoRequest("0x1", null));

        r.Should().BeOfType<NotFoundObjectResult>();
    }
}
```

`TokenTypeVendibleTests.cs`:

```csharp
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Tokens;
using MLMConquerorGlobalEdition.AdminAPI.Mappings;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.TokenAdmin;

/// <summary>
/// IsPurchasable se edita en Token Types. Un PUT que no lo manda (pantallas viejas, scripts) no lo
/// pone a false sin querer.
/// </summary>
public class TokenTypeVendibleTests
{
    [Fact]
    public void Un_PUT_sin_IsPurchasable_lo_deja_como_estaba()
    {
        var entity = new TokenType { Name = "VIP", IsPurchasable = true };

        new UpdateTokenTypeDto { Name = "VIP", IsActive = true }.ApplyTo(entity);

        entity.IsPurchasable.Should().BeTrue();
    }

    [Fact]
    public void Un_PUT_con_IsPurchasable_lo_cambia_y_el_DTO_lo_devuelve()
    {
        var entity = new TokenType { Name = "VIP FREE", IsPurchasable = true };

        new UpdateTokenTypeDto { Name = "VIP FREE", IsActive = true, IsPurchasable = false }.ApplyTo(entity);

        entity.IsPurchasable.Should().BeFalse();
        entity.ToDto().IsPurchasable.Should().BeFalse();
    }
}
```

- [ ] **Step 2: `IsPurchasable` en los DTOs y el mapeo**

`TokenTypeDto.cs`: añadir `public bool IsPurchasable { get; set; }`.
`CreateTokenTypeDto.cs`: añadir `public bool IsPurchasable { get; set; }`.
`UpdateTokenTypeDto.cs`: añadir

```csharp
    /// <summary>Null = no se toca. Así un PUT que no lo conoce no lo pone a false.</summary>
    public bool? IsPurchasable { get; set; }
```

`AdminMappingExtensions.cs`: en `ToDto(this TokenType entity)` añadir `IsPurchasable = entity.IsPurchasable,` después de `Category = entity.Category` (con coma en la línea anterior); en `ToNewEntity(this CreateTokenTypeDto dto)` añadir `IsPurchasable = dto.IsPurchasable,`; y al final de `ApplyTo(this UpdateTokenTypeDto dto, TokenType entity)`:

```csharp
        if (dto.IsPurchasable.HasValue)
            entity.IsPurchasable = dto.IsPurchasable.Value;
```

- [ ] **Step 3: Crear `AdminAPI/Controllers/AdminTokenPurchasesController.cs`**

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Constants;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Compras de tokens que necesitan a una persona: las pagadas en cripto (confirmar la transferencia) y
/// las que quedaron a conciliar (débito eWallet dudoso). Las acciones las ejecuta BILLING, que es quien
/// emite; AdminAPI reenvía con el Bearer del administrador para que Billing compruebe el rol y firme el
/// rastro con la persona real.
/// </summary>
[ApiController]
[Route("api/v1/admin/token-purchases")]
[Authorize(Roles = AppRoles.CryptoPaymentApprovers)]
public class AdminTokenPurchasesController : ControllerBase
{
    private const string Treasury = "SuperAdmin,Admin,BillingManager";

    private readonly AppDbContext       _db;
    private readonly IHttpClientFactory _httpFactory;

    public AdminTokenPurchasesController(AppDbContext db, IHttpClientFactory httpFactory)
    {
        _db          = db;
        _httpFactory = httpFactory;
    }

    /// <summary>GET /api/v1/admin/token-purchases/crypto-pending</summary>
    [HttpGet("crypto-pending")]
    public async Task<IActionResult> ListCryptoPending(CancellationToken ct = default)
    {
        var rows = await (
            from c in _db.CryptoPaymentConfirmations.AsNoTracking()
            where c.Purpose == CryptoPaymentPurpose.TokenPurchase && c.Status == CryptoPaymentConfirmationStatus.AwaitingPayment
            join p in _db.TokenPurchases.AsNoTracking() on c.OrderId equals p.OrderId
            join o in _db.Orders.AsNoTracking() on c.OrderId equals o.Id
            join m in _db.MemberProfiles.AsNoTracking() on c.MemberId equals m.MemberId
            join t in _db.TokenTypes.AsNoTracking() on p.TokenTypeId equals t.Id
            orderby c.CreationDate descending
            select new PendingCryptoTokenPurchaseDto(
                c.OrderId, o.OrderNo, c.MemberId, (m.FirstName + " " + m.LastName).Trim(), c.MemberEmail,
                t.Name, p.Quantity, c.CryptoCurrency, c.AmountDue, c.CreationDate)
        ).ToListAsync(ct);

        return Ok(ApiResponse<IEnumerable<PendingCryptoTokenPurchaseDto>>.Ok(rows));
    }

    /// <summary>POST /api/v1/admin/token-purchases/crypto/{orderId}/confirm</summary>
    [HttpPost("crypto/{orderId}/confirm")]
    public async Task<IActionResult> ConfirmCrypto(string orderId, [FromBody] ConfirmCryptoRequest request, CancellationToken ct = default)
    {
        var exists = await _db.CryptoPaymentConfirmations.AsNoTracking()
            .AnyAsync(c => c.OrderId == orderId && c.Purpose == CryptoPaymentPurpose.TokenPurchase, ct);
        if (!exists)
            return NotFound(ApiResponse<object>.Fail("CRYPTO_PAYMENT_NOT_FOUND", "No crypto token purchase awaiting confirmation was found for that order."));

        return await ForwardAsync($"api/v1/billing/tokens/purchases/crypto/{Uri.EscapeDataString(orderId)}/confirm",
            new { cryptoTransactionId = request.CryptoTransactionId, notes = request.Notes }, ct);
    }

    /// <summary>
    /// POST /api/v1/admin/token-purchases/crypto/{orderId}/reject — la transferencia no llegó o no vale.
    /// Billing cierra la compra sin emitir y libera el cupo (D18).
    /// </summary>
    [HttpPost("crypto/{orderId}/reject")]
    public async Task<IActionResult> RejectCrypto(string orderId, [FromBody] RejectCryptoRequest request, CancellationToken ct = default)
    {
        var exists = await _db.CryptoPaymentConfirmations.AsNoTracking()
            .AnyAsync(c => c.OrderId == orderId && c.Purpose == CryptoPaymentPurpose.TokenPurchase, ct);
        if (!exists)
            return NotFound(ApiResponse<object>.Fail("CRYPTO_PAYMENT_NOT_FOUND", "No crypto token purchase awaiting confirmation was found for that order."));

        return await ForwardAsync($"api/v1/billing/tokens/purchases/crypto/{Uri.EscapeDataString(orderId)}/reject",
            new { reason = request.Reason }, ct);
    }

    /// <summary>GET /api/v1/admin/token-purchases/review — compras a conciliar (resultado del cobro dudoso).</summary>
    [HttpGet("review")]
    [Authorize(Roles = Treasury)]
    public async Task<IActionResult> ListToReview(CancellationToken ct = default)
    {
        var rows = await (
            from p in _db.TokenPurchases.AsNoTracking()
            where p.Status == TokenPurchaseStatus.NeedsReconciliation
            join o in _db.Orders.AsNoTracking() on p.OrderId equals o.Id
            join m in _db.MemberProfiles.AsNoTracking() on p.MemberId equals m.MemberId
            join t in _db.TokenTypes.AsNoTracking() on p.TokenTypeId equals t.Id
            orderby p.CreationDate
            select new TokenPurchaseToReviewDto(
                p.Id, o.OrderNo, p.MemberId, (m.FirstName + " " + m.LastName).Trim(), m.Email,
                t.Name, p.Quantity, p.TotalAmount, p.PaymentMethod.ToString(), p.FailureCode, p.Notes, p.CreationDate)
        ).ToListAsync(ct);

        return Ok(ApiResponse<IEnumerable<TokenPurchaseToReviewDto>>.Ok(rows));
    }

    /// <summary>POST /api/v1/admin/token-purchases/{purchaseId}/reconcile</summary>
    [HttpPost("{purchaseId}/reconcile")]
    [Authorize(Roles = Treasury)]
    public async Task<IActionResult> Reconcile(string purchaseId, [FromBody] ReconcileRequest request, CancellationToken ct = default)
    {
        var exists = await _db.TokenPurchases.AsNoTracking()
            .AnyAsync(p => p.Id == purchaseId && p.Status == TokenPurchaseStatus.NeedsReconciliation, ct);
        if (!exists)
            return NotFound(ApiResponse<object>.Fail("TOKEN_PURCHASE_NOT_UNDER_REVIEW", "No token purchase awaiting reconciliation was found."));

        return await ForwardAsync($"api/v1/billing/tokens/purchases/{Uri.EscapeDataString(purchaseId)}/reconcile",
            new { charged = request.Charged, gatewayReference = request.GatewayReference, notes = request.Notes }, ct);
    }

    private async Task<IActionResult> ForwardAsync(string path, object body, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient("BillingApi");
        var bearer = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(bearer) && AuthenticationHeaderValue.TryParse(bearer, out var parsed))
            http.DefaultRequestHeaders.Authorization = parsed;

        var resp = await http.PostAsJsonAsync(path, body, ct);
        return new ContentResult
        {
            Content     = await resp.Content.ReadAsStringAsync(ct),
            ContentType = "application/json",
            StatusCode  = (int)resp.StatusCode
        };
    }

    public record ConfirmCryptoRequest(string CryptoTransactionId, string? Notes);
    public record ReconcileRequest(bool Charged, string? GatewayReference, string? Notes);
    public record RejectCryptoRequest(string? Reason);

    public record PendingCryptoTokenPurchaseDto(
        string OrderId, string? OrderNo, string MemberId, string FullName, string Email,
        string TokenTypeName, int Quantity, string CryptoCurrency, decimal AmountDue, DateTime RequestedAt);

    public record TokenPurchaseToReviewDto(
        string PurchaseId, string? OrderNo, string MemberId, string FullName, string Email, string TokenTypeName,
        int Quantity, decimal TotalAmount, string PaymentMethod, string? Reason, string? Notes, DateTime RequestedAt);
}
```

En `AdminAPI/Program.cs`, debajo del `AddHttpClient("SignupApi", ...)`:

```csharp
// Billing confirma compras de tokens en cripto y cierra las que quedaron a conciliar (Plan F). Sin
// desactivar la validación del certificado: el de desarrollo es de confianza en la máquina.
builder.Services.AddHttpClient("BillingApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Billing"] ?? "https://localhost:7007");
});
```

- [ ] **Step 4: Correr las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests --filter "AdminTokenPurchasesControllerTests|TokenTypeVendibleTests" -nologo` → PASS.

- [ ] **Step 5: Crear `AdminWeb/Components/Pages/AdminTokenPurchases.razor`**

```razor
@page "/admin/billing/token-purchases"
@rendermode @(new InteractiveServerRenderMode(prerender: false))
@attribute [Microsoft.AspNetCore.Authorization.Authorize(Roles = AppRoles.CryptoPaymentApprovers)]
@inject HttpClient Http

@*
    Compras de tokens que necesitan a una persona. Arriba, las pagadas en cripto (cuatro roles de
    aprobación). Abajo, las que quedaron a conciliar porque el débito eWallet no se pudo verificar
    (tesorería). El atributo solo esconde la pantalla: AdminAPI y Billing vuelven a comprobar el rol.
*@

<PageTitle>Token Purchases — Admin</PageTitle>

<BreadcrumbNav Items="@(new List<BreadcrumbNav.BreadcrumbItem> {
    new("Admin Dashboard", "/admin"), new("Billing"), new("Token Purchases") })" />

@if (_alertMsg is not null)
{
    <div class="alert @(_alertIsError ? "alert-danger" : "alert-success") alert-dismissible mb-3">
        @_alertMsg <button type="button" class="btn-close" @onclick="() => _alertMsg = null"></button>
    </div>
}

<div class="card border-0 shadow-sm mb-4">
    <div class="card-header py-3 d-flex justify-content-between align-items-center">
        <div>
            <h5 class="mb-0 fw-bold">Crypto — awaiting payment confirmation</h5>
            <small class="text-muted">No tokens are issued until the transfer is confirmed. Purchases are non-refundable. Unconfirmed purchases expire after a few days.</small>
        </div>
        <button class="btn btn-sm btn-outline-secondary" @onclick="LoadAsync" disabled="@_loading"><i class="ri-refresh-line"></i> Refresh</button>
    </div>
    <div class="card-body p-0">
        @if (_crypto.Count == 0)
        {
            <div class="text-center py-4 text-muted"><p class="mb-0">Nothing waiting.</p></div>
        }
        else
        {
            <div class="table-responsive">
                <table class="table align-middle mb-0">
                    <thead><tr><th>Requested</th><th>Order</th><th>Member</th><th>Token type</th><th class="text-end">Qty</th><th>Currency</th><th class="text-end">Amount due</th><th></th></tr></thead>
                    <tbody>
                        @foreach (var p in _crypto)
                        {
                            var dto = p;
                            <tr>
                                <td class="small text-muted">@dto.RequestedAt.ToString("yyyy-MM-dd HH:mm")</td>
                                <td>@(dto.OrderNo ?? dto.OrderId)</td>
                                <td>@dto.FullName <span class="text-muted small">(@dto.MemberId)</span></td>
                                <td>@dto.TokenTypeName</td>
                                <td class="text-end">@dto.Quantity</td>
                                <td><span class="badge bg-warning text-dark">@dto.CryptoCurrency</span></td>
                                <td class="text-end">@dto.AmountDue.ToString("C")</td>
                                <td class="text-nowrap">
                                    <button class="btn btn-sm btn-outline-success py-0" @onclick="() => OpenConfirm(dto)">Confirm payment</button>
                                    <button class="btn btn-sm btn-outline-danger py-0" @onclick="() => OpenReject(dto)">Reject</button>
                                </td>
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
        }
    </div>
</div>

<AuthorizeView Roles="SuperAdmin,Admin,BillingManager">
    <Authorized>
        <div class="card border-0 shadow-sm">
            <div class="card-header py-3">
                <h5 class="mb-0 fw-bold">To review — charge outcome unknown</h5>
                <small class="text-muted">
                    The eWallet charge could not be verified (processing, pending or no response). Check the member's
                    i-Payout account for the purchase id as merchant reference, then close it.
                </small>
            </div>
            <div class="card-body p-0">
                @if (_review.Count == 0)
                {
                    <div class="text-center py-4 text-muted"><p class="mb-0">Nothing to review.</p></div>
                }
                else
                {
                    <div class="table-responsive">
                        <table class="table align-middle mb-0">
                            <thead><tr><th>Requested</th><th>Purchase id</th><th>Member</th><th>Token type</th><th class="text-end">Qty</th><th class="text-end">Total</th><th>Reason</th><th></th></tr></thead>
                            <tbody>
                                @foreach (var p in _review)
                                {
                                    var dto = p;
                                    <tr>
                                        <td class="small text-muted">@dto.RequestedAt.ToString("yyyy-MM-dd HH:mm")</td>
                                        <td class="font-monospace small">@dto.PurchaseId</td>
                                        <td>@dto.FullName <span class="text-muted small">(@dto.MemberId)</span></td>
                                        <td>@dto.TokenTypeName</td>
                                        <td class="text-end">@dto.Quantity</td>
                                        <td class="text-end">@dto.TotalAmount.ToString("C")</td>
                                        <td class="small" title="@dto.Notes">@dto.Reason</td>
                                        <td class="text-nowrap">
                                            <button class="btn btn-sm btn-outline-success py-0" @onclick="() => OpenReconcile(dto, true)">Charged</button>
                                            <button class="btn btn-sm btn-outline-danger py-0" @onclick="() => OpenReconcile(dto, false)">Not charged</button>
                                        </td>
                                    </tr>
                                }
                            </tbody>
                        </table>
                    </div>
                }
            </div>
        </div>
    </Authorized>
</AuthorizeView>

@if (_confirmTarget is not null || _reconcileTarget is not null || _rejectTarget is not null)
{
    <div class="modal d-block" tabindex="-1" style="background:rgba(0,0,0,.45)">
        <div class="modal-dialog modal-dialog-centered">
            <div class="modal-content">
                <div class="modal-header">
                    <h5 class="modal-title">@(_rejectTarget is not null ? "Reject crypto payment" : _confirmTarget is not null ? "Confirm crypto payment" : _charged ? "Mark as charged" : "Mark as not charged")</h5>
                    <button type="button" class="btn-close" @onclick="CloseModal" disabled="@_saving"></button>
                </div>
                <div class="modal-body">
                    @if (_rejectTarget is not null)
                    {
                        <p class="mb-2"><strong>@_rejectTarget.FullName</strong> — @_rejectTarget.Quantity × @_rejectTarget.TokenTypeName, @_rejectTarget.AmountDue.ToString("C") in @_rejectTarget.CryptoCurrency</p>
                        <div class="alert alert-warning py-2 small">No tokens are issued and the member's monthly quota is released. The order can no longer be confirmed.</div>
                        <label class="form-label small fw-semibold">Reason <span class="text-danger">*</span></label>
                    }
                    else if (_confirmTarget is not null)
                    {
                        <p class="mb-2"><strong>@_confirmTarget.FullName</strong> — @_confirmTarget.Quantity × @_confirmTarget.TokenTypeName, @_confirmTarget.AmountDue.ToString("C") in @_confirmTarget.CryptoCurrency</p>
                        <div class="alert alert-warning py-2 small">Confirming issues the tokens. Only do this once the transfer is verified on-chain.</div>
                        <label class="form-label small fw-semibold">Transaction id <span class="text-danger">*</span></label>
                    }
                    else
                    {
                        <p class="mb-2"><strong>@_reconcileTarget!.FullName</strong> — @_reconcileTarget.Quantity × @_reconcileTarget.TokenTypeName, @_reconcileTarget.TotalAmount.ToString("C")</p>
                        <div class="alert alert-warning py-2 small">
                            @(_charged ? "The tokens will be issued to the member." : "The purchase will be closed and the monthly quota released. No tokens are issued.")
                        </div>
                        <label class="form-label small fw-semibold">i-Payout reference @(_charged ? "" : "(optional)")</label>
                    }
                    @if (_rejectTarget is null)
                    {
                        <input class="form-control form-control-sm mb-3" @bind="_reference" @bind:event="oninput" maxlength="128" />
                        <label class="form-label small fw-semibold">Notes (optional)</label>
                    }
                    <textarea class="form-control form-control-sm" rows="3" @bind="_notes" @bind:event="oninput" maxlength="400"></textarea>
                </div>
                <div class="modal-footer">
                    <button class="btn btn-secondary" @onclick="CloseModal" disabled="@_saving">Cancel</button>
                    <button class="btn btn-success" disabled="@(_saving || (_confirmTarget is not null && string.IsNullOrWhiteSpace(_reference)) || (_rejectTarget is not null && string.IsNullOrWhiteSpace(_notes)))" @onclick="SubmitAsync">
                        @if (_saving) { <span class="spinner-border spinner-border-sm me-1"></span> }
                        Confirm
                    </button>
                </div>
            </div>
        </div>
    </div>
}

@code {
    private List<CryptoDto> _crypto = new();
    private List<ReviewDto> _review = new();
    private bool _loading = true, _saving, _alertIsError, _charged;
    private string? _alertMsg, _notes;
    private string _reference = string.Empty;
    private CryptoDto? _confirmTarget;
    private ReviewDto? _reconcileTarget;
    private CryptoDto? _rejectTarget;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            _crypto = (await Http.GetFromJsonAsync<ApiResp<List<CryptoDto>>>("api/v1/admin/token-purchases/crypto-pending"))?.Data ?? new();
            try { _review = (await Http.GetFromJsonAsync<ApiResp<List<ReviewDto>>>("api/v1/admin/token-purchases/review"))?.Data ?? new(); }
            catch (HttpRequestException) { _review = new(); } // SupportLevel3 no ve la sección de tesorería.
        }
        catch (Exception ex) { _alertMsg = $"Error: {ex.Message}"; _alertIsError = true; }
        finally { _loading = false; }
    }

    private void OpenConfirm(CryptoDto dto) { _confirmTarget = dto; _reconcileTarget = null; _rejectTarget = null; _reference = string.Empty; _notes = null; }
    private void OpenReject(CryptoDto dto) { _rejectTarget = dto; _confirmTarget = null; _reconcileTarget = null; _reference = string.Empty; _notes = null; }
    private void OpenReconcile(ReviewDto dto, bool charged) { _reconcileTarget = dto; _confirmTarget = null; _rejectTarget = null; _charged = charged; _reference = string.Empty; _notes = null; }
    private void CloseModal() { _confirmTarget = null; _reconcileTarget = null; _rejectTarget = null; }

    private async Task SubmitAsync()
    {
        _saving = true;
        try
        {
            var notes = string.IsNullOrWhiteSpace(_notes) ? null : _notes!.Trim();
            var resp = _rejectTarget is not null
                ? await Http.PostAsJsonAsync($"api/v1/admin/token-purchases/crypto/{_rejectTarget.OrderId}/reject",
                    new { Reason = notes })
                : _confirmTarget is not null
                ? await Http.PostAsJsonAsync($"api/v1/admin/token-purchases/crypto/{_confirmTarget.OrderId}/confirm",
                    new { CryptoTransactionId = _reference.Trim(), Notes = notes })
                : await Http.PostAsJsonAsync($"api/v1/admin/token-purchases/{_reconcileTarget!.PurchaseId}/reconcile",
                    new { Charged = _charged, GatewayReference = string.IsNullOrWhiteSpace(_reference) ? null : _reference.Trim(), Notes = notes });

            if (resp.IsSuccessStatusCode)
            {
                _alertMsg = "Done."; _alertIsError = false;
                CloseModal();
                await LoadAsync();
            }
            else
            {
                var payload = await resp.Content.ReadFromJsonAsync<ApiResp<object>>();
                _alertMsg = payload?.Errors?.FirstOrDefault() ?? payload?.Message ?? payload?.ErrorCode ?? $"Failed ({(int)resp.StatusCode}).";
                _alertIsError = true;
            }
        }
        catch (Exception ex) { _alertMsg = $"Error: {ex.Message}"; _alertIsError = true; }
        finally { _saving = false; }
    }

    private record ApiResp<T>(bool Success, T? Data, string? Message, string? ErrorCode, IEnumerable<string>? Errors);
    private record CryptoDto(string OrderId, string? OrderNo, string MemberId, string FullName, string Email,
        string TokenTypeName, int Quantity, string CryptoCurrency, decimal AmountDue, DateTime RequestedAt);
    private record ReviewDto(string PurchaseId, string? OrderNo, string MemberId, string FullName, string Email, string TokenTypeName,
        int Quantity, decimal TotalAmount, string PaymentMethod, string? Reason, string? Notes, DateTime RequestedAt);
}
```

- [ ] **Step 6: Menú y tipos de token**

`AdminSidebarMenu.razor`: dentro del `<Authorized>` de `CryptoPaymentApprovers` (~:310), después del `</li>` de "Crypto Signups":

```razor
                <li class="menu-item @(IsActive("/admin/billing/token-purchases") == "active" ? "active" : "")">
                    <a href="/admin/billing/token-purchases" class="menu-link @IsActive("/admin/billing/token-purchases")">
                        <i class="ri-coins-line menu-icon"></i>
                        <span class="title">Token Purchases</span>
                    </a>
                </li>
```

`AdminTokenTypes.razor`:
- Debajo de la línea `<div class="form-check"><input class="form-check-input" type="checkbox" id="igp" @bind="_form.IsGuestPass"/><label class="form-check-label" for="igp">Is Guest Pass</label></div>` añadir:

```razor
                    <div class="form-check mt-2"><input class="form-check-input" type="checkbox" id="ipur" @bind="_form.IsPurchasable"/><label class="form-check-label" for="ipur">For sale in the Biz Center (members can buy it)</label></div>
```

- En `OpenEdit`, `IsGuestPass = t.IsGuestPass, Category = t.Category` → `IsGuestPass = t.IsGuestPass, Category = t.Category, IsPurchasable = t.IsPurchasable`.
- En el `PutAsJsonAsync`, `_form.Category, IsActive = _editIsActive` → `_form.Category, IsActive = _editIsActive, _form.IsPurchasable`.
- En `TokenTypeFormModel`, añadir `public bool IsPurchasable { get; set; }`.
- `private record TokenTypeDto(int Id, string Name, string? Description, bool IsGuestPass, string? TemplateUrl, bool IsActive, string Category);` → añadir `, bool IsPurchasable` antes del `)`.

- [ ] **Step 7: Compilar**

Run: `dotnet build MLMConquerorGlobalEdition.AdminWeb -v q -nologo` → `0 Error(s)`.
Run: `dotnet test MLMConquerorGlobalEdition.AdminAPI.Tests -nologo` → PASS.

- [ ] **Step 8: Commit**

```bash
git add MLMConquerorGlobalEdition.AdminAPI MLMConquerorGlobalEdition.AdminAPI.Tests MLMConquerorGlobalEdition.AdminWeb MLMConquerorGlobalEdition.SharedComponents/Components/Layout/AdminSidebarMenu.razor
git commit -m "Panel de compras de tokens: confirmar las pagadas en cripto y cerrar las que quedaron a conciliar, reenviando a Billing con el Bearer del administrador; los tipos de token se marcan como vendibles desde su pantalla"
```

---
### Task 13: BizCenter — cliente de Billing y endpoints

**Files:**
- Create: `MLMConquerorGlobalEdition.BizCenter/Services/BearerToken.cs`, `IBillingClient.cs`, `BillingClient.cs`
- Modify: `MLMConquerorGlobalEdition.BizCenter/Program.cs` (después de `AddScoped<IRankEngineClient, RankEngineClient>()`)
- Modify: `MLMConquerorGlobalEdition.BizCenter/Controllers/TokensController.cs`, `WalletController.cs`, `RanksController.cs:132-143`
- Create: `MLMConquerorGlobalEdition.BizCenter/Features/Tokens/GetTokenSummary/GetTokenSummaryQuery.cs`, `GetTokenSummaryHandler.cs`
- Create: `MLMConquerorGlobalEdition.BizCenter/DTOs/Tokens/Validators/PurchaseTokensRequestValidator.cs`
- Modify: `MLMConquerorGlobalEdition.BizCenter/DTOs/Tokens/TokenBalanceDto.cs`, `Features/Tokens/GetTokenBalances/GetTokenBalancesHandler.cs`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/CompraDeTokensEnElBizCenterTests.cs`

> **Tarjeta apagada y clave reutilizada (revisión de la Tarea 9).** `TokenCatalogDto.CardEnabled` y `TokenPurchaseFundsDto.CardEnabled` viajan tal cual desde Billing (`Billing:TokenPurchase:CardEnabled`, apagada por defecto, R13); el BizCenter no los cambia. Con la tarjeta apagada Billing responde `400 CARD_PAYMENTS_DISABLED` y el BizCenter lo reenvía sin traducirlo. Reusar una clave de idempotencia con otro tipo, cantidad o método es `409 IDEMPOTENCY_KEY_REUSED`: `BillingClient` debe reenviar el 409 con su código (no convertirlo en `BILLING_UNAVAILABLE`, que conservaría la clave). Lo mismo para `PAYMENT_SERVICE_UNAVAILABLE` (400): es un fallo definitivo de esa clave.

> **Step-up por ruta (D13).** El filtro de Plan E es incondicional por acción, así que la condición la pone la ruta: `POST tokens/purchase/balance` lleva `[RequiresStepUp(TokenPurchaseFromBalance)]` y solo acepta comisiones o eWallet; `POST tokens/purchase` no lo lleva y solo acepta tarjeta o cripto. Mandar un método por la ruta que no le toca es `400 PAYMENT_METHOD_WRONG_ENDPOINT`: así no hay forma de pagar con saldo por la ruta sin código. El permiso ya validado (y quemado) se reenvía a Billing en `X-Step-Up-Token`.

- [ ] **Step 1: Escribir las pruebas (fallan: los tipos no existen)**

```csharp
using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MediatR;
using MLMConquerorGlobalEdition.BizCenter.Controllers;
using MLMConquerorGlobalEdition.BizCenter.Features.Tokens.GetTokenSummary;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SharedKernel.Server.Security;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;
using IDateTimeProvider   = MLMConquerorGlobalEdition.BizCenter.Services.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// El BizCenter es un paso hacia Billing: reenvía el Bearer del miembro tal cual, no añade MemberId,
/// exige step-up en la ruta de pago con saldo (y no deja pagar con saldo por la otra), y convierte una
/// caída de Billing en un código que la pantalla sabe traducir. El resumen es local: solo lee.
/// </summary>
public class CompraDeTokensEnElBizCenterTests
{
    private sealed class Manejador(Func<HttpRequestMessage, HttpResponseMessage> f) : HttpMessageHandler
    {
        public HttpRequestMessage? Ultima;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Ultima = r;
            return Task.FromResult(f(r));
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode s, string json) =>
        new(s) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static BillingClient Cliente(HttpMessageHandler h)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(BillingClient.HttpClientName))
               .Returns(new HttpClient(h) { BaseAddress = new Uri("https://billing.pruebas/") });
        return new BillingClient(factory.Object, NullLogger<BillingClient>.Instance);
    }

    private static TokensController Controlador(Mock<IBillingClient> billing, string? grant = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Authorization = "Bearer jwt.miembro";
        if (grant is not null) ctx.Request.Headers[StepUpGrant.HeaderName] = grant;
        return new TokensController(Mock.Of<IMediator>(), billing.Object) { ControllerContext = new ControllerContext { HttpContext = ctx } };
    }

    [Fact]
    public async Task El_cliente_reenvia_el_Bearer_y_lee_los_datos()
    {
        var h = new Manejador(_ => Json(HttpStatusCode.OK,
            """{"success":true,"data":{"monthlyLimit":50,"purchasedThisMonth":3,"monthlyRemaining":47,"items":[],"cryptoDeposits":[]}}"""));

        var r = await Cliente(h).GetTokenCatalogAsync("jwt.del.miembro");

        r.Value!.MonthlyRemaining.Should().Be(47);
        h.Ultima!.RequestUri!.AbsolutePath.Should().Be("/api/v1/billing/tokens/catalog");
        h.Ultima.Headers.Authorization!.ToString().Should().Be("Bearer jwt.del.miembro");
    }

    [Fact]
    public async Task El_cliente_manda_el_permiso_de_step_up_si_lo_hay()
    {
        var h = new Manejador(_ => Json(HttpStatusCode.OK, """{"success":true,"data":{"purchaseId":"p1"}}"""));

        await Cliente(h).PurchaseTokensAsync(new PurchaseTokensRequest(), "jwt", "permiso");

        h.Ultima!.Headers.GetValues(StepUpGrant.HeaderName).Single().Should().Be("permiso");
    }

    [Fact]
    public async Task El_cliente_conserva_el_codigo_de_error_de_Billing()
    {
        var h = new Manejador(_ => Json(HttpStatusCode.BadRequest,
            """{"success":false,"errorCode":"TOKEN_MONTHLY_LIMIT_REACHED","errors":["limit"]}"""));

        (await Cliente(h).PurchaseTokensAsync(new PurchaseTokensRequest(), "jwt")).ErrorCode
            .Should().Be(TokenPurchaseErrors.MonthlyLimitReached);
    }

    [Fact]
    public async Task Si_Billing_no_responde_es_BILLING_UNAVAILABLE()
        => (await Cliente(new Manejador(_ => throw new HttpRequestException("caido"))).GetPurchaseFundsAsync("jwt"))
            .ErrorCode.Should().Be(TokenPurchaseErrors.BillingUnavailable);

    [Fact]
    public async Task Sin_Bearer_no_llama()
    {
        var h = new Manejador(_ => throw new InvalidOperationException("no debía llamar"));

        (await Cliente(h).GetTokenCatalogAsync("")).ErrorCode.Should().Be("AUTH_TOKEN_MISSING");
        h.Ultima.Should().BeNull();
    }

    [Fact]
    public void Solo_la_ruta_de_saldo_exige_step_up_y_con_su_operacion()
    {
        typeof(TokensController).GetMethod(nameof(TokensController.PurchaseFromBalance))!
            .GetCustomAttribute<RequiresStepUpAttribute>()!.OperationKey.Should().Be(StepUpOperations.TokenPurchaseFromBalance);
        typeof(TokensController).GetMethod(nameof(TokensController.Purchase))!
            .GetCustomAttribute<RequiresStepUpAttribute>().Should().BeNull();
    }

    [Theory]
    [InlineData(TokenPaymentMethods.CommissionBalance)]
    [InlineData(TokenPaymentMethods.EWallet)]
    public async Task Pagar_con_saldo_por_la_ruta_sin_step_up_es_400(string metodo)
    {
        var billing = new Mock<IBillingClient>(MockBehavior.Strict);

        var r = await Controlador(billing).Purchase(new PurchaseTokensRequest { PaymentMethod = metodo }, default);

        r.Should().BeOfType<BadRequestObjectResult>().Which.Value.Should().BeOfType<ApiResponse<TokenPurchaseResultDto>>()
         .Which.ErrorCode.Should().Be(TokenPurchaseErrors.WrongEndpoint);
    }

    [Fact]
    public async Task La_ruta_de_saldo_no_acepta_tarjeta()
    {
        var billing = new Mock<IBillingClient>(MockBehavior.Strict);

        (await Controlador(billing).PurchaseFromBalance(new PurchaseTokensRequest { PaymentMethod = TokenPaymentMethods.CreditCard }, default))
            .Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task La_ruta_de_saldo_reenvia_el_permiso_y_el_Bearer()
    {
        var billing = new Mock<IBillingClient>();
        billing.Setup(b => b.PurchaseTokensAsync(It.IsAny<PurchaseTokensRequest>(), "jwt.miembro", "permiso", It.IsAny<CancellationToken>()))
               .ReturnsAsync(Result<TokenPurchaseResultDto>.Success(new TokenPurchaseResultDto { PurchaseId = "p1" }));

        var r = await Controlador(billing, grant: "permiso")
            .PurchaseFromBalance(new PurchaseTokensRequest { PaymentMethod = TokenPaymentMethods.EWallet }, default);

        r.Should().BeOfType<OkObjectResult>();
        billing.VerifyAll();
    }

    [Fact]
    public async Task Si_Billing_cae_la_compra_es_503()
    {
        var billing = new Mock<IBillingClient>();
        billing.Setup(b => b.PurchaseTokensAsync(It.IsAny<PurchaseTokensRequest>(), "jwt.miembro", null, It.IsAny<CancellationToken>()))
               .ReturnsAsync(Result<TokenPurchaseResultDto>.Failure(TokenPurchaseErrors.BillingUnavailable, "caido"));

        var r = await Controlador(billing).Purchase(new PurchaseTokensRequest { PaymentMethod = TokenPaymentMethods.CreditCard }, default);

        r.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task El_resumen_cuenta_las_instancias_del_miembro()
    {
        var now = new DateTime(2026, 10, 15, 10, 0, 0);
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        TokenTransaction I(string code, TokenInstanceStatus s, string owner = "AMB-1", DateTime? expira = null) => new()
        {
            MemberId = owner, TokenTypeId = 13, TransactionType = TokenTransactionType.Purchased, Quantity = 1,
            ReferenceId = code, Status = s, ExpiresAt = expira, CreatedBy = "seed", CreationDate = now
        };
        db.TokenTransactions.AddRange(
            I("A1", TokenInstanceStatus.Issued), I("A2", TokenInstanceStatus.Distributed), I("A3", TokenInstanceStatus.Used),
            I("A4", TokenInstanceStatus.Issued, expira: now.AddDays(-1)), I("B1", TokenInstanceStatus.Issued, owner: "AMB-2"));
        db.TokenTransactions.Add(new TokenTransaction
        {
            MemberId = "AMB-1", TokenTypeId = 13, TransactionType = TokenTransactionType.Distributed, Quantity = 2, CreatedBy = "seed", CreationDate = now
        });
        await db.SaveChangesAsync();
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.MemberId).Returns("AMB-1");
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.Now).Returns(now);

        var r = await new GetTokenSummaryHandler(db, user.Object, clock.Object).Handle(new GetTokenSummaryQuery(), default);

        r.Value!.TotalTokens.Should().Be(4);
        r.Value.TotalUsed.Should().Be(1);
        r.Value.TotalAvailable.Should().Be(2);
    }
}
```

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter CompraDeTokensEnElBizCenterTests -nologo` → FAIL.

- [ ] **Step 2: Crear `BizCenter/Services/BearerToken.cs`**

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace MLMConquerorGlobalEdition.BizCenter.Services;

/// <summary>El token crudo de Authorization, para reenviarlo a otro servicio. Vacío si no hay Bearer.</summary>
public static class BearerToken
{
    public static string From(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(HeaderNames.Authorization, out var values))
            return string.Empty;

        var raw = values.ToString();
        const string prefix = "Bearer ";
        return raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? raw.Substring(prefix.Length).Trim()
            : string.Empty;
    }
}
```

En `RanksController.cs`, reemplazar el método `ExtractBearerToken` completo (de `private string ExtractBearerToken()` a su `}`) por `private string ExtractBearerToken() => BearerToken.From(Request);` y añadir `using MLMConquerorGlobalEdition.BizCenter.Services;` (hoy no lo importa). El `using Microsoft.Net.Http.Headers;` queda sin uso y se quita.

- [ ] **Step 3: Crear `IBillingClient.cs` y `BillingClient.cs`**

```csharp
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.BizCenter.Services;

/// <summary>
/// Paso hacia Billing para la compra de tokens. El BizCenter no calcula precios, cupos ni saldos: se
/// reenvía el JWT del miembro y Billing saca de él quién compra.
/// </summary>
public interface IBillingClient
{
    Task<Result<TokenCatalogDto>> GetTokenCatalogAsync(string bearerToken, CancellationToken ct = default);
    Task<Result<TokenPurchaseFundsDto>> GetPurchaseFundsAsync(string bearerToken, CancellationToken ct = default);

    /// <param name="stepUpGrant">Permiso de step-up ya validado por el filtro; obligatorio para pagar con saldo.</param>
    Task<Result<TokenPurchaseResultDto>> PurchaseTokensAsync(
        PurchaseTokensRequest request, string bearerToken, string? stepUpGrant = null, CancellationToken ct = default);

    /// <summary>Retira una compra en cripto sin pagar del miembro del token (D18).</summary>
    Task<Result<TokenPurchaseResultDto>> CancelTokenPurchaseAsync(
        string purchaseId, string bearerToken, CancellationToken ct = default);
}
```

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.BizCenter.Services;

/// <inheritdoc cref="IBillingClient"/>
/// <remarks>
/// SIN REINTENTOS AUTOMÁTICOS: si la compra se corta, reintenta la persona con la MISMA clave de
/// idempotencia y Billing devuelve la compra que ya hizo.
/// </remarks>
public sealed class BillingClient : IBillingClient
{
    public const string HttpClientName = "billing";

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<BillingClient> _logger;

    public BillingClient(IHttpClientFactory factory, ILogger<BillingClient> logger)
    {
        _factory = factory;
        _logger  = logger;
    }

    public Task<Result<TokenCatalogDto>> GetTokenCatalogAsync(string bearerToken, CancellationToken ct = default) =>
        SendAsync<TokenCatalogDto>(new HttpRequestMessage(HttpMethod.Get, "api/v1/billing/tokens/catalog"), bearerToken, ct);

    public Task<Result<TokenPurchaseFundsDto>> GetPurchaseFundsAsync(string bearerToken, CancellationToken ct = default) =>
        SendAsync<TokenPurchaseFundsDto>(new HttpRequestMessage(HttpMethod.Get, "api/v1/billing/tokens/funds"), bearerToken, ct);

    public Task<Result<TokenPurchaseResultDto>> PurchaseTokensAsync(
        PurchaseTokensRequest request, string bearerToken, string? stepUpGrant = null, CancellationToken ct = default)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "api/v1/billing/tokens/purchase") { Content = JsonContent.Create(request) };
        if (!string.IsNullOrWhiteSpace(stepUpGrant))
            message.Headers.TryAddWithoutValidation(StepUpGrant.HeaderName, stepUpGrant);
        return SendAsync<TokenPurchaseResultDto>(message, bearerToken, ct);
    }

    public Task<Result<TokenPurchaseResultDto>> CancelTokenPurchaseAsync(
        string purchaseId, string bearerToken, CancellationToken ct = default) =>
        SendAsync<TokenPurchaseResultDto>(new HttpRequestMessage(HttpMethod.Post,
            $"api/v1/billing/tokens/purchases/{Uri.EscapeDataString(purchaseId)}/cancel"), bearerToken, ct);

    private async Task<Result<T>> SendAsync<T>(HttpRequestMessage request, string bearerToken, CancellationToken ct) where T : class
    {
        using (request)
        {
            if (string.IsNullOrWhiteSpace(bearerToken))
                return Result<T>.Failure("AUTH_TOKEN_MISSING", "Caller bearer token is required.");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            var client = _factory.CreateClient(HttpClientName);

            try
            {
                using var response = await client.SendAsync(request, ct);
                var body = await TryReadAsync<T>(response, ct);

                if (response.IsSuccessStatusCode && body?.Success == true && body.Data is not null)
                    return Result<T>.Success(body.Data);

                // ApiResponse.Fail rellena Errors, no Message.
                var code    = body?.ErrorCode ?? $"BILLING_HTTP_{(int)response.StatusCode}";
                var message = body?.Errors?.FirstOrDefault() ?? body?.Message ?? $"Billing returned {(int)response.StatusCode}.";
                _logger.LogWarning("Billing respondió {Status} {Code} a {Path}.", (int)response.StatusCode, code, request.RequestUri);
                return Result<T>.Failure(code, message);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "No se pudo llamar a Billing ({Path}).", request.RequestUri);
                return Result<T>.Failure(TokenPurchaseErrors.BillingUnavailable, "Billing is unavailable.");
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "Billing no respondió a tiempo ({Path}).", request.RequestUri);
                return Result<T>.Failure(TokenPurchaseErrors.BillingUnavailable, "Billing did not respond in time.");
            }
        }
    }

    private static async Task<ApiResponse<T>?> TryReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        try { return await response.Content.ReadFromJsonAsync<ApiResponse<T>>(cancellationToken: ct); }
        catch { return null; }
    }
}
```

- [ ] **Step 4: Registrar en `BizCenter/Program.cs`**, debajo de `builder.Services.AddScoped<IRankEngineClient, RankEngineClient>();`:

```csharp
// Cliente de Billing para la compra de tokens (Plan F). 90 s porque una compra con tarjeta puede
// probar varios procesadores y una con eWallet llama dos veces a i-Payout.
{
    var billingBaseUrl = builder.Configuration["Services:BillingBaseUrl"] ?? "https://localhost:7007/";
    builder.Services.AddHttpClient(BillingClient.HttpClientName, c =>
    {
        c.BaseAddress = new Uri(billingBaseUrl);
        c.Timeout     = TimeSpan.FromSeconds(90);
    });
}
builder.Services.AddScoped<IBillingClient, BillingClient>();
```

- [ ] **Step 5: Resumen local**

`Features/Tokens/GetTokenSummary/GetTokenSummaryQuery.cs`:

```csharp
using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tokens.GetTokenSummary;

public record GetTokenSummaryQuery() : IRequest<Result<TokenSummaryDto>>;
```

`Features/Tokens/GetTokenSummary/GetTokenSummaryHandler.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tokens.GetTokenSummary;

/// <summary>Las tres cifras de la cabecera, sobre las instancias canjeables (con código) que hoy son del miembro.</summary>
public class GetTokenSummaryHandler : IRequestHandler<GetTokenSummaryQuery, Result<TokenSummaryDto>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider   _dateTime;

    public GetTokenSummaryHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db          = db;
        _currentUser = currentUser;
        _dateTime    = dateTime;
    }

    public async Task<Result<TokenSummaryDto>> Handle(GetTokenSummaryQuery request, CancellationToken ct)
    {
        var memberId  = _currentUser.MemberId;
        var now       = _dateTime.Now;
        var instances = _db.TokenTransactions.AsNoTracking().Where(t => t.MemberId == memberId && t.ReferenceId != null);

        return Result<TokenSummaryDto>.Success(new TokenSummaryDto
        {
            TotalTokens    = await instances.CountAsync(ct),
            TotalUsed      = await instances.CountAsync(t => t.Status == TokenInstanceStatus.Used, ct),
            TotalAvailable = await instances.CountAsync(t =>
                (t.Status == TokenInstanceStatus.Issued || t.Status == TokenInstanceStatus.Distributed)
                && (t.ExpiresAt == null || t.ExpiresAt > now), ct)
        });
    }
}
```

- [ ] **Step 6: Validador de entrada** — `DTOs/Tokens/Validators/PurchaseTokensRequestValidator.cs`:

```csharp
using FluentValidation;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Tokens.Validators;

/// <summary>Forma de la petición antes de molestar a Billing. Precio, cupo, saldo y dirección cripto los decide Billing.</summary>
public class PurchaseTokensRequestValidator : AbstractValidator<PurchaseTokensRequest>
{
    private static readonly string[] Methods =
    [
        TokenPaymentMethods.CommissionBalance, TokenPaymentMethods.EWallet,
        TokenPaymentMethods.CreditCard, TokenPaymentMethods.Crypto
    ];

    public PurchaseTokensRequestValidator()
    {
        RuleFor(x => x.TokenTypeId).GreaterThan(0);
        RuleFor(x => x.Quantity).InclusiveBetween(1, TokenPurchaseRules.MonthlyLimit);
        RuleFor(x => x.PaymentMethod).Must(m => Methods.Contains(m)).WithMessage("Unsupported payment method.");
        RuleFor(x => x.IdempotencyKey).Must(TokenPurchaseRules.IsValidIdempotencyKey).WithMessage("Invalid idempotency key.");
        RuleFor(x => x.CreditCardId).NotEmpty().MaximumLength(450).When(x => x.PaymentMethod == TokenPaymentMethods.CreditCard);
        RuleFor(x => x.CryptoCurrency)
            .Must(c => c is not null && TokenPurchaseRules.CryptoCurrencies.Contains(c.Trim().ToUpperInvariant()))
            .When(x => x.PaymentMethod == TokenPaymentMethods.Crypto)
            .WithMessage("Unsupported crypto currency.");
    }
}
```

- [ ] **Step 7: Endpoints en `TokensController.cs`**

Imports nuevos:

```csharp
using MLMConquerorGlobalEdition.BizCenter.Features.Tokens.GetTokenSummary;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SharedKernel.Server.Security;
```

Constructor:

```csharp
    private readonly IMediator _mediator;
    private readonly IBillingClient _billing;

    public TokensController(IMediator mediator, IBillingClient billing)
    {
        _mediator = mediator;
        _billing  = billing;
    }
```

Acciones nuevas, antes de la clase `TransferTokenInstanceRequest`:

```csharp
    /// <summary>GET /api/v1/bizcenter/tokens/summary — total, usados y disponibles del miembro.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTokenSummaryQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<TokenSummaryDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<TokenSummaryDto>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/tokens/types — catálogo de compra (precio, si se vende, cupo, monedas cripto). Lo calcula Billing.</summary>
    [HttpGet("types")]
    public async Task<IActionResult> GetPurchasableTypes(CancellationToken ct)
    {
        var result = await _billing.GetTokenCatalogAsync(BearerToken.From(Request), ct);
        return result.IsSuccess ? Ok(ApiResponse<TokenCatalogDto>.Ok(result.Value!)) : BillingFailure(result);
    }

    /// <summary>
    /// POST /api/v1/bizcenter/tokens/purchase — compra con tarjeta o cripto. Pagar con saldo por aquí es
    /// 400: tiene su propia ruta, con step-up. No reembolsable.
    /// </summary>
    [HttpPost("purchase")]
    public async Task<IActionResult> Purchase([FromBody] PurchaseTokensRequest request, CancellationToken ct)
    {
        if (TokenPaymentMethods.IsBalance(request.PaymentMethod))
            return WrongEndpoint("Paying with your balance uses tokens/purchase/balance.");

        var result = await _billing.PurchaseTokensAsync(request, BearerToken.From(Request), null, ct);
        return result.IsSuccess ? Ok(ApiResponse<TokenPurchaseResultDto>.Ok(result.Value!)) : BillingFailure(result);
    }

    /// <summary>
    /// POST /api/v1/bizcenter/tokens/purchase/balance — compra con comisiones o eWallet. Exige un permiso
    /// de step-up (lo valida y quema el filtro); se reenvía a Billing, que lo vuelve a validar.
    /// </summary>
    [HttpPost("purchase/balance")]
    [RequiresStepUp(StepUpOperations.TokenPurchaseFromBalance)]
    public async Task<IActionResult> PurchaseFromBalance([FromBody] PurchaseTokensRequest request, CancellationToken ct)
    {
        if (!TokenPaymentMethods.IsBalance(request.PaymentMethod))
            return WrongEndpoint("Card and crypto purchases use tokens/purchase.");

        var grant  = Request.Headers[StepUpGrant.HeaderName].ToString();
        var result = await _billing.PurchaseTokensAsync(request, BearerToken.From(Request), grant, ct);
        return result.IsSuccess ? Ok(ApiResponse<TokenPurchaseResultDto>.Ok(result.Value!)) : BillingFailure(result);
    }

    /// <summary>
    /// POST /api/v1/bizcenter/tokens/purchases/{purchaseId}/cancel — el miembro retira una compra en
    /// cripto que no va a pagar y recupera el cupo. Billing comprueba que es suya y que sigue sin pagar.
    /// </summary>
    [HttpPost("purchases/{purchaseId}/cancel")]
    public async Task<IActionResult> CancelPurchase(string purchaseId, CancellationToken ct)
    {
        var result = await _billing.CancelTokenPurchaseAsync(purchaseId, BearerToken.From(Request), ct);
        return result.IsSuccess ? Ok(ApiResponse<TokenPurchaseResultDto>.Ok(result.Value!)) : BillingFailure(result);
    }

    private IActionResult WrongEndpoint(string message) =>
        BadRequest(ApiResponse<TokenPurchaseResultDto>.Fail(TokenPurchaseErrors.WrongEndpoint, message, HttpContext.TraceIdentifier));

    private IActionResult BillingFailure<T>(Result<T> result)
    {
        var body = ApiResponse<T>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier);
        return result.ErrorCode == TokenPurchaseErrors.BillingUnavailable
            ? StatusCode(StatusCodes.Status503ServiceUnavailable, body)
            : BadRequest(body);
    }
```

- [ ] **Step 8: `wallet/balances` en `WalletController.cs`** — añadir `using MLMConquerorGlobalEdition.SharedKernel.Billing;` y, después de `GET wallet`:

```csharp
    /// <summary>
    /// GET /api/v1/bizcenter/wallet/balances — con cuánto puede pagar tokens: comisiones gastables y
    /// saldo real de i-Payout. Lo calcula Billing.
    /// </summary>
    [HttpGet("wallet/balances")]
    public async Task<IActionResult> GetPurchaseBalances(
        [FromServices] MLMConquerorGlobalEdition.BizCenter.Services.IBillingClient billing, CancellationToken ct)
    {
        var result = await billing.GetPurchaseFundsAsync(MLMConquerorGlobalEdition.BizCenter.Services.BearerToken.From(Request), ct);
        if (result.IsSuccess)
            return Ok(ApiResponse<TokenPurchaseFundsDto>.Ok(result.Value!));

        var body = ApiResponse<TokenPurchaseFundsDto>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier);
        return result.ErrorCode == TokenPurchaseErrors.BillingUnavailable
            ? StatusCode(StatusCodes.Status503ServiceUnavailable, body)
            : BadRequest(body);
    }
```

- [ ] **Step 9: `TokenTypeId` en los saldos** — en `TokenBalanceDto.cs` añadir `public int TokenTypeId { get; set; }` como primera propiedad, y en `GetTokenBalancesHandler.cs`, dentro de `new TokenBalanceDto { ... }`, `TokenTypeId = tt.Id,`.

> La caché `member:{id}:token-balances` (10 min) puede servir filas sin `TokenTypeId` justo tras el despliegue; caducan solas y la pantalla no ofrece tipo `0`.

- [ ] **Step 10: Correr las pruebas y compilar**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "CompraDeTokensEnElBizCenterTests|RankCertificateOnDemandHandlersTests|StepUpAuthorizationFilterTests" -nologo` → PASS.
Run: `dotnet build MLMConquerorGlobalEdition.BizCenter -v q -nologo` → `0 Error(s)`.

- [ ] **Step 11: Commit**

```bash
git add MLMConquerorGlobalEdition.BizCenter MLMConquerorGlobalEdition.BizCenter.Tests
git commit -m "El BizCenter expone tokens/types, tokens/summary, tokens/purchase, tokens/purchase/balance con step-up y wallet/balances: el dinero lo resuelve Billing con el JWT del miembro y pagar con saldo solo entra por la ruta que pide el codigo"
```

---
### Task 14: BizCenter — la distribución mueve los códigos

**Files:**
- Modify: `MLMConquerorGlobalEdition.BizCenter/Features/Tokens/DistributeToken/DistributeTokenHandler.cs` (reemplazo completo)
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/DistribucionDeTokensTests.cs`

- [ ] **Step 1: Escribir las pruebas (fallan: hoy acuña códigos nuevos y no mira el equipo)**

```csharp
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tokens;
using MLMConquerorGlobalEdition.BizCenter.Features.Tokens.DistributeToken;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;
using IDateTimeProvider   = MLMConquerorGlobalEdition.BizCenter.Services.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// DISTRIBUIR ES MOVER, NO DUPLICAR. El handler viejo restaba del saldo del que enviaba y acuñaba
/// códigos nuevos para el que recibía, pero dejaba los códigos del que enviaba en Issued: los dos
/// podían canjearlos. Y no miraba si el destinatario era de su equipo.
/// </summary>
public class DistribucionDeTokensTests : IDisposable
{
    private const string Sender   = "AMB-SENDER";
    private const string Downline = "AMB-DOWN-1";
    private const string Outsider = "AMB-OTHER";
    private const int    Type     = 13;
    private static readonly DateTime Now = new(2026, 10, 15, 10, 0, 0);

    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public void Dispose() => _db.Dispose();

    private DistributeTokenHandler Handler()
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.MemberId).Returns(Sender);
        user.Setup(u => u.UserId).Returns("user-sender");
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.Now).Returns(Now);
        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var push = new Mock<IPushNotificationService>();
        push.Setup(p => p.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new DistributeTokenHandler(_db, user.Object, clock.Object, cache.Object, push.Object);
    }

    private async Task SeedAsync(params TokenTransaction[] instances)
    {
        _db.MemberProfiles.AddRange(
            new MemberProfile { MemberId = Sender,   FirstName = "S", LastName = "S", MemberType = MemberType.Ambassador, EnrollDate = Now, Country = "US", CreatedBy = "seed", LastUpdateDate = Now },
            new MemberProfile { MemberId = Downline, FirstName = "D", LastName = "D", MemberType = MemberType.Ambassador, EnrollDate = Now, Country = "US", CreatedBy = "seed", LastUpdateDate = Now },
            new MemberProfile { MemberId = Outsider, FirstName = "O", LastName = "O", MemberType = MemberType.Ambassador, EnrollDate = Now, Country = "US", CreatedBy = "seed", LastUpdateDate = Now });
        _db.GenealogyTree.AddRange(
            new GenealogyEntity { MemberId = Sender,   ParentMemberId = "ROOT", HierarchyPath = $"/{Sender}/",             Level = 1, CreatedBy = "seed", CreationDate = Now },
            new GenealogyEntity { MemberId = Downline, ParentMemberId = Sender, HierarchyPath = $"/{Sender}/{Downline}/", Level = 2, CreatedBy = "seed", CreationDate = Now },
            new GenealogyEntity { MemberId = Outsider, ParentMemberId = "ROOT", HierarchyPath = $"/{Outsider}/",           Level = 1, CreatedBy = "seed", CreationDate = Now });
        _db.TokenTypes.Add(new TokenType { Id = Type, Name = "VIP Member", IsActive = true, CreatedBy = "seed", CreationDate = Now });
        _db.TokenTransactions.AddRange(instances);
        _db.TokenBalances.Add(new TokenBalance { MemberId = Sender, TokenTypeId = Type, Balance = instances.Length, CreatedBy = "seed", CreationDate = Now });
        await _db.SaveChangesAsync();
    }

    private static TokenTransaction I(long id, string code, int daysAgo,
        TokenInstanceStatus status = TokenInstanceStatus.Issued, DateTime? expires = null) => new()
    {
        Id = id, MemberId = Sender, OriginalOwnerMemberId = Sender, TokenTypeId = Type,
        TransactionType = TokenTransactionType.Purchased, Quantity = 1, ReferenceId = code,
        Status = status, ExpiresAt = expires, CreatedBy = "seed", CreationDate = Now.AddDays(-daysAgo)
    };

    private static DistributeTokenCommand Cmd(string to, int qty) =>
        new(new DistributeTokenRequest { TokenTypeId = Type, RecipientMemberId = to, Quantity = qty });

    [Fact]
    public async Task Mueve_los_codigos_mas_antiguos_sin_crear_nuevos()
    {
        await SeedAsync(I(1, "OLD0001", 9), I(2, "OLD0002", 5), I(3, "NEW0003", 1));

        var r = await Handler().Handle(Cmd(Downline, 2), default);

        r.IsSuccess.Should().BeTrue();
        var instancias = await _db.TokenTransactions.Where(t => t.ReferenceId != null).ToListAsync();
        instancias.Should().HaveCount(3, "distribuir no acuña códigos");
        instancias.Where(t => t.MemberId == Downline).Select(t => t.ReferenceId)
                  .Should().BeEquivalentTo(new[] { "OLD0001", "OLD0002" });
        instancias.Where(t => t.MemberId == Downline)
                  .Should().OnlyContain(t => t.PreviousOwnerMemberId == Sender && t.Status == TokenInstanceStatus.Distributed);
        instancias.Single(t => t.ReferenceId == "NEW0003").MemberId.Should().Be(Sender);
        (await _db.TokenBalances.SingleAsync(b => b.MemberId == Sender)).Balance.Should().Be(1);
        (await _db.TokenBalances.SingleAsync(b => b.MemberId == Downline)).Balance.Should().Be(2);
        (await _db.TokenTransactions.SingleAsync(t => t.ReferenceId == null)).Quantity.Should().Be(2);
    }

    [Fact]
    public async Task Fuera_del_equipo_se_rechaza_y_no_mueve_nada()
    {
        await SeedAsync(I(1, "OLD0001", 9));

        var r = await Handler().Handle(Cmd(Outsider, 1), default);

        r.ErrorCode.Should().Be("RECIPIENT_NOT_IN_DOWNLINE");
        (await _db.TokenTransactions.SingleAsync()).MemberId.Should().Be(Sender);
    }

    [Fact]
    public async Task No_cuenta_usados_ni_vencidos_como_disponibles()
    {
        await SeedAsync(
            I(1, "USED001", 9, TokenInstanceStatus.Used),
            I(2, "EXPI002", 8, expires: Now.AddDays(-1)),
            I(3, "GOOD003", 1));

        var r = await Handler().Handle(Cmd(Downline, 2), default);

        r.ErrorCode.Should().Be("INSUFFICIENT_TOKEN_BALANCE");
        _db.TokenTransactions.Count(t => t.MemberId == Downline).Should().Be(0);
    }
}
```

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter DistribucionDeTokensTests -nologo`
Expected: FAIL — el handler actual acuña códigos y no mira el equipo.

- [ ] **Step 2: Reemplazar `DistributeTokenHandler.cs` completo**

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tokens.DistributeToken;

/// <summary>
/// Distribuye N tokens de un tipo a un miembro del equipo de inscripción. Es N transferencias de
/// instancia de una vez: los MISMOS códigos pasan al destinatario (los más antiguos primero), con
/// la cadena de custodia de <see cref="TransferTokenInstance.TransferTokenInstanceHandler"/>.
/// </summary>
/// <remarks>
/// La versión anterior restaba del agregado del que enviaba y acuñaba códigos nuevos para el que
/// recibía, dejando los del que enviaba en Issued: dos personas podían canjear "el mismo" token. Y
/// aceptaba cualquier destinatario existente, sin mirar el equipo.
///
/// Para dar un token a alguien FUERA del equipo no hace falta este endpoint: los códigos son al
/// portador y se canjean al inscribirse (ValidateToken no exige relación con el dueño). La pantalla
/// ofrece copiar el código.
/// </remarks>
public class DistributeTokenHandler : IRequestHandler<DistributeTokenCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICacheService _cache;
    private readonly IPushNotificationService _push;

    public DistributeTokenHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime,
        ICacheService cache,
        IPushNotificationService push)
    {
        _db          = db;
        _currentUser = currentUser;
        _dateTime    = dateTime;
        _cache       = cache;
        _push        = push;
    }

    public async Task<Result<bool>> Handle(DistributeTokenCommand command, CancellationToken ct)
    {
        var senderId  = _currentUser.MemberId;
        var req       = command.Request;
        var recipient = req.RecipientMemberId.Trim();
        var now       = _dateTime.Now;

        if (string.Equals(senderId, recipient, StringComparison.OrdinalIgnoreCase))
            return Result<bool>.Failure("INVALID_RECIPIENT", "Cannot distribute tokens to yourself.");

        var tokenTypeExists = await _db.TokenTypes.AsNoTracking()
            .AnyAsync(tt => tt.Id == req.TokenTypeId && tt.IsActive, ct);
        if (!tokenTypeExists)
            return Result<bool>.Failure("TOKEN_TYPE_NOT_FOUND", "Token type not found or inactive.");

        var recipientExists = await _db.MemberProfiles.AsNoTracking().AnyAsync(m => m.MemberId == recipient, ct);
        if (!recipientExists)
            return Result<bool>.Failure("RECIPIENT_NOT_FOUND", "Recipient member not found.");

        // Misma regla que TransferTokenInstance: el destinatario cuelga del que envía en el árbol de inscripción.
        var senderPath = await _db.GenealogyTree.AsNoTracking()
            .Where(g => g.MemberId == senderId)
            .Select(g => g.HierarchyPath)
            .FirstOrDefaultAsync(ct);
        if (senderPath is null)
            return Result<bool>.Failure("SENDER_NOT_IN_TREE", "Sender's enrollment node not found.");

        var inDownline = await _db.GenealogyTree.AsNoTracking()
            .AnyAsync(g => g.MemberId == recipient && g.MemberId != senderId && g.HierarchyPath.StartsWith(senderPath), ct);
        if (!inDownline)
            return Result<bool>.Failure("RECIPIENT_NOT_IN_DOWNLINE", "Recipient must be a member of your enrollment team.");

        var instances = await _db.TokenTransactions
            .Where(t => t.MemberId == senderId
                     && t.TokenTypeId == req.TokenTypeId
                     && t.ReferenceId != null
                     && (t.Status == TokenInstanceStatus.Issued || t.Status == TokenInstanceStatus.Distributed)
                     && (t.ExpiresAt == null || t.ExpiresAt > now))
            .OrderBy(t => t.CreationDate).ThenBy(t => t.Id)
            .Take(req.Quantity)
            .ToListAsync(ct);

        if (instances.Count < req.Quantity)
            return Result<bool>.Failure("INSUFFICIENT_TOKEN_BALANCE",
                $"You only have {instances.Count} available token(s) of this type.");

        foreach (var instance in instances)
        {
            instance.PreviousOwnerMemberId = instance.MemberId;
            instance.MemberId              = recipient;
            instance.Status                = TokenInstanceStatus.Distributed;
            instance.LastUpdateDate        = now;
            instance.LastUpdateBy          = _currentUser.UserId;
        }

        _db.TokenTransactions.Add(new TokenTransaction
        {
            MemberId              = senderId,
            TokenTypeId           = req.TokenTypeId,
            TransactionType       = TokenTransactionType.Distributed,
            Quantity              = req.Quantity,
            DistributedToMemberId = recipient,
            ReferenceId           = null,
            PreviousOwnerMemberId = senderId,
            Status                = TokenInstanceStatus.Distributed,
            CreatedBy             = _currentUser.UserId,
            CreationDate          = now,
            Notes                 = $"{req.Quantity} token(s) distributed to {recipient}: " +
                                    string.Join(", ", instances.Select(i => i.ReferenceId))
        });

        var senderBalance = await _db.TokenBalances
            .FirstOrDefaultAsync(tb => tb.MemberId == senderId && tb.TokenTypeId == req.TokenTypeId, ct);
        if (senderBalance is not null)
        {
            senderBalance.Balance        = Math.Max(0, senderBalance.Balance - req.Quantity);
            senderBalance.LastUpdateDate = now;
            senderBalance.LastUpdateBy   = _currentUser.UserId;
        }

        var recipientBalance = await _db.TokenBalances
            .FirstOrDefaultAsync(tb => tb.MemberId == recipient && tb.TokenTypeId == req.TokenTypeId, ct);
        if (recipientBalance is null)
        {
            recipientBalance = new TokenBalance
            {
                MemberId     = recipient,
                TokenTypeId  = req.TokenTypeId,
                Balance      = 0,
                CreatedBy    = _currentUser.UserId,
                CreationDate = now
            };
            _db.TokenBalances.Add(recipientBalance);
        }
        recipientBalance.Add(req.Quantity);
        recipientBalance.LastUpdateDate = now;
        recipientBalance.LastUpdateBy   = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);

        await Task.WhenAll(
            _cache.RemoveAsync(CacheKeys.MemberTokenBalances(senderId), ct),
            _cache.RemoveAsync(CacheKeys.MemberTokenBalances(recipient), ct));

        _ = _push.SendAsync(recipient, NotificationEvents.TokenReceived,
            "Tokens Received", $"You have received {req.Quantity} token(s).", ct);

        return Result<bool>.Success(true);
    }
}
```

- [ ] **Step 3: Correr las pruebas**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter "DistribucionDeTokensTests|TransferTokenInstanceHandlerTests" -nologo`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add MLMConquerorGlobalEdition.BizCenter MLMConquerorGlobalEdition.BizCenter.Tests
git commit -m "Distribuir tokens mueve los mismos codigos al destinatario y exige que sea del equipo de inscripcion: antes se acunaban codigos nuevos y los del que enviaba seguian canjeables"
```

---

### Task 15: La pantalla de tokens (SharedComponents)

> **Pestaña de tarjeta solo si está encendida (revisión de la Tarea 9, R13).** `PaymentTabs` se filtra: la pestaña `CreditCard` solo aparece si `_catalog.CardEnabled` es `true` (sin catálogo, tampoco). Si la pestaña elegida deja de estar disponible al recargar, se vuelve a la primera. `TokenPurchaseMessages.ErrorKeyFor` añade `CardPaymentsDisabled` → `Tokens.Error.CardDisabled` ("El pago con tarjeta no está disponible por ahora.", en los 9 `.resx`), `IdempotencyKeyReused` → `Tokens.Error.Generic` (y se genera clave nueva) y `PaymentServiceUnavailable` → `Tokens.Error.Unavailable`. `KeepsKey` **no** incluye ninguno de los tres: son fallos definitivos de esa clave. Prueba bUnit: con `cardEnabled: false` en el catálogo no existe `#tokens-tab-CreditCard`; con `true`, sí.

> **Cancelar una cripto sin pagar (D18).** El resultado `AwaitingCryptoConfirmation` muestra, junto a la dirección, un botón "Cancelar esta compra" que llama a `POST tokens/purchases/{purchaseId}/cancel` (Tarea 13) y, si va bien, recarga el catálogo (el cupo vuelve). Los estados `Cancelled`, `Rejected` y `Expired` y el código `TOKEN_PURCHASE_NOT_AWAITING_CRYPTO` necesitan texto en `TokenPurchaseMessages` y en los `.resx`.

**Files:**
- Create: `MLMConquerorGlobalEdition.SharedComponents/Components/Tokens/TokenPurchaseMessages.cs`
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Components/Tokens/TokensPage.razor` (reemplazo completo)
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Components/Account/StepUp/StepUpMessages.cs`, `StepUpDialog.razor`
- Modify: `MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources*.resx` (9)
- Modify: `MLMConquerorGlobalEdition.BizCenterWeb/Components/Pages/Tokens.razor`, `MLMConquerorGlobalEdition.BizCenterApp/Components/Pages/Tokens.razor`
- Test: `MLMConquerorGlobalEdition.BizCenter.Tests/PantallaDeTokensTests.cs`

- [ ] **Step 1: Escribir las pruebas bUnit (fallan: la pantalla no tiene esos ids, parámetros ni step-up)**

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MLMConquerorGlobalEdition.SharedComponents.Components.Account.StepUp;
using MLMConquerorGlobalEdition.SharedComponents.Components.Tokens;
using MLMConquerorGlobalEdition.SharedComponents.Extensions;
using MLMConquerorGlobalEdition.SharedComponents.Resources;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// La pantalla de tokens tragaba los errores, llamaba a endpoints inexistentes y mandaba el
/// TokenTypeId como texto. Estas pruebas fijan el contrato real, los errores traducidos, la clave de
/// idempotencia que sobrevive a una caída, el step-up al pagar con saldo (y no con tarjeta) y la
/// dirección de depósito al pagar en cripto.
/// </summary>
public class PantallaDeTokensTests : BunitContext
{
    private sealed class Manejador(Func<HttpRequestMessage, Task<HttpResponseMessage>> f) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) => f(r);
    }

    private sealed class ClienteQueConcede : IStepUpClient
    {
        public Task<StepUpOutcome<StepUpChallenge>> RequestChallengeAsync(string o, CancellationToken ct = default) =>
            Task.FromResult(new StepUpOutcome<StepUpChallenge>(true, null,
                new StepUpChallenge("reto", "Email", "n***@x.com", DateTime.Now.AddMinutes(5))));

        public Task<StepUpOutcome<string>> VerifyAsync(string o, string c, string code, CancellationToken ct = default) =>
            Task.FromResult(new StepUpOutcome<string>(true, null, "permiso"));
    }

    private static HttpResponseMessage Json(HttpStatusCode s, string json) =>
        new(s) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private const string Catalogo = """{"success":true,"data":{"monthlyLimit":50,"purchasedThisMonth":0,"monthlyRemaining":50,"items":[{"tokenTypeId":13,"name":"VIP Member","unitPrice":99.99,"purchasable":true},{"tokenTypeId":92,"name":"VIP Member FREE","purchasable":false,"notPurchasableReason":"TOKEN_TYPE_NOT_FOR_SALE"}],"cryptoDeposits":[{"currency":"USDT","address":"TQ5NkEQwK6x9C2iGBDbTj9JxF8tqmL7Ak5","network":"TRC-20"}]}}""";
    private const string Fondos   = """{"success":true,"data":{"commissionBalance":500,"eWalletBalance":null,"hasEWallet":false}}""";
    private const string Resumen  = """{"success":true,"data":{"totalTokens":0,"totalUsed":0,"totalAvailable":0}}""";
    private const string Lista    = """{"success":true,"data":{"items":[],"totalCount":0}}""";
    private const string Vacio    = """{"success":true,"data":[]}""";
    private const string Tarjetas = """{"success":true,"data":[{"id":"c1","last4":"1111","cardBrand":"Visa","expiryMonth":12,"expiryYear":2030,"isExpired":false}]}""";
    private const string Hecha    = """{"success":true,"data":{"purchaseId":"p1","orderId":"o1","orderNo":"TP1","status":"Completed","quantity":1,"totalAmount":99.99,"tokenCodes":["ABC1234"],"monthlyRemaining":49}}""";
    private const string EnCripto = """{"success":true,"data":{"purchaseId":"p2","orderId":"o2","orderNo":"TP2","status":"AwaitingCryptoConfirmation","quantity":1,"totalAmount":99.99,"tokenCodes":[],"monthlyRemaining":49,"cryptoCurrency":"USDT","cryptoDepositAddress":"TQ5NkEQwK6x9C2iGBDbTj9JxF8tqmL7Ak5","cryptoNetwork":"TRC-20"}}""";

    private static HttpResponseMessage Get(HttpRequestMessage r)
    {
        var path = r.RequestUri!.AbsolutePath;
        if (path.EndsWith("/tokens/types"))         return Json(HttpStatusCode.OK, Catalogo);
        if (path.EndsWith("/wallet/balances"))      return Json(HttpStatusCode.OK, Fondos);
        if (path.EndsWith("/tokens/summary"))       return Json(HttpStatusCode.OK, Resumen);
        if (path.EndsWith("/billing/credit-cards")) return Json(HttpStatusCode.OK, Tarjetas);
        if (path.EndsWith("/tokens"))               return Json(HttpStatusCode.OK, Lista);
        return Json(HttpStatusCode.OK, Vacio); // tokens/balances
    }

    private IRenderedComponent<TokensPage> Montar(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> f, bool compra = true)
    {
        Services.AddSharedComponents();
        Services.AddSingleton(new HttpClient(new Manejador(f)) { BaseAddress = new Uri("https://api.pruebas/") });
        Services.AddSingleton<IStringLocalizer<SharedResources>>(new StepUpDialogTests.LocalizadorDeClaves());
        Services.AddSingleton<IStepUpClient>(new ClienteQueConcede());
        JSInterop.Mode = JSRuntimeMode.Loose;
        return Render<TokensPage>(p => p
            .Add(c => c.MemberProfileId, "AMB-1")
            .Add(c => c.PurchaseEnabled, compra)
            .Add(c => c.DistributionEnabled, compra));
    }

    private static void ElegirVip(IRenderedComponent<TokensPage> page)
    {
        page.WaitForAssertion(() => page.Find("#tokens-purchase-type option[value='13']"));
        page.Find("#tokens-purchase-type").Change("13");
    }

    [Fact]
    public async Task Un_error_de_compra_se_ve_traducido()
    {
        var page = Montar(r => Task.FromResult(r.Method == HttpMethod.Post
            ? Json(HttpStatusCode.BadRequest, """{"success":false,"errorCode":"TOKEN_MONTHLY_LIMIT_REACHED"}""")
            : Get(r)));

        ElegirVip(page);
        await page.Find("#tokens-buy").ClickAsync(new());

        page.WaitForAssertion(() => page.Find("#tokens-purchase-error").TextContent.Should().Contain("Tokens.Error.MonthlyLimit"));
    }

    [Fact]
    public async Task Manda_el_contrato_del_backend_sin_memberId_y_muestra_los_codigos()
    {
        string? cuerpo = null;
        var page = Montar(async r =>
        {
            if (r.Method != HttpMethod.Post) return Get(r);
            cuerpo = await r.Content!.ReadAsStringAsync();
            return Json(HttpStatusCode.OK, Hecha);
        });

        ElegirVip(page);
        await page.Find("#tokens-buy").ClickAsync(new());

        page.WaitForAssertion(() => page.Markup.Should().Contain("Tokens.PurchaseDone").And.Contain("ABC1234"));
        using var json = JsonDocument.Parse(cuerpo!);
        json.RootElement.GetProperty("tokenTypeId").GetInt32().Should().Be(13);
        json.RootElement.GetProperty("paymentMethod").GetString().Should().Be(TokenPaymentMethods.CommissionBalance);
        json.RootElement.GetProperty("idempotencyKey").GetString().Should().NotBeNullOrWhiteSpace();
        json.RootElement.TryGetProperty("memberId", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Tras_una_caida_el_reintento_reusa_la_clave()
    {
        var claves = new List<string>();
        var page = Montar(async r =>
        {
            if (r.Method != HttpMethod.Post) return Get(r);
            using var doc = JsonDocument.Parse(await r.Content!.ReadAsStringAsync());
            claves.Add(doc.RootElement.GetProperty("idempotencyKey").GetString()!);
            if (claves.Count == 1) throw new HttpRequestException("caida");
            return Json(HttpStatusCode.OK, Hecha);
        });

        ElegirVip(page);
        await page.Find("#tokens-buy").ClickAsync(new());
        page.WaitForAssertion(() => page.Find("#tokens-purchase-error").TextContent.Should().Contain("Tokens.Error.Unavailable"));
        await page.Find("#tokens-buy").ClickAsync(new());

        page.WaitForAssertion(() => page.Markup.Should().Contain("Tokens.PurchaseDone"));
        claves.Should().HaveCount(2);
        claves[1].Should().Be(claves[0]);
    }

    [Fact]
    public void Pagar_con_saldo_pide_el_codigo_y_reintenta_con_el_permiso()
    {
        var posts = new List<(string Path, string? Grant)>();
        var page = Montar(r =>
        {
            if (r.Method != HttpMethod.Post) return Task.FromResult(Get(r));
            var grant = r.Headers.TryGetValues(StepUpGrant.HeaderName, out var v) ? v.Single() : null;
            posts.Add((r.RequestUri!.AbsolutePath, grant));
            return Task.FromResult(grant is null
                ? Json(HttpStatusCode.Forbidden, """{"success":false,"errorCode":"STEP_UP_REQUIRED"}""")
                : Json(HttpStatusCode.OK, Hecha));
        });

        ElegirVip(page);
        _ = page.Find("#tokens-buy").ClickAsync(new());          // queda esperando el código
        page.WaitForAssertion(() => page.Find(".stepup-target"));
        page.Find("#stepup-code").Input("123456");
        _ = page.Find(".stepup-confirm").ClickAsync(new());

        page.WaitForAssertion(() => page.Markup.Should().Contain("Tokens.PurchaseDone"));
        posts.Should().HaveCount(2).And.OnlyContain(p => p.Path.EndsWith("/tokens/purchase/balance"));
        posts[1].Grant.Should().Be("permiso");
    }

    [Fact]
    public async Task Con_tarjeta_va_a_la_ruta_sin_step_up()
    {
        var rutas = new List<string>();
        var page = Montar(r =>
        {
            if (r.Method != HttpMethod.Post) return Task.FromResult(Get(r));
            rutas.Add(r.RequestUri!.AbsolutePath);
            return Task.FromResult(Json(HttpStatusCode.OK, Hecha));
        });

        ElegirVip(page);
        await page.Find("#tokens-tab-CreditCard").ClickAsync(new());
        page.WaitForAssertion(() => page.Find("#tokens-card-c1"));
        await page.Find("#tokens-card-c1").ClickAsync(new());
        await page.Find("#tokens-buy").ClickAsync(new());

        page.WaitForAssertion(() => rutas.Should().ContainSingle().Which.Should().EndWith("/tokens/purchase"));
    }

    [Fact]
    public async Task En_cripto_muestra_la_direccion_antes_y_despues_de_pedir()
    {
        var page = Montar(r => Task.FromResult(r.Method == HttpMethod.Post ? Json(HttpStatusCode.OK, EnCripto) : Get(r)));

        ElegirVip(page);
        await page.Find("#tokens-tab-Crypto").ClickAsync(new());
        page.WaitForAssertion(() => page.Markup.Should().Contain("TQ5NkEQwK6x9C2iGBDbTj9JxF8tqmL7Ak5").And.Contain("Tokens.CryptoAmountDue"));
        await page.Find("#tokens-buy").ClickAsync(new());

        page.WaitForAssertion(() =>
        {
            page.Markup.Should().Contain("Tokens.PurchaseCryptoPending");
            page.Find("#tokens-crypto-address").TextContent.Should().Be("TQ5NkEQwK6x9C2iGBDbTj9JxF8tqmL7Ak5");
        });
    }

    [Fact]
    public void Los_tipos_que_no_se_venden_no_se_ofrecen()
    {
        var page = Montar(r => Task.FromResult(Get(r)));

        page.WaitForAssertion(() => page.Find("#tokens-purchase-type option[value='13']"));
        page.FindAll("#tokens-purchase-type option[value='92']").Should().BeEmpty();
    }

    [Fact]
    public void Sin_PurchaseEnabled_no_hay_compra_ni_se_pide_el_catalogo()
    {
        var pedidas = new List<string>();
        var page = Montar(r => { pedidas.Add(r.RequestUri!.AbsolutePath); return Task.FromResult(Get(r)); }, compra: false);

        page.WaitForAssertion(() => pedidas.Should().Contain(p => p.EndsWith("/tokens/summary")));
        page.FindAll("#tokens-buy").Should().BeEmpty();
        pedidas.Should().NotContain(p => p.EndsWith("/tokens/types"));
    }

    [Fact]
    public void Si_una_carga_falla_se_avisa_en_vez_de_callar()
    {
        var page = Montar(r => Task.FromResult(r.RequestUri!.AbsolutePath.EndsWith("/tokens/summary")
            ? Json(HttpStatusCode.InternalServerError, "{}")
            : Get(r)));

        page.WaitForAssertion(() => page.Find("#tokens-load-error"));
    }

    [Fact]
    public void El_dialogo_explica_por_que_pide_el_codigo_segun_la_operacion()
    {
        StepUpMessages.WhyKeyFor(StepUpOperations.TokenPurchaseFromBalance).Should().Be("StepUp.WhyTokenPurchase");
        StepUpMessages.WhyKeyFor(StepUpOperations.PayoutMethodChange).Should().Be("StepUp.Why");
    }
}
```

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests --filter PantallaDeTokensTests -nologo` → FAIL.

- [ ] **Step 2: Texto del diálogo por operación**

En `StepUpMessages.cs`, dentro de la clase:

```csharp
    /// <summary>
    /// Por qué se pide el código, según la operación. El texto de siempre ("antes de cambiar a dónde
    /// se pagan tus comisiones") no sirve para pagar tokens con saldo.
    /// </summary>
    public static string WhyKeyFor(string? operationKey) =>
        operationKey == StepUpOperations.TokenPurchaseFromBalance ? "StepUp.WhyTokenPurchase" : "StepUp.Why";
```

En `StepUpDialog.razor`, reemplazar `<p class="text-muted small">@L["StepUp.Why"]</p>` por:

```razor
                    <p class="text-muted small">@L[StepUpMessages.WhyKeyFor(_operationKey)]</p>
```

- [ ] **Step 3: Crear `Components/Tokens/TokenPurchaseMessages.cs`**

```csharp
using static MLMConquerorGlobalEdition.SharedKernel.Billing.TokenPurchaseErrors;

namespace MLMConquerorGlobalEdition.SharedComponents.Components.Tokens;

/// <summary>Traduce los códigos de compra y distribución a claves de recurso.</summary>
public static class TokenPurchaseMessages
{
    public static string ErrorKeyFor(string? code) => code switch
    {
        MonthlyLimitReached                                              => "Tokens.Error.MonthlyLimit",
        InsufficientCommissionBalance                                    => "Tokens.Error.InsufficientCommissions",
        InsufficientEWalletBalance                                       => "Tokens.Error.InsufficientEWallet",
        EWalletNotFound                                                  => "Tokens.Error.NoEWallet",
        EWalletRejected                                                  => "Tokens.Error.EWalletDeclined",
        NeedsReview                                                      => "Tokens.Error.NeedsReview",
        TokenTypeNotFound or NotForSale or NoGrantedProduct
            or ProductInactive or PriceNotSet                            => "Tokens.Error.NotPurchasable",
        CreditCardNotFound or CreditCardExpired or CreditCardNotChargeable => "Tokens.Error.Card",
        CardChargeFailed or NoImmediateGateway                           => "Tokens.Error.CardDeclined",
        CryptoAddressNotConfigured or CryptoCurrencyNotSupported         => "Tokens.CryptoUnavailable",
        InProgress or Busy                                               => "Tokens.Error.InProgress",
        Impersonation or "IMPERSONATION_READ_ONLY"                       => "Tokens.Error.Impersonation",
        MemberInactive                                                   => "Tokens.Error.MemberInactive",
        BillingUnavailable or EWalletUnavailable                         => "Tokens.Error.Unavailable",
        "INSUFFICIENT_TOKEN_BALANCE"                                     => "Tokens.Error.NotEnoughTokens",
        "RECIPIENT_NOT_IN_DOWNLINE" or "RECIPIENT_NOT_FOUND" or "INVALID_RECIPIENT" => "Tokens.Error.Recipient",
        _                                                                => "Tokens.Error.Generic"
    };

    /// <summary>
    /// ¿Hay que conservar la clave? Sí cuando no se sabe si la compra entró (caída, en curso, ocupado)
    /// o cuando quedó en revisión: repetir con la misma clave devuelve lo ya hecho, y una clave nueva
    /// podría cobrar dos veces. Un fallo definitivo cierra esa clave.
    /// </summary>
    public static bool KeepsKey(string? code) =>
        code is InProgress or Busy or BillingUnavailable or NeedsReview;
}
```

- [ ] **Step 4: Reemplazar `TokensPage.razor` completo**

```razor
@using System.Net.Http.Json
@using MLMConquerorGlobalEdition.SharedComponents.Components.Account.StepUp
@using MLMConquerorGlobalEdition.SharedKernel.Billing
@using MLMConquerorGlobalEdition.SharedKernel.Security
@inject HttpClient Http
@inject IStringLocalizer<SharedResources> L
@inject IJSRuntime JS

@*
    Pantalla de tokens compartida (componentes unificados): la montan BizCenterWeb, BizCenterApp y el
    panel admin. Los anfitriones del miembro encienden compra y distribución; el panel no, porque nadie
    compra con el dinero de otro. Ningún dato de identidad viaja en las peticiones. Pagar con saldo
    pasa por el step-up (StepUpDialog + StepUpHttp); tarjeta y cripto no. Los errores se ven.
*@

<StepUpDialog @ref="_stepUp" />

<BreadcrumbNav Items="@_breadcrumb" />

@if (_loadFailed)
{
    <div class="alert alert-warning" role="alert" id="tokens-load-error">@L["Tokens.Error.LoadFailed"]</div>
}

<div class="card bg-white border-0 rounded-3 mb-4">
    <div class="card-body p-4">
        <h3 class="mb-3">@L["Tokens.Resume"]</h3>
        <div class="row">
            @StatBox("content_paste", "primary", L["Tokens.TotalTokens"], _summary?.TotalTokens)
            @StatBox("check_circle", "info", L["Tokens.TotalUsed"], _summary?.TotalUsed)
            @StatBox("token", "success", L["Tokens.TotalAvailable"], _summary?.TotalAvailable)
        </div>
    </div>
</div>

@if (PurchaseEnabled)
{
    <div class="card bg-white border-0 rounded-3 mb-4">
        <div class="card-body p-4">
            <h4 class="fs-14 mb-2">@L["Tokens.PurchaseNote"]</h4>
            @if (_catalog is not null)
            {
                <p class="text-muted small mb-3" id="tokens-remaining">@L["Tokens.MonthlyRemaining", _catalog.MonthlyRemaining]</p>
            }

            @if (_purchaseErrorKey is not null)
            {
                <div class="alert alert-danger py-2" role="alert" id="tokens-purchase-error">@L[_purchaseErrorKey]</div>
            }
            @if (_purchaseResult is not null)
            {
                <div class="alert alert-success py-2" role="status" id="tokens-purchase-done">
                    @if (_purchaseResult.Status == TokenPurchaseStatuses.AwaitingCryptoConfirmation)
                    {
                        <div>@(L["Tokens.PurchaseCryptoPending", _purchaseResult.OrderNo ?? _purchaseResult.OrderId, _purchaseResult.TotalAmount.ToString("C"), _purchaseResult.CryptoCurrency ?? string.Empty])</div>
                        @if (!string.IsNullOrWhiteSpace(_purchaseResult.CryptoDepositAddress))
                        {
                            <div class="mt-2 small">@L["Tokens.CryptoAddress"]</div>
                            <div class="font-monospace text-break" id="tokens-crypto-address">@_purchaseResult.CryptoDepositAddress</div>
                            @if (!string.IsNullOrWhiteSpace(_purchaseResult.CryptoNetwork))
                            {
                                <div class="small">@L["Tokens.CryptoNetwork", _purchaseResult.CryptoNetwork]</div>
                            }
                        }
                    }
                    else
                    {
                        @L["Tokens.PurchaseDone", _purchaseResult.Quantity]
                        <div class="mt-1 font-monospace">@string.Join(", ", _purchaseResult.TokenCodes)</div>
                    }
                </div>
            }

            @if (_catalog is not null && !PurchasableItems.Any())
            {
                <p class="text-muted">@L["Tokens.NoPurchasableTypes"]</p>
            }

            <div class="row g-3 align-items-end">
                <div class="col-md-5">
                    <label class="form-label small" for="tokens-purchase-type">@L["Tokens.TokenType"]</label>
                    <select id="tokens-purchase-type" class="form-select" value="@_purchaseTokenTypeId" @onchange="OnPurchaseTypeChanged">
                        <option value="">—</option>
                        @foreach (var item in PurchasableItems)
                        {
                            <option value="@item.TokenTypeId">@item.Name</option>
                        }
                    </select>
                </div>
                <div class="col-md-3">
                    <label class="form-label small" for="tokens-purchase-qty">@L["Tokens.Amount"]</label>
                    <select id="tokens-purchase-qty" class="form-select" value="@_purchaseAmount" @onchange="OnPurchaseAmountChanged">
                        @for (var n = 1; n <= MaxPurchasable; n++)
                        {
                            var v = n;
                            <option value="@v">@v</option>
                        }
                    </select>
                </div>
                <div class="col-md-4">
                    @if (SelectedItem?.UnitPrice is decimal price)
                    {
                        <div class="small">@L["Tokens.UnitPrice", price.ToString("C")]</div>
                        <div class="fw-semibold" id="tokens-total">@L["Tokens.TotalPrice", (price * _purchaseAmount).ToString("C")]</div>
                    }
                </div>
            </div>

            <ul class="nav nav-tabs bg-transparent border-bottom gap-2 gap-sm-5 mt-4" role="tablist">
                @foreach (var (method, key) in PaymentTabs)
                {
                    var m = method;
                    <li class="nav-item" role="presentation">
                        <button id="tokens-tab-@m" type="button"
                                class="nav-link p-0 bg-transparent border-0 fs-12 fw-medium text-body @(_payTab == m ? "active" : "")"
                                @onclick="() => SelectTab(m)">@L[key]</button>
                    </li>
                }
            </ul>

            <div class="pt-4">
                @if (_payTab == TokenPaymentMethods.CommissionBalance)
                {
                    <span>@L["Tokens.CommissionBalanceLabel"]</span>
                    <h3 class="fs-20 mt-1 mb-0">@(_funds?.CommissionBalance.ToString("C") ?? "—")</h3>
                }
                else if (_payTab == TokenPaymentMethods.EWallet)
                {
                    @if (_funds is { HasEWallet: false })
                    {
                        <p class="text-muted mb-0">@L["Tokens.Error.NoEWallet"]</p>
                    }
                    else
                    {
                        <span>@L["Tokens.EWalletBalanceLabel"]</span>
                        <h3 class="fs-20 mt-1 mb-0">@(_funds?.EWalletBalance?.ToString("C") ?? "—")</h3>
                    }
                }
                else if (_payTab == TokenPaymentMethods.CreditCard)
                {
                    @if (_cards.Count == 0)
                    {
                        <p class="text-muted text-center py-3">@L["Tokens.NoCreditCards"]</p>
                    }
                    else
                    {
                        <div class="row g-3">
                            @foreach (var card in _cards)
                            {
                                var c = card;
                                <div class="col-lg-4 col-md-4">
                                    <button type="button" id="tokens-card-@c.Id" disabled="@c.IsExpired"
                                            class="btn w-100 text-start p-3 border rounded-3 @(_selectedCardId == c.Id ? "border-primary border-2" : "")"
                                            @onclick="() => SelectCard(c)">
                                        <div class="fw-semibold">@c.CardBrand •••• @c.Last4</div>
                                        <div class="small text-muted">@L["Tokens.CardExpiryPrefix"] @($"{c.ExpiryMonth:00}/{c.ExpiryYear}")</div>
                                    </button>
                                </div>
                            }
                        </div>
                    }
                }
                else if (_payTab == TokenPaymentMethods.Crypto)
                {
                    @if (CryptoDeposits.Count == 0)
                    {
                        <p class="text-muted mb-0" id="tokens-crypto-unavailable">@L["Tokens.CryptoUnavailable"]</p>
                    }
                    else
                    {
                        <div class="row g-3 align-items-end">
                            <div class="col-md-3">
                                <label class="form-label small" for="tokens-crypto">@L["Tokens.CryptoCurrency"]</label>
                                <select id="tokens-crypto" class="form-select" value="@_cryptoCurrency" @onchange="OnCryptoChanged">
                                    @foreach (var d in CryptoDeposits)
                                    {
                                        <option value="@d.Currency">@d.Currency</option>
                                    }
                                </select>
                            </div>
                            <div class="col-md-9">
                                @if (SelectedDeposit is not null)
                                {
                                    <div class="small">@L["Tokens.CryptoAddress"]: <span class="font-monospace text-break">@SelectedDeposit.Address</span></div>
                                    @if (!string.IsNullOrWhiteSpace(SelectedDeposit.Network))
                                    {
                                        <div class="small">@L["Tokens.CryptoNetwork", SelectedDeposit.Network]</div>
                                    }
                                    @if (SelectedItem?.UnitPrice is decimal p)
                                    {
                                        <div class="fw-semibold">@L["Tokens.CryptoAmountDue", (p * _purchaseAmount).ToString("C"), SelectedDeposit.Currency]</div>
                                    }
                                }
                            </div>
                        </div>
                        <p class="text-muted small mt-2 mb-0">@L["Tokens.CryptoNote"]</p>
                    }
                }
            </div>

            <button id="tokens-buy" class="btn btn-primary mt-4" disabled="@(!CanBuy)" @onclick="PurchaseTokensAsync">
                @if (_purchaseBusy) { <span class="spinner-border spinner-border-sm me-1"></span> }
                @L["Tokens.BuyNow"]
            </button>
        </div>
    </div>
}

<div class="card bg-white border-0 rounded-3 mb-4">
    <div class="card-body p-0">
        <LoadingSpinner IsLoading="@_isLoading" />
        @if (!_isLoading)
        {
            <div class="default-table-area style-two default-table-width">
                <div class="table-responsive">
                    <table class="table align-middle">
                        <thead>
                            <tr>
                                <th scope="col">@L["Tokens.ColDate"]</th>
                                <th scope="col">@L["Tokens.ColTransaction"]</th>
                                <th scope="col">@L["Tokens.ColTokenCode"]</th>
                                <th scope="col">@L["Tokens.ColType"]</th>
                                <th scope="col">@L["Tokens.ColUseDate"]</th>
                                <th scope="col">@L["Tokens.ColUsedBy"]</th>
                            </tr>
                        </thead>
                        <tbody>
                            @foreach (var item in _items)
                            {
                                var row = item;
                                <tr>
                                    <td class="text-body">@row.OccurredAt.ToString("MM/dd/yyyy")</td>
                                    <td class="text-body">@row.TransactionType</td>
                                    <td class="text-body">
                                        @if (!string.IsNullOrWhiteSpace(row.TokenCode))
                                        {
                                            <span class="font-monospace">@row.TokenCode</span>
                                            @if (!row.IsUsed)
                                            {
                                                <button type="button" class="btn btn-sm btn-link p-0 ms-2" title="@L["Tokens.Copy"]" @onclick="() => CopyAsync(row.TokenCode!)">
                                                    <i class="@(_copiedCode == row.TokenCode ? "ri-check-line" : "ri-file-copy-line")"></i>
                                                </button>
                                            }
                                        }
                                        else
                                        {
                                            <span class="text-muted">—</span>
                                        }
                                    </td>
                                    <td class="text-body">@row.TokenTypeName</td>
                                    <td>
                                        @if (row.IsUsed)
                                        {
                                            <span class="badge bg-primary bg-opacity-10 text-primary p-2 fs-12 fw-normal">@L["Tokens.StatusUsed"]</span>
                                        }
                                        else if (!string.IsNullOrWhiteSpace(row.TokenCode))
                                        {
                                            <span class="badge bg-success bg-opacity-10 text-success p-2 fs-12 fw-normal">@L["Tokens.StatusAvailable"]</span>
                                        }
                                    </td>
                                    <td class="text-body">@row.UsedByMemberName</td>
                                </tr>
                            }
                            @if (!_items.Any())
                            {
                                <tr><td colspan="6" class="text-center text-muted py-4">@L["Common.NoDataAvailable"]</td></tr>
                            }
                        </tbody>
                    </table>
                </div>
                <div class="p-4 pt-lg-4">
                    <div class="d-flex justify-content-center justify-content-sm-between align-items-center text-center flex-wrap gap-2 showing-wrap">
                        <span class="fs-12 fw-medium">@L["Reports.Showing"] @_items.Count @L["Common.Of"] @_total @L["Reports.Results"]</span>
                        @if (_total > PageSize)
                        {
                            var totalPages = (int)Math.Ceiling((double)_total / PageSize);
                            <nav aria-label="@L["Tokens.PaginationAria"]">
                                <ul class="pagination mb-0 justify-content-center">
                                    <li class="page-item @(_page <= 1 ? "disabled" : "")">
                                        <button type="button" class="page-link icon" @onclick="() => LoadPageAsync(_page - 1)" aria-label="@L["Common.Previous"]">
                                            <i class="material-symbols-outlined">keyboard_arrow_left</i>
                                        </button>
                                    </li>
                                    @for (var pg = 1; pg <= totalPages; pg++)
                                    {
                                        var p = pg;
                                        <li class="page-item @(p == _page ? "active" : "")">
                                            <button type="button" class="page-link" @onclick="() => LoadPageAsync(p)">@p</button>
                                        </li>
                                    }
                                    <li class="page-item @(_page >= totalPages ? "disabled" : "")">
                                        <button type="button" class="page-link icon" @onclick="() => LoadPageAsync(_page + 1)" aria-label="@L["Common.Next"]">
                                            <i class="material-symbols-outlined">keyboard_arrow_right</i>
                                        </button>
                                    </li>
                                </ul>
                            </nav>
                        }
                    </div>
                </div>
            </div>
        }
    </div>
</div>

@if (DistributionEnabled)
{
    <div class="card bg-white border-0 rounded-3 mb-4">
        <div class="card-body p-4">
            <h4 class="fs-18 mb-2">@L["Tokens.Distribution"]</h4>
            <p class="fs-14 mb-3">@L["Tokens.DistributionNote"]</p>

            @if (_distributeErrorKey is not null)
            {
                <div class="alert alert-danger py-2" role="alert" id="tokens-distribute-error">@L[_distributeErrorKey]</div>
            }
            @if (_distributeDone)
            {
                <div class="alert alert-success py-2" role="status">@L["Tokens.DistributeDone"]</div>
            }

            <div class="row g-3 align-items-end">
                <div class="col-md-5">
                    <label class="form-label small" for="tokens-distribute-type">@L["Tokens.TokenType"]</label>
                    <select id="tokens-distribute-type" class="form-select" value="@_distributeTokenTypeId" @onchange="OnDistributeTypeChanged">
                        <option value="">—</option>
                        @foreach (var b in _balances.Where(b => b.TokenTypeId > 0 && b.Balance > 0))
                        {
                            <option value="@b.TokenTypeId">@b.TokenTypeName (@b.Balance)</option>
                        }
                    </select>
                </div>
                <div class="col-md-3">
                    <label class="form-label small" for="tokens-distribute-qty">@L["Tokens.Amount"]</label>
                    <select id="tokens-distribute-qty" class="form-select" value="@_distributeAmount" @onchange="OnDistributeAmountChanged">
                        @for (var n = 1; n <= MaxDistributable; n++)
                        {
                            var v = n;
                            <option value="@v">@v</option>
                        }
                    </select>
                </div>
            </div>

            <div class="position-relative mt-3">
                <label class="form-label small mb-1">@L["Tokens.Member"]</label>
                @if (_selectedMember is null)
                {
                    <div class="input-group">
                        <span class="input-group-text"><i class="ri-search-line"></i></span>
                        <input type="text" class="form-control" placeholder="@L["Tokens.SearchMemberPlaceholder"]"
                               autocomplete="off" value="@_memberSearchInput" @oninput="OnMemberSearchInput" />
                    </div>
                    @if (_memberResults.Any())
                    {
                        <div class="dropdown-menu w-100 shadow mt-1 show" style="max-height:260px; overflow:auto; z-index:1060;">
                            @foreach (var m in _memberResults)
                            {
                                var member = m;
                                <button type="button" class="dropdown-item" @onclick="() => SelectMember(member)">@member.FullName</button>
                            }
                        </div>
                    }
                }
                else
                {
                    <div class="mt-2">
                        <span class="chip">@_selectedMember.FullName</span>
                        <button type="button" class="btn btn-sm btn-link text-secondary ms-1 p-0" @onclick="ClearMember">@L["Tokens.Clear"]</button>
                    </div>
                }
            </div>

            <button id="tokens-distribute" class="btn btn-outline-primary mt-3"
                    disabled="@(_distributeBusy || _distributeTokenTypeId is null || _selectedMember is null)"
                    @onclick="DistributeTokensAsync">
                <i class="ri-token-swap-fill me-1"></i>@L["Tokens.DistributeButton"]
            </button>

            <p class="text-muted small mt-3 mb-0">@L["Tokens.ExternalShareNote"]</p>
        </div>
    </div>
}

<style>
    .chip {
        display: inline-flex; align-items: center; padding: .35rem .55rem; border-radius: 999px;
        background: #f1f5f9; margin: .2rem; font-size: .85rem; font-weight: 600; color: #334155;
    }
    .nav-tabs .nav-link.active { color: #4936F5 !important; }
</style>

@code {
    [Parameter, EditorRequired] public string MemberProfileId { get; set; } = string.Empty;
    [Parameter] public string ApiBaseUrl { get; set; } = "api/v1/bizcenter";

    /// <summary>Lo enciende el anfitrión del miembro. Apagado por defecto: el panel admin no compra.</summary>
    [Parameter] public bool PurchaseEnabled { get; set; }

    /// <summary>Lo enciende el anfitrión del miembro. Apagado por defecto.</summary>
    [Parameter] public bool DistributionEnabled { get; set; }

    private const int PageSize = 20;

    private static readonly (string Method, string Key)[] PaymentTabs =
    [
        (TokenPaymentMethods.CommissionBalance, "Tokens.TabCommissions"),
        (TokenPaymentMethods.EWallet,           "Tokens.TabEWallet"),
        (TokenPaymentMethods.CreditCard,        "Tokens.TabCreditCard"),
        (TokenPaymentMethods.Crypto,            "Tokens.TabCrypto")
    ];

    private StepUpDialog _stepUp = default!;

    private bool _loadFailed;
    private bool _isLoading = true;
    private TokenSummaryDto? _summary;
    private TokenCatalogDto? _catalog;
    private TokenPurchaseFundsDto? _funds;
    private List<CardItem> _cards = new();
    private List<BalanceItem> _balances = new();
    private List<TokenItem> _items = new();
    private int _page = 1, _total;

    private string _payTab = TokenPaymentMethods.CommissionBalance;
    private int? _purchaseTokenTypeId;
    private int _purchaseAmount = 1;
    private string? _selectedCardId;
    private string? _cryptoCurrency;
    private string _idempotencyKey = NewKey();
    private bool _purchaseBusy;
    private string? _purchaseErrorKey;
    private TokenPurchaseResultDto? _purchaseResult;

    private int? _distributeTokenTypeId;
    private int _distributeAmount = 1;
    private string _memberSearchInput = string.Empty;
    private List<MemberResult> _memberResults = new();
    private MemberResult? _selectedMember;
    private bool _distributeBusy;
    private string? _distributeErrorKey;
    private bool _distributeDone;
    private string? _copiedCode;

    private List<BreadcrumbNav.BreadcrumbItem> _breadcrumb => new()
    {
        new(L["Nav.Dashboard"].Value, "/dashboard"),
        new(L["Nav.Tokens"].Value)
    };

    private IEnumerable<TokenCatalogItemDto> PurchasableItems =>
        _catalog?.Items.Where(i => i.Purchasable) ?? Enumerable.Empty<TokenCatalogItemDto>();

    private TokenCatalogItemDto? SelectedItem => PurchasableItems.FirstOrDefault(i => i.TokenTypeId == _purchaseTokenTypeId);

    private List<CryptoDepositAddressDto> CryptoDeposits => _catalog?.CryptoDeposits ?? new();

    private CryptoDepositAddressDto? SelectedDeposit => CryptoDeposits.FirstOrDefault(d => d.Currency == _cryptoCurrency);

    private int MaxPurchasable => Math.Clamp(_catalog?.MonthlyRemaining ?? 0, 1, TokenPurchaseRules.MonthlyLimit);

    private bool CanBuy =>
        !_purchaseBusy
        && SelectedItem is not null
        && (_catalog?.MonthlyRemaining ?? 0) >= _purchaseAmount
        && (_payTab != TokenPaymentMethods.CreditCard || _selectedCardId is not null)
        && (_payTab != TokenPaymentMethods.Crypto || SelectedDeposit is not null);

    private int MaxDistributable =>
        Math.Max(1, _balances.FirstOrDefault(b => b.TokenTypeId == _distributeTokenTypeId)?.Balance ?? 1);

    private static string NewKey() => Guid.NewGuid().ToString("N");

    private RenderFragment StatBox(string icon, string color, string label, int? value) => __builder =>
    {
        <div class="col-xxl-4 col-xl-4 col-sm-4">
            <div class="card bg-@color bg-opacity-10 border-@color border-opacity-10 rounded-3 mb-4 stats-box style-three">
                <div class="card-body p-4 d-flex align-items-center">
                    <i class="material-symbols-outlined fs-40 text-@color flex-shrink-0">@icon</i>
                    <div class="flex-grow-1 ms-2">
                        <span>@label</span>
                        @if (value is null) { <span class="placeholder col-6 d-block mt-1"></span> }
                        else { <h3 class="fs-20 mt-1 mb-0">@value</h3> }
                    </div>
                </div>
            </div>
        </div>
    };

    protected override async Task OnParametersSetAsync()
    {
        if (string.IsNullOrEmpty(MemberProfileId)) return;

        var loads = new List<Task> { LoadSummaryAsync(), LoadPageAsync(1) };
        if (PurchaseEnabled) loads.AddRange([LoadCatalogAsync(), LoadFundsAsync(), LoadCardsAsync()]);
        if (DistributionEnabled) loads.Add(LoadBalancesAsync());
        await Task.WhenAll(loads);
    }

    /// <summary>Una carga que falla deja su dato vacío y ENCIENDE el aviso. Nada se calla.</summary>
    private async Task<T?> GetAsync<T>(string url) where T : class
    {
        try
        {
            var resp = await Http.GetFromJsonAsync<ApiWrapper<T>>(url);
            if (resp?.Success == true && resp.Data is not null) return resp.Data;
        }
        catch (Exception)
        {
            // Cae al aviso de abajo.
        }
        _loadFailed = true;
        return null;
    }

    private async Task LoadSummaryAsync()  => _summary  = await GetAsync<TokenSummaryDto>($"{ApiBaseUrl}/tokens/summary");
    private async Task LoadFundsAsync()    => _funds    = await GetAsync<TokenPurchaseFundsDto>($"{ApiBaseUrl}/wallet/balances");
    private async Task LoadCardsAsync()    => _cards    = await GetAsync<List<CardItem>>($"{ApiBaseUrl}/billing/credit-cards") ?? new();
    private async Task LoadBalancesAsync() => _balances = await GetAsync<List<BalanceItem>>($"{ApiBaseUrl}/tokens/balances") ?? new();

    private async Task LoadCatalogAsync()
    {
        _catalog = await GetAsync<TokenCatalogDto>($"{ApiBaseUrl}/tokens/types");
        if (SelectedDeposit is null) _cryptoCurrency = CryptoDeposits.FirstOrDefault()?.Currency;
    }

    private async Task LoadPageAsync(int page)
    {
        _page = page;
        _isLoading = true;
        var data = await GetAsync<PagedData<TokenItem>>($"{ApiBaseUrl}/tokens?page={page}&pageSize={PageSize}");
        _items = data?.Items ?? new();
        _total = data?.TotalCount ?? 0;
        _isLoading = false;
    }

    // ── Compra ────────────────────────────────────────────────────────────────

    /// <summary>Cambiar lo que se compra es otra compra: clave nueva. Repetir sin tocar nada reusa la clave.</summary>
    private void ResetPurchaseAttempt()
    {
        _idempotencyKey = NewKey();
        _purchaseErrorKey = null;
    }

    private void OnPurchaseTypeChanged(ChangeEventArgs e)
    {
        _purchaseTokenTypeId = int.TryParse(e.Value?.ToString(), out var id) ? id : null;
        ResetPurchaseAttempt();
    }

    private void OnPurchaseAmountChanged(ChangeEventArgs e)
    {
        _purchaseAmount = int.TryParse(e.Value?.ToString(), out var n) ? n : 1;
        ResetPurchaseAttempt();
    }

    private void SelectTab(string method)
    {
        _payTab = method;
        ResetPurchaseAttempt();
    }

    private void SelectCard(CardItem card)
    {
        if (card.IsExpired) return;
        _selectedCardId = card.Id;
        ResetPurchaseAttempt();
    }

    private void OnCryptoChanged(ChangeEventArgs e)
    {
        _cryptoCurrency = e.Value?.ToString();
        ResetPurchaseAttempt();
    }

    private async Task PurchaseTokensAsync()
    {
        if (!CanBuy) return;

        _purchaseBusy = true;
        _purchaseErrorKey = null;
        _purchaseResult = null;
        try
        {
            var request = new PurchaseTokensRequest
            {
                TokenTypeId    = _purchaseTokenTypeId!.Value,
                Quantity       = _purchaseAmount,
                PaymentMethod  = _payTab,
                CreditCardId   = _payTab == TokenPaymentMethods.CreditCard ? _selectedCardId : null,
                CryptoCurrency = _payTab == TokenPaymentMethods.Crypto ? _cryptoCurrency : null,
                IdempotencyKey = _idempotencyKey
            };

            // Pagar con saldo va por la ruta con step-up: si la API pide el código, el diálogo lo pide
            // y la petición se repite UNA vez con el permiso. Tarjeta y cripto van directas.
            var fromBalance = TokenPaymentMethods.IsBalance(_payTab);
            var url = fromBalance ? $"{ApiBaseUrl}/tokens/purchase/balance" : $"{ApiBaseUrl}/tokens/purchase";
            HttpRequestMessage Build() => new(HttpMethod.Post, url) { Content = JsonContent.Create(request) };

            var resp = fromBalance
                ? await StepUpHttp.SendAsync(Http, StepUpOperations.TokenPurchaseFromBalance, Build, _stepUp.RequireAsync)
                : await Http.SendAsync(Build());

            if (resp is null) return; // canceló el código: no se cobró nada y la clave sigue valiendo

            using (resp)
            {
                if (await StepUpHttp.IsGrantRejectedAsync(resp))
                {
                    _purchaseErrorKey = "StepUp.RetryFailed";
                    return;
                }

                var body = await ReadAsync<TokenPurchaseResultDto>(resp);
                if (resp.IsSuccessStatusCode && body?.Success == true && body.Data is not null)
                {
                    _purchaseResult = body.Data;
                    _idempotencyKey = NewKey();
                    await Task.WhenAll(LoadSummaryAsync(), LoadCatalogAsync(), LoadFundsAsync(), LoadPageAsync(1),
                                       DistributionEnabled ? LoadBalancesAsync() : Task.CompletedTask);
                }
                else
                {
                    var code = body?.ErrorCode;
                    _purchaseErrorKey = TokenPurchaseMessages.ErrorKeyFor(code);
                    if (!TokenPurchaseMessages.KeepsKey(code)) _idempotencyKey = NewKey();
                }
            }
        }
        catch (Exception)
        {
            // No se sabe si entró: se conserva la clave para que reintentar no cobre dos veces.
            _purchaseErrorKey = "Tokens.Error.Unavailable";
        }
        finally
        {
            _purchaseBusy = false;
        }
    }

    // ── Distribución ──────────────────────────────────────────────────────────

    private void OnDistributeTypeChanged(ChangeEventArgs e)
    {
        _distributeTokenTypeId = int.TryParse(e.Value?.ToString(), out var id) ? id : null;
        _distributeAmount = 1;
    }

    private void OnDistributeAmountChanged(ChangeEventArgs e) =>
        _distributeAmount = int.TryParse(e.Value?.ToString(), out var qty) ? qty : 1;

    private async Task OnMemberSearchInput(ChangeEventArgs e)
    {
        _memberSearchInput = e.Value?.ToString() ?? string.Empty;
        if (_memberSearchInput.Length < 2) { _memberResults.Clear(); return; }
        try
        {
            var resp = await Http.GetFromJsonAsync<ApiWrapper<PagedData<MemberResult>>>(
                $"{ApiBaseUrl}/team/enrollment/my-team?search={Uri.EscapeDataString(_memberSearchInput)}&pageSize=10");
            _memberResults = resp?.Data?.Items ?? new();
        }
        catch (Exception)
        {
            _memberResults.Clear();
            _distributeErrorKey = "Tokens.Error.LoadFailed";
        }
    }

    private void SelectMember(MemberResult m)
    {
        _selectedMember = m;
        _memberSearchInput = string.Empty;
        _memberResults.Clear();
    }

    private void ClearMember()
    {
        _selectedMember = null;
        _memberSearchInput = string.Empty;
        _memberResults.Clear();
    }

    private async Task DistributeTokensAsync()
    {
        if (_distributeBusy || _distributeTokenTypeId is null || _selectedMember is null) return;

        _distributeBusy = true;
        _distributeErrorKey = null;
        _distributeDone = false;
        try
        {
            var payload = new { TokenTypeId = _distributeTokenTypeId.Value, RecipientMemberId = _selectedMember.MemberId, Quantity = _distributeAmount };
            var resp = await Http.PostAsJsonAsync($"{ApiBaseUrl}/tokens/distribute", payload);
            var body = await ReadAsync<object>(resp);

            if (resp.IsSuccessStatusCode && body?.Success == true)
            {
                _distributeDone = true;
                ClearMember();
                _distributeTokenTypeId = null;
                _distributeAmount = 1;
                await Task.WhenAll(LoadSummaryAsync(), LoadBalancesAsync(), LoadPageAsync(1));
            }
            else
            {
                _distributeErrorKey = TokenPurchaseMessages.ErrorKeyFor(body?.ErrorCode);
            }
        }
        catch (Exception)
        {
            _distributeErrorKey = "Tokens.Error.Generic";
        }
        finally
        {
            _distributeBusy = false;
        }
    }

    private async Task CopyAsync(string code)
    {
        try
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", code);
            _copiedCode = code;
        }
        catch (JSException)
        {
            _copiedCode = null;
        }
    }

    private static async Task<ApiWrapper<T>?> ReadAsync<T>(HttpResponseMessage resp)
    {
        try { return await resp.Content.ReadFromJsonAsync<ApiWrapper<T>>(); }
        catch { return null; }
    }

    // Contratos locales de lo que no está en SharedKernel (historial, tarjetas, saldos, equipo).
    private sealed class ApiWrapper<T> { public bool Success { get; set; } public T? Data { get; set; } public string? ErrorCode { get; set; } }
    private sealed class PagedData<T> { public List<T> Items { get; set; } = new(); public int TotalCount { get; set; } }

    private sealed class CardItem
    {
        public string Id { get; set; } = string.Empty;
        public string Last4 { get; set; } = string.Empty;
        public string CardBrand { get; set; } = string.Empty;
        public int ExpiryMonth { get; set; }
        public int ExpiryYear { get; set; }
        public bool IsExpired { get; set; }
    }

    private sealed class BalanceItem
    {
        public int TokenTypeId { get; set; }
        public string TokenTypeName { get; set; } = string.Empty;
        public int Balance { get; set; }
    }

    private sealed class MemberResult
    {
        public string MemberId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
    }

    private sealed class TokenItem
    {
        public string TokenTypeName { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty;
        public string? TokenCode { get; set; }
        public bool IsUsed { get; set; }
        public string? UsedByMemberName { get; set; }
        public DateTime OccurredAt { get; set; }
    }
}
```

> Se quitan el buscador y las fechas del historial (`GET tokens` no los acepta) y el botón "Añadir tarjeta" que no hacía nada. La clave `Tokens.PoweredByNowPayments` queda sin uso: no hay integración con NowPayments.

- [ ] **Step 5: Encender compra y distribución en los anfitriones del miembro**

En `BizCenterWeb/Components/Pages/Tokens.razor` y `BizCenterApp/Components/Pages/Tokens.razor`, reemplazar `<TokensPage MemberProfileId="@ViewContext.ViewingMemberId" />` por:

```razor
<TokensPage MemberProfileId="@ViewContext.ViewingMemberId"
            PurchaseEnabled="@(!ViewContext.IsImpersonating)"
            DistributionEnabled="@(!ViewContext.IsImpersonating)" />
```

Los dos anfitriones ya registran `IStepUpClient` (BizCenterWeb con `AddStepUpClient`, BizCenterApp en `MauiProgram.cs:63`). AdminWeb y AdminApp no cambian: los parámetros nuevos vienen apagados.

- [ ] **Step 6: Textos en los 9 `.resx`** — agregar estas claves (`<data name="..." xml:space="preserve"><value>...</value></data>`):

| Clave | en | es | fr | de | it | pt | ko | ka | zh |
|---|---|---|---|---|---|---|---|---|---|
| Tokens.MonthlyRemaining | You can buy {0} more tokens this month. | Puedes comprar {0} tokens más este mes. | Vous pouvez encore acheter {0} jetons ce mois-ci. | Sie können diesen Monat noch {0} Token kaufen. | Puoi acquistare ancora {0} token questo mese. | Você ainda pode comprar {0} tokens este mês. | 이번 달에 토큰을 {0}개 더 구매할 수 있습니다. | ამ თვეში კიდევ {0} ტოკენის ყიდვა შეგიძლიათ. | 本月您还可以购买 {0} 个代币。 |
| Tokens.UnitPrice | Price per token: {0} | Precio por token: {0} | Prix par jeton : {0} | Preis pro Token: {0} | Prezzo per token: {0} | Preço por token: {0} | 토큰당 가격: {0} | ერთი ტოკენის ფასი: {0} | 每个代币价格：{0} |
| Tokens.TotalPrice | Total: {0} | Total: {0} | Total : {0} | Gesamt: {0} | Totale: {0} | Total: {0} | 합계: {0} | სულ: {0} | 合计：{0} |
| Tokens.NoPurchasableTypes | No token types are available for purchase right now. | No hay tipos de token disponibles para comprar en este momento. | Aucun type de jeton n'est disponible à l'achat pour le moment. | Derzeit können keine Token-Typen gekauft werden. | Al momento non ci sono tipi di token acquistabili. | No momento não há tipos de token disponíveis para compra. | 현재 구매할 수 있는 토큰 유형이 없습니다. | ამჟამად შესაძენად ხელმისაწვდომი ტოკენის ტიპი არ არის. | 目前没有可购买的代币类型。 |
| Tokens.CryptoCurrency | Currency | Moneda | Devise | Währung | Valuta | Moeda | 통화 | ვალუტა | 币种 |
| Tokens.CryptoNote | Crypto payments are confirmed manually. Your tokens are issued once our team verifies the transfer. | Los pagos en cripto se confirman manualmente. Tus tokens se emiten cuando nuestro equipo verifica la transferencia. | Les paiements en crypto sont confirmés manuellement. Vos jetons sont émis dès que notre équipe a vérifié le transfert. | Krypto-Zahlungen werden manuell bestätigt. Ihre Token werden ausgegeben, sobald unser Team die Überweisung geprüft hat. | I pagamenti in cripto vengono confermati manualmente. I tuoi token vengono emessi quando il nostro team verifica il trasferimento. | Pagamentos em cripto são confirmados manualmente. Seus tokens são emitidos quando nossa equipe verificar a transferência. | 암호화폐 결제는 수동으로 확인됩니다. 팀이 송금을 확인하면 토큰이 발급됩니다. | კრიპტოგადახდები ხელით დასტურდება. ტოკენები გაიცემა, როცა ჩვენი გუნდი გადარიცხვას გადაამოწმებს. | 加密货币付款需人工确认。我们的团队核实转账后即发放您的代币。 |
| Tokens.PurchaseDone | {0} token(s) added to your account. | Se agregaron {0} token(s) a tu cuenta. | {0} jeton(s) ajouté(s) à votre compte. | {0} Token wurden Ihrem Konto gutgeschrieben. | {0} token aggiunti al tuo account. | {0} token(s) adicionado(s) à sua conta. | 계정에 토큰 {0}개가 추가되었습니다. | თქვენს ანგარიშს დაემატა {0} ტოკენი. | 已向您的账户添加 {0} 个代币。 |
| Tokens.PurchaseCryptoPending | Order {0} is waiting for your transfer of {1} in {2}. Your tokens will be issued when we confirm it. | El pedido {0} espera tu transferencia de {1} en {2}. Tus tokens se emitirán cuando la confirmemos. | La commande {0} attend votre transfert de {1} en {2}. Vos jetons seront émis dès que nous l'aurons confirmé. | Bestellung {0} wartet auf Ihre Überweisung von {1} in {2}. Ihre Token werden ausgegeben, sobald wir sie bestätigt haben. | L'ordine {0} attende il tuo trasferimento di {1} in {2}. I tuoi token verranno emessi quando lo confermeremo. | O pedido {0} aguarda sua transferência de {1} em {2}. Seus tokens serão emitidos quando a confirmarmos. | 주문 {0}이(가) {2}(으)로 {1} 송금을 기다리고 있습니다. 확인되면 토큰이 발급됩니다. | შეკვეთა {0} ელოდება თქვენს გადარიცხვას: {1}, ვალუტა {2}. ტოკენები გაიცემა დადასტურების შემდეგ. | 订单 {0} 正在等待您以 {2} 转账 {1}。确认后将发放您的代币。 |
| Tokens.DistributeDone | Tokens sent. | Tokens enviados. | Jetons envoyés. | Token gesendet. | Token inviati. | Tokens enviados. | 토큰을 보냈습니다. | ტოკენები გაიგზავნა. | 代币已发送。 |
| Tokens.ColTransaction | Transaction | Movimiento | Opération | Vorgang | Movimento | Movimento | 거래 | ოპერაცია | 交易 |
| Tokens.StatusUsed | Used | Usado | Utilisé | Verwendet | Usato | Usado | 사용됨 | გამოყენებული | 已使用 |
| Tokens.Copy | Copy | Copiar | Copier | Kopieren | Copia | Copiar | 복사 | კოპირება | 复制 |
| Tokens.ExternalShareNote | To give a token to someone outside your team, copy an available code and share it: they enter it when they sign up. | Para darle un token a alguien fuera de tu equipo, copia un código disponible y compártelo: lo ingresa al inscribirse. | Pour offrir un jeton à une personne extérieure à votre équipe, copiez un code disponible et partagez-le : elle le saisira lors de son inscription. | Um jemandem außerhalb Ihres Teams einen Token zu geben, kopieren Sie einen verfügbaren Code und teilen Sie ihn: Er wird bei der Anmeldung eingegeben. | Per dare un token a qualcuno fuori dal tuo team, copia un codice disponibile e condividilo: lo inserirà al momento dell'iscrizione. | Para dar um token a alguém de fora da sua equipe, copie um código disponível e compartilhe: a pessoa o informa ao se cadastrar. | 팀 외부 사람에게 토큰을 주려면 사용 가능한 코드를 복사해 공유하세요. 가입할 때 입력하면 됩니다. | გუნდის გარეთ ვინმესთვის ტოკენის გადასაცემად დააკოპირეთ ხელმისაწვდომი კოდი და გაუზიარეთ: რეგისტრაციისას შეიყვანს. | 如需将代币送给团队以外的人，请复制一个可用代码并分享给对方：对方注册时输入即可。 |
| Tokens.Error.LoadFailed | We couldn't load this information. Refresh the page to try again. | No pudimos cargar esta información. Recarga la página para intentarlo de nuevo. | Impossible de charger ces informations. Actualisez la page pour réessayer. | Diese Informationen konnten nicht geladen werden. Laden Sie die Seite neu, um es erneut zu versuchen. | Impossibile caricare queste informazioni. Ricarica la pagina per riprovare. | Não foi possível carregar estas informações. Atualize a página para tentar novamente. | 정보를 불러오지 못했습니다. 페이지를 새로고침하여 다시 시도하세요. | ინფორმაციის ჩატვირთვა ვერ მოხერხდა. სცადეთ გვერდის განახლება. | 无法加载此信息。请刷新页面重试。 |
| Tokens.Error.Generic | The operation could not be completed. | No se pudo completar la operación. | L'opération n'a pas pu être effectuée. | Der Vorgang konnte nicht abgeschlossen werden. | Impossibile completare l'operazione. | Não foi possível concluir a operação. | 작업을 완료할 수 없습니다. | ოპერაციის დასრულება ვერ მოხერხდა. | 无法完成该操作。 |
| Tokens.Error.MonthlyLimit | You've reached the limit of 50 tokens per month. | Alcanzaste el límite de 50 tokens por mes. | Vous avez atteint la limite de 50 jetons par mois. | Sie haben das Limit von 50 Token pro Monat erreicht. | Hai raggiunto il limite di 50 token al mese. | Você atingiu o limite de 50 tokens por mês. | 월 50개 토큰 한도에 도달했습니다. | მიაღწიეთ თვეში 50 ტოკენის ლიმიტს. | 您已达到每月 50 个代币的上限。 |
| Tokens.Error.InsufficientCommissions | Your commission balance isn't enough for this purchase. | Tu saldo de comisiones no alcanza para esta compra. | Votre solde de commissions est insuffisant pour cet achat. | Ihr Provisionsguthaben reicht für diesen Kauf nicht aus. | Il tuo saldo commissioni non è sufficiente per questo acquisto. | Seu saldo de comissões não é suficiente para esta compra. | 커미션 잔액이 이 구매에 부족합니다. | საკომისიოს ნაშთი ამ შენაძენისთვის არ კმარა. | 您的佣金余额不足以完成此次购买。 |
| Tokens.Error.InsufficientEWallet | Your eWallet balance isn't enough for this purchase. | Tu saldo de eWallet no alcanza para esta compra. | Le solde de votre eWallet est insuffisant pour cet achat. | Ihr eWallet-Guthaben reicht für diesen Kauf nicht aus. | Il saldo del tuo eWallet non è sufficiente per questo acquisto. | Seu saldo da eWallet não é suficiente para esta compra. | eWallet 잔액이 이 구매에 부족합니다. | eWallet-ის ნაშთი ამ შენაძენისთვის არ კმარა. | 您的 eWallet 余额不足以完成此次购买。 |
| Tokens.Error.NotPurchasable | This token type can't be purchased. | Este tipo de token no se puede comprar. | Ce type de jeton ne peut pas être acheté. | Dieser Token-Typ kann nicht gekauft werden. | Questo tipo di token non può essere acquistato. | Este tipo de token não pode ser comprado. | 이 토큰 유형은 구매할 수 없습니다. | ამ ტიპის ტოკენის ყიდვა შეუძლებელია. | 此代币类型无法购买。 |
| Tokens.Error.Card | That card can't be used. Check it in your billing settings. | Esa tarjeta no se puede usar. Revísala en tu configuración de facturación. | Cette carte ne peut pas être utilisée. Vérifiez-la dans vos paramètres de facturation. | Diese Karte kann nicht verwendet werden. Prüfen Sie sie in Ihren Zahlungseinstellungen. | Questa carta non può essere usata. Controllala nelle impostazioni di fatturazione. | Esse cartão não pode ser usado. Verifique-o nas configurações de cobrança. | 해당 카드는 사용할 수 없습니다. 결제 설정에서 확인하세요. | ამ ბარათის გამოყენება შეუძლებელია. შეამოწმეთ გადახდის პარამეტრებში. | 该卡无法使用。请在账单设置中检查。 |
| Tokens.Error.CardDeclined | The card payment was declined. | El pago con tarjeta fue rechazado. | Le paiement par carte a été refusé. | Die Kartenzahlung wurde abgelehnt. | Il pagamento con carta è stato rifiutato. | O pagamento com cartão foi recusado. | 카드 결제가 거절되었습니다. | ბარათით გადახდა უარყოფილია. | 银行卡付款被拒绝。 |
| Tokens.Error.InProgress | A purchase is already being processed. Wait a moment and try again. | Ya hay una compra en proceso. Espera un momento e inténtalo de nuevo. | Un achat est déjà en cours. Patientez un instant puis réessayez. | Ein Kauf wird bereits verarbeitet. Warten Sie einen Moment und versuchen Sie es erneut. | Un acquisto è già in elaborazione. Attendi un momento e riprova. | Já há uma compra em processamento. Aguarde um momento e tente novamente. | 이미 구매가 처리 중입니다. 잠시 후 다시 시도하세요. | შენაძენი უკვე მუშავდება. მოიცადეთ და სცადეთ თავიდან. | 已有一笔购买正在处理中。请稍候再试。 |
| Tokens.Error.Impersonation | Purchases aren't allowed while viewing an account on someone else's behalf. | No se permiten compras mientras ves una cuenta en nombre de otra persona. | Les achats ne sont pas autorisés lorsque vous consultez un compte au nom d'une autre personne. | Käufe sind nicht erlaubt, während Sie ein Konto im Namen einer anderen Person ansehen. | Gli acquisti non sono consentiti mentre visualizzi un account per conto di un'altra persona. | Compras não são permitidas enquanto você visualiza uma conta em nome de outra pessoa. | 다른 사람을 대신해 계정을 보는 동안에는 구매할 수 없습니다. | სხვისი სახელით ანგარიშის ნახვისას ყიდვა დაუშვებელია. | 代他人查看账户时不允许购买。 |
| Tokens.Error.MemberInactive | Your account must be active to buy tokens. | Tu cuenta debe estar activa para comprar tokens. | Votre compte doit être actif pour acheter des jetons. | Ihr Konto muss aktiv sein, um Token zu kaufen. | Il tuo account deve essere attivo per acquistare token. | Sua conta precisa estar ativa para comprar tokens. | 토큰을 구매하려면 계정이 활성 상태여야 합니다. | ტოკენების საყიდლად ანგარიში აქტიური უნდა იყოს. | 您的账户必须处于激活状态才能购买代币。 |
| Tokens.Error.Unavailable | We couldn't reach the payment service. Try again: you won't be charged twice. | No pudimos contactar el servicio de pagos. Inténtalo de nuevo: no se te cobrará dos veces. | Impossible de joindre le service de paiement. Réessayez : vous ne serez pas débité deux fois. | Der Zahlungsdienst ist nicht erreichbar. Versuchen Sie es erneut – Sie werden nicht doppelt belastet. | Impossibile raggiungere il servizio di pagamento. Riprova: non ti verrà addebitato due volte. | Não foi possível acessar o serviço de pagamento. Tente novamente: você não será cobrado duas vezes. | 결제 서비스에 연결할 수 없습니다. 다시 시도하세요. 중복 청구되지 않습니다. | გადახდის სერვისთან დაკავშირება ვერ მოხერხდა. სცადეთ თავიდან: ორჯერ არ ჩამოგეჭრებათ. | 无法连接支付服务。请重试：不会重复扣款。 |
| Tokens.Error.NotEnoughTokens | You don't have enough available tokens of that type. | No tienes suficientes tokens disponibles de ese tipo. | Vous n'avez pas assez de jetons disponibles de ce type. | Sie haben nicht genügend verfügbare Token dieses Typs. | Non hai abbastanza token disponibili di quel tipo. | Você não tem tokens disponíveis suficientes desse tipo. | 해당 유형의 사용 가능한 토큰이 부족합니다. | ამ ტიპის საკმარისი ხელმისაწვდომი ტოკენი არ გაქვთ. | 您没有足够的该类型可用代币。 |
| Tokens.Error.Recipient | The recipient must be a member of your enrollment team. | El destinatario debe ser miembro de tu equipo de inscripción. | Le destinataire doit faire partie de votre équipe d'inscription. | Der Empfänger muss Mitglied Ihres Enrollment-Teams sein. | Il destinatario deve far parte del tuo team di iscrizione. | O destinatário deve ser membro da sua equipe de inscrição. | 받는 사람은 귀하의 등록 팀 구성원이어야 합니다. | მიმღები უნდა იყოს თქვენი ჩარიცხვის გუნდის წევრი. | 接收人必须是您推荐团队的成员。 |
| Tokens.Error.NoEWallet | You don't have an eWallet account to pay with. | No tienes una cuenta de eWallet con la que pagar. | Vous n'avez pas de compte eWallet pour payer. | Sie haben kein eWallet-Konto zum Bezahlen. | Non hai un account eWallet con cui pagare. | Você não tem uma conta eWallet para pagar. | 결제에 사용할 eWallet 계정이 없습니다. | გადასახდელად eWallet-ის ანგარიში არ გაქვთ. | 您没有可用于付款的 eWallet 账户。 |
| Tokens.Error.EWalletDeclined | The eWallet charge was declined. | El cobro en eWallet fue rechazado. | Le débit eWallet a été refusé. | Die eWallet-Belastung wurde abgelehnt. | L'addebito eWallet è stato rifiutato. | A cobrança na eWallet foi recusada. | eWallet 결제가 거절되었습니다. | eWallet-ით ჩამოჭრა უარყოფილია. | eWallet 扣款被拒绝。 |
| Tokens.Error.NeedsReview | We couldn't confirm the eWallet charge. Our team will review it and your tokens will be issued if it went through. Don't try again. | No pudimos confirmar el cobro en eWallet. Nuestro equipo lo revisará y, si entró, se emitirán tus tokens. No lo intentes de nuevo. | Nous n'avons pas pu confirmer le débit eWallet. Notre équipe va le vérifier et vos jetons seront émis s'il a abouti. Ne réessayez pas. | Wir konnten die eWallet-Belastung nicht bestätigen. Unser Team prüft sie, und Ihre Token werden ausgegeben, falls sie erfolgt ist. Bitte nicht erneut versuchen. | Non siamo riusciti a confermare l'addebito eWallet. Il nostro team lo verificherà e, se è andato a buon fine, i tuoi token verranno emessi. Non riprovare. | Não conseguimos confirmar a cobrança na eWallet. Nossa equipe vai revisá-la e, se tiver sido concluída, seus tokens serão emitidos. Não tente novamente. | eWallet 결제를 확인하지 못했습니다. 팀이 검토하여 결제가 완료되었다면 토큰이 발급됩니다. 다시 시도하지 마세요. | eWallet-ით ჩამოჭრის დადასტურება ვერ მოხერხდა. ჩვენი გუნდი გადაამოწმებს და, თუ შესრულდა, ტოკენები გაიცემა. თავიდან ნუ სცდით. | 我们无法确认 eWallet 扣款。我们的团队将进行核查，如扣款成功将发放您的代币。请勿重试。 |
| Tokens.CryptoAmountDue | Amount due: {0}, payable in {1}. | Importe a pagar: {0}, en {1}. | Montant dû : {0}, payable en {1}. | Fälliger Betrag: {0}, zahlbar in {1}. | Importo dovuto: {0}, pagabile in {1}. | Valor devido: {0}, pagável em {1}. | 결제 금액: {0}, {1}(으)로 지불 | გადასახდელი თანხა: {0}, ვალუტა {1}. | 应付金额：{0}，以 {1} 支付。 |
| Tokens.CryptoAddress | Deposit address | Dirección de depósito | Adresse de dépôt | Einzahlungsadresse | Indirizzo di deposito | Endereço de depósito | 입금 주소 | დეპოზიტის მისამართი | 充值地址 |
| Tokens.CryptoNetwork | Network: {0} | Red: {0} | Réseau : {0} | Netzwerk: {0} | Rete: {0} | Rede: {0} | 네트워크: {0} | ქსელი: {0} | 网络：{0} |
| Tokens.CryptoUnavailable | Crypto payments aren't available right now. | Los pagos en cripto no están disponibles en este momento. | Les paiements en crypto ne sont pas disponibles pour le moment. | Krypto-Zahlungen sind derzeit nicht verfügbar. | I pagamenti in cripto non sono disponibili al momento. | Pagamentos em cripto não estão disponíveis no momento. | 현재 암호화폐 결제를 사용할 수 없습니다. | კრიპტოგადახდები ამჟამად მიუწვდომელია. | 目前无法使用加密货币付款。 |
| StepUp.WhyTokenPurchase | For your security, enter a verification code before paying for tokens with your balance. | Por seguridad, ingresa un código de verificación antes de pagar tokens con tu saldo. | Pour votre sécurité, saisissez un code de vérification avant de payer des jetons avec votre solde. | Geben Sie zu Ihrer Sicherheit einen Bestätigungscode ein, bevor Sie Token mit Ihrem Guthaben bezahlen. | Per sicurezza, inserisci un codice di verifica prima di pagare i token con il tuo saldo. | Por segurança, digite um código de verificação antes de pagar tokens com seu saldo. | 보안을 위해 잔액으로 토큰을 결제하기 전에 인증 코드를 입력하세요. | უსაფრთხოებისთვის, ნაშთით ტოკენების გადახდამდე შეიყვანეთ დამადასტურებელი კოდი. | 为了您的安全，使用余额购买代币前请输入验证码。 |

Y en la clave existente `Tokens.PurchaseNote` de los 9 archivos, cambiar el límite de 10 a 50 (el número aparece una sola vez en cada valor):

Run: `for f in MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources*.resx; do sed -i '/name="Tokens.PurchaseNote"/s/10/50/' "$f"; done`

Verificar:
Run: `for f in MLMConquerorGlobalEdition.SharedComponents/Resources/SharedResources*.resx; do echo "$f $(grep -c 'name="Tokens\.' $f) $(grep 'name="Tokens.PurchaseNote"' $f | grep -c 50) $(grep -c 'name="StepUp.WhyTokenPurchase"' $f)"; done`
Expected: `69 1 1` en cada línea (35 claves `Tokens.*` de antes + 34 nuevas; la nota con 50; el texto del diálogo).

- [ ] **Step 7: Correr las pruebas y compilar la solución**

Run: `dotnet test MLMConquerorGlobalEdition.BizCenter.Tests -nologo` → PASS (incluye `StepUpDialogTests` y `StepUpEnComponentesTests`, que siguen viendo `StepUp.Why` para el cambio de cobro).
Run: `dotnet build MLMConquerorGlobalEdition.slnx -v q -nologo` → `0 Error(s)`.

- [ ] **Step 8: Commit**

```bash
git add MLMConquerorGlobalEdition.SharedComponents MLMConquerorGlobalEdition.BizCenterWeb MLMConquerorGlobalEdition.BizCenterApp MLMConquerorGlobalEdition.BizCenter.Tests
git commit -m "La pantalla de tokens habla el contrato real: catalogo con precio, cupo y monedas cripto, step-up al pagar con saldo, direccion de deposito e importe en cripto, errores traducidos en vez de callados, clave de idempotencia que se conserva cuando no se sabe si entro, distribucion con los ids correctos y copiar codigo para dar un token fuera del equipo"
```

---

### Task 16: Verificación en la app levantada y documentación

**Files:**
- Modify: `docs/deployment/pruebas-de-seguridad.md` (sección nueva al final)
- Modify: `MWR Life → MLMConqueror SOW BizCenter y matriz de paridad.md` (filas 64-66, resumen de la línea 7 y la fila "Alta | Compra de tokens" de brechas)

- [ ] **Step 1: Levantar con el código nuevo** Billing (https://localhost:7007, aplica `CompraDeTokens`, `SiembraPlantillaReciboCompraTokens` y `AmpliaCreadoPorDeCompraDeTokens` al arrancar), BizCenter API (7003), BizCenterWeb (7004), SignupAPI (7005, login y step-up), Signups (alta), AdminAPI y AdminWeb. MailHog en http://localhost:8025 recibe los códigos de 2FA, los de step-up y los recibos (proveedor de correo de desarrollo, ver `docs/deployment/correo-en-desarrollo.md`).

- [ ] **Step 2: Comprobar la regla de tipos vendibles** (SSMS):

```sql
SELECT Id, Name, IsPurchasable FROM TokenTypes WHERE IsActive = 1 ORDER BY IsPurchasable, Name;
```
Expected: las variantes `FREE`, `(Coupon)`, `SpecialPromo` y `(Help a Friend)` y los `Upgrade:` con `IsPurchasable = 0`; el resto con `1`. **Repetir esta consulta en producción después de aplicar la migración (R11)**: la regla inicial va por nombre y un tipo promocional con otro nombre quedaría a la venta; corregirlo en *Token Types*.

- [ ] **Step 3: Configurar las direcciones** — AdminWeb → Settings → Global Parameters: crear `Crypto.DepositAddress.BTC` = `bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh` y `Crypto.DepositAddress.USDT` = `TQ5NkEQwK6x9C2iGBDbTj9JxF8tqmL7Ak5` con `Crypto.DepositNetwork.USDT` = `TRC-20` (direcciones de prueba). Intentar guardar `Crypto.DepositAddress.BTC` = `hola` → error de validación. El log de AdminAPI muestra el aviso del cambio de dirección.

- [ ] **Step 4: Preparar un miembro de prueba con saldo** (`@m` = MemberId de un Ambassador activo con login):

```sql
DECLARE @m nvarchar(36) = N'AMB-XXXXXX';
INSERT INTO CommissionEarnings (Id, BeneficiaryMemberId, CommissionTypeId, Amount, Status, EarnedDate, PaymentDate,
                                IsManualEntry, Notes, CreatedBy, CreationDate, LastUpdateDate, IsDeleted)
VALUES (NEWID(), @m, 1, 500, 1, DATEADD(day,-10,GETDATE()), DATEADD(day,-1,GETDATE()),
        1, N'Plan F prueba', N'qa', GETDATE(), GETDATE(), 0);
```

(`Status = 1` es `Pending`; `PaymentDate` en el pasado para que cuente como gastable.)

- [ ] **Step 5: API con `curl`** (token de acceso del miembro en `$T`, tras iniciar sesión en https://localhost:7004 con el código de MailHog):

```bash
curl -sk https://localhost:7003/api/v1/bizcenter/tokens/types -H "Authorization: Bearer $T"
```
Expected: `monthlyRemaining: 50`, tipos vendibles con `unitPrice` = suma de sus productos (p. ej. "Enrollment: Ambassador + VIP" = Ambassador + VIP), los FREE con `TOKEN_TYPE_NOT_FOR_SALE`, y `cryptoDeposits` con BTC y USDT.

```bash
curl -sk -X POST https://localhost:7003/api/v1/bizcenter/tokens/purchase -H "Authorization: Bearer $T" -H "Content-Type: application/json" \
  -d '{"tokenTypeId":13,"quantity":1,"paymentMethod":"CommissionBalance","idempotencyKey":"qa-f-0"}' -w "\n%{http_code}\n"
curl -sk -X POST https://localhost:7003/api/v1/bizcenter/tokens/purchase/balance -H "Authorization: Bearer $T" -H "Content-Type: application/json" \
  -d '{"tokenTypeId":13,"quantity":1,"paymentMethod":"CommissionBalance","idempotencyKey":"qa-f-0"}' -w "\n%{http_code}\n"
curl -sk -X POST https://localhost:7007/api/v1/billing/tokens/purchase -H "Authorization: Bearer $T" -H "Content-Type: application/json" \
  -d '{"tokenTypeId":13,"quantity":1,"paymentMethod":"EWallet","idempotencyKey":"qa-f-0b"}' -w "\n%{http_code}\n"
```
Expected: `400 PAYMENT_METHOD_WRONG_ENDPOINT`; `403 STEP_UP_REQUIRED` (sin permiso); y directo a Billing también `403 STEP_UP_REQUIRED`. Nada cobrado (`SELECT COUNT(*) FROM TokenPurchases` = 0).

- [ ] **Step 6: El tope aguanta compras simultáneas** (contra SQL Server, donde vive el candado; cripto no pide step-up):

```bash
for k in qa-conc-a qa-conc-b; do
  curl -sk -X POST https://localhost:7003/api/v1/bizcenter/tokens/purchase -H "Authorization: Bearer $T" -H "Content-Type: application/json" \
    -d "{\"tokenTypeId\":13,\"quantity\":30,\"paymentMethod\":\"Crypto\",\"cryptoCurrency\":\"USDT\",\"idempotencyKey\":\"$k\"}" -w " %{http_code}\n" &
done; wait
```
Expected: una `200` (`AwaitingCryptoConfirmation`, con la dirección USDT y la red TRC-20) y una `400 TOKEN_MONTHLY_LIMIT_REACHED`. Si las dos dan 200, el candado no funciona: revisar el registro de `SqlServerMemberFundsGate` y que `SqlQuery` no se compuso. Repetir una con la misma clave devuelve la misma compra.

- [ ] **Step 7: Recorrido en el navegador** (https://localhost:7004/tokens):
  1. La nota dice "50 por mes"; el cupo restante coincide con la API (20 tras el paso 6).
  2. VIP, 1, pestaña Comisiones, Comprar → se abre el diálogo con el texto de compra con saldo ("antes de pagar tokens con tu saldo"); el código llega a MailHog; al confirmarlo aparece el código del token y el saldo baja. En MailHog llega el **recibo** (`Recibo de tu compra de tokens — pedido …`) **sin** el código del token.
  3. Cancelar el diálogo no cobra nada; repetir sin tocar nada reusa la clave.
  4. Pestaña eWallet: si el miembro no tiene wallet eWallet, "No tienes una cuenta de eWallet"; con wallet y sandbox de i-Payout, el saldo real (o "—" si i-Payout no responde: en desarrollo los identificadores son correos sintéticos, R3). Para probar la conciliación sin i-Payout: apuntar el `BaseUrl` de la credencial `EWallet` (Sandbox) a una URL que no responda y comprar → "No pudimos confirmar el cobro…"; la compra queda `NeedsReconciliation` y aparece en AdminWeb → Billing → Token Purchases → *To review*. Marcarla *Not charged* libera el cupo; repetir con otra y marcarla *Charged* emite los tokens y manda el recibo.
  5. Pestaña Tarjeta: con `Billing:TokenPurchase:CardEnabled = false` (lo configurado) **no aparece**, y un `POST` directo con `"paymentMethod":"CreditCard"` da `400 CARD_PAYMENTS_DISABLED` sin crear pedido. Solo en desarrollo y encendiéndola: con tarjeta guardada la compra queda "en revisión" (`NeedsReconciliation`, `CARD_SIMULATED_CHARGE`, sin tokens) porque el gateway es un stub, o muestra "El pago con tarjeta fue rechazado" si faltan credenciales de Spreedly. No pide código. Volver a apagarla.
  6. Pestaña Cripto: muestra la dirección, la red y el importe; una moneda sin dirección no aparece; sin ninguna, "Los pagos en cripto no están disponibles". Tras pedir, el mensaje repite dirección e importe. En AdminWeb → Token Purchases (no en Crypto Signups) aparece la compra; confirmarla emite los tokens y llega el recibo; volver a confirmarla por API da `409`.
  7. Apagar Billing y comprar: "No pudimos contactar el servicio de pagos…". Encenderlo y pulsar de nuevo sin tocar nada: entra una sola vez.
  8. Distribución a un miembro de la línea: los mismos códigos cambian de dueño (`PreviousOwnerMemberId` = quien envió). Fuera del equipo (por API) → `RECIPIENT_NOT_IN_DOWNLINE`. "Copiar" deja el código en el portapapeles.
  9. Suplantar al miembro desde el panel (solo lectura y completa): la compra no aparece; un `POST` directo da `403`.
  10. Alta en http://localhost:7002 (ventana privada: abrir el alta cierra la sesión): la pestaña Crypto muestra la dirección configurada y deja elegir BTC o USDT; sin direcciones, no deja continuar por cripto.
  11. AdminWeb → Tokens → Token Types: desmarcar "For sale" en un tipo vendible lo quita del catálogo del BizCenter al recargar.

- [ ] **Step 8: Actualizar los documentos**
  - `pruebas-de-seguridad.md`: completar la sección "9. Compra de tokens" (sus condiciones de despliegue ya están) con los pasos 5, 6, 7.3, 7.4, 7.5 y 7.9 como pruebas (propiedad por token, step-up en las dos capas, idempotencia, tope bajo concurrencia, conciliación, tarjeta apagada, suplantación), y una nota sobre R8 (cambio de direcciones sin segundo factor).
  - Matriz de paridad:
    - Fila 64 → `Tokens | Compra con comisiones, eWallet, tarjeta o crypto | tokens/purchase y tokens/purchase/balance (step-up) en Billing vía BizCenter; eWallet con saldo real de i-Payout y conciliación manual de lo dudoso; cripto con dirección configurable y aprobación en Admin; recibo por correo | Cubierto`
    - Fila 65 → `Tokens | Límite de 50 tokens por mes, no reembolsables | Tope por mes calendario con candado por miembro; sin ruta de devolución | Cubierto`
    - Fila 66 → `Tokens | Distribución a equipo y a cliente externo | distribute mueve los códigos dentro del equipo de inscripción; a externos se comparte el código (copiar) | Cubierto`
    - Resumen (línea 7): 33 cubiertas, 8 parciales, 15 faltan.
    - Brechas: `| ~~Alta~~ Hecho | Compra de tokens | Hecho en el Plan F | BizCenter + Billing |`

- [ ] **Step 9: Bloqueantes de producción (no se despliega a producción sin esto)**
  - **Tarjeta (R13):** `Billing:TokenPurchase:CardEnabled` queda en `false` en producción mientras `SpreedlyCardGatewayService` sea un stub. Encenderla exige el cobro real con Spreedly (petición con `SpreedlyPaymentMethodToken` y `DownstreamProcessor`), sus rechazos explícitos añadidos a `CardChargeOutcomes.ExplicitNoChargeCodes` (R5b) y una compra de prueba real en sandbox que emita y deje un `PaymentHistory` con id de transacción no simulado.
  - **Barrido de Pending (R5):** comprobar en `/hangfire` que existe `token-purchase-stale-pending` y que `Billing:TokenPurchase:StalePendingMinutes` no es `0`.
  - Los dos están en `docs/deployment/pruebas-de-seguridad.md`, sección "9. Compra de tokens", **Condiciones de despliegue**.

- [ ] **Step 10: Commit**

```bash
git add docs "MWR Life → MLMConqueror SOW BizCenter y matriz de paridad.md"
git commit -m "Documenta la compra de tokens, sus pruebas de seguridad y la marca cubierta en la matriz de paridad"
```

---

## Preguntas abiertas para el dueño

1. **Tipo de cambio cripto.** La pantalla muestra el importe en USD ("a pagar en USDT"). Para BTC/ETH el miembro tiene que calcular el equivalente y el admin cotejarlo. ¿Se quiere un importe en la moneda (con cotización y tolerancia) o basta así?
2. **Segundo factor para cambiar direcciones de depósito (R8).** ¿Se exige step-up en el admin (cuando exista) o al menos una alerta por correo a los SuperAdmin cuando cambie una dirección?
3. **Reconciliación automática.** Hoy la conciliación es manual. Si i-Payout tiene (o tendrá) una consulta por `MerchantReferenceID`, un trabajo podría cerrar solas las compras dudosas y las que quedaron `Pending` tras una caída (R5).
4. **`MemberJoin.razor`** tiene el mismo texto falso de cripto que tenía el alta; ¿se arregla igual en este plan o aparte?

## Decisiones de cierre (2026-10-15)

Respuestas a las preguntas abiertas de la revisión del plan, tomadas para no frenar la ejecución:

1. **Monto cripto en USD.** No hay integración de tipo de cambio; la pantalla muestra el monto en USD y el admin confirma lo recibido.
2. **Cambiar una dirección de depósito** queda auditado y avisa por correo a los SuperAdmin (se agrega a la Tarea 3). El step-up para admins llega con la ronda del Admin.
3. **Conciliación del eWallet: manual.** i-Payout no ofrece consulta por referencia.
4. **`MemberJoin.razor`** se corrige en este plan, en la Tarea 11, igual que `Signup.razor`.


## Correcciones tras la revisión de las Tareas 1–7 (2026-10-15)

Las Tareas 1–7 ya están en la rama; el código de sus bloques de arriba es el de la primera versión. Donde difiera, **manda el repositorio**. Lo que cambió y lo que las tareas siguientes deben tener en cuenta:

| # | Cambio | Dónde | Efecto en las tareas pendientes |
|---|---|---|---|
| I1 | El cupo imputa lo emitido al `PeriodKey` de su compra (join `TokenTransaction.TokenPurchaseId` → `TokenPurchase`); lo `Purchased` sin compra sigue por `CreationDate`. Confirmar o conciliar no vuelve a mirar el tope. | `TokenMonthlyQuota` | Tarea 9: `ConfirmCryptoAsync`/`ReconcileAsync` no comprueban el cupo (documentado); prueba de septiembre/octubre. |
| I2 | Estados `Cancelled = 6`, `Rejected = 7`, `Expired = 8`; `TokenPurchase.CancelByMember`, `Reject`, `Expire`, `IsCryptoExpired`; `CryptoPaymentConfirmationStatus.Cancelled = 3`; `TokenPurchaseStatuses.*`, `TokenPurchaseErrors.NotAwaitingCrypto`, `RejectCryptoTokenPurchaseRequest`, `TokenPurchaseRules.CryptoExpiryDaysConfigKey` (3 días por defecto). Enums `int`: sin migración. | Dominio y SharedKernel | Tareas 9, 10, 12, 13 y 15 (D18): cancelar, rechazar, caducar. |
| I3 | El candado es de **fondos del miembro** y compartido: `IMemberFundsGate` / `SqlServerMemberFundsGate` / `MemberFundsBusyException` en `Billing/Services/MemberFundsGate.cs` (namespace `MLMConquerorGlobalEdition.Billing.Services`), recurso `member-funds:{memberId}`. Lo toman `PayoutOrchestrator` (solo lectura + reserva de comisiones; ocupado → `MEMBER_FUNDS_BUSY` sin escribir nada) y `PayoutBatchExportService` (`TryAcquireAsync` por miembro en orden ordinal, salta al ocupado y relee bajo candado). Registrado en `AddPayoutServices`. | Billing | Tareas 9 y 10 usan los nombres nuevos; Program.cs ya no registra el candado. |
| I4 | `TokenCodeGenerator` usa `RandomNumberGenerator.GetInt32`; mismo formato (3 letras + 4 dígitos, mezclados) y ya salen la Z y el 9. | SharedKernel | — |
| I5 | `EnterAsync` deshace y suelta la transacción con `CancellationToken.None` si la consulta del candado falla o se agota; tiempo de espera inyectable (`lockTimeoutMs`). El `HttpClient` "ewallet" mantiene 30 s explícitos, comentados. | Billing, Repository | — |
| I6 | El CRUD de parámetros globales solo admite `Crypto.*` (sin distinguir mayúsculas); otras claves → 400 `GLOBAL_PARAMETER_NOT_MANAGED_HERE`. La clave se guarda canónica (`Crypto.DepositAddress.BTC`). Cambiar `Crypto.DepositNetwork.*` también avisa (moneda "USDT network"); crear una clave vacía no avisa. | AdminAPI, AdminWeb | Tarea 16, paso 3: solo claves `Crypto.*`. |
| I7 | Pruebas de SQL Server con `[SqlServerFact]` propio: se marcan *skipped* si no hay servidor. `Xunit.SkippableFact` es MS-PL (no MIT), por eso no se añadió. | Billing.Tests | — |
| M1 | Precio = suma de `SetupFee + MonthlyFee` de los otorgados, **sin** `× QuantityGranted`. | `TokenPricingService` | Decisión 3 y D2 actualizadas. |
| M2 | El débito eWallet se redondea a centavos (`EWalletFunds.ChargeAmount`, AwayFromZero) y ese mismo importe se usa al verificar el saldo. | `EWalletFunds` | Tarea 9: comparar saldos y registrar el `PaymentHistory` con `ChargeAmount(d.Total)`. |
| M4–M6 | `DeductAsync` trata `OperationCanceledException` como "no se sabe"; el usuario de i-Payout va enmascarado en los registros; a las notas solo llega un resumen corto, sin cuerpos crudos. `TokenPurchase` recorta `Notes` (500) y `FailureCode` (64). | Repository, Billing, Dominio | `Truncate` de la Tarea 9 queda como redundante, no estorba. |
| M8 | `UserNameOfAsync` solo acepta wallets eWallet **aprobadas** y no borradas. | `EWalletFunds` | — |
| M9 | `TokenPurchases.CreatedBy` pasa a 256 (migración `AmpliaCreadoPorDeCompraDeTokens`). | Repository | La aplica Billing al arrancar. |
| M10–M11 | `CompraDeTokens` documenta los `UpdateData` vacíos que se quitaron; revisar `IsPurchasable` en producción (R11, Tarea 16 paso 2). | Migraciones, plan | — |


## Correcciones tras la revisión de la Tarea 9 (2026-10-15)

La Tarea 9 (servicio de compra) y la 10 (endpoints) ya están en la rama; donde el código de arriba difiera, **manda el repositorio**.

| # | Cambio | Dónde | Efecto en las tareas pendientes |
|---|---|---|---|
| I1 | **Tarjeta apagada por defecto** (`Billing:TokenPurchase:CardEnabled`, `TokenPurchaseRules.CardEnabledConfigKey`): apagada, `CARD_PAYMENTS_DISABLED` antes de reservar. `TokenCatalogDto.CardEnabled` y `TokenPurchaseFundsDto.CardEnabled`. Un id de transacción `simulated…`/`STUB-…` nunca emite: `NeedsReconciliation` con `CARD_SIMULATED_CHARGE`. Bloqueante de producción (R13). | Billing, SharedKernel, `appsettings.json` | Tarea 13 reenvía el flag y el código; Tarea 15 oculta la pestaña; Tarea 16, pasos 7.5 y 9. |
| I2 | Solo un fallo de la lista `CardChargeOutcomes.ExplicitNoChargeCodes` libera el cupo; cualquier otro, conciliación (`CARD_OUTCOME_UNKNOWN`). El orquestador se llama **un paso cada vez** y solo se sigue al siguiente tras un "no cobrado" explícito. | `TokenPurchaseService`, `CardChargeOutcomes` | R5b documenta el contrato del orquestador (cobro doble en los otros caminos). |
| I3 | Ninguna compra queda en `Pending`: antes del cobro se abandona (`PAYMENT_SERVICE_UNAVAILABLE`, `EWALLET_UNAVAILABLE`, `NO_IMMEDIATE_GATEWAY`); después, se relee y pasa a conciliar (`EWALLET_CHARGED_NOT_ISSUED`, `EWALLET_REJECTED_NOT_SAVED`, `CARD_CHARGED_NOT_ISSUED`); si ni eso, `Critical` con el id. Trabajo `TokenPurchaseStalePendingJob` cada hora (`token-purchase-stale-pending`, a la media) con `Billing:TokenPurchase:StalePendingMinutes` (30): `Pending` de tarjeta o eWallet → `NeedsReconciliation` (`STALE_PENDING`), nunca liberar. | Billing | Tarea 12: las compras `STALE_PENDING` salen en *To review* como las demás; la ayuda dice "mirar el gateway antes de cerrar". |
| I4 | La fase 3 de la tarjeta (emitir + `TokenBalance`) va bajo el candado del miembro con `CancellationToken.None`; ocupado → a conciliar (`CARD_CHARGED_GATE_BUSY`), nunca falla. | `TokenPurchaseService` | R7 corregido. |
| I5 | El miembro nunca recibe texto de i-Payout: mensajes fijos por código; el detalle va al log con el usuario enmascarado y a las notas de la compra. `GetFundsAsync` con i-Payout caído devuelve `EWalletBalance = null`, no un 500. | `TokenPurchaseService` | Tarea 15 ya muestra "—" con `null`. |
| M1 | La conciliación "cobrada" escribe el `PaymentHistory` como los caminos normales (importe a centavos; `eWallet`, o el procesador si lo escribió el orquestador) y, si el pedido ya tiene uno, lo enlaza en vez de duplicarlo. "No cobrada" deja ese `PaymentHistory` en `Failed`. | `ReconcileAsync` | — |
| M2 | Misma clave con otro tipo, cantidad o método → `409 IDEMPOTENCY_KEY_REUSED`. | Servicio y controlador | Tarea 13 reenvía el 409; Tarea 15 genera clave nueva. |
| M3 | Un choque con el índice único `(MemberId, IdempotencyKey)` al reservar devuelve la compra ganadora; un choque de `ReferenceId` al confirmar o conciliar se repite una vez con códigos nuevos y, si vuelve, `409 TOKEN_ISSUE_FAILED` sin guardar nada. | `TokenPurchaseService`, controlador | Tarea 12: mostrar `TOKEN_ISSUE_FAILED` como "inténtalo de nuevo". |
| M5 | Holgura del débito eWallet = `min(0,01; cobro / 2)` (`EWalletFunds.ToleranceFor`). | `EWalletFunds` | — |
| M8 | Sin `CS8602` en la lectura del resultado del cobro. | `TokenPurchaseService` | — |
| Pruebas | `SqlServerTokenPurchaseTests` (`[SqlServerFact]`): dos confirmaciones simultáneas de la misma cripto emiten una vez (RowVersion) y dos compras simultáneas con la misma clave cobran y emiten una vez (candado real). Miembro sintético `TST-…` que se borra al terminar. | Billing.Tests | — |


## Correcciones tras la revisión de la Tarea 13 (2026-10-15)

La Tarea 13 (cliente de Billing y endpoints del BizCenter) ya está en la rama; donde el código de arriba difiera, **manda el repositorio**.

| # | Cambio | Dónde | Efecto en las tareas pendientes |
|---|---|---|---|
| I1 | El cliente lee el sobre como `ApiResponse<JsonElement>` y solo convierte `data` a `T` si es un éxito: un 403 con `"data":false` llegaba como 502. El `Forbidden` de Billing pasa a `ApiResponse<object>` (`"data":null`). Pruebas con los cuerpos reales de `STEP_UP_REQUIRED`, `NOT_A_MEMBER` e `IMPERSONATION`. | `BillingClient`, `TokenPurchasesController` (Billing) | Tarea 15 recibe el 403 con su código, no un 502. |
| I2 | **Billing también quema el permiso de step-up**, con su propia marca (`CacheKeys.BillingStepUpGrantConsumed(jti)` = `stepup:grant:{jti}:consumed:billing`, distinta de la del BizCenter) y `ICacheService.TryClaimOnceAsync`. Orden: validar → si la clave de idempotencia ya tiene compra del miembro (`ITokenPurchaseService.HasPurchaseAsync`), no quemar y devolver la repetición → si no, quemar **antes** de cobrar. Ya usado o caché caída: `403 STEP_UP_INVALID` (falla cerrado). `IStepUpGrantCheck` pasa a `ValidJti` + `TryConsumeAsync`. Billing registra `IConnectionMultiplexer` para que la marca sea atómica entre instancias. | Billing | La repetición con la misma clave pasa aunque el permiso ya esté quemado; el mismo permiso con otra clave se rechaza. |
| M1 | Un 4xx de Billing **sin** `errorCode` (ProblemDetails, 404 de ruta, 405, 415, cuerpo ilegible) → `502 BILLING_CONTRACT_ERROR` con `LogError`. Billing no ejecutó nada: no es "reintenta con la misma clave". | `BillingClient`, `TokenPurchaseErrors.BillingContractError` | Tarea 15: ver nota. |
| M2 | Un 401 de Billing → `502 BILLING_AUTH_MISMATCH` con `LogError` y mensaje propio (sigue sin sacar al miembro de la sesión). | `BillingClient`, `TokenPurchaseErrors.BillingAuthMismatch` | Tarea 15: ver nota. |
| M3 | Comentario de `ImpersonationForbidden` corregido: en la ruta de saldo el filtro ya quemó el permiso del BizCenter. | `TokensController` | — |
| M4 | El cliente "billing" se registra en `BillingClientRegistration.AddBillingClient`: fuera de Development sin `Services:BillingBaseUrl` el arranque lanza `InvalidOperationException`; la `BaseAddress` siempre acaba en `/`; 90 s; sin tocar la validación del certificado. `appsettings.json` sigue declarando `https://localhost:7007/` (misma convención que `Services:Billing` en AdminAPI): en producción hay que sobrescribirla. | BizCenter | Tarea 16: comprobar `Services:BillingBaseUrl` en cada entorno. |
| M5 | Cancelar: `purchases/{purchaseId:guid}/cancel` (el id de `TokenPurchase` es un Guid en texto); otra cosa es 404 sin llamar a Billing. | `TokensController` | Tarea 15 manda el `PurchaseId` tal cual lo devolvió Billing. |
| M6 | El resumen hace **una** consulta (`GroupBy` constante con tres `COUNT(CASE …)`); prueba de que traduce a un solo `SELECT` en SQL Server y de que los números no cambian. | `GetTokenSummaryHandler.CountsQuery` | — |
| Pruebas | 401 → 502 (no 401); 4xx sin código; cuerpo real del 403 (`data:false` y `data:null`); configuración del cliente "billing"; permiso de Billing: quemar una vez, marca distinta de la del BizCenter, caché caída, repetición con permiso quemado, permiso reutilizado con clave nueva. | BizCenter.Tests, Billing.Tests | — |

**Nota para la Tarea 15 (reintentos en la pantalla):**

- Tras un `502 BILLING_UNAVAILABLE` en la **ruta de saldo**, el reintento necesita un **permiso de step-up nuevo**: el anterior ya lo quemó el filtro del BizCenter (y quizá Billing). La pantalla vuelve a pedir el código y reenvía **el mismo cuerpo con la misma `IdempotencyKey`**; si la compra llegó a hacerse, Billing devuelve la repetición sin quemar el permiso nuevo.
- Con `BILLING_CONTRACT_ERROR` o `BILLING_AUTH_MISMATCH` **no** se conserva la clave ni se reintenta sola: son fallos de configuración entre servicios. No van en `TokenPurchaseMessages.KeepsKey`; se muestran con un texto de "contacta con soporte" (o `Tokens.Error.Generic`).
- `STEP_UP_INVALID` en la ruta de saldo (permiso ya usado o caché caída) se trata como `STEP_UP_REQUIRED`: pedir un código nuevo y repetir con la misma clave.


## Correcciones al ejecutar la Tarea 14 (2026-10-15)

La Tarea 14 ya está en la rama; donde el código de arriba difiera, **manda el repositorio**.

| # | Cambio | Dónde | Efecto en las tareas pendientes |
|---|---|---|---|
| C1 | El candado de fondos (`IMemberFundsGate`, `SqlServerMemberFundsGate`, `MemberFundsBusyException`, `MemberFundsLock`) pasa de Billing a `Repository/Services/Funds` (namespace `MLMConquerorGlobalEdition.Repository.Services.Funds`), sin cambiar su comportamiento. El BizCenter lo registra. | Repository, Billing, BizCenter | — |
| C2 | Distribuir y transferir eligen y mueven los códigos **bajo el candado `member-funds:{id}` de los dos miembros**, tomados en orden ordinal y leyendo después de tomarlo: dos distribuciones a la vez no mueven el mismo código ni dejan saldo negativo, y no se cruzan con la emisión de una compra (que sube `TokenBalance` bajo el mismo candado). Ocupado → `PURCHASE_BUSY` (`TokenPurchaseErrors.Busy`) sin escribir nada. Sin migración: `TokenTransaction` no tiene RowVersion. | `DistributeTokenHandler`, `TransferTokenInstanceHandler`, `Features/Tokens/TokenCustody.cs` | Tarea 15 ya traduce `Busy` a "en curso". |
| C3 | Transferir también usa hora de servidor (`Now`), rechaza un código vencido (`TOKEN_NOT_TRANSFERABLE`) y usa el id del destinatario tal como está guardado. `TokenTransaction` no tiene `LastUpdateDate/LastUpdateBy` (el bloque de arriba los asignaba). | `TransferTokenInstanceHandler` | — |
| C4 | Ni distribuir ni transferir bajo suplantación: `403` con `data:null` y código `IMPERSONATION_CANNOT_DISTRIBUTE` (`TokenDistributionErrors.Impersonation`). | `TokensController`, SharedKernel | **Tarea 15:** añadir `TokenDistributionErrors.Impersonation` a `TokenPurchaseMessages.ErrorKeyFor` (texto propio de distribución, no el de compras). |
| R | Pendiente fuera de esta tarea: canjear un token en el alta (`TokenRedemptionService`) no toma el candado ni comprueba el estado al guardar; un canje y una distribución simultáneos del mismo código podrían pisarse (el último en guardar gana). Cerrarlo pide un token de concurrencia en `TokenTransaction.Status` (migración) o el candado del dueño en SignupAPI. | SignupAPI | Decidir en la ronda de altas. |

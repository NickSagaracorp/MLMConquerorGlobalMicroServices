using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MLMConquerorGlobalEdition.ClientCore;
using MLMConquerorGlobalEdition.SharedComponents.Components.Account;
using MLMConquerorGlobalEdition.SharedComponents.Resources;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// Los dos modos de envío de los formularios del área de cuenta.
///
/// QUÉ SE ESTÁ PROTEGIENDO AQUÍ. Estos componentes los montan cuatro anfitriones y solo dos tienen
/// servidor debajo: AdminWeb y BizCenterWeb son ASP.NET Core, pero AdminApp y BizCenterApp son
/// MAUI Blazor Hybrid, donde no hay SSR estático ni endpoint al que postear. Un mismo componente
/// tiene que servir a los cuatro:
///
///   OnSubmit sin asignar -> &lt;form method="post" action="…"&gt;, recarga completa. Es lo que hace
///                           AdminWeb hoy y lo que NO puede cambiar.
///   OnSubmit asignado    -> se cancela el envío nativo y se llama a la devolución. Sin recarga.
///
/// Y —lo que de verdad decide si la tarea salió bien— CON UN SOLO CUERPO DE MARCADO. Por eso la
/// prueba central de este archivo no es ninguna de las de comportamiento sino
/// <see cref="ElCuerpoDelFormularioEsElMISMOEnLosDosModos"/>: si alguien parte una pantalla en dos
/// ramas <c>@if</c> para que cada modo tenga la suya, esa prueba se pone roja aunque las dos ramas
/// funcionen. Repetir campos, etiquetas y validaciones es exactamente lo que este diseño existe
/// para evitar.
///
/// CÓMO SE OBSERVA EL MODO. bUnit renderiza interactivamente siempre, así que no puede "postear"
/// de verdad; lo que sí pinta son los atributos internos de Blazor. Un formulario que cancela el
/// envío nativo sale con <c>blazor:onsubmit:preventDefault</c>, y uno que no, sin él. Las dos
/// caras se comprueban juntas en cada prueba a propósito: si bUnit cambiara cómo pinta esos
/// atributos, la prueba fallaría de golpe en vez de quedarse verde sin comprobar nada.
/// </summary>
public class AccountFormsDosModosTests : BunitContext
{
    /// <summary>
    /// Localizador que devuelve la CLAVE en vez del texto traducido. Así las comprobaciones sobre
    /// mensajes de error hablan de <c>Account.Error.PasswordChangeFailed</c> y no de una frase en
    /// castellano que cambia en cuanto alguien retoca el .resx.
    /// </summary>
    private sealed class LocalizadorDeClaves : IStringLocalizer<SharedResources>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name, resourceNotFound: false);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    public AccountFormsDosModosTests() =>
        Services.AddSingleton<IStringLocalizer<SharedResources>>(new LocalizadorDeClaves());

    // -------------------------------------------------------------------------------------------
    //  Ayudas
    // -------------------------------------------------------------------------------------------

    /// <summary>El atributo con el que Blazor marca que el envío nativo se cancela.</summary>
    private const string CancelaElEnvioNativo = "blazor:onsubmit:preventDefault";

    /// <summary>
    /// El formulario postea al navegador: tiene method, tiene action y NO cancela el envío nativo.
    /// Es literalmente lo que necesita AdminWeb para seguir funcionando igual.
    /// </summary>
    private static void DebePostearA(IElement form, string action)
    {
        form.GetAttribute("method").Should().Be("post");
        form.GetAttribute("action").Should().Be(action);
        form.HasAttribute(CancelaElEnvioNativo).Should().BeFalse(
            "sin OnSubmit el envío tiene que llegar al navegador, que es quien postea");
    }

    /// <summary>El formulario se queda el envío: cancela el nativo y no lleva a ninguna ruta.</summary>
    private static void DebeEnviarSinRecargar(IElement form)
    {
        form.HasAttribute(CancelaElEnvioNativo).Should().BeTrue(
            "con OnSubmit el envío nativo se cancela y no hay recarga de página");
        form.HasAttribute("action").Should().BeFalse(
            "en móvil no hay endpoint al que postear, así que no se pinta un action que no existe");
    }

    private static string[] NombresDeCampo(IRenderedComponent<IComponent> cut) =>
        [.. cut.FindAll("input").Select(i => i.GetAttribute("name") ?? string.Empty)];

    private static void Escribir(IRenderedComponent<IComponent> cut, string selector, string valor) =>
        cut.Find(selector).Input(new ChangeEventArgs { Value = valor });

    // -------------------------------------------------------------------------------------------
    //  Login — la puerta
    // -------------------------------------------------------------------------------------------

    [Fact]
    public void Login_SinOnSubmit_PosteaElFormularioDeSiempre()
    {
        var cut = Render<Login>(p => p
            .Add(c => c.FormAction, "/account/login")
            .Add(c => c.ForgotPasswordUrl, "/forgot-password"));

        DebePostearA(cut.Find("form"), "/account/login");

        // Los mismos nombres que AuthEndpoints.LoginForm. Si alguien renombra uno de los dos, el
        // POST llega con el campo vacío y la puerta responde "credenciales inválidas" con
        // credenciales buenas — un fallo que no rompe ninguna compilación.
        NombresDeCampo(cut).Should().Equal("Email", "Password");
    }

    [Fact]
    public void Login_ConOnSubmit_EntregaLasCredencialesYNoRecarga()
    {
        LoginFormModel? recibido = null;

        var cut = Render<Login>(p => p
            .Add(c => c.OnSubmit, (LoginFormModel m) => recibido = m));

        Escribir(cut, "#login-email",    "alguien@example.com");
        Escribir(cut, "#login-password", "LaSuya1A");

        var form = cut.Find("form");
        DebeEnviarSinRecargar(form);
        form.Submit(EventArgs.Empty);

        recibido.Should().NotBeNull();
        recibido!.Email.Should().Be("alguien@example.com");
        recibido.Password.Should().Be("LaSuya1A");
    }

    /// <summary>
    /// La contraseña del login NO lleva minlength. Una cuenta creada antes de la política de hoy
    /// tiene que poder entrar para poder cambiarla; bloquearla en la puerta la deja fuera de la
    /// única pantalla desde la que podría arreglarlo.
    /// </summary>
    [Fact]
    public void Login_LaContrasenaNoExigeLongitudMinima()
    {
        var cut = Render<Login>(p => p.Add(c => c.FormAction, "/x"));

        cut.Find("#login-password").HasAttribute("minlength").Should().BeFalse();
        cut.Find("#login-password").HasAttribute("required").Should().BeTrue();
    }

    /// <summary>
    /// TODOS los códigos que LoginErrorMessages conoce se enseñan de verdad, con su clave y su
    /// severidad. Esta es la prueba que faltaba cuando cada pantalla llevaba su propia cadena de
    /// <c>@if</c>: SERVICE_UNAVAILABLE se emitía desde la puerta y ninguna de las dos lo traducía,
    /// así que el usuario veía el formulario otra vez sin un solo aviso.
    /// </summary>
    [Theory]
    [InlineData(LoginErrorMessages.Invalid,            "Login.ErrorInvalid",            "alert-danger")]
    [InlineData(LoginErrorMessages.AccessDenied,       "Login.ErrorAccessDenied",       "alert-warning")]
    [InlineData(LoginErrorMessages.Inactive,           "Login.ErrorInactive",           "alert-warning")]
    [InlineData(LoginErrorMessages.SessionExpired,     "Login.ErrorSessionExpired",     "alert-info")]
    [InlineData(LoginErrorMessages.ServiceUnavailable, "Login.ErrorServiceUnavailable", "alert-warning")]
    // Los dos que la puerta dejó de aplastar a "invalid": ocurren DESPUÉS de dar la contraseña por
    // buena, así que decirlos con su nombre no revela nada de una cuenta que no se tenga ya.
    [InlineData(LoginErrorMessages.TooManyRequests,    "Login.ErrorTooManyRequests",    "alert-warning")]
    [InlineData(LoginErrorMessages.ChannelUnavailable, "TwoFactor.Error.ChannelUnavailable", "alert-warning")]
    public void Login_CadaCodigoConocidoSeEnsenaConSuTextoYSuSeveridad(
        string codigo, string claveEsperada, string claseEsperada)
    {
        var cut = Render<Login>(p => p
            .Add(c => c.FormAction, "/account/login")
            .Add(c => c.ErrorCode, codigo));

        var aviso = cut.Find(".alert");
        aviso.TextContent.Trim().Should().Be(claveEsperada);
        aviso.ClassList.Should().Contain(claseEsperada);
    }

    /// <summary>
    /// Y la lista de arriba está completa: si alguien añade un código a LoginErrorMessages sin
    /// añadir su caso aquí, esto se pone rojo en vez de dejar el código sin cubrir.
    /// </summary>
    [Fact]
    public void Login_NoHayCodigosConocidosSinProbar()
    {
        LoginErrorMessages.AllCodes.Should().BeEquivalentTo(
            new[]
            {
                LoginErrorMessages.Invalid,
                LoginErrorMessages.AccessDenied,
                LoginErrorMessages.Inactive,
                LoginErrorMessages.SessionExpired,
                LoginErrorMessages.ServiceUnavailable,
                LoginErrorMessages.TooManyRequests,
                LoginErrorMessages.ChannelUnavailable
            },
            "cada código que la puerta sabe emitir tiene que tener su caso en " +
            "Login_CadaCodigoConocidoSeEnsenaConSuTextoYSuSeveridad");
    }

    /// <summary>
    /// Un código que esta versión de la interfaz no conoce se calla. Enseñar el literal en crudo
    /// —"THROTTLED_BY_EDGE"— no le dice nada al usuario y encima convierte la query string en un
    /// altavoz para meter texto ajeno en la pantalla de login.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("un_codigo_que_nadie_conoce")]
    public void Login_UnCodigoQueNoConoceNoPintaNada(string codigo)
    {
        var cut = Render<Login>(p => p
            .Add(c => c.FormAction, "/account/login")
            .Add(c => c.ErrorCode, codigo));

        cut.FindAll(".alert").Should().BeEmpty();
    }

    /// <summary>
    /// El error se pinta igual venga del POST (query string) o de una llamada interactiva. Es el
    /// mismo parámetro y el mismo marcado en los dos modos.
    /// </summary>
    [Fact]
    public void Login_ElErrorSePintaIgualEnLosDosModos()
    {
        var enFormulario = Render<Login>(p => p
            .Add(c => c.FormAction, "/account/login")
            .Add(c => c.ErrorCode, LoginErrorMessages.ServiceUnavailable));

        var interactivo = Render<Login>(p => p
            .Add(c => c.OnSubmit, (LoginFormModel _) => { })
            .Add(c => c.ErrorCode, LoginErrorMessages.ServiceUnavailable));

        enFormulario.Find(".alert").TextContent.Trim()
            .Should().Be("Login.ErrorServiceUnavailable");
        interactivo.Find(".alert").TextContent.Trim()
            .Should().Be("Login.ErrorServiceUnavailable");
    }

    /// <summary>
    /// Los dos enlaces se pintan solo si el portal los da. Administración no tiene alta pública
    /// —sus cuentas las crea otro administrador—, así que no puede aparecer un "regístrate" que no
    /// lleva a ninguna parte.
    /// </summary>
    [Fact]
    public void Login_LosEnlacesSePintanSoloSiElPortalLosDa()
    {
        var sinEnlaces = Render<Login>(p => p.Add(c => c.FormAction, "/x"));
        sinEnlaces.FindAll("a").Should().BeEmpty();

        var conEnlaces = Render<Login>(p => p
            .Add(c => c.FormAction, "/x")
            .Add(c => c.ForgotPasswordUrl, "/forgot-password")
            .Add(c => c.SignupUrl, "/signup"));

        conEnlaces.FindAll("a").Select(a => a.GetAttribute("href"))
            .Should().Equal("/forgot-password", "/signup");
    }

    /// <summary>
    /// El enlace de recuperación va DENTRO del formulario: así el orden de tabulación lleva de la
    /// contraseña a "no la recuerdo" y de ahí al botón, en vez de saltárselo.
    /// </summary>
    [Fact]
    public void Login_ElEnlaceDeRecuperacionVaDentroDelFormulario()
    {
        var cut = Render<Login>(p => p
            .Add(c => c.FormAction, "/x")
            .Add(c => c.ForgotPasswordUrl, "/forgot-password"));

        cut.Find("form").QuerySelectorAll("a").Select(a => a.GetAttribute("href"))
            .Should().Equal("/forgot-password");
    }

    // -------------------------------------------------------------------------------------------
    //  ChangePassword
    // -------------------------------------------------------------------------------------------

    [Fact]
    public void ChangePassword_SinOnSubmit_PosteaElFormularioDeSiempre()
    {
        var cut = Render<ChangePassword>(p => p
            .Add(c => c.FormAction, "/account/change-password")
            .Add(c => c.CancelUrl, "/admin/account"));

        DebePostearA(cut.Find("form"), "/account/change-password");

        NombresDeCampo(cut).Should().Equal("CurrentPassword", "NewPassword", "ConfirmPassword");
    }

    [Fact]
    public void ChangePassword_ConOnSubmit_EntregaLasTresCasillasYNoRecarga()
    {
        ChangePasswordFormModel? recibido = null;

        var cut = Render<ChangePassword>(p => p
            .Add(c => c.OnSubmit, (ChangePasswordFormModel m) => recibido = m));

        Escribir(cut, "#change-password-current", "LaDeAntes1");
        Escribir(cut, "#change-password-new",     "LaNueva1A!");
        Escribir(cut, "#change-password-confirm", "LaNueva1A!");

        var form = cut.Find("form");
        DebeEnviarSinRecargar(form);
        form.Submit(EventArgs.Empty);

        recibido.Should().NotBeNull();
        recibido!.CurrentPassword.Should().Be("LaDeAntes1");
        recibido.NewPassword.Should().Be("LaNueva1A!");
        recibido.ConfirmPassword.Should().Be("LaNueva1A!");
    }

    /// <summary>
    /// La única comprobación que el navegador no sabe hacer. En modo formulario la hace
    /// <c>AccountEndpoints.PasswordsMatch</c>; en modo interactivo no hay manejador, así que la
    /// hace el componente — y con el MISMO código de error, para que el usuario lea el mismo
    /// mensaje por los dos caminos.
    /// </summary>
    [Fact]
    public void ChangePassword_ConOnSubmit_YContrasenasQueNoCoinciden_NiLlama_NiCalla()
    {
        var llamadas = 0;

        var cut = Render<ChangePassword>(p => p
            .Add(c => c.OnSubmit, (ChangePasswordFormModel _) => llamadas++));

        Escribir(cut, "#change-password-current", "LaDeAntes1");
        Escribir(cut, "#change-password-new",     "LaNueva1A!");
        Escribir(cut, "#change-password-confirm", "OtraCosa1B!");

        cut.Find("form").Submit(EventArgs.Empty);

        llamadas.Should().Be(0, "no hay nada que mandar a la API si el usuario se equivocó al repetirla");
        cut.Find(".alert-danger").TextContent.Trim()
           .Should().Be("Account.Error.PasswordChangeFailed");
    }

    /// <summary>Y cuando se corrige, el aviso desaparece y la llamada sale.</summary>
    [Fact]
    public void ChangePassword_ConOnSubmit_AlCorregirLaConfirmacion_ElAvisoDesaparece()
    {
        var llamadas = 0;

        var cut = Render<ChangePassword>(p => p
            .Add(c => c.OnSubmit, (ChangePasswordFormModel _) => llamadas++));

        Escribir(cut, "#change-password-current", "LaDeAntes1");
        Escribir(cut, "#change-password-new",     "LaNueva1A!");
        Escribir(cut, "#change-password-confirm", "OtraCosa1B!");
        cut.Find("form").Submit(EventArgs.Empty);

        Escribir(cut, "#change-password-confirm", "LaNueva1A!");
        cut.Find("form").Submit(EventArgs.Empty);

        llamadas.Should().Be(1);
        cut.FindAll(".alert-danger").Should().BeEmpty();
    }

    // -------------------------------------------------------------------------------------------
    //  SetPassword
    // -------------------------------------------------------------------------------------------

    [Fact]
    public void SetPassword_SinOnSubmit_PosteaElFormularioDeSiempre()
    {
        var cut = Render<SetPassword>(p => p.Add(c => c.FormAction, "/account/set-password"));

        DebePostearA(cut.Find("form"), "/account/set-password");
        NombresDeCampo(cut).Should().Equal("NewPassword", "ConfirmPassword");
    }

    [Fact]
    public void SetPassword_ConOnSubmit_EntregaLasDosCasillas()
    {
        SetPasswordFormModel? recibido = null;

        var cut = Render<SetPassword>(p => p
            .Add(c => c.OnSubmit, (SetPasswordFormModel m) => recibido = m));

        Escribir(cut, "#set-password-new",     "Primera1A!");
        Escribir(cut, "#set-password-confirm", "Primera1A!");

        DebeEnviarSinRecargar(cut.Find("form"));
        cut.Find("form").Submit(EventArgs.Empty);

        recibido.Should().NotBeNull();
        recibido!.NewPassword.Should().Be("Primera1A!");
    }

    [Fact]
    public void SetPassword_ConOnSubmit_YContrasenasQueNoCoinciden_UsaSuPropioCodigo()
    {
        var llamadas = 0;

        var cut = Render<SetPassword>(p => p
            .Add(c => c.OnSubmit, (SetPasswordFormModel _) => llamadas++));

        Escribir(cut, "#set-password-new",     "Primera1A!");
        Escribir(cut, "#set-password-confirm", "Segunda1B!");
        cut.Find("form").Submit(EventArgs.Empty);

        llamadas.Should().Be(0);
        // PASSWORD_SET_FAILED comparte texto con PASSWORD_RESET_FAILED a propósito; ver AccountMessages.
        cut.Find(".alert-danger").TextContent.Trim()
           .Should().Be("Account.Error.PasswordResetFailed");
    }

    // -------------------------------------------------------------------------------------------
    //  ResetPassword
    // -------------------------------------------------------------------------------------------

    [Fact]
    public void ResetPassword_SinOnSubmit_PosteaConElEnlaceEnCamposOcultos()
    {
        var cut = Render<ResetPassword>(p => p
            .Add(c => c.UserId, "usr-1")
            .Add(c => c.Token, "tok-1")
            .Add(c => c.FormAction, "/account/reset-password"));

        DebePostearA(cut.Find("form"), "/account/reset-password");
        NombresDeCampo(cut).Should().Equal("UserId", "Token", "NewPassword", "ConfirmPassword");
        cut.Find("input[name=UserId]").GetAttribute("value").Should().Be("usr-1");
        cut.Find("input[name=Token]").GetAttribute("value").Should().Be("tok-1");
    }

    [Fact]
    public void ResetPassword_ConOnSubmit_EntregaElEnlaceYLaContrasena()
    {
        ResetPasswordFormModel? recibido = null;

        var cut = Render<ResetPassword>(p => p
            .Add(c => c.UserId, "usr-1")
            .Add(c => c.Token, "tok-1")
            .Add(c => c.OnSubmit, (ResetPasswordFormModel m) => recibido = m));

        Escribir(cut, "#reset-password-new",     "Recien1AB!");
        Escribir(cut, "#reset-password-confirm", "Recien1AB!");

        DebeEnviarSinRecargar(cut.Find("form"));
        cut.Find("form").Submit(EventArgs.Empty);

        recibido.Should().NotBeNull();
        recibido!.UserId.Should().Be("usr-1");
        recibido.Token.Should().Be("tok-1");
        recibido.NewPassword.Should().Be("Recien1AB!");
    }

    /// <summary>
    /// La política de contraseñas —longitud, mayúscula, MINÚSCULA, dígito y CARÁCTER ESPECIAL— se
    /// comprueba antes de llamar, y con el mismo código de error que usa el manejador del POST.
    ///
    /// ESTO SUBIÓ DESDE LA PANTALLA PROPIA DE BizCenterWeb al unificarla: aquella lo comprobaba y
    /// el componente compartido no, así que convertirla en envoltorio sin traerse esta regla habría
    /// sido perder una validación por el camino. La minúscula es la que se olvida: SignupAPI no
    /// sobreescribe RequireLowercase, que en Identity vale true por defecto, así que "PASSWORD1"
    /// pasaba el filtro del cliente y el servidor la rechazaba después sin decir por qué.
    ///
    /// CADA CASO INCUMPLE UNA SOLA COSA, y las demás las cumple. No es cosmético: si las cadenas
    /// incumplieran dos requisitos a la vez, la prueba seguiría verde aunque una de las cinco
    /// condiciones desapareciera del código.
    /// </summary>
    [Theory]
    [InlineData("Cort1A!")]      // menos de 8
    [InlineData("password1a!")]  // sin mayúscula
    [InlineData("PASSWORD1A!")]  // sin minúscula
    [InlineData("PasswordAB!")]  // sin dígito
    [InlineData("Password1A")]   // sin carácter especial — el que la lista no mencionaba
    public void ResetPassword_ConOnSubmit_YContrasenaQueIncumpleLaPolitica_NiLlama_NiCalla(
        string contrasena)
    {
        var llamadas = 0;

        var cut = Render<ResetPassword>(p => p
            .Add(c => c.UserId, "usr-1")
            .Add(c => c.Token, "tok-1")
            .Add(c => c.OnSubmit, (ResetPasswordFormModel _) => llamadas++));

        Escribir(cut, "#reset-password-new",     contrasena);
        Escribir(cut, "#reset-password-confirm", contrasena);
        cut.Find("form").Submit(EventArgs.Empty);

        llamadas.Should().Be(0, "no hay nada que mandar a la API si la contraseña ya se sabe mala");
        cut.Find(".alert-danger").TextContent.Trim()
           .Should().Be("Account.Error.PasswordResetFailed");
    }

    /// <summary>Y la misma regla vale en las otras dos pantallas de contraseña, con su código.</summary>
    [Fact]
    public void ChangePassword_YSetPassword_TambienExigenLaPolitica()
    {
        var cambios = 0;
        var altas   = 0;

        var cambio = Render<ChangePassword>(p => p
            .Add(c => c.OnSubmit, (ChangePasswordFormModel _) => cambios++));

        Escribir(cambio, "#change-password-current", "LaDeAntes1");
        Escribir(cambio, "#change-password-new",     "PASSWORD1A!");
        Escribir(cambio, "#change-password-confirm", "PASSWORD1A!");
        cambio.Find("form").Submit(EventArgs.Empty);

        cambios.Should().Be(0);
        cambio.Find(".alert-danger").TextContent.Trim()
              .Should().Be("Account.Error.PasswordChangeFailed");

        var alta = Render<SetPassword>(p => p
            .Add(c => c.OnSubmit, (SetPasswordFormModel _) => altas++));

        Escribir(alta, "#set-password-new",     "PASSWORD1A!");
        Escribir(alta, "#set-password-confirm", "PASSWORD1A!");
        alta.Find("form").Submit(EventArgs.Empty);

        altas.Should().Be(0);
        alta.Find(".alert-danger").TextContent.Trim()
            .Should().Be("Account.Error.PasswordResetFailed");
    }

    // -------------------------------------------------------------------------------------------
    //  La lista de requisitos y la comprobación tienen que decir LO MISMO
    //
    //  El fallo que esto cierra: ValidationPatterns.PasswordPattern exigía un carácter especial, la
    //  lista que el usuario lee no lo mencionaba y PasswordFailsPolicy tampoco lo miraba. El
    //  usuario cumplía las cuatro líneas que tenía delante y el servidor le rechazaba la contraseña
    //  sin decirle nunca qué le faltaba.
    //
    //  Se arregla en pareja, y por eso se prueba en pareja: las dos mitades por separado vuelven a
    //  desalinearse, cada una por su lado.
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Las tres pantallas de contraseña enseñan LA MISMA lista, con las mismas claves. No es
    /// cosmético: si una añade una línea y las otras no, el usuario aprende una política distinta
    /// según por dónde entre a cambiar su contraseña.
    /// </summary>
    [Fact]
    public void LasTresPantallasDeContrasenaEnsenanLaMismaListaDeRequisitos()
    {
        var esperada = new[]
        {
            "ResetPassword.RequirementLength",
            "ResetPassword.RequirementUppercase",
            "ResetPassword.RequirementLowercase",
            "ResetPassword.RequirementDigit",
            "ResetPassword.RequirementSpecial"
        };

        Requisitos(Render<ChangePassword>(p => p.Add(c => c.FormAction, "/x")))
            .Should().Equal(esperada);

        Requisitos(Render<SetPassword>(p => p.Add(c => c.FormAction, "/x")))
            .Should().Equal(esperada);

        Requisitos(Render<ResetPassword>(p => p
                .Add(c => c.UserId, "usr-1")
                .Add(c => c.Token, "tok-1")
                .Add(c => c.FormAction, "/x")))
            .Should().Equal(esperada);
    }

    /// <summary>
    /// Y la comprobación del cliente exige EXACTAMENTE esas cinco cosas: ni una de menos —el
    /// usuario se llevaría un rechazo del servidor por algo que la pantalla nunca le pidió— ni una
    /// de más —el formulario le diría que no cumple una lista que sí cumple—.
    ///
    /// Cada cadena de abajo incumple UNA sola línea de la lista y cumple las otras cuatro, así que
    /// si una condición desapareciera del código su caso se pondría rojo a solas.
    /// </summary>
    [Theory]
    [InlineData("Cort1A!",     "ResetPassword.RequirementLength")]
    [InlineData("password1a!", "ResetPassword.RequirementUppercase")]
    [InlineData("PASSWORD1A!", "ResetPassword.RequirementLowercase")]
    [InlineData("PasswordAB!", "ResetPassword.RequirementDigit")]
    [InlineData("Password1A",  "ResetPassword.RequirementSpecial")]
    public void CadaLineaDeLaListaTieneSuCondicionEnLaComprobacion(
        string contrasena, string requisitoQueIncumple)
    {
        var llamadas = 0;

        var cut = Render<ChangePassword>(p => p
            .Add(c => c.OnSubmit, (ChangePasswordFormModel _) => llamadas++));

        // La línea existe en la pantalla…
        Requisitos(cut).Should().Contain(requisitoQueIncumple);

        // …y una contraseña que solo incumple esa línea se rechaza aquí, sin viaje a la API.
        Escribir(cut, "#change-password-current", "LaDeAntes1!");
        Escribir(cut, "#change-password-new",     contrasena);
        Escribir(cut, "#change-password-confirm", contrasena);
        cut.Find("form").Submit(EventArgs.Empty);

        llamadas.Should().Be(0);
        cut.Find(".alert-danger").TextContent.Trim()
           .Should().Be("Account.Error.PasswordChangeFailed");
    }

    /// <summary>
    /// La otra cara: una contraseña que cumple las cinco líneas SÍ pasa. Sin esto, la prueba de
    /// arriba se quedaría verde con una comprobación que rechazara absolutamente todo.
    /// </summary>
    [Fact]
    public void UnaContrasenaQueCumpleLaListaEnteraSePuedeEnviar()
    {
        var llamadas = 0;

        var cut = Render<ChangePassword>(p => p
            .Add(c => c.OnSubmit, (ChangePasswordFormModel _) => llamadas++));

        Escribir(cut, "#change-password-current", "LaDeAntes1!");
        Escribir(cut, "#change-password-new",     "Password1A!");
        Escribir(cut, "#change-password-confirm", "Password1A!");
        cut.Find("form").Submit(EventArgs.Empty);

        llamadas.Should().Be(1);
        cut.FindAll(".alert-danger").Should().BeEmpty();
    }

    /// <summary>Las claves de la lista de requisitos que pinta una pantalla de contraseña.</summary>
    private static string[] Requisitos(IRenderedComponent<IComponent> cut) =>
        [.. cut.FindAll("ul.small li").Select(li => li.TextContent.Trim())];

    /// <summary>Sin enlace no se pinta formulario, en ninguno de los dos modos.</summary>
    [Fact]
    public void ResetPassword_SinUserIdNiToken_NoPintaFormularioAunqueHayaOnSubmit()
    {
        var cut = Render<ResetPassword>(p => p
            .Add(c => c.OnSubmit, (ResetPasswordFormModel _) => { }));

        cut.FindAll("form").Should().BeEmpty();
        cut.Find(".alert-danger").TextContent.Trim().Should().Be("ResetPassword.InvalidLink");
    }

    // -------------------------------------------------------------------------------------------
    //  ForgotPassword
    // -------------------------------------------------------------------------------------------

    [Fact]
    public void ForgotPassword_SinOnSubmit_PosteaElFormularioDeSiempre()
    {
        var cut = Render<ForgotPassword>(p => p.Add(c => c.FormAction, "/account/forgot-password"));

        DebePostearA(cut.Find("form"), "/account/forgot-password");
        NombresDeCampo(cut).Should().Equal("Email");
    }

    [Fact]
    public void ForgotPassword_ConOnSubmit_EntregaElCorreo()
    {
        ForgotPasswordFormModel? recibido = null;

        var cut = Render<ForgotPassword>(p => p
            .Add(c => c.OnSubmit, (ForgotPasswordFormModel m) => recibido = m));

        Escribir(cut, "#forgot-password-email", "alguien@example.com");

        DebeEnviarSinRecargar(cut.Find("form"));
        cut.Find("form").Submit(EventArgs.Empty);

        recibido!.Email.Should().Be("alguien@example.com");
    }

    /// <summary>
    /// Que SignupAPI no responda se dice con su nombre y no como "algo salió mal". El código lo
    /// emite AuthApiGateway y hasta ahora caía en la rama genérica de AccountMessages, que le pide
    /// al usuario que reintente sin decirle que el problema no es suyo — el mismo agujero que
    /// LoginErrorMessages tapó en las pantallas de login.
    /// </summary>
    [Fact]
    public void ForgotPassword_UnServicioQueNoRespondeSeDiceConSuNombre()
    {
        var cut = Render<ForgotPassword>(p => p
            .Add(c => c.FormAction, "/account/forgot-password")
            .Add(c => c.ErrorCode, AuthApiGateway.Unreachable));

        cut.Find(".alert-danger").TextContent.Trim().Should().Be("ForgotPassword.ServerError");
    }

    // -------------------------------------------------------------------------------------------
    //  AddPhoneNumber
    // -------------------------------------------------------------------------------------------

    [Fact]
    public void AddPhoneNumber_SinOnSubmit_PosteaYLlegaConElNumeroPuesto()
    {
        var cut = Render<AddPhoneNumber>(p => p
            .Add(c => c.FormAction, "/account/phone/add")
            .Add(c => c.PhoneE164, "+15551234567"));

        DebePostearA(cut.Find("form"), "/account/phone/add");
        NombresDeCampo(cut).Should().Equal("PhoneE164");
        cut.Find("#add-phone-number").GetAttribute("value").Should().Be("+15551234567");
    }

    [Fact]
    public void AddPhoneNumber_ConOnSubmit_EntregaElNumeroTecleado()
    {
        PhoneFormModel? recibido = null;

        var cut = Render<AddPhoneNumber>(p => p
            .Add(c => c.OnSubmit, (PhoneFormModel m) => recibido = m));

        Escribir(cut, "#add-phone-number", "+15559876543");

        DebeEnviarSinRecargar(cut.Find("form"));
        cut.Find("form").Submit(EventArgs.Empty);

        recibido!.PhoneE164.Should().Be("+15559876543");
    }

    /// <summary>
    /// Un repintado por otro motivo —poner un ErrorCode, por ejemplo— no puede borrar lo que el
    /// usuario está tecleando. Es lo que evita el guardián de OnParametersSet.
    /// </summary>
    [Fact]
    public void AddPhoneNumber_UnRepintadoNoPisaLoQueElUsuarioEstaTecleando()
    {
        PhoneFormModel? recibido = null;

        var cut = Render<AddPhoneNumber>(p => p
            .Add(c => c.PhoneE164, "+15551234567")
            .Add(c => c.OnSubmit, (PhoneFormModel m) => recibido = m));

        Escribir(cut, "#add-phone-number", "+15559876543");
        cut.Render(p => p.Add(c => c.ErrorCode, "INVALID_PHONE"));

        cut.Find("form").Submit(EventArgs.Empty);

        recibido!.PhoneE164.Should().Be("+15559876543");
    }

    // -------------------------------------------------------------------------------------------
    //  VerifyPhoneNumber y EnrollAuthenticator: el mismo campo de seis dígitos
    // -------------------------------------------------------------------------------------------

    [Fact]
    public void VerifyPhoneNumber_SinOnSubmit_PosteaElFormularioDeSiempre()
    {
        var cut = Render<VerifyPhoneNumber>(p => p
            .Add(c => c.FormAction, "/account/phone/verify")
            .Add(c => c.MaskedPhone, "***4567"));

        DebePostearA(cut.Find("form"), "/account/phone/verify");
        NombresDeCampo(cut).Should().Equal("Code");
    }

    [Fact]
    public void VerifyPhoneNumber_ConOnSubmit_EntregaElCodigo()
    {
        CodeFormModel? recibido = null;

        var cut = Render<VerifyPhoneNumber>(p => p
            .Add(c => c.OnSubmit, (CodeFormModel m) => recibido = m));

        Escribir(cut, "#verify-phone-code", "123456");

        DebeEnviarSinRecargar(cut.Find("form"));
        cut.Find("form").Submit(EventArgs.Empty);

        recibido!.Code.Should().Be("123456");
    }

    [Fact]
    public void EnrollAuthenticator_SinOnSubmit_PosteaElFormularioDeSiempre()
    {
        var cut = Render<EnrollAuthenticator>(p => p
            .Add(c => c.FormAction, "/auth/two-factor/enroll")
            .Add(c => c.SharedKey, "ABCDEF"));

        DebePostearA(cut.Find("form"), "/auth/two-factor/enroll");
        NombresDeCampo(cut).Should().Equal("Code");
    }

    [Fact]
    public void EnrollAuthenticator_ConOnSubmit_EntregaElPrimerCodigo()
    {
        CodeFormModel? recibido = null;

        var cut = Render<EnrollAuthenticator>(p => p
            .Add(c => c.OnSubmit, (CodeFormModel m) => recibido = m));

        Escribir(cut, "#enroll-code", "654321");

        DebeEnviarSinRecargar(cut.Find("form"));
        cut.Find("form").Submit(EventArgs.Empty);

        recibido!.Code.Should().Be("654321");
    }

    // -------------------------------------------------------------------------------------------
    //  TwoFactorVerify: dos formularios, dos modos independientes
    // -------------------------------------------------------------------------------------------

    [Fact]
    public void TwoFactorVerify_SinOnSubmit_PosteaElCodigoYElReenvio()
    {
        var cut = Render<TwoFactorVerify>(p => p
            .Add(c => c.FormAction, "/auth/two-factor")
            .Add(c => c.ResendAction, "/auth/two-factor/resend")
            .Add(c => c.Channel, "Sms"));

        var formularios = cut.FindAll("form");
        formularios.Should().HaveCount(2);
        DebePostearA(formularios[0], "/auth/two-factor");
        DebePostearA(formularios[1], "/auth/two-factor/resend");
    }

    [Fact]
    public void TwoFactorVerify_ConOnSubmit_EntregaElCodigoYElReenvioPorSeparado()
    {
        CodeFormModel? recibido = null;
        var reenvios = 0;

        var cut = Render<TwoFactorVerify>(p => p
            .Add(c => c.Channel, "Sms")
            .Add(c => c.OnSubmit, (CodeFormModel m) => recibido = m)
            .Add(c => c.OnResend, () => reenvios++));

        Escribir(cut, "#two-factor-code", "111222");

        var formularios = cut.FindAll("form");
        DebeEnviarSinRecargar(formularios[0]);
        DebeEnviarSinRecargar(formularios[1]);

        formularios[0].Submit(EventArgs.Empty);
        formularios[1].Submit(EventArgs.Empty);

        recibido!.Code.Should().Be("111222");
        reenvios.Should().Be(1);
    }

    /// <summary>
    /// El reenvío se ofrece si hay POR DÓNDE hacerlo, sea la ruta o la devolución. Sin ninguna de
    /// las dos, el botón no se pinta — y con el autenticador tampoco, porque no hay nada que
    /// reenviar.
    /// </summary>
    [Theory]
    [InlineData("Sms", true, false, 2)]
    [InlineData("Sms", false, true, 2)]
    [InlineData("Sms", false, false, 1)]
    [InlineData("Authenticator", true, true, 1)]
    public void TwoFactorVerify_ElReenvioSeOfreceSiHayPorDondeHacerlo(
        string canal, bool conRuta, bool conDevolucion, int formulariosEsperados)
    {
        var cut = Render<TwoFactorVerify>(p =>
        {
            p.Add(c => c.Channel, canal);
            p.Add(c => c.FormAction, "/auth/two-factor");
            if (conRuta) p.Add(c => c.ResendAction, "/auth/two-factor/resend");
            if (conDevolucion) p.Add(c => c.OnResend, () => { });
        });

        cut.FindAll("form").Should().HaveCount(formulariosEsperados);
    }

    // -------------------------------------------------------------------------------------------
    //  ManageIndex: dos acciones sin campos
    // -------------------------------------------------------------------------------------------

    [Fact]
    public void ManageIndex_SinDevoluciones_PosteaElReenvioDeConfirmacion()
    {
        var cut = Render<ManageIndex>(p => p
            .Add(c => c.Email, "alguien@example.com")
            .Add(c => c.EmailConfirmed, false)
            .Add(c => c.ResendConfirmationAction, "/account/resend-confirmation"));

        DebePostearA(cut.Find("form"), "/account/resend-confirmation");
    }

    [Fact]
    public void ManageIndex_ConDevolucion_ReenviaLaConfirmacionSinRecargar()
    {
        var reenvios = 0;

        var cut = Render<ManageIndex>(p => p
            .Add(c => c.Email, "alguien@example.com")
            .Add(c => c.EmailConfirmed, false)
            .Add(c => c.OnResendConfirmation, () => reenvios++));

        var form = cut.Find("form");
        DebeEnviarSinRecargar(form);
        form.Submit(EventArgs.Empty);

        reenvios.Should().Be(1);
    }

    /// <summary>
    /// Quitar el teléfono sigue sin ejecutarse desde el primer clic también en modo interactivo:
    /// el formulario que actúa solo aparece con ConfirmRemovePhone puesto.
    /// </summary>
    [Fact]
    public void ManageIndex_ConDevolucion_QuitarElTelefonoSigueNecesitandoConfirmacion()
    {
        var bajas = 0;

        var cut = Render<ManageIndex>(p => p
            .Add(c => c.Email, "alguien@example.com")
            .Add(c => c.EmailConfirmed, true)
            .Add(c => c.HasPhone, true)
            .Add(c => c.PhoneConfirmed, true)
            .Add(c => c.MaskedPhone, "***4567")
            .Add(c => c.RemovePhoneConfirmUrl, "/admin/account?confirm=remove-phone")
            .Add(c => c.OnRemovePhone, () => bajas++));

        cut.FindAll("form").Should().BeEmpty("el primer clic es un enlace, no un envío");

        cut.Render(p => p.Add(c => c.ConfirmRemovePhone, true));

        var form = cut.Find("form");
        DebeEnviarSinRecargar(form);
        form.Submit(EventArgs.Empty);

        bajas.Should().Be(1);
    }

    // -------------------------------------------------------------------------------------------
    //  TwoFactorPanel: el canal preferido y la baja del segundo factor
    // -------------------------------------------------------------------------------------------

    [Fact]
    public void TwoFactorPanel_SinDevoluciones_PosteaElCambioDeCanal()
    {
        var cut = Render<TwoFactorPanel>(p => p
            .Add(c => c.TwoFactorEnabled, true)
            .Add(c => c.PreferredChannel, "Email")
            .Add(c => c.AvailableChannels, new[] { "Email", "Sms" })
            .Add(c => c.ChangeChannelAction, "/account/two-factor/channel"));

        DebePostearA(cut.Find("form"), "/account/two-factor/channel");

        NombresDeCampo(cut).Should().Equal("Channel", "Channel", "Channel");
        cut.Find("#two-factor-channel-email").HasAttribute("checked").Should().BeTrue();
        cut.Find("#two-factor-channel-authenticator").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void TwoFactorPanel_ConDevolucion_EntregaElCanalElegido()
    {
        TwoFactorChannelFormModel? recibido = null;

        var cut = Render<TwoFactorPanel>(p => p
            .Add(c => c.TwoFactorEnabled, true)
            .Add(c => c.PreferredChannel, "Email")
            .Add(c => c.AvailableChannels, new[] { "Email", "Sms" })
            .Add(c => c.OnChangeChannel, (TwoFactorChannelFormModel m) => recibido = m));

        cut.Find("#two-factor-channel-sms").Change(new ChangeEventArgs { Value = true });

        var form = cut.Find("form");
        DebeEnviarSinRecargar(form);
        form.Submit(EventArgs.Empty);

        recibido!.Channel.Should().Be("Sms");
    }

    [Fact]
    public void TwoFactorPanel_ConDevolucion_ApagarElSegundoFactorSigueNecesitandoConfirmacion()
    {
        var bajas = 0;

        var cut = Render<TwoFactorPanel>(p => p
            .Add(c => c.TwoFactorEnabled, true)
            .Add(c => c.PreferredChannel, "Email")
            .Add(c => c.AvailableChannels, new[] { "Email" })
            .Add(c => c.DisableConfirmUrl, "/admin/account/security?confirm=disable")
            .Add(c => c.OnDisable, () => bajas++));

        // El bloque de canales pinta siempre su formulario; el de la baja aparece solo tras
        // confirmar. Lo que no puede existir antes de confirmar es el botón que apaga.
        cut.FindAll(".two-factor-panel-disable form").Should().BeEmpty(
            "el primer clic es un enlace a la pantalla de aviso, no un envío");

        cut.Render(p => p.Add(c => c.ConfirmDisable, true));

        var form = cut.Find(".two-factor-panel-disable form");
        DebeEnviarSinRecargar(form);
        form.Submit(EventArgs.Empty);

        bajas.Should().Be(1);
    }

    // -------------------------------------------------------------------------------------------
    //  El doble envío
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Dos pulsaciones seguidas mientras la primera sigue en el aire son UNA sola llamada. En modo
    /// formulario de esto se encargaba la recarga de página; en interactivo la pantalla se queda
    /// donde está y el botón sigue ahí, así que hace falta el pestillo — dos altas de teléfono son
    /// dos SMS con su coste.
    /// </summary>
    [Fact]
    public async Task ConOnSubmit_DosEnviosSeguidos_SonUnaSolaLlamada()
    {
        var puerta   = new TaskCompletionSource();
        var llamadas = 0;

        var cut = Render<AddPhoneNumber>(p => p
            .Add(c => c.OnSubmit, async (PhoneFormModel _) =>
            {
                llamadas++;
                await puerta.Task;
            }));

        Escribir(cut, "#add-phone-number", "+15559876543");

        var form     = cut.Find("form");
        var primera  = form.SubmitAsync(EventArgs.Empty);
        var segunda  = form.SubmitAsync(EventArgs.Empty);

        puerta.SetResult();
        await primera;
        await segunda;

        llamadas.Should().Be(1);
    }

    /// <summary>Y mientras tanto el botón sale deshabilitado, que es lo que lo explica al usuario.</summary>
    [Fact]
    public async Task ConOnSubmit_MientrasLaLlamadaEstaEnElAire_ElBotonSeDeshabilita()
    {
        var puerta = new TaskCompletionSource();

        var cut = Render<AddPhoneNumber>(p => p
            .Add(c => c.OnSubmit, async (PhoneFormModel _) => await puerta.Task));

        Escribir(cut, "#add-phone-number", "+15559876543");

        var enVuelo = cut.Find("form").SubmitAsync(EventArgs.Empty);

        cut.Find("button[type=submit]").HasAttribute("disabled").Should().BeTrue();

        puerta.SetResult();
        await enVuelo;

        cut.Find("button[type=submit]").HasAttribute("disabled").Should().BeFalse();
    }

    // -------------------------------------------------------------------------------------------
    //  El error se pinta igual venga de donde venga
    // -------------------------------------------------------------------------------------------

    [Fact]
    public void ElErrorDeLaApiSePintaIgualEnLosDosModos()
    {
        var enFormulario = Render<ChangePassword>(p => p
            .Add(c => c.FormAction, "/account/change-password")
            .Add(c => c.ErrorCode, "PASSWORD_CHANGE_FAILED"));

        var interactivo = Render<ChangePassword>(p => p
            .Add(c => c.OnSubmit, (ChangePasswordFormModel _) => { })
            .Add(c => c.ErrorCode, "PASSWORD_CHANGE_FAILED"));

        enFormulario.Find(".alert-danger").TextContent.Trim()
            .Should().Be("Account.Error.PasswordChangeFailed");
        interactivo.Find(".alert-danger").TextContent.Trim()
            .Should().Be("Account.Error.PasswordChangeFailed");
    }

    // -------------------------------------------------------------------------------------------
    //  LA PRUEBA CENTRAL: un solo cuerpo de marcado para los dos modos
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// El cuerpo del formulario —campos, etiquetas, required, minlength, pattern, autocomplete,
    /// placeholders y botón— es EXACTAMENTE el mismo en los dos modos. Lo único que cambia es el
    /// propio <c>&lt;form&gt;</c>: en un modo lleva <c>action</c>, en el otro cancela el envío
    /// nativo.
    ///
    /// Esta es la prueba que se rompe si alguien "arregla" un modo duplicando la pantalla en dos
    /// ramas. Un componente se programa una sola vez, y esto es lo que lo comprueba.
    /// </summary>
    [Fact]
    public void ElCuerpoDelFormularioEsElMISMOEnLosDosModos()
    {
        DebenTenerElMismoCuerpo(
            Render<Login>(p => p
                .Add(c => c.ForgotPasswordUrl, "/f").Add(c => c.SignupUrl, "/s")
                .Add(c => c.FormAction, "/x")),
            Render<Login>(p => p
                .Add(c => c.ForgotPasswordUrl, "/f").Add(c => c.SignupUrl, "/s")
                .Add(c => c.OnSubmit, (LoginFormModel _) => { })));

        DebenTenerElMismoCuerpo(
            Render<ChangePassword>(p => p.Add(c => c.FormAction, "/x")),
            Render<ChangePassword>(p => p.Add(c => c.OnSubmit, (ChangePasswordFormModel _) => { })));

        DebenTenerElMismoCuerpo(
            Render<SetPassword>(p => p.Add(c => c.FormAction, "/x")),
            Render<SetPassword>(p => p.Add(c => c.OnSubmit, (SetPasswordFormModel _) => { })));

        DebenTenerElMismoCuerpo(
            Render<ResetPassword>(p => p
                .Add(c => c.UserId, "u").Add(c => c.Token, "t").Add(c => c.FormAction, "/x")),
            Render<ResetPassword>(p => p
                .Add(c => c.UserId, "u").Add(c => c.Token, "t")
                .Add(c => c.OnSubmit, (ResetPasswordFormModel _) => { })));

        DebenTenerElMismoCuerpo(
            Render<ForgotPassword>(p => p.Add(c => c.FormAction, "/x")),
            Render<ForgotPassword>(p => p.Add(c => c.OnSubmit, (ForgotPasswordFormModel _) => { })));

        DebenTenerElMismoCuerpo(
            Render<AddPhoneNumber>(p => p.Add(c => c.FormAction, "/x")),
            Render<AddPhoneNumber>(p => p.Add(c => c.OnSubmit, (PhoneFormModel _) => { })));

        DebenTenerElMismoCuerpo(
            Render<VerifyPhoneNumber>(p => p.Add(c => c.FormAction, "/x")),
            Render<VerifyPhoneNumber>(p => p.Add(c => c.OnSubmit, (CodeFormModel _) => { })));

        DebenTenerElMismoCuerpo(
            Render<EnrollAuthenticator>(p => p.Add(c => c.FormAction, "/x")),
            Render<EnrollAuthenticator>(p => p.Add(c => c.OnSubmit, (CodeFormModel _) => { })));

        DebenTenerElMismoCuerpo(
            Render<TwoFactorVerify>(p => p
                .Add(c => c.Channel, "Sms").Add(c => c.FormAction, "/x").Add(c => c.ResendAction, "/y")),
            Render<TwoFactorVerify>(p => p
                .Add(c => c.Channel, "Sms")
                .Add(c => c.OnSubmit, (CodeFormModel _) => { })
                .Add(c => c.OnResend, () => { })));

        DebenTenerElMismoCuerpo(
            Render<TwoFactorPanel>(p => p
                .Add(c => c.TwoFactorEnabled, true)
                .Add(c => c.PreferredChannel, "Email")
                .Add(c => c.AvailableChannels, new[] { "Email", "Sms" })
                .Add(c => c.ChangeChannelAction, "/x")),
            Render<TwoFactorPanel>(p => p
                .Add(c => c.TwoFactorEnabled, true)
                .Add(c => c.PreferredChannel, "Email")
                .Add(c => c.AvailableChannels, new[] { "Email", "Sms" })
                .Add(c => c.OnChangeChannel, (TwoFactorChannelFormModel _) => { })));

        DebenTenerElMismoCuerpo(
            Render<ManageIndex>(p => p
                .Add(c => c.Email, "a@b.c").Add(c => c.ResendConfirmationAction, "/x")),
            Render<ManageIndex>(p => p
                .Add(c => c.Email, "a@b.c").Add(c => c.OnResendConfirmation, () => { })));
    }

    /// <summary>
    /// Compara el interior de cada <c>&lt;form&gt;</c> de las dos versiones. Los identificadores
    /// de manejador que pinta bUnit (<c>blazor:oninput="4"</c>) cambian de un render a otro y se
    /// normalizan: lo que se compara es el marcado, no la numeración interna del renderizador.
    /// </summary>
    private static void DebenTenerElMismoCuerpo(
        IRenderedComponent<IComponent> enFormulario, IRenderedComponent<IComponent> interactivo)
    {
        var unos = enFormulario.FindAll("form").Select(f => Normalizar(f.InnerHtml)).ToArray();
        var otros = interactivo.FindAll("form").Select(f => Normalizar(f.InnerHtml)).ToArray();

        unos.Should().NotBeEmpty("si no hay formulario no se está comparando nada");
        otros.Should().Equal(unos);
    }

    private static string Normalizar(string html) =>
        System.Text.RegularExpressions.Regex.Replace(html, @"blazor:([A-Za-z]+)=""\d+""", "blazor:$1");
}

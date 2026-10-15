using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

/// <summary>
/// La respuesta de Billing hacia la pantalla: el dato en 200, o el estado y el código de Billing tal
/// cual (502 BILLING_UNAVAILABLE si Billing no contestó).
/// </summary>
internal static class BillingResultExtensions
{
    public static IActionResult ToActionResult<T>(this ControllerBase controller, BillingResult<T> result) where T : class
    {
        if (result.IsSuccess)
            return controller.Ok(ApiResponse<T>.Ok(result.Value!));

        var body = ApiResponse<T>.Fail(result.ErrorCode!, result.Error!, controller.HttpContext.TraceIdentifier);
        return controller.StatusCode(result.StatusCode, body);
    }
}

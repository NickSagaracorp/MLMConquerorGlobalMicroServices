using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Repository.Services.Links;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

/// <summary>"Mis enlaces" del miembro que llama. El miembro sale del token: no hay sujeto en la URL.</summary>
[ApiController]
[Route("api/v1/bizcenter/my-links")]
[Authorize]
public class MyLinksController : ControllerBase
{
    private readonly IMyLinksService     _links;
    private readonly ICurrentUserService _currentUser;

    public MyLinksController(IMyLinksService links, ICurrentUserService currentUser)
    {
        _links       = links;
        _currentUser = currentUser;
    }

    /// <summary>GET /api/v1/bizcenter/my-links</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(ApiResponse<MyLinksView>.Ok(await _links.GetAsync(_currentUser.MemberId, ct)));
}

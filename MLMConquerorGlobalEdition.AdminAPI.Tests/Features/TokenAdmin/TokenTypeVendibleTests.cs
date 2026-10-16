using MLMConquerorGlobalEdition.AdminAPI.DTOs.Tokens;
using MLMConquerorGlobalEdition.AdminAPI.Mappings;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.TokenAdmin;

/// <summary>
/// IsPurchasable se edita en Token Types. Un PUT que no lo manda (pantallas viejas, scripts) no lo
/// pone a false sin querer, y un tipo nuevo nace sin vender salvo que se diga lo contrario.
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

    [Fact]
    public void Un_tipo_nuevo_nace_sin_vender_salvo_que_se_pida()
    {
        new CreateTokenTypeDto { Name = "Nuevo" }.ToNewEntity().IsPurchasable.Should().BeFalse();
        new CreateTokenTypeDto { Name = "Nuevo", IsPurchasable = true }.ToNewEntity().IsPurchasable.Should().BeTrue();
    }
}

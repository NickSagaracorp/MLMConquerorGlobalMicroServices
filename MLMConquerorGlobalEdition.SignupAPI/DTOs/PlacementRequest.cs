namespace MLMConquerorGlobalEdition.SignupAPI.DTOs;

public class PlacementRequest
{
    public string PlaceUnderMemberId { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;   // Left | Right
}
